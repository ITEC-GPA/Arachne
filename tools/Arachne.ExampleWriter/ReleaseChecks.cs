using System;
using System.Linq;
using System.Collections.Generic;
using Arachne.Core;
using Arachne.Grasshopper.Components;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

internal static class ReleaseChecks
{
    private static int passed,failed;
    private static void Check(bool b,string message="Assertion failed"){if(!b)throw new Exception(message);}
    private static void Test(string name,Action action){try{action();passed++;Console.WriteLine("RELEASE PASS "+name);}catch(Exception e){failed++;Console.WriteLine("RELEASE FAIL "+name+": "+e.GetBaseException().Message);}}
    private static void Set<T>(GH_Component c,int i,params T[] values)where T:class,IGH_Goo{var p=(GH_PersistentParam<T>)c.Params.Input[i];p.PersistentData.Clear();p.SetPersistentData((IEnumerable<T>)values);}
    private static object[] Output(GH_Component c,int i)=>c.Params.Output[i].VolatileData.AllData(true).Select(g=>g.ScriptVariable()).ToArray();
    private static void Run(GH_Component c,Action check){using(var doc=new GH_Document{Enabled=true}){c.CreateAttributes();doc.AddObject(c,false);doc.NewSolution(false);Check(c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count==0,string.Join(";",c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));check();}}
    public static void Run()
    {
        GH_Document.EnableSolutions=true;
        var a=new GPC.Geometry.Point3d(0,0,0);var b=new GPC.Geometry.Point3d(2,0,0);var center=new GPC.Geometry.Point3d(1,0,0);
        var model=ModelBuilder.Build(new[]{new Cable{Start=a,End=center,Target=2},new Cable{Start=center,End=b,Target=2}},new[]{new NodeCondition{Position=a,FixX=true,FixY=true,FixZ=true},new NodeCondition{Position=b,FixX=true,FixY=true,FixZ=true},new NodeCondition{Position=center,Force=new GPC.Geometry.Vector3d(0,0,-4)}});
        var result=new TensileFormFindingSolver().Solve(model,new SolverSettings{Tolerance=1e-10});Check(result.Status==SolveStatus.Converged,string.Join(";",result.Messages));
        Test("Deconstruct model IDs and restraint branches",()=>{var c=new DeconstructModelComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(model));Run(c,()=>Check(Output(c,2).Length==3&&c.Params.Output[7].VolatileData.PathCount==3&&Output(c,4).Length==2));});
        Test("Form Find appends structured outputs without changing old ports",()=>{var c=new FormFindComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(model));Set(c,2,new GH_Boolean(true));Run(c,()=>Check(Output(c,6).Single().ToString()=="Converged"&&Output(c,9).Single() is FormResult&&Output(c,10).Single() is SolvedModel));});
        Test("Node query honors selected ID and exposes reaction",()=>{var c=new NodeResultsComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(result));Set(c,1,new GH_Integer(1));Run(c,()=>{Check((int)Output(c,0).Single()==1);var v=(Vector3d)Output(c,4).Single();Check(Math.Abs(v.Z-2)<1e-7);});});
        Test("Beam query reports N=q*L",()=>{var c=new BeamResultsComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(result));Run(c,()=>{double n=(double)Output(c,4)[0],q=(double)Output(c,5)[0],l=(double)Output(c,6)[0];Check(Math.Abs(n-q*l)<1e-9);});});
        Test("Equilibrium component independent acceptance",()=>{var c=new EquilibriumReportComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(result));Run(c,()=>Check((bool)Output(c,0).Single()));});
        Test("History component exposes inner iteration count",()=>{var c=new SolverHistoryComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(result));Run(c,()=>Check(Output(c,1).Length==result.History.Count&&result.History.Count>0));});
        Test("Deconstruct result retains initial model and solved snapshot",()=>{var c=new DeconstructResultComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(result));Run(c,()=>Check(Output(c,0).Single() is FormModel&&Output(c,1).Single() is SolvedModel));});
        Test("Check Model valid restrained cable",()=>{var c=new CheckModelComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(model));Run(c,()=>Check((bool)Output(c,0).Single()));});
        Test("Preview scalar and geometry counts",()=>{var c=new ResultPreviewComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(result));Run(c,()=>Check(Output(c,0).Length==2&&Output(c,1).Length==2&&Output(c,2).Length==2));});
        Test("Unit settings and numeric conversion",()=>{var c=new UnitSettingsComponent();Set(c,0,new GH_Integer(0));Run(c,()=>Check(Math.Abs((double)Output(c,1).Single()-0.001)<1e-12));var convert=new ConvertUnitsComponent();Set(convert,0,new GH_Number(1000));Set<IGH_Goo>(convert,1,new GH_ObjectWrapper(new UnitSettings(GPC.Utilities.Units.UnitsConvert.LengthUnits.mm)));Run(convert,()=>Check((double)Output(convert,0).Single()==1));});
        Test("Panel distortion detects 10 percent scaling",()=>{var m=new Mesh();m.Vertices.Add(0,0,0);m.Vertices.Add(1,0,0);m.Vertices.Add(0,1,0);m.Faces.AddFace(0,1,2);var flat=m.DuplicateMesh();flat.Scale(0.9);var c=new PanelDistortionComponent();Set(c,0,new GH_Mesh(m));Set(c,1,new GH_Mesh(flat));Run(c,()=>Check(Math.Abs((double)Output(c,4).Single()-0.1)<1e-6));});
        Test("All local dependencies load and managed ARAP runs",()=>{var c=new DependencyCheckComponent();Run(c,()=>Check((bool)Output(c,0).Single(),string.Join(";",Output(c,1))));});
        Test("Background result completes and Run false clears all result outputs",()=>{
            var c=new FormFindComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(model));Set(c,2,new GH_Boolean(true));Set(c,3,new GH_Boolean(true));
            using(var doc=new GH_Document{Enabled=true}){
                c.CreateAttributes();doc.AddObject(c,false);doc.NewSolution(false);var watch=System.Diagnostics.Stopwatch.StartNew();
                while(!Output(c,9).Any()&&watch.ElapsedMilliseconds<10000){System.Threading.Thread.Sleep(10);Rhino.RhinoApp.Wait();c.ExpireSolution(false);doc.NewSolution(false);}
                Check(Output(c,9).Single() is FormResult,"Background result missing");Set(c,2,new GH_Boolean(false));c.ExpireSolution(false);doc.NewSolution(false);Check(!Output(c,9).Any()&&!Output(c,10).Any()&&Output(c,6).Single().ToString()=="Ready");
            }
        });
        Test("Plate query keeps ordered boundary IDs, forces and unavailable tensor status",()=>{
            var pts=Enumerable.Range(0,9).Select(i=>new GPC.Geometry.Point3d(i%3,i/3,0)).ToArray();var cables=new List<Cable>();var conditions=new List<NodeCondition>();var faces=new List<GPC.Geometry.Point3d[]>();
            for(int i=0;i<9;i++){if(i%3<2)cables.Add(new Cable{Start=pts[i],End=pts[i+1],Target=2});if(i<6)cables.Add(new Cable{Start=pts[i],End=pts[i+3],Target=2});if(i!=4)conditions.Add(new NodeCondition{Position=pts[i],FixX=true,FixY=true,FixZ=true});if(i%3<2&&i<6)faces.Add(new[]{pts[i],pts[i+1],pts[i+4],pts[i+3]});}
            var r=new TensileFormFindingSolver().Solve(ModelBuilder.Build(cables,conditions,faces),new SolverSettings{CreateMesh=false});Check(r.Status==SolveStatus.Converged,string.Join(";",r.Messages));
            var c=new PlateResultsComponent();Set<IGH_Goo>(c,0,new GH_ObjectWrapper(r));Set(c,1,new GH_Integer(2));Run(c,()=>{Check((int)Output(c,0).Single()==2&&Output(c,1).Single() is Mesh);Check(Output(c,3).Cast<int>().SequenceEqual(r.Solved.Plate(2).BoundaryBeamIds));Check(Output(c,4).Cast<double>().SequenceEqual(r.Solved.Plate(2).BoundaryAxialForces));Check(!(bool)Output(c,7).Single());});
        });
        Console.WriteLine($"Release component checks: {passed} passed, {failed} failed.");if(failed>0)throw new Exception("Release component checks failed.");
    }
}
