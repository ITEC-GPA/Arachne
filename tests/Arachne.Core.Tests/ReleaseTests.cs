using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using Arachne.Core;
using GPC.Geometry;
using GPC.Utilities.Units;

internal static class ReleaseTests
{
    private static void Check(bool b,string why="Assertion failed"){if(!b)throw new Exception(why);}
    private static void Near(double a,double b,double tol=1e-7){Check(Math.Abs(a-b)<=tol,$"Expected {b}, got {a}");}
    internal static FormModel Grid()
    {
        var p=Enumerable.Range(0,9).Select(i=>new Point3d(i%3,i/3,0)).ToArray();var cables=new List<Cable>();var conditions=new List<NodeCondition>();var faces=new List<Point3d[]>();
        for(int i=0;i<9;i++){
            if(i%3<2)cables.Add(new Cable{Start=p[i],End=p[i+1],Target=2});
            if(i<6)cables.Add(new Cable{Start=p[i],End=p[i+3],Target=2});
            if(i!=4)conditions.Add(new NodeCondition{Position=p[i],FixX=true,FixY=true,FixZ=true});
            if(i%3<2&&i<6)faces.Add(new[]{p[i],p[i+1],p[i+4],p[i+3]});
        }
        return ModelBuilder.Build(cables,conditions,faces);
    }
    public static void Run(Action<string,Action> test)
    {
        test("Model protects coordinates, cable data and face arrays",()=>{var m=Grid();m.Nodes[0].Position.X=99;m.Edges[0].Cable.Target=99;m.Faces[0][0]=99;Near(m.Nodes[0].Position.X,0);Near(m.Edges[0].Cable.Target,2);Check(m.Faces[0][0]==0);Check(m.Snapshot().Id==m.Id);});
        test("Model check detects unsupported disconnected part",()=>{var m=ModelBuilder.Build(new[]{new Cable{Start=new Point3d(0,0,0),End=new Point3d(1,0,0)},new Cable{Start=new Point3d(3,0,0),End=new Point3d(4,0,0)}},new[]{new NodeCondition{Position=Point3d.Origin,FixX=true,FixY=true,FixZ=true}});var c=ModelChecks.Check(m);Check(!c.Valid&&c.ConnectedParts==2&&c.Errors.Count==3);});
        test("Model check detects degenerate face",()=>{var m=ModelBuilder.Build(new[]{new Cable{Start=new Point3d(0,0,0),End=new Point3d(1,0,0)},new Cable{Start=new Point3d(1,0,0),End=new Point3d(2,0,0)},new Cable{Start=new Point3d(2,0,0),End=Point3d.Origin}},faces:new[]{new[]{Point3d.Origin,new Point3d(1,0,0),new Point3d(2,0,0)}});Check(ModelChecks.Check(m).Errors.Any(e=>e.Contains("zero area")));});
        test("Units preserve dimensions and round trip",()=>{var u=new UnitSettings(UnitsConvert.LengthUnits.mm,UnitsConvert.ForceUnits.kN);Near(u.Convert(1000,Quantity.Length),1);Near(u.Convert(2,Quantity.ForcePerLength),2e6);Near(u.Convert(3,Quantity.Pressure),3e9);Near(u.Convert(u.Convert(45,Quantity.MassDensity),Quantity.MassDensity,false),45);});
        test("Panel distortion measures known anisotropic shrinkage",()=>{var a=new[]{new Point3d(0,0,0),new Point3d(2,0,0),new Point3d(2,2,0),new Point3d(0,2,0)};var b=a.Select(p=>new Point3d(p.X*0.9,p.Y*0.8,p.Z)).ToArray();var r=PanelDistortion.Evaluate(a,b,new[]{new[]{0,1,2,3}});Near(r.MaxEdgeChange,0.2);Near(r.AreaChanges[0],-0.28);Check(!r.Accepted);});
        if(!TensileFormFindingSolver.IsAvailable)return;
        test("Fully restrained triangle retains pressure and reaction results",()=>{
            var p=new[]{new Point3d(0,0,0),new Point3d(2,0,0),new Point3d(0,3,0)};
            var m=ModelBuilder.Build(Enumerable.Range(0,3).Select(i=>new Cable{Start=p[i],End=p[(i+1)%3],Target=2}),p.Select(x=>new NodeCondition{Position=x,FixX=true,FixY=true,FixZ=true}),new[]{p});
            var r=new TensileFormFindingSolver().Solve(m,new SolverSettings{Pressure=10});Check(r.Status==SolveStatus.Converged,string.Join(";",r.Messages));Near(r.Solved.Nodes.Sum(n=>n.Reaction.Z),-30);Check(EquilibriumReport.Evaluate(r.Solved).Accepted);
        });
        test("Pressure grid analytical displacement, force and moment balance",()=>{
            var r=new TensileFormFindingSolver().Solve(Grid(),new SolverSettings{Pressure=8,Tolerance=1e-10,AlgebraicToleranceA=1e-12,CreateMesh=false});
            Check(r.Status==SolveStatus.Converged,string.Join(";",r.Messages));var m=r.Solved;Check(m!=null&&m.Plates.Count==4&&r.Faces.Count==4);
            var center=m.Nodes.Single(n=>!n.FixZ);Near(center.Position.Z,1);Near(m.Nodes.Sum(n=>n.PressureForce.Z),32);Near(m.Nodes.Sum(n=>n.Reaction.Z),-32);
            var e=EquilibriumReport.Evaluate(m,1e-7,1e-7);Check(e.Accepted,"Equilibrium force/moment check failed");
        });
        test("Plate result maps all ordered sides to solved cable force",()=>{var r=new TensileFormFindingSolver().Solve(Grid(),new SolverSettings());Check(r.Status==SolveStatus.Converged,string.Join(";",r.Messages));foreach(var p in r.Solved.Plates){Near(p.Area,1);Check(!p.HasMembraneResultants);for(int i=0;i<4;i++)Near(p.BoundaryAxialForces[i],r.Solved.Beam(p.BoundaryBeamIds[i]).AxialForce);}Check(r.Solved.ModelId==r.Model.Id);});
        test("Canonical results independent of legacy mutable output lists",()=>{var r=new TensileFormFindingSolver().Solve(Grid(),new SolverSettings());double x=r.Solved.Node(1).Position.X;r.Positions[0].X=999;r.Solved.Node(1).Position.X=99;r.AxialForces.Clear();Near(r.Solved.Node(1).Position.X,x);Check(r.Solved.Beams.Count==12);});
        test("Actual internal history has ordered steps",()=>{var r=new TensileFormFindingSolver().Solve(Grid(),new SolverSettings{Pressure=8,Tolerance=1e-9});Check(r.History.Count>0);Check(r.History.Select(h=>h.Inner).SequenceEqual(Enumerable.Range(1,r.History.Count)));Check(r.History.All(h=>h.Outer==1&&h.Metric.Contains("displacement")));});
        test("Cancellation during inner iteration keeps history and suppresses results",()=>{using(var c=new CancellationTokenSource()){var r=new TensileFormFindingSolver().Solve(Grid(),new SolverSettings{Pressure=8,Tolerance=1e-12},c.Token,step=>c.Cancel());Check(r.Status==SolveStatus.Cancelled&&r.History.Count==1&&r.Solved==null&&r.Positions.Count==0);}});
        test("Invalid result ID is rejected",()=>{var r=new TensileFormFindingSolver().Solve(Grid(),new SolverSettings());try{r.Solved.Node(0);}catch(ArgumentException){return;}throw new Exception("Unknown ID accepted");});
        test("Self weight and cable point loads have separate balanced channels",()=>{
            var g=Grid();var cables=g.Edges.Select(e=>e.Cable).ToArray();foreach(var c in cables){c.Area=0.001;c.Density=10;c.Loads.Add(new CableLoad(0.5,new Vector3d(0,0,-1)));}
            var m=ModelBuilder.Build(cables,g.Nodes.Where(n=>n.FixZ).Select(n=>new NodeCondition{Position=n.Position,FixX=true,FixY=true,FixZ=true}),g.Faces.Select(f=>f.Select(i=>g.Nodes[i].Position).ToArray()));
            var r=new TensileFormFindingSolver().Solve(m,new SolverSettings{Gravity=10,Tolerance=1e-10,AlgebraicToleranceA=1e-12});Check(r.Status==SolveStatus.Converged,string.Join(";",r.Messages));Near(r.Solved.Nodes.Sum(n=>n.CableLoadForce.Z),-12);Check(r.Solved.Nodes.Sum(n=>n.SelfWeightForce.Z)<0);Check(EquilibriumReport.Evaluate(r.Solved,1e-6,1e-6).Accepted);
        });
    }
}
