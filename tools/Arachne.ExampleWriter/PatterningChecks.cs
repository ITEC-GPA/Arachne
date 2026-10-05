using System;
using System.Linq;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Arachne.Grasshopper.Components.Patterning;

internal static class PatterningChecks
{
    private static int passed, failed;
    private static readonly List<GH_Document> documents = new List<GH_Document>();
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PATTERN PASS " + name); }
        catch(Exception e) { failed++; Console.WriteLine("PATTERN FAIL " + name + ": " + e.GetBaseException().Message); }
    }
    private static void Check(bool condition, string message) { if(!condition) throw new Exception(message); }
    private static void Set<T>(GH_Component component, int index, params T[] values) where T : class, IGH_Goo
    {
        var parameter = (GH_PersistentParam<T>)component.Params.Input[index];
        parameter.PersistentData.Clear();
        parameter.SetPersistentData((IEnumerable<T>)values);
    }
    private static void Solve(GH_Component component)
    {
        var doc=new GH_Document { Enabled=true };
        documents.Add(doc);
            component.CreateAttributes();doc.AddObject(component,false);doc.NewSolution(false);
            var errors=component.RuntimeMessages(GH_RuntimeMessageLevel.Error);
            Check(errors.Count==0,component.Name+": "+string.Join("; ",errors));
    }
    private static T Output<T>(GH_Component component,int index=0) where T:class,IGH_Goo
    { return (T)component.Params.Output[index].VolatileData.AllData(true).First(); }
    private static Mesh Panel(bool quad=true)
    {
        var m=new Mesh();m.Vertices.Add(1,1,0);m.Vertices.Add(3,1,0);m.Vertices.Add(3,3,0);m.Vertices.Add(1,3,0);
        if(quad)m.Faces.AddFace(0,1,2,3);else{m.Faces.AddFace(0,1,2);m.Faces.AddFace(0,2,3);}
        m.Normals.ComputeNormals();return m;
    }
    private static void ValidMesh(GH_Component component)
    { var m=Output<GH_Mesh>(component).Value;Check(m!=null && m.IsValid && m.Vertices.Count>0,"Invalid or empty mesh"); }
    public static void Run()
    {
        GH_Document.EnableSolutions = true;
        Test("All 30 components instantiate with distinct GUIDs",()=>{
            var types=typeof(FlatteningComponent).Assembly.GetTypes().Where(t=>!t.IsAbstract && typeof(GH_Component).IsAssignableFrom(t)).ToArray();
            var components=types.Select(t=>(GH_Component)Activator.CreateInstance(t)).ToArray();
            Check(components.Length==30 && components.Select(c=>c.ComponentGuid).Distinct().Count()==30,"Incomplete inventory or duplicate GUIDs");
        });
        Test("Flattening preserves planar lengths",()=>{
            var c=new FlatteningComponent();var input=Panel();Set(c,0,new GH_Mesh(input));Solve(c);ValidMesh(c);
            var flat=Output<GH_Mesh>(c).Value;Check(Math.Abs(flat.Vertices[0].DistanceTo(flat.Vertices[1])-2)<1e-4,"Edge length changed");
            Check(input.Faces[0].IsQuad,"Input mesh modified");
        });
        Test("Compensation without shrink preserves area",()=>{
            var c=new CompensationComponent();var input=Panel();Set(c,0,new GH_Mesh(input));Set(c,1,new GH_Vector(Vector3d.XAxis));Set(c,2,new GH_Number(0));Set(c,3,new GH_Number(0));Solve(c);ValidMesh(c);
            Check(Math.Abs(AreaMassProperties.Compute(Output<GH_Mesh>(c).Value).Area-4)<1e-4,"Area changed");Check(input.Faces[0].IsQuad,"Input mesh modified");
        });
        Test("Native ARAP flatten produces finite planar mesh",()=>{
            var c=new FlatteningArapComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Integer(20));Set(c,2,new GH_Integer(20));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Vertices.All(v=>Math.Abs(v.Z)<1e-7),"Nonplanar result");
        });
        Test("Native ARAP deformation respects pins",()=>{
            var c=new DeformArapComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Integer(20));
            Set(c,2,new GH_Integer(0),new GH_Integer(1));Set(c,3,new GH_Point(new Point3d(1,1,0)),new GH_Point(new Point3d(3,1,0)));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Vertices[0].DistanceTo(new Point3f(1,1,0))<1e-5,"Pin moved: " + Output<GH_Mesh>(c).Value.Vertices[0]);
        });
        Test("Decompensation descriptor and identity mapping",()=>{
            var descriptor=new DecompensationDescriptorComponent();for(int i=2;i<6;i++)Set(descriptor,i,new GH_Number(0));Solve(descriptor);
            var c=new DecompensationComponent();var curve=new PolylineCurve(new[]{new Point3d(1,1,0),new Point3d(3,1,0),new Point3d(3,3,0)});
            Set(c,0,new GH_Curve(curve));Set<IGH_Goo>(c,1,Output<GH_ObjectWrapper>(descriptor));Solve(c);
            var result=Output<GH_Curve>(c).Value;Check(result.IsValid && Math.Abs(result.GetLength()-4)<1e-7,"Identity decompensation failed");
        });
        Test("Extended decompensation descriptor",()=>{
            var c=new DecompensationDescriptorExtComponent();Set(c,0,new GH_Number(0),new GH_Number(1));Set(c,1,new GH_Boolean(true),new GH_Boolean(true));
            Set(c,2,new GH_Number(0),new GH_Number(0));Set(c,3,new GH_Number(0),new GH_Number(0));Solve(c);Output<GH_ObjectWrapper>(c);
        });
        Test("Decompensation interpolates original arc length",()=>{
            var descriptor=new Arachne.Grasshopper.Helpers.DecompensationDescriptor();
            descriptor.Points.Add(new Arachne.Grasshopper.Helpers.DecompensationDescriptor.DecompensationPoint(true,0,1,1));
            descriptor.Points.Add(new Arachne.Grasshopper.Helpers.DecompensationDescriptor.DecompensationPoint(true,1,0.5,1));
            var c=new DecompensationComponent();Set(c,0,new GH_Curve(new PolylineCurve(new[]{new Point3d(0,0,0),new Point3d(1,0,0),new Point3d(1,3,0)})));
            Set<IGH_Goo>(c,1,new GH_ObjectWrapper(descriptor));Solve(c);
            Check(Output<GH_Curve>(c).Value.TryGetPolyline(out var result),"Not a polyline");
            Check(Math.Abs(result[1].X-0.9375)<1e-7,"Interpolation uses curve parameter instead of arc length: "+result[1]);
        });
        Test("Decompensation preserves reusable absolute descriptor",()=>{
            var descriptor=new Arachne.Grasshopper.Helpers.DecompensationDescriptor();
            descriptor.Points.Add(new Arachne.Grasshopper.Helpers.DecompensationDescriptor.DecompensationPoint(false,0,1,1));
            descriptor.Points.Add(new Arachne.Grasshopper.Helpers.DecompensationDescriptor.DecompensationPoint(false,4,1,1));
            var c=new DecompensationComponent();Set(c,0,new GH_Curve(new PolylineCurve(new[]{Point3d.Origin,new Point3d(4,0,0)})));
            Set<IGH_Goo>(c,1,new GH_ObjectWrapper(descriptor));Solve(c);
            Check(!descriptor.Points[1].IsNormalized && descriptor.Points[1].Length==4,"Input descriptor was changed");
        });
        Test("Extended descriptor rejects mismatched lists",()=>{
            var c=new DecompensationDescriptorExtComponent();Set(c,0,new GH_Number(0),new GH_Number(1));Set(c,1,new GH_Boolean(true));
            Set(c,2,new GH_Number(0),new GH_Number(0));Set(c,3,new GH_Number(0),new GH_Number(0));Solve(c);
            Check(c.Params.Output[0].VolatileDataCount==0 && c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count>0,"Invalid lists accepted");
        });
        Test("Borders and ordering",()=>{
            var c=new BordersComponent();Set(c,0,new GH_Mesh(Panel()));Solve(c);Check(c.Params.Output[0].VolatileDataCount>0,"No borders");
            var o=new OrderBordersComponent();Set(o,0,c.Params.Output[0].VolatileData.AllData(true).Cast<GH_Curve>().ToArray());Solve(o);Check(o.Params.Output[0].VolatileDataCount>0,"No ordered borders");
        });
        Test("Mesh pipe",()=>{var c=new MeshPipeComponent();Set(c,0,new GH_Line(new Line(0,0,0,0,0,3)));Solve(c);ValidMesh(c);});
        Test("Mesh clean",()=>{var c=new MeshCleanComponent();Set(c,0,new GH_Mesh(Panel()));Solve(c);ValidMesh(c);});
        Test("Subdivision",()=>{var c=new SubdivideMeshComponent();Set(c,0,new GH_Mesh(Panel()));Solve(c);Check(c.Params.Output[0].VolatileDataCount>0,"No subdivision output");});
        Test("Mesh remap keeps identity point",()=>{
            var c=new MeshRemapComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Mesh(Panel()));
            Set<IGH_GeometricGoo>(c,2,new GH_Point(new Point3d(2,2,0)));Set<IGH_GeometricGoo>(c,3,new GH_Point(new Point3d(2,2,0)));Solve(c);
            var p=Output<GH_Point>(c).Value;Check(p.DistanceTo(new Point3d(2,2,0))<1e-6,"Remap changed point");
        });
        Test("Line-up",()=>{
            var c=new LineUpComponent();Set<IGH_GeometricGoo>(c,0,new GH_Mesh(Panel()),new GH_Mesh(Panel()));Set(c,2,new GH_Number(1));Solve(c);Check(c.Params.Output[0].VolatileDataCount==2,"Lost geometry; inputs="+string.Join(",",c.Params.Input.Select(p=>p.VolatileDataCount))+"; phase="+c.Phase+"; warnings="+string.Join(";",c.RuntimeMessages(GH_RuntimeMessageLevel.Warning)));
        });
        Test("Mesh geodesic on plane",()=>{
            var c=new MeshGeodesicComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Point(new Point3d(1,2,0)));Set(c,2,new GH_Point(new Point3d(3,2,0)));Set(c,3,new GH_Number(0.25));Set(c,4,new GH_Integer(10));Solve(c);
            Check(Math.Abs(Output<GH_Curve>(c).Value.GetLength()-2)<1e-3,"Incorrect planar geodesic");
        });
        Test("Mesh splitting",()=>{
            var c=new SplitMeshOnCurvesComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Curve(new LineCurve(new Point3d(2,0,0),new Point3d(2,4,0))));Solve(c);Check(c.Params.Output[0].VolatileDataCount>=2,"Mesh not split");
        });
        Test("Mesh divide produces central geodesic",()=>{
            var c=new MeshDivideComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Integer(2));Set(c,3,new GH_Number(0.25));Set(c,4,new GH_Integer(10));Solve(c);
            Check(Math.Abs(Output<GH_Curve>(c).Value.GetLength()-2)<1e-3,"Incorrect cut length");
        });
        Test("Geodesic offset produces half-unit strip",()=>{
            var c=new GeodesicOffsetsComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Curve(new LineCurve(new Point3d(2,1,0),new Point3d(2,3,0))));
            Set(c,3,new GH_Number(0.5));Set(c,4,new GH_Number(0.01));Set(c,5,new GH_Number(0.25));Set(c,6,new GH_Integer(10));Solve(c);
            Check(c.Params.Output[0].VolatileDataCount>0,"No offset curve");
            Check(Math.Abs(AreaMassProperties.Compute(Output<GH_Mesh>(c,1).Value).Area-1)<0.03,"Incorrect strip area");
        });
        Test("Unweld shared edge",()=>{
            var c=new UnweldMeshComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Curve(new LineCurve(new Point3d(1,1,0),new Point3d(3,3,0))));Solve(c);Check(c.Params.Output[0].VolatileDataCount>0,"No unweld output");
        });
        Console.WriteLine("Patterning checks: "+passed+" passed, "+failed+" failed.");
        foreach(var document in documents) document.Dispose();
        if(failed>0)throw new Exception("Patterning checks failed.");
    }
}
