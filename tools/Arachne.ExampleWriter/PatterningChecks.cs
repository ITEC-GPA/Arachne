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
    private static void Solve(GH_Component component, bool expectError=false)
    {
        var doc=new GH_Document { Enabled=true };
        documents.Add(doc);
            component.CreateAttributes();doc.AddObject(component,false);doc.NewSolution(false);
            var errors=component.RuntimeMessages(GH_RuntimeMessageLevel.Error);
            Check(expectError ? errors.Count>0 : errors.Count==0,component.Name+": "+string.Join("; ",errors));
    }
    private static T Output<T>(GH_Component component,int index=0) where T:class,IGH_Goo
    { return (T)component.Params.Output[index].VolatileData.AllData(true).First(); }
    private static Mesh Panel(bool quad=true)
    {
        var m=new Mesh();m.Vertices.Add(1,1,0);m.Vertices.Add(3,1,0);m.Vertices.Add(3,3,0);m.Vertices.Add(1,3,0);
        if(quad)m.Faces.AddFace(0,1,2,3);else{m.Faces.AddFace(0,1,2);m.Faces.AddFace(0,2,3);}
        m.Normals.ComputeNormals();return m;
    }
    private static IGH_Goo FixedBeam(Line line,double length)
    {
        var c=new Canvas();var chain=FormFindingChecks.Start(c);var fl=c.Add(new Arachne.Grasshopper.Components.Attributes.FixedLengthComponent());
        Canvas.Link(fl,0,FormFindingChecks.Beams(chain,new[]{line}));Canvas.Link(fl,1,chain.LoadCase);Canvas.Set(fl,2,new GH_Number(length));c.Solve();
        documents.Add(c.Document);return Canvas.Out(fl).Single();
    }
    private static void ValidMesh(GH_Component component)
    { var m=Output<GH_Mesh>(component).Value;Check(m!=null && m.IsValid && m.Vertices.Count>0,"Invalid or empty mesh"); }
    internal static int Run()
    {
        passed = failed = 0;
        GH_Document.EnableSolutions = true;
        Test("Flattening preserves planar lengths",()=>{
            var c=new FlatteningComponent();var input=Panel();Set(c,0,new GH_Mesh(input));Solve(c);ValidMesh(c);
            var flat=Output<GH_Mesh>(c).Value;Check(Math.Abs(flat.Vertices[0].DistanceTo(flat.Vertices[1])-2)<1e-4,"Edge length changed");
            Check(input.Faces[0].IsQuad,"Input mesh modified");
        });
        Test("Flattening accepts convergence on last iteration",()=>{
            var c=new FlatteningComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,2,new GH_Integer(1));Solve(c);ValidMesh(c);
            Check(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count==0,"Converged panel rejected at iteration limit");
        });
        Test("Flattening publishes the not converged mesh with a warning (FeMM)",()=>{
            var m=Panel();m.Vertices.SetVertex(2,3,3,1);var c=new FlatteningComponent();Set(c,0,new GH_Mesh(m));Set(c,2,new GH_Integer(1));Set(c,3,new GH_Number(1e-12));Solve(c);
            Check(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count>0 && c.Params.Output[0].VolatileData.AllData(true).Any(),"Not converged flattening without warning or geometry");
        });
        Test("Compensation publishes the not converged panel with a warning (FeMM)",()=>{
            var c=new CompensationComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Vector(Vector3d.XAxis));Set(c,2,new GH_Number(0));Set(c,3,new GH_Number(0));Set(c,5,new GH_Integer(1));Set(c,6,new GH_Number(1e-12));
            var incompatible=FixedBeam(new Line(1,1,0,3,1,0),20);
            Set<IGH_Goo>(c,4,incompatible);Solve(c);
            Check(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count>0 && c.Params.Output[0].VolatileData.AllData(true).Any(),"Not converged compensation without warning or geometry");
        });
        Test("Compensation suppresses output after solver exception",()=>{
            var c=new CompensationComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Vector(Vector3d.XAxis));Set(c,2,new GH_Number(10));Set(c,3,new GH_Number(10));Set(c,8,new GH_Number(-1));Solve(c,true);
            Check(!c.Params.Output[0].VolatileData.AllData(true).Any(),"Solver exception published geometry");
        });
        Test("ARAP flatten suppresses unconverged managed result",()=>{
            var c=new FlatteningArapComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Integer(20));Set(c,2,new GH_Integer(0));Solve(c);
            Check(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count>0 && !c.Params.Output[0].VolatileData.AllData(true).Any(),"Unsuccessful ARAP result published geometry");
        });
        Test("Compensation without shrink preserves area",()=>{
            var c=new CompensationComponent();var input=Panel();Set(c,0,new GH_Mesh(input));Set(c,1,new GH_Vector(Vector3d.XAxis));Set(c,2,new GH_Number(0));Set(c,3,new GH_Number(0));Solve(c);ValidMesh(c);
            Check(Math.Abs(AreaMassProperties.Compute(Output<GH_Mesh>(c).Value).Area-4)<1e-4,"Area changed");Check(input.Faces[0].IsQuad,"Input mesh modified");
        });
        Test("Managed ARAP flatten produces finite planar mesh",()=>{
            var c=new FlatteningArapComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Integer(20));Set(c,2,new GH_Integer(20));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Vertices.All(v=>Math.Abs(v.Z)<1e-7),"Nonplanar result");
        });
        Test("Managed ARAP deformation respects pins",()=>{
            var c=new DeformArapComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Integer(20));
            Set(c,2,new GH_Integer(0),new GH_Integer(1));Set(c,3,new GH_Point(new Point3d(1,1,0)),new GH_Point(new Point3d(3,1,0)));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Vertices[0].DistanceTo(new Point3f(1,1,0))<1e-5,"Pin moved: " + Output<GH_Mesh>(c).Value.Vertices[0]);
        });
        Test("Managed ARAP unwrap respects translated pins",()=>{
            var c=new DeformArapComponent();var panel=Panel(false);panel.Translate(new Vector3d(-20,-30,-5));
            Set(c,0,new GH_Mesh(panel));Set(c,1,new GH_Integer(20));Set(c,2,new GH_Integer(0),new GH_Integer(1));
            Set(c,3,new GH_Point(new Point3d(-19,-29,0)),new GH_Point(new Point3d(-17,-29,0)));Set(c,4,new GH_Boolean(true));Solve(c);ValidMesh(c);
            var mesh=Output<GH_Mesh>(c).Value;
            Check(mesh.Vertices[0].DistanceTo(new Point3f(-19,-29,0))<1e-5 && mesh.Vertices[1].DistanceTo(new Point3f(-17,-29,0))<1e-5,"Unwrap moved pins");
        });
        Test("ARAP rejects invalid pins before calculation",()=>{
            var c=new DeformArapComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,2,new GH_Integer(-1));Set(c,3,new GH_Point(Point3d.Origin));Solve(c,true);
            Check(!c.Params.Output[0].VolatileData.AllData(true).Any(),"Invalid pins produced geometry");
        });
        Test("ARAP vertical quad preserves topology",()=>{
            var panel=Panel();panel.Transform(Transform.Rotation(Math.PI/2,Vector3d.YAxis,Point3d.Origin));
            var c=new FlatteningArapComponent();Set(c,0,new GH_Mesh(panel));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Faces[0].IsQuad,"Quad topology lost");
        });
        Test("ARAP invalid tolerances report failure without a mesh",()=>{
            var c=new FlatteningArapComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,3,new GH_Number(-1));Solve(c,true);
            Check(!c.Params.Output[0].VolatileData.AllData(true).Any(),"Invalid settings produced geometry");
        });
        Test("ARAP default anchors support disconnected mesh parts",()=>{
            var panel=Panel();var second=Panel();second.Translate(5,0,0);panel.Append(second);
            var c=new DeformArapComponent();Set(c,0,new GH_Mesh(panel));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Vertices.Count==8,"Disconnected vertices lost");
        });
        Test("ARAP preserves double precision Rhino vertices",()=>{
            var panel=Panel();panel.Vertices.UseDoublePrecisionVertices=true;
            for(int i=0;i<panel.Vertices.Count;i++){var p=panel.Vertices.Point3dAt(i);panel.Vertices.SetVertex(i,p.X+1e8+0.1,p.Y+1e8+0.3,p.Z);}
            var c=new DeformArapComponent();Set(c,0,new GH_Mesh(panel));Solve(c);ValidMesh(c);
            var output=Output<GH_Mesh>(c).Value;
            Check(output.Vertices.UseDoublePrecisionVertices,"Single precision output");
            for(int i=0;i<panel.Vertices.Count;i++)Check(output.Vertices.Point3dAt(i).DistanceTo(panel.Vertices.Point3dAt(i))<1e-7,"Precision lost");
        });
        Test("ARAP pin position without index keeps the original convention",()=>{
            var c=new DeformArapComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,3,new GH_Point(new Point3d(0.1,0.3,0.7)));Solve(c);ValidMesh(c);
            Check(Output<GH_Mesh>(c).Value.Vertices.Point3dAt(0).DistanceTo(new Point3d(0.1,0.3,0.7))<1e-12,"Default pin convention changed");
        });
        Test("ARAP integration never loads the legacy DLL",()=>{
            using(var process=System.Diagnostics.Process.GetCurrentProcess())
            Check(!process.Modules.Cast<System.Diagnostics.ProcessModule>().Any(m=>string.Equals(m.ModuleName,"Arachne.IGL.dll",StringComparison.OrdinalIgnoreCase)),"Legacy ARAP DLL loaded");
        });
        Test("Compensation applies warp and weft percentages",()=>{
            var c=new CompensationComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Vector(Vector3d.XAxis));Set(c,2,new GH_Number(10));Set(c,3,new GH_Number(20));Solve(c);ValidMesh(c);
            var result=Output<GH_Mesh>(c).Value;
            Check(Math.Abs(AreaMassProperties.Compute(result).Area-2.88)<1e-3,"Wrong compensated area");
            Check(Math.Abs(result.Vertices[0].DistanceTo(result.Vertices[1])-1.8)<1e-3,"Wrong warp length");
            Check(Math.Abs(result.Vertices[1].DistanceTo(result.Vertices[2])-1.6)<1e-3,"Wrong weft length");
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
        Test("Decompensation accepts lines and identical duplicate breakpoints",()=>{
            var descriptor=new Arachne.Grasshopper.Helpers.DecompensationDescriptor();
            foreach(double s in new[]{0.0,0.5,0.5,1.0})descriptor.Points.Add(new Arachne.Grasshopper.Helpers.DecompensationDescriptor.DecompensationPoint(true,s,1,1));
            var c=new DecompensationComponent();Set(c,0,new GH_Curve(new LineCurve(Point3d.Origin,new Point3d(4,0,0))));Set<IGH_Goo>(c,1,new GH_ObjectWrapper(descriptor));Solve(c);
            Check(Output<GH_Curve>(c).Value.IsValid && Math.Abs(Output<GH_Curve>(c).Value.GetLength()-4)<1e-7,"Invalid curve at a breakpoint");
        });
        Test("Extended descriptor rejects mismatched lists",()=>{
            var c=new DecompensationDescriptorExtComponent();Set(c,0,new GH_Number(0),new GH_Number(1));Set(c,1,new GH_Boolean(true));
            Set(c,2,new GH_Number(0),new GH_Number(0));Set(c,3,new GH_Number(0),new GH_Number(0));Solve(c);
            Check(!c.Params.Output[0].VolatileData.AllData(true).Any() && c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count>0,"Invalid lists accepted");
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
        Test("Mesh remap rejects incompatible topology",()=>{
            var c=new MeshRemapComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Mesh(Panel(false)));Set<IGH_GeometricGoo>(c,2,new GH_Point(new Point3d(2,2,0)));Solve(c);
            Check(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count>0 && !c.Params.Output[0].VolatileData.AllData(true).Any(),"Incompatible topology was mapped");
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
        Test("Remesh keeps the panel area",()=>{
            var c=new Arachne.Grasshopper.Components.Tools.RemeshComponent();Set(c,0,new GH_Mesh(Panel()));Set(c,1,new GH_Number(0.5));Solve(c);
            var m=c.Params.Output[0].VolatileData.AllData(true).OfType<GH_Mesh>().Single().Value;
            Check(m.IsValid && m.Faces.Count>4 && Math.Abs(AreaMassProperties.Compute(m).Area-4)<1e-3,"Remeshed area "+AreaMassProperties.Compute(m).Area+", faces "+m.Faces.Count);
        });
        Test("Unweld shared edge",()=>{
            var c=new UnweldMeshComponent();Set(c,0,new GH_Mesh(Panel(false)));Set(c,1,new GH_Curve(new LineCurve(new Point3d(1,1,0),new Point3d(3,3,0))));Solve(c);Check(c.Params.Output[0].VolatileDataCount>0,"No unweld output");
        });
        Test("Panel Distortion: no change on a moved panel, 1% on a scaled one",()=>{
            var panel=Panel();var moved=panel.DuplicateMesh();moved.Translate(new Vector3d(10,0,0));
            var same=new PanelDistortionComponent();Set(same,0,new GH_Mesh(panel));Set(same,1,new GH_Mesh(moved));Solve(same);
            Check(same.Params.Output[0].VolatileDataCount==4 && same.Params.Output[1].VolatileDataCount==4,"Edges "+same.Params.Output[0].VolatileDataCount);
            Check(Output<GH_Number>(same,2).Value<1e-9 && Math.Abs(Output<GH_Number>(same,3).Value)<1e-9,"Distortion of a moved panel");
            var scaled=panel.DuplicateMesh();scaled.Transform(Transform.Scale(Point3d.Origin,1.01));
            var larger=new PanelDistortionComponent();Set(larger,0,new GH_Mesh(panel));Set(larger,1,new GH_Mesh(scaled));Solve(larger);
            Check(Math.Abs(Output<GH_Number>(larger,2).Value-1)<1e-9 && Math.Abs(Output<GH_Number>(larger,3).Value-2.01)<1e-9,
                "Scaled panel: "+Output<GH_Number>(larger,2).Value+"%, area "+Output<GH_Number>(larger,3).Value+"%");
        });
        Test("Panel Distortion of a curved panel and of its flattening",()=>{
            var curved=new Mesh();
            for(int y=0;y<=4;y++)for(int x=0;x<=4;x++)curved.Vertices.Add(x,y,0.05*(x-2)*(x-2)-0.05*(y-2)*(y-2));
            for(int y=0;y<4;y++)for(int x=0;x<4;x++)curved.Faces.AddFace(y*5+x,y*5+x+1,(y+1)*5+x+1,(y+1)*5+x);
            curved.Normals.ComputeNormals();
            // Default inputs: with the FeMM solver tolerance 0 a curved panel failed with "Solver not converged"
            var flat=new FlatteningComponent();Set(flat,0,new GH_Mesh(curved));Solve(flat);
            Check(flat.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count==0,"Flattening: "+string.Join("; ",flat.RuntimeMessages(GH_RuntimeMessageLevel.Warning)));
            var distortion=new PanelDistortionComponent();Set(distortion,0,new GH_Mesh(curved));Set(distortion,1,Output<GH_Mesh>(flat));Solve(distortion);
            double max=Output<GH_Number>(distortion,2).Value;
            Check(distortion.Params.Output[1].VolatileDataCount==curved.TopologyEdges.Count && max>0 && max<5,"Max strain "+max+"%");
            var other=new PanelDistortionComponent();Set(other,0,new GH_Mesh(curved));Set(other,1,new GH_Mesh(Panel()));Solve(other,true);
        });
        Console.WriteLine("Patterning checks: "+passed+" passed, "+failed+" failed.");
        foreach(var document in documents) document.Dispose();
        return failed;
    }
}
