using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arachne.Arap;
using GPC.Geometry;
using GPC.Utilities.Units;

internal static class Program
{
    private static int passed, failed;
    internal static int Main(string[] args)
    {
        Check("standalone assembly has no Rhino, GH, Tensile, libigl or MathNet reference", () =>
        {
            var references = typeof(ArapSolver).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Require(!references.Any(n => n.Contains("Rhino") || n.Contains("Grasshopper") || n.Contains("Tensile") || n.Contains("IGL") || n.Contains("MathNet")), string.Join(",", references));
            Require(references.Contains("GPCGeometry") && references.Contains("GPCUtilities"), "Missing GPC references");
        });
        Check("GPC mesh snapshot and explicit units", () =>
        {
            var p = new[] { new Point3d(0, 0, 0), new Point3d(1000, 0, 0), new Point3d(0, 1000, 0) }; var f = new[] { 0, 1, 2 };
            var m = new ArapMesh(p, f); p[1].X = 7; f[0] = 2;
            var meters = m.ConvertUnits(UnitsConvert.LengthUnits.mm, UnitsConvert.LengthUnits.m);
            Near(meters.GetVertices()[1].X, 1, 1e-15); Require(m.GetTriangleIndices()[0] == 0, "Mutable input leaked");
            var copy = m.GetVertices(); copy[0].X = 99; Near(m.GetVertices()[0].X, 0, 0);
        });
        Check("XY planar panel preserves lengths", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3))), 1e-8));
        Check("vertical YZ panel develops without XY collapse", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3, (x,y) => new Point3d(7, x, y)))), 1e-8));
        Check("oblique plane develops isometrically", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3, (x,y) => new Point3d(x, y * 0.6, y * 0.8)))), 1e-8));
        Check("obtuse triangles retain signed cotangent weights", () =>
        {
            var m = new ArapMesh(new[] { P(0,0), P(2,0), P(0.1,0.15), P(2,1) }, new[] { 0,1,2, 1,3,2 });
            Isometry(Success(ArapSolver.Unwrap(m)), 1e-7);
        });
        foreach (double scale in new[] { 1e-6, 1.0, 1e6 })
        {
            double s = scale;
            Check("scale invariance " + s, () => Isometry(Success(ArapSolver.Flatten(Grid(3, 3, (x,y) => new Point3d(s*x, s*y*0.6, s*y*0.8)), new ArapOptions { PerimeterTolerance = s*1e-8 })), 1e-7));
        }
        Check("large negative translation", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3, (x,y) => new Point3d(x-1e8, y-2e8, -3e8)))), 1e-7));
        Check("rigid 3D rotation and translation with three pins", () =>
        {
            var m = Grid(5,4); var original = m.GetVertices();
            Func<Point3d,Point3d> transform = p => new Point3d(11-p.Y, -7, 3+p.X);
            var pins = new[] { 0,4,15 }.Select(i => new ArapPin(i, transform(original[i]))).ToArray();
            var r = Success(ArapSolver.Deform(m, pins)); Isometry(r,1e-7);
            var v = r.GetVertices(); for (int i=0;i<v.Length;i++) Near(Distance(v[i], transform(original[i])),0,1e-6);
        });
        Check("3D nonrigid constrained panel and decreasing energy", () =>
        {
            var m = Grid(6,5); var p=m.GetVertices();
            var pins=new[]{new ArapPin(0,p[0]),new ArapPin(5,p[5]),new ArapPin(29,new Point3d(p[29].X,p[29].Y,0.6))};
            var r=Success(ArapSolver.Deform(m,pins,new ArapOptions{MaxIterations=3000})); Pins(r,pins); Monotonic(r);
        });
        Check("2D explicit pins are absolute despite translation", () =>
        {
            var m=Grid(4,3,(x,y)=>new Point3d(x-20,y-30,-5));
            var pins=new[]{new ArapPin(0,P(-19,-29)),new ArapPin(3,P(-17,-29))};
            Pins(Success(ArapSolver.Unwrap(m,pins)),pins);
        });
        Check("all vertices pinned remain exact", () =>
        {
            var m=Grid(3,3); var pins=m.GetVertices().Select((p,i)=>new ArapPin(i,new Point3d(p.X,p.Y,p.X*0.1))).ToArray();
            Pins(Success(ArapSolver.Deform(m,pins)),pins);
        });
        Check("pin output retains the exact supplied doubles", () =>
        {
            var m=Grid(2,2);var pins=m.GetVertices().Select((p,i)=>new ArapPin(i,new Point3d(p.X+0.1,p.Y+0.3,0.7))).ToArray();
            var v=Success(ArapSolver.Deform(m,pins)).GetVertices();
            foreach(var pin in pins)Require(v[pin.Vertex].X==pin.Position.X && v[pin.Vertex].Y==pin.Position.Y && v[pin.Vertex].Z==pin.Position.Z,"Pin rounded during denormalization");
        });
        Check("cylindrical strip unwrap is developable", () => Isometry(Success(ArapSolver.Unwrap(Cylinder(), options:new ArapOptions{MaxIterations=4000})),2e-5));
        Check("folded disk uses positive harmonic initialization", () =>
        {
            var m=Grid(9,3,(x,y)=>new Point3d(Math.Cos(x*2.7),Math.Sin(x*2.7),y));
            var r=Success(ArapSolver.Unwrap(m,options:new ArapOptions{MaxIterations=5000,PositionTolerance=1e-6})); Require(r.GetVertices().All(p=>p.Z==0),"Nonplanar result");
        });
        Check("smooth circular boundary with no angle corners", () => Isometry(Success(ArapSolver.Flatten(Fan(64,0),new ArapOptions{CornerAngle=0.2})),1e-7));
        Check("nondevelopable dome reports distortion and matches perimeter", () =>
        {
            var r=Success(ArapSolver.Flatten(Fan(8,0.6),new ArapOptions{PerimeterTolerance=1e-6,MaxIterations=3000,PerimeterSolveIterations=1000}));
            Require(r.MaxRelativeEdgeError>0.01,"Missing intrinsic distortion"); Require(r.MaxPerimeterError<=1e-6,"Perimeter tolerance");
        });
        Check("perimeter is checked after the last permitted pass", () =>
        {
            var m=Fan(8,0.6);
            var before=ArapSolver.Flatten(m,new ArapOptions{MaxPerimeterIterations=0,MaxIterations=3000});
            var first=ArapSolver.Flatten(m,new ArapOptions{MaxPerimeterIterations=1,PerimeterTolerance=0,MaxIterations=3000,PerimeterSolveIterations=1000});
            Require(first.MaxPerimeterError<before.MaxPerimeterError,"Perimeter pass failed to improve");
            var r=Success(ArapSolver.Flatten(m,new ArapOptions{MaxPerimeterIterations=1,PerimeterTolerance=(before.MaxPerimeterError+first.MaxPerimeterError)/2,MaxIterations=3000,PerimeterSolveIterations=1000}));
            Require(r.PerimeterIterations==1,"Last pass was not exercised");
        });
        Check("unreachable perimeter tolerance publishes no mesh", () => Status(ArapSolver.Flatten(Fan(8,0.6),new ArapOptions{MaxPerimeterIterations=1,PerimeterTolerance=1e-12}),ArapStatus.PerimeterLimit));
        Check("planar annulus with hole", () => Isometry(Success(ArapSolver.Unwrap(Annulus())),1e-7));
        Check("disconnected panels auto-pin each part", () => Isometry(Success(ArapSolver.Flatten(Disconnected())),1e-7));
        Check("one unpinned component rejected", () => Status(ArapSolver.Deform(Disconnected(),new[]{new ArapPin(0,P(0,0))}),ArapStatus.InvalidInput));
        Check("closed tetrahedron may deform", () => Isometry(Success(ArapSolver.Deform(Tetrahedron())),1e-7));
        Check("closed tetrahedron requires cuts to flatten", () => Status(ArapSolver.Flatten(Tetrahedron()),ArapStatus.InvalidInput));
        Check("duplicate pin rejected", () => Status(ArapSolver.Unwrap(Grid(2,2),new[]{new ArapPin(0,P(0,0)),new ArapPin(0,P(1,0))}),ArapStatus.InvalidInput));
        Check("invalid pin index rejected", () => Status(ArapSolver.Deform(Grid(2,2),new[]{new ArapPin(-1,P(0,0))}),ArapStatus.InvalidInput));
        Check("nonfinite pin rejected", () => Status(ArapSolver.Deform(Grid(2,2),new[]{new ArapPin(0,P(double.NaN,0))}),ArapStatus.InvalidInput));
        Check("nonplanar unwrap pin rejected", () => Status(ArapSolver.Unwrap(Grid(2,2),new[]{new ArapPin(0,new Point3d(0,0,1))}),ArapStatus.InvalidInput));
        Check("null mesh rejected", () => Status(ArapSolver.Flatten(null),ArapStatus.InvalidInput));
        Check("empty mesh rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new Point3d[0],new int[0])),ArapStatus.InvalidInput));
        Check("NaN coordinate rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(double.NaN,1)},new[]{0,1,2})),ArapStatus.InvalidInput));
        Check("collinear triangle rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(2,0)},new[]{0,1,2})),ArapStatus.InvalidInput));
        Check("bad face index rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1)},new[]{0,1,3})),ArapStatus.InvalidInput));
        Check("incomplete index triple rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1)},new[]{0,1})),ArapStatus.InvalidInput));
        Check("duplicate triangle rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1)},new[]{0,1,2,2,0,1})),ArapStatus.InvalidInput));
        Check("unused vertex rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1),P(9,9)},new[]{0,1,2})),ArapStatus.InvalidInput));
        Check("inconsistent orientation rejected", () => Status(ArapSolver.Flatten(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1),P(1,1)},new[]{0,1,2,1,2,3})),ArapStatus.InvalidInput));
        Check("three-face nonmanifold edge rejected", () => Status(ArapSolver.Deform(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1),P(0,-1),new Point3d(0,0,1)},new[]{0,1,2,1,0,3,0,1,4})),ArapStatus.InvalidInput));
        Check("bow-tie vertex rejected", () => Status(ArapSolver.Deform(new ArapMesh(new[]{P(0,0),P(1,0),P(0,1),P(-1,0),P(0,-1)},new[]{0,1,2,0,3,4})),ArapStatus.InvalidInput));
        Check("zero perimeter budget publishes no mesh", () => Status(ArapSolver.Flatten(Grid(3,3),new ArapOptions{MaxPerimeterIterations=0}),ArapStatus.PerimeterLimit));
        Check("negative iteration budget rejected", () => Status(ArapSolver.Unwrap(Grid(3,3),options:new ArapOptions{MaxIterations=0}),ArapStatus.InvalidInput));
        Check("NaN tolerance rejected", () => Status(ArapSolver.Unwrap(Grid(3,3),options:new ArapOptions{PositionTolerance=double.NaN}),ArapStatus.InvalidInput));
        Check("iteration limit publishes no mesh", () => Status(ArapSolver.Unwrap(Cylinder(),options:new ArapOptions{MaxIterations=1}),ArapStatus.IterationLimit));
        Check("linear iteration limit is reported", () =>
        {
            var m=Grid(7,7); var p=m.GetVertices(); Status(ArapSolver.Deform(m,new[]{new ArapPin(0,p[0]),new ArapPin(48,new Point3d(3,2,1))},new ArapOptions{MaxLinearIterations=1}),ArapStatus.NumericalFailure);
        });
        Check("reflected planar constraints rejected", () =>
        {
            var m=Grid(2,2); var pins=m.GetVertices().Select((p,i)=>new ArapPin(i,P(-p.X,p.Y))); Status(ArapSolver.Unwrap(m,pins),ArapStatus.InvalidParameterization);
        });
        Check("collapsed 3D triangles rejected", () =>
        {
            var m=Grid(2,2); var pins=m.GetVertices().Select((p,i)=>new ArapPin(i,P(0,0))); Status(ArapSolver.Deform(m,pins),ArapStatus.InvalidParameterization);
        });
        Check("pre-cancelled solve publishes no mesh", () => Status(ArapSolver.Flatten(Grid(3,3),cancellationToken:new CancellationToken(true)),ArapStatus.Cancelled));
        Check("cancellation during iteration", () =>
        {
            var cancel=new CancellationTokenSource(); Status(ArapSolver.Unwrap(Cylinder(),cancellationToken:cancel.Token,progress:s=>cancel.Cancel()),ArapStatus.Cancelled);
        });
        Check("options snapshot survives caller mutation", () =>
        {
            var options=new ArapOptions(); var r=ArapSolver.Flatten(Grid(3,3),options,progress:s=>{options.PerimeterTolerance=double.NaN;options.MaxPerimeterIterations=0;}); Success(r);
        });
        Check("parallel solves do not share settings", () =>
        {
            var jobs=Enumerable.Range(0,8).Select(i=>Task.Run(()=>ArapSolver.Flatten(Grid(4,3),new ArapOptions{MaxPerimeterIterations=i%2==0?0:20}))).ToArray();
            Task.WaitAll(jobs); for(int i=0;i<jobs.Length;i++)Status(jobs[i].Result,i%2==0?ArapStatus.PerimeterLimit:ArapStatus.Converged);
        });
        Check("sparse 1681-vertex planar grid", () => Isometry(Success(ArapSolver.Flatten(Grid(41,41))),1e-7));
        Check("sparse 1681-vertex nonrigid deformation", () =>
        {
            var m=Grid(41,41);var v=m.GetVertices();var pins=new[]{new ArapPin(0,v[0]),new ArapPin(40,v[40]),new ArapPin(1680,new Point3d(2,2,0.2))};
            var r=Success(ArapSolver.Deform(m,pins,new ArapOptions{MaxIterations=3000}));Pins(r,pins);Monotonic(r);
        });
        if(args.Contains("--native")) NativeComparison.Run(Check,Require);
        Console.WriteLine("ARAP checks: " + passed + " passed, " + failed + " failed."); return failed==0?0:1;
    }
    internal static void Check(string name,Action action)
    {
        var watch=Stopwatch.StartNew(); try{action();passed++;Console.WriteLine("PASS | "+name+" | "+watch.ElapsedMilliseconds+" ms");}
        catch(Exception e){failed++;Console.WriteLine("FAIL | "+name+" | "+e.Message);}
    }
    internal static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    internal static void Near(double value,double expected,double tolerance)=>Require(Math.Abs(value-expected)<=tolerance,"Expected "+expected+", got "+value+", tolerance "+tolerance);
    internal static ArapResult Success(ArapResult r){Require(r.Success,r.Status+": "+r.Message+" (iterations="+r.History.Count+")");Require(r.GetVertices().Length>0,"Empty successful result");return r;}
    internal static void Status(ArapResult r,ArapStatus status){Require(r.Status==status,"Expected "+status+", got "+r.Status+": "+r.Message);if(!r.Success)Require(r.GetVertices().Length==0,"Failed result exposed geometry");}
    internal static void Isometry(ArapResult r,double tolerance)=>Require(r.MaxRelativeEdgeError<=tolerance,"Edge distortion "+r.MaxRelativeEdgeError+" > "+tolerance);
    internal static void Pins(ArapResult r,IEnumerable<ArapPin> pins){var v=r.GetVertices();foreach(var pin in pins)Near(Distance(v[pin.Vertex],pin.Position),0,1e-10);}
    internal static void Monotonic(ArapResult r){for(int i=1;i<r.History.Count;i++)if(r.History[i].PerimeterPass==r.History[i-1].PerimeterPass)Require(r.History[i].Energy<=r.History[i-1].Energy+1e-10,"Increasing energy");}
    internal static double Distance(Point3d a,Point3d b)=>Math.Sqrt((a.X-b.X)*(a.X-b.X)+(a.Y-b.Y)*(a.Y-b.Y)+(a.Z-b.Z)*(a.Z-b.Z));
    internal static Point3d P(double x,double y)=>new Point3d(x,y,0);
    internal static ArapMesh Grid(int nx,int ny,Func<double,double,Point3d> map=null)
    {
        map=map??((x,y)=>P(x,y));var p=new List<Point3d>();var f=new List<int>();
        for(int j=0;j<ny;j++)for(int i=0;i<nx;i++)p.Add(map(2.0*i/(nx-1),2.0*j/(ny-1)));
        for(int j=0;j<ny-1;j++)for(int i=0;i<nx-1;i++){int a=j*nx+i;f.AddRange(new[]{a,a+1,a+nx+1,a,a+nx+1,a+nx});}
        return new ArapMesh(p,f);
    }
    internal static ArapMesh Cylinder()=>Grid(9,4,(x,y)=>new Point3d(Math.Sin(x*0.7),y,1-Math.Cos(x*0.7)));
    private static ArapMesh Fan(int n,double height)
    {
        var p=new List<Point3d>{new Point3d(0,0,height)};var f=new List<int>();
        for(int i=0;i<n;i++)p.Add(P(Math.Cos(2*Math.PI*i/n),Math.Sin(2*Math.PI*i/n)));
        for(int i=0;i<n;i++)f.AddRange(new[]{0,i+1,(i+1)%n+1});return new ArapMesh(p,f);
    }
    private static ArapMesh Annulus()
    {
        const int n=12;var p=new List<Point3d>();var f=new List<int>();for(int r=1;r<=2;r++)for(int i=0;i<n;i++)p.Add(P(r*Math.Cos(2*Math.PI*i/n),r*Math.Sin(2*Math.PI*i/n)));
        for(int i=0;i<n;i++){int j=(i+1)%n;f.AddRange(new[]{i,n+i,n+j,i,n+j,j});}return new ArapMesh(p,f);
    }
    private static ArapMesh Disconnected(){var a=Grid(2,2);var b=Grid(2,2,(x,y)=>P(x+5,y));return new ArapMesh(a.GetVertices().Concat(b.GetVertices()),a.GetTriangleIndices().Concat(b.GetTriangleIndices().Select(i=>i+4)));}
    private static ArapMesh Tetrahedron()=>new ArapMesh(new[]{P(0,0),P(1,0),P(0,1),new Point3d(0,0,1)},new[]{0,2,1,0,1,3,0,3,2,1,2,3});
}
