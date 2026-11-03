#if GPC_PILOT
using System;
using System.Linq;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Models;
using GPC.Model.Structure.Assignments;
using TensileLib.FormFinding;
using TModel = TensileLib.Geometry.Model;
using TBeam = TensileLib.Geometry.Beam;

internal static class GpcF5Objectives
{
    private static void Check(bool condition,string name)
    { if(!condition) throw new Exception(name); Console.WriteLine("PASS F5 " + name); }
    private static void Near(double actual,double expected,string name)
        => Check(!double.IsNaN(actual) && Math.Abs(actual-expected)<=1e-7*Math.Max(1,Math.Abs(expected)),name);
    internal static void Run()
    {
        Stepped(false); Stepped(true); Curvature(); SignedForceDensity();
        Console.WriteLine("F5 OBJECTIVES PASS");
    }
    private static void Stepped(bool axial)
    {
        // Two symmetric cables, half-span 500 mm, central downward force 10 N.
        // Length control: sag=sqrt(L²-500²). Force control: sin(theta)=10/(2N).
        var source=new TModel(); source.AddNewNode(-500,0,0); source.AddNewNode(500,0,0); source.AddNewNode(0,0,-100);
        foreach(var node in source.Nodes) { node.Tx=true; node.Ty=true; node.Tz=node.Id!=3; }
        source.Nodes[3].SetForce(0,0,-10);
        source.AddNewBeam(1,3); source.AddNewBeam(2,3);
        foreach(var beam in source.Beams)
        {
            if(axial) { beam.TargetAxialForce=100; beam.FixNLAxialForce(1); }
            else { beam.TargetForceDensity=.01; beam.FixNLLength(600,1); }
        }
        var settings=new FormFindingDefinition {Method=FormFindingMethod.SteppedLinearForceDensity,MaxIterations=500,RelativeTolerance=1e-10,SolverToleranceA=1e-12,SolverToleranceB=1e-12};
        var model=GpcPilotAdapter.Import(source,Guid.NewGuid(),settings); string before=model.AnalysisFingerprint();
        var restored=ModelValues.CopyValue(model); var export=GpcPilotAdapter.Export(restored);
        var status=GpcPilotAdapter.Solve(export.Solver,restored.FormFinding);
        Check(status!=Solver.ResultCodes.NotconvergedAtMaxIterationNumber,"stepped converges " + axial);
        double sag=axial ? 500*.05/Math.Sqrt(1-.05*.05) : Math.Sqrt(600*600-500*500);
        Near(export.Solver.Nodes[3].Z,-sag,"analytic sag " + axial);
        foreach(var beam in export.Solver.Beams) Near(axial?beam.AxialForce:beam.Length,axial?100:600,"analytic target " + axial);
        Check(restored.AnalysisFingerprint()==before,"stepped preserves domain targets");
        GpcPilotAdapter.Attach(restored,export,status);
        var limited=ModelValues.CopyValue(model); limited.FormFinding.MaxIterations=1;
        var limitedExport=GpcPilotAdapter.Export(limited); var limitedStatus=GpcPilotAdapter.Solve(limitedExport.Solver,limited.FormFinding);
        Check(limitedStatus==Solver.ResultCodes.NotconvergedAtMaxIterationNumber,"outer iteration limit");
        bool rejected=false; try { GpcPilotAdapter.Attach(limited,limitedExport,limitedStatus); } catch(InvalidOperationException) { rejected=true; }
        Check(rejected,"unconverged results rejected");
        var unsupported=ModelValues.CopyValue(model); unsupported.NodesElements.Values.First().Assignments.Physical=new NodePhysicalData();
        unsupported.NodesElements.Values.First().Assignments.Physical.Temperatures.Add(new NodalTemperatureAssignment {Case=unsupported.LoadCases.Values.Single(),Celsius=20});
        rejected=false; try { GpcPilotAdapter.Export(unsupported); } catch(NotSupportedException) { rejected=true; }
        Check(rejected,"new unsupported physical data rejected");
    }
    private static void Curvature()
    {
        var source=new TModel(); var ids=new int[3,3]; TBeam curved=null;
        for(int j=0;j<3;j++) for(int i=0;i<3;i++)
        {
            ids[i,j]=source.AddNewNode(i,j,0); var node=source.Nodes[ids[i,j]];
            node.Tx=node.Ty=node.Tz=i!=1||j!=1;
        }
        for(int j=0;j<3;j++) for(int i=0;i<3;i++)
        {
            if(i<2) { var b=source.Beams[source.AddNewBeam(ids[i,j],ids[i+1,j])]; b.MembraneType=TBeam.MembraneTypes.Warp; b.TargetForceAreaDensity=1; }
            if(j<2)
            {
                var b=source.Beams[source.AddNewBeam(ids[i,j],ids[i,j+1])];
                if(i==0&&j==0) { b.CurveChord=4; b.TargetCurvatureRatio=10; curved=b; }
                else { b.MembraneType=TBeam.MembraneTypes.Weft; b.TargetForceAreaDensity=1; }
            }
        }
        var settings=new FormFindingDefinition {MaxIterations=20,RelativeTolerance=1e-9};
        var model=GpcPilotAdapter.Import(source,Guid.NewGuid(),settings); var copy=ModelValues.CopyValue(model);
        var export=GpcPilotAdapter.Export(copy); var baselineStatus=GpcPilotAdapter.Solve(source,settings); var status=GpcPilotAdapter.Solve(export.Solver,settings);
        Check(status==baselineStatus,"curvature solver status");
        Near(export.Solver.Beams[curved.Id].ForceDensity,5.2,"curvature analytical density");
        foreach(var b in source.Beams) Near(export.Solver.Beams[b.Id].AxialForce,b.AxialForce,"curvature baseline force");
        Check(copy.FormFinding.Goals.Single(g=>g.Kind==FormFindingGoalKind.CurvatureRatio).ChordLengthMm==4,"curvature chord archived");
        // A fixed axial objective must not silently replace an independent force-density source.
        var goal=model.FormFinding.Goals.First(g=>g.Kind==FormFindingGoalKind.MembraneForcePerWidth);
        model.FormFinding.Method=FormFindingMethod.NonlinearForceDensity;
        model.FormFinding.Goals.Add(new FormFindingGoal {BeamGuid=goal.BeamGuid,Kind=FormFindingGoalKind.AxialForce,Target=2});
        var both=GpcPilotAdapter.Export(model); var target=both.Solver.Beams[both.Ids[goal.BeamGuid]];
        Check(target.ForceDensitySource==TBeam.ForceDensitySources.FromMembrane && target.HasNLFixedAxialForce && target.TargetAxialForce==2,"independent primary and axial objectives");
    }
    private static void SignedForceDensity()
    {
        var source=new TModel(); source.AddNewNode(-500,0,0); source.AddNewNode(500,0,0); source.AddNewNode(0,0,-20);
        foreach(var node in source.Nodes) { node.Tx=node.Ty=true; node.Tz=node.Id!=3; }
        source.Nodes[3].SetForce(0,0,-10); source.AddNewBeam(1,3); source.AddNewBeam(2,3);
        // Mixed signs with a positive reduced stiffness q1+q2=.1 N/mm.
        source.Beams[1].TargetForceDensity=.2; source.Beams[2].TargetForceDensity=-.1;
        var settings=new FormFindingDefinition {MaxIterations=100,RelativeTolerance=1e-10};
        var model=GpcPilotAdapter.Import(source,Guid.NewGuid(),settings); var restored=ModelValues.CopyValue(model);
        var exported=GpcPilotAdapter.Export(restored); var status=GpcPilotAdapter.Solve(exported.Solver,settings);
        Check(status!=Solver.ResultCodes.NotconvergedAtMaxIterationNumber,"signed force density converges");
        Near(exported.Solver.Nodes[3].Z,-100,"signed force density analytical equilibrium");
        Check(exported.Solver.Beams[1].AxialForce>0 && exported.Solver.Beams[2].AxialForce<0,"force signs preserved");
        // The frozen backend uses CG: an all-compressive reduced matrix is not positive definite.
        foreach(var goal in model.FormFinding.Goals) goal.Target=-.1;
        var indefinite=GpcPilotAdapter.Export(model); bool rejected=false;
        try { GpcPilotAdapter.Solve(indefinite.Solver,settings); } catch(NotSupportedException error) { rejected=error.Message.Contains("CG solver not converged"); }
        Check(rejected,"non-positive-definite backend failure stays explicit");
    }
}
#endif
