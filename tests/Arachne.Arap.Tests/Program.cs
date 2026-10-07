using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Arachne.Arap;
using GPC.Geometry;
using GPC.Utilities.Units;

// Checks of the managed ARAP engine: isometric developments with known answers, pins,
// curved surfaces, rejected inputs, limits, cancellation and size. With --native the results
// are also compared with the old native library (Arachne.IGL.dll), when it is available.
internal static class Program
{
    private static int passed, failed;

    internal static int Main(string[] args)
    {
        // ---- Engine and mesh ----

        Check("The engine depends only on the GPC libraries", () =>
        {
            var references = typeof(ArapSolver).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
            Require(!references.Any(n => n.Contains("Rhino") || n.Contains("Grasshopper") || n.Contains("Tensile") || n.Contains("IGL") || n.Contains("MathNet")),
                "Unexpected references: " + string.Join(", ", references));
            Require(references.Contains("GPCGeometry") && references.Contains("GPCUtilities"), "GPC references missing");
        });

        Check("The mesh copies its input and converts units explicitly", () =>
        {
            var points = new[] { new Point3d(0, 0, 0), new Point3d(1000, 0, 0), new Point3d(0, 1000, 0) };
            var faces = new[] { 0, 1, 2 };
            var mesh = new ArapMesh(points, faces);
            points[1].X = 7; faces[0] = 2;   // changing the caller's arrays does not change the mesh
            Near(mesh.ConvertUnits(UnitsConvert.LengthUnits.mm, UnitsConvert.LengthUnits.m).GetVertices()[1].X, 1, 1e-15);
            Require(mesh.GetTriangleIndices()[0] == 0, "Faces changed through the input array");
            var copy = mesh.GetVertices(); copy[0].X = 99;
            Near(mesh.GetVertices()[0].X, 0, 0);
        });

        // ---- Isometric developments: a developable panel keeps its edge lengths ----

        Check("Flat panel on XY", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3))), 1e-8));
        Check("Flat vertical panel on YZ, without collapsing on XY", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3, (x, y) => new Point3d(7, x, y)))), 1e-8));
        Check("Flat oblique panel", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3, (x, y) => new Point3d(x, y * 0.6, y * 0.8)))), 1e-8));
        Check("Obtuse triangles keep the sign of their cotangent weights", () =>
            Isometry(Success(ArapSolver.Unwrap(new ArapMesh(new[] { P(0, 0), P(2, 0), P(0.1, 0.15), P(2, 1) }, new[] { 0, 1, 2, 1, 3, 2 }))), 1e-7));
        foreach (double scale in new[] { 1e-6, 1.0, 1e6 })
        {
            double s = scale;
            Check("Scale " + s + ": same relative accuracy", () =>
                Isometry(Success(ArapSolver.Flatten(Grid(3, 3, (x, y) => new Point3d(s * x, s * y * 0.6, s * y * 0.8)), new ArapOptions { PerimeterTolerance = s * 1e-8 })), 1e-7));
        }
        Check("Panel 1e8 from the origin", () => Isometry(Success(ArapSolver.Flatten(Grid(4, 3, (x, y) => new Point3d(x - 1e8, y - 2e8, -3e8)))), 1e-7));

        // ---- Pins ----

        Check("Three pins moved rigidly move the whole panel rigidly", () =>
        {
            var mesh = Grid(5, 4); var original = mesh.GetVertices();
            Func<Point3d, Point3d> move = p => new Point3d(11 - p.Y, -7, 3 + p.X);
            var pins = new[] { 0, 4, 15 }.Select(i => new ArapPin(i, move(original[i]))).ToArray();
            var result = Success(ArapSolver.Deform(mesh, pins));
            Isometry(result, 1e-7);
            var vertices = result.GetVertices();
            for (int i = 0; i < vertices.Length; i++) Near(Distance(vertices[i], move(original[i])), 0, 1e-6);
        });

        Check("A pin lifted out of the panel: pins kept, energy never increasing", () =>
        {
            var mesh = Grid(6, 5); var p = mesh.GetVertices();
            var pins = new[] { new ArapPin(0, p[0]), new ArapPin(5, p[5]), new ArapPin(29, new Point3d(p[29].X, p[29].Y, 0.6)) };
            var result = Success(ArapSolver.Deform(mesh, pins, new ArapOptions { MaxIterations = 3000 }));
            Pins(result, pins); Monotonic(result);
        });

        Check("Unwrap pins are absolute positions, also for a translated panel", () =>
        {
            var mesh = Grid(4, 3, (x, y) => new Point3d(x - 20, y - 30, -5));
            var pins = new[] { new ArapPin(0, P(-19, -29)), new ArapPin(3, P(-17, -29)) };
            Pins(Success(ArapSolver.Unwrap(mesh, pins)), pins);
        });

        Check("All vertices pinned: exactly where given", () =>
        {
            var mesh = Grid(3, 3);
            var pins = mesh.GetVertices().Select((p, i) => new ArapPin(i, new Point3d(p.X, p.Y, p.X * 0.1))).ToArray();
            Pins(Success(ArapSolver.Deform(mesh, pins)), pins);
        });

        Check("Pinned vertices keep the given doubles bit for bit", () =>
        {
            var mesh = Grid(2, 2);
            var pins = mesh.GetVertices().Select((p, i) => new ArapPin(i, new Point3d(p.X + 0.1, p.Y + 0.3, 0.7))).ToArray();
            var vertices = Success(ArapSolver.Deform(mesh, pins)).GetVertices();
            foreach (var pin in pins)
                Require(vertices[pin.Vertex].X == pin.Position.X && vertices[pin.Vertex].Y == pin.Position.Y && vertices[pin.Vertex].Z == pin.Position.Z, "Pin rounded");
        });

        // ---- Curved and special surfaces ----

        Check("Cylindrical strip: developable, unwrapped without distortion", () =>
            Isometry(Success(ArapSolver.Unwrap(Cylinder(), options: new ArapOptions { MaxIterations = 4000 })), 2e-5));

        Check("Strip folded on itself: planar result from a positive start", () =>
        {
            var mesh = Grid(9, 3, (x, y) => new Point3d(Math.Cos(x * 2.7), Math.Sin(x * 2.7), y));
            var result = Success(ArapSolver.Unwrap(mesh, options: new ArapOptions { MaxIterations = 5000, PositionTolerance = 1e-6 }));
            Require(result.GetVertices().All(p => p.Z == 0), "Not planar");
        });

        Check("Round border without corners", () => Isometry(Success(ArapSolver.Flatten(Fan(64, 0), new ArapOptions { CornerAngle = 0.2 })), 1e-7));

        Check("Dome (not developable): distortion reported, perimeter matched", () =>
        {
            var result = Success(ArapSolver.Flatten(Fan(8, 0.6), new ArapOptions { PerimeterTolerance = 1e-6, MaxIterations = 3000, PerimeterSolveIterations = 1000 }));
            Require(result.MaxRelativeEdgeError > 0.01, "No distortion reported for a dome");
            Require(result.MaxPerimeterError <= 1e-6, "Perimeter error " + result.MaxPerimeterError);
        });

        Check("The perimeter is checked after the last permitted pass", () =>
        {
            var dome = Fan(8, 0.6);
            var none = ArapSolver.Flatten(dome, new ArapOptions { MaxPerimeterIterations = 0, MaxIterations = 3000 });
            var one = ArapSolver.Flatten(dome, new ArapOptions { MaxPerimeterIterations = 1, PerimeterTolerance = 0, MaxIterations = 3000, PerimeterSolveIterations = 1000 });
            Require(one.MaxPerimeterError < none.MaxPerimeterError, "The perimeter pass did not improve");
            var result = Success(ArapSolver.Flatten(dome, new ArapOptions
            {
                MaxPerimeterIterations = 1, PerimeterTolerance = (none.MaxPerimeterError + one.MaxPerimeterError) / 2, MaxIterations = 3000, PerimeterSolveIterations = 1000
            }));
            Require(result.PerimeterIterations == 1, "The last pass was not used");
        });

        Check("Unreachable perimeter tolerance: no mesh", () =>
            Status(ArapSolver.Flatten(Fan(8, 0.6), new ArapOptions { MaxPerimeterIterations = 1, PerimeterTolerance = 1e-12 }), ArapStatus.PerimeterLimit));
        Check("Flat ring with a hole", () => Isometry(Success(ArapSolver.Unwrap(Annulus())), 1e-7));
        Check("Separate panels: each one pinned automatically", () => Isometry(Success(ArapSolver.Flatten(Disconnected())), 1e-7));
        Check("Separate panels with a pin on one only: rejected", () => Status(ArapSolver.Deform(Disconnected(), new[] { new ArapPin(0, P(0, 0)) }), ArapStatus.InvalidInput));
        Check("Closed surface (tetrahedron): can be deformed", () => Isometry(Success(ArapSolver.Deform(Tetrahedron())), 1e-7));
        Check("Closed surface (tetrahedron): needs cuts to be flattened", () => Status(ArapSolver.Flatten(Tetrahedron()), ArapStatus.InvalidInput));

        // ---- Rejected inputs ----

        Check("Two pins on the same vertex", () => Status(ArapSolver.Unwrap(Grid(2, 2), new[] { new ArapPin(0, P(0, 0)), new ArapPin(0, P(1, 0)) }), ArapStatus.InvalidInput));
        Check("Pin on a missing vertex", () => Status(ArapSolver.Deform(Grid(2, 2), new[] { new ArapPin(-1, P(0, 0)) }), ArapStatus.InvalidInput));
        Check("Pin with a NaN coordinate", () => Status(ArapSolver.Deform(Grid(2, 2), new[] { new ArapPin(0, P(double.NaN, 0)) }), ArapStatus.InvalidInput));
        Check("Unwrap pin out of the plane", () => Status(ArapSolver.Unwrap(Grid(2, 2), new[] { new ArapPin(0, new Point3d(0, 0, 1)) }), ArapStatus.InvalidInput));
        Check("No mesh", () => Status(ArapSolver.Flatten(null), ArapStatus.InvalidInput));
        Check("Empty mesh", () => Status(ArapSolver.Flatten(new ArapMesh(new Point3d[0], new int[0])), ArapStatus.InvalidInput));
        Check("Vertex with a NaN coordinate", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(double.NaN, 1) }, new[] { 0, 1, 2 })), ArapStatus.InvalidInput));
        Check("Collinear triangle", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(2, 0) }, new[] { 0, 1, 2 })), ArapStatus.InvalidInput));
        Check("Face index out of range", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1) }, new[] { 0, 1, 3 })), ArapStatus.InvalidInput));
        Check("Incomplete index triple", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1) }, new[] { 0, 1 })), ArapStatus.InvalidInput));
        Check("Same triangle twice", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1) }, new[] { 0, 1, 2, 2, 0, 1 })), ArapStatus.InvalidInput));
        Check("Unused vertex", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1), P(9, 9) }, new[] { 0, 1, 2 })), ArapStatus.InvalidInput));
        Check("Triangles turning the opposite way", () => Status(ArapSolver.Flatten(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1), P(1, 1) }, new[] { 0, 1, 2, 1, 2, 3 })), ArapStatus.InvalidInput));
        Check("Edge shared by three faces", () =>
            Status(ArapSolver.Deform(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1), P(0, -1), new Point3d(0, 0, 1) }, new[] { 0, 1, 2, 1, 0, 3, 0, 1, 4 })), ArapStatus.InvalidInput));
        Check("Two fans touching in one vertex (bow-tie)", () =>
            Status(ArapSolver.Deform(new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1), P(-1, 0), P(0, -1) }, new[] { 0, 1, 2, 0, 3, 4 })), ArapStatus.InvalidInput));
        Check("No perimeter pass allowed: no mesh", () => Status(ArapSolver.Flatten(Grid(3, 3), new ArapOptions { MaxPerimeterIterations = 0 }), ArapStatus.PerimeterLimit));
        Check("No iteration allowed", () => Status(ArapSolver.Unwrap(Grid(3, 3), options: new ArapOptions { MaxIterations = 0 }), ArapStatus.InvalidInput));
        Check("NaN tolerance", () => Status(ArapSolver.Unwrap(Grid(3, 3), options: new ArapOptions { PositionTolerance = double.NaN }), ArapStatus.InvalidInput));

        // ---- Limits and numerical failures: never a mesh ----

        Check("Iteration limit reached: no mesh", () => Status(ArapSolver.Unwrap(Cylinder(), options: new ArapOptions { MaxIterations = 1 }), ArapStatus.IterationLimit));
        Check("Linear solver limit reached: reported", () =>
        {
            var mesh = Grid(7, 7); var p = mesh.GetVertices();
            Status(ArapSolver.Deform(mesh, new[] { new ArapPin(0, p[0]), new ArapPin(48, new Point3d(3, 2, 1)) }, new ArapOptions { MaxLinearIterations = 1 }), ArapStatus.NumericalFailure);
        });
        Check("Mirrored plane pins: rejected parameterization", () =>
        {
            var mesh = Grid(2, 2);
            Status(ArapSolver.Unwrap(mesh, mesh.GetVertices().Select((p, i) => new ArapPin(i, P(-p.X, p.Y)))), ArapStatus.InvalidParameterization);
        });
        Check("All pins on one point: rejected parameterization", () =>
        {
            var mesh = Grid(2, 2);
            Status(ArapSolver.Deform(mesh, mesh.GetVertices().Select((p, i) => new ArapPin(i, P(0, 0)))), ArapStatus.InvalidParameterization);
        });

        // ---- Cancellation and concurrency ----

        Check("Cancelled before starting: no mesh", () => Status(ArapSolver.Flatten(Grid(3, 3), cancellationToken: new CancellationToken(true)), ArapStatus.Cancelled));
        Check("Cancelled during the iterations: no mesh", () =>
        {
            var cancel = new CancellationTokenSource();
            Status(ArapSolver.Unwrap(Cylinder(), cancellationToken: cancel.Token, progress: s => cancel.Cancel()), ArapStatus.Cancelled);
        });
        Check("Options changed by the caller during the solution are not used", () =>
        {
            var options = new ArapOptions();
            Success(ArapSolver.Flatten(Grid(3, 3), options, progress: s => { options.PerimeterTolerance = double.NaN; options.MaxPerimeterIterations = 0; }));
        });
        Check("Parallel solutions do not share settings", () =>
        {
            var jobs = Enumerable.Range(0, 8).Select(i => Task.Run(() => ArapSolver.Flatten(Grid(4, 3), new ArapOptions { MaxPerimeterIterations = i % 2 == 0 ? 0 : 20 }))).ToArray();
            Task.WaitAll(jobs);
            for (int i = 0; i < jobs.Length; i++) Status(jobs[i].Result, i % 2 == 0 ? ArapStatus.PerimeterLimit : ArapStatus.Converged);
        });

        // ---- Size ----

        Check("Flat grid of 1681 vertices", () => Isometry(Success(ArapSolver.Flatten(Grid(41, 41))), 1e-7));
        Check("Grid of 1681 vertices with a lifted corner", () =>
        {
            var mesh = Grid(41, 41); var v = mesh.GetVertices();
            var pins = new[] { new ArapPin(0, v[0]), new ArapPin(40, v[40]), new ArapPin(1680, new Point3d(2, 2, 0.2)) };
            var result = Success(ArapSolver.Deform(mesh, pins, new ArapOptions { MaxIterations = 3000 }));
            Pins(result, pins); Monotonic(result);
        });

        if (args.Contains("--native")) NativeComparison.Run(Check, Require);
        Console.WriteLine("ARAP checks: " + passed + " passed, " + failed + " failed.");
        return failed == 0 ? 0 : 1;
    }

    // ---- Checks ----

    internal static void Check(string name, Action action)
    {
        var watch = Stopwatch.StartNew();
        try { action(); passed++; Console.WriteLine("PASS | " + name + " | " + watch.ElapsedMilliseconds + " ms"); }
        catch (Exception e) { failed++; Console.WriteLine("FAIL | " + name + " | " + e.Message); }
    }

    internal static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static void Near(double value, double expected, double tolerance)
        => Require(Math.Abs(value - expected) <= tolerance, "Expected " + expected + ", got " + value + ", tolerance " + tolerance);

    // A successful result with a mesh
    internal static ArapResult Success(ArapResult result)
    {
        Require(result.Success, result.Status + ": " + result.Message + " (iterations " + result.History.Count + ")");
        Require(result.GetVertices().Length > 0, "Successful result without vertices");
        return result;
    }

    // The expected status; a failed result never exposes geometry
    internal static void Status(ArapResult result, ArapStatus status)
    {
        Require(result.Status == status, "Expected " + status + ", got " + result.Status + ": " + result.Message);
        if (!result.Success) Require(result.GetVertices().Length == 0, "Failed result with geometry");
    }

    internal static void Isometry(ArapResult result, double tolerance)
        => Require(result.MaxRelativeEdgeError <= tolerance, "Edge distortion " + result.MaxRelativeEdgeError + " > " + tolerance);

    internal static void Pins(ArapResult result, IEnumerable<ArapPin> pins)
    {
        var vertices = result.GetVertices();
        foreach (var pin in pins) Near(Distance(vertices[pin.Vertex], pin.Position), 0, 1e-10);
    }

    // Within each perimeter pass the energy never increases
    internal static void Monotonic(ArapResult result)
    {
        for (int i = 1; i < result.History.Count; i++)
            if (result.History[i].PerimeterPass == result.History[i - 1].PerimeterPass)
                Require(result.History[i].Energy <= result.History[i - 1].Energy + 1e-10, "Energy increased at iteration " + i);
    }

    internal static double Distance(Point3d a, Point3d b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));

    internal static Point3d P(double x, double y) => new Point3d(x, y, 0);

    // ---- Meshes ----

    // nx x ny vertices on the square 2 x 2, two triangles per cell, mapped by map
    internal static ArapMesh Grid(int nx, int ny, Func<double, double, Point3d> map = null)
    {
        map = map ?? ((x, y) => P(x, y));
        var points = new List<Point3d>(); var faces = new List<int>();
        for (int j = 0; j < ny; j++) for (int i = 0; i < nx; i++) points.Add(map(2.0 * i / (nx - 1), 2.0 * j / (ny - 1)));
        for (int j = 0; j < ny - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i;
                faces.AddRange(new[] { a, a + 1, a + nx + 1, a, a + nx + 1, a + nx });
            }
        return new ArapMesh(points, faces);
    }

    internal static ArapMesh Cylinder() => Grid(9, 4, (x, y) => new Point3d(Math.Sin(x * 0.7), y, 1 - Math.Cos(x * 0.7)));

    // n triangles around a centre at the given height: flat disk or dome
    internal static ArapMesh Fan(int n, double height)
    {
        var points = new List<Point3d> { new Point3d(0, 0, height) }; var faces = new List<int>();
        for (int i = 0; i < n; i++) points.Add(P(Math.Cos(2 * Math.PI * i / n), Math.Sin(2 * Math.PI * i / n)));
        for (int i = 0; i < n; i++) faces.AddRange(new[] { 0, i + 1, (i + 1) % n + 1 });
        return new ArapMesh(points, faces);
    }

    private static ArapMesh Annulus()
    {
        const int n = 12;
        var points = new List<Point3d>(); var faces = new List<int>();
        for (int r = 1; r <= 2; r++) for (int i = 0; i < n; i++) points.Add(P(r * Math.Cos(2 * Math.PI * i / n), r * Math.Sin(2 * Math.PI * i / n)));
        for (int i = 0; i < n; i++) { int j = (i + 1) % n; faces.AddRange(new[] { i, n + i, n + j, i, n + j, j }); }
        return new ArapMesh(points, faces);
    }

    private static ArapMesh Disconnected()
    {
        var a = Grid(2, 2); var b = Grid(2, 2, (x, y) => P(x + 5, y));
        return new ArapMesh(a.GetVertices().Concat(b.GetVertices()), a.GetTriangleIndices().Concat(b.GetTriangleIndices().Select(i => i + 4)));
    }

    private static ArapMesh Tetrahedron()
        => new ArapMesh(new[] { P(0, 0), P(1, 0), P(0, 1), new Point3d(0, 0, 1) }, new[] { 0, 2, 1, 0, 1, 3, 0, 3, 2, 1, 2, 3 });
}
