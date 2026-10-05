using System;
using System.Threading;
using Arachne.Core;
using GPC.Geometry;
using GPC.Utilities.Units;

int passed=0, failed=0;
void Test(string name, Action run)
{
    try {run();passed++;Console.WriteLine("PASS " + name);}
    catch(Exception error){failed++;Console.WriteLine("FAIL " + name + ": " + error.Message);}
}
void Check(bool value){if(!value)throw new Exception("Assertion failed.");}
void Reject(Action run){try{run();}catch(ArgumentException){return;}throw new Exception("Expected rejection.");}
Cable Edge(double x0,double x1)=>new Cable{Start=new Point3d(x0,0,0),End=new Point3d(x1,0,0)};
Test("Shared endpoints merge including negative cell boundaries",()=>{
    var model=ModelBuilder.Build(new[]{Edge(-1,0),Edge(-0.0000001,1)},tolerance:1e-6);
    Check(model.Nodes.Count==3 && model.Edges[0].End==model.Edges[1].Start);
});
Test("Repeated loads sum and restraints combine",()=>{
    var model=ModelBuilder.Build(new[]{Edge(0,1)},new[]{
        new NodeCondition{Position=Point3d.Origin,FixX=true,Force=new Vector3d(1,2,3)},
        new NodeCondition{Position=Point3d.Origin,FixZ=true,Force=new Vector3d(4,5,6)}});
    Check(model.Nodes[0].Force.X==5 && model.Nodes[0].Force.Y==7 && model.Nodes[0].Force.Z==9);
    Check(model.Nodes[0].FixX && model.Nodes[0].FixZ);
});
Test("Model snapshots do not modify input cable or loads",()=>{
    var cable=Edge(0,1);var force=new Vector3d(1,0,0);cable.Loads.Add(new CableLoad(0.5,force));
    var model=ModelBuilder.Build(new[]{cable});cable.Start.X=100;cable.Target=100;force.X=100;
    Check(model.Nodes[0].Position.X==0 && model.Edges[0].Cable.Target==1 && model.Edges[0].Cable.Loads[0].Force.X==1);
});
Test("Collapsed and reversed duplicate cables rejected",()=>{
    Reject(()=>ModelBuilder.Build(new[]{Edge(0,1e-8)}));
    Reject(()=>ModelBuilder.Build(new[]{Edge(0,1),Edge(1,0)}));
});
Test("Unmatched support rejected",()=>Reject(()=>ModelBuilder.Build(new[]{Edge(0,1)},new[]{new NodeCondition{Position=new Point3d(5,0,0)}})));
Test("NaN target and invalid load position rejected",()=>{
    var cable=Edge(0,1);cable.Target=double.NaN;Reject(()=>ModelBuilder.Build(new[]{cable}));
    cable.Target=1;cable.Loads.Add(new CableLoad(2,new Vector3d(1,0,0)));Reject(()=>ModelBuilder.Build(new[]{cable}));
});
Test("Face vertices map to merged nodes",()=>{
    var a=Point3d.Origin;var b=new Point3d(1,0,0);var c=new Point3d(0,1,0);
    var model=ModelBuilder.Build(new[]{new Cable{Start=a,End=b},new Cable{Start=b,End=c},new Cable{Start=c,End=a}},faces:new[]{new[]{a,b,c}});
    Check(model.Faces.Count==1 && model.Nodes.Count==3);
    Reject(()=>ModelBuilder.Build(new[]{Edge(0,1)},faces:new[]{new[]{a,b,b}}));
});
Test("Outer iteration cap is exact",()=>{
    int calls=0;bool result=IterationControl.Run(3,i=>{calls++;return false;},default,out int count);
    Check(!result && count==3 && calls==3);
});
Test("Convergence on last allowed iteration is accepted",()=>{
    bool result=IterationControl.Run(3,i=>i==3,default,out int count);Check(result && count==3);
});
Test("Cancellation stops before solve",()=>{
    int calls=0;try{IterationControl.Run(3,i=>{calls++;return true;},new CancellationToken(true),out _);}
    catch(OperationCanceledException){Check(calls==0);return;}throw new Exception("Cancellation ignored.");
});
Test("Density update rejects zero denominator",()=>{
    Reject(()=>IterationControl.RescaleDensity(1,1,0));Check(IterationControl.RescaleDensity(2,3,2)==3);
});
Test("GPC units convert millimetres to metres",()=>Check(Math.Abs(UnitConversion.ToMetres(1000,UnitsConvert.LengthUnits.mm)-1)<1e-12));
Test("Invalid settings return failure with no geometry",()=>{
    var result=new TensileFormFindingSolver().Solve(ModelBuilder.Build(new[]{Edge(0,1)}),new SolverSettings{MaxIterations=0});
    Check(result.Status==SolveStatus.Failed && result.Positions.Count==0);
});
if(!TensileFormFindingSolver.IsAvailable)
    Test("Missing TensileLib never returns simulated results",()=>{
        var result=new TensileFormFindingSolver().Solve(ModelBuilder.Build(new[]{Edge(0,1)}),new SolverSettings());
        Check(result.Status==SolveStatus.DependencyMissing && result.Positions.Count==0 && result.Messages.Count>0);
    });
Console.WriteLine($"{passed} passed, {failed} failed. Numerical TensileLib validation is not included.");
return failed==0?0:1;
