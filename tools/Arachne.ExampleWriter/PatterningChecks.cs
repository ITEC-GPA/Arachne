using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Grasshopper.Components.Patterning;
using Arachne.Grasshopper.Components.Tools;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// Patterning components on small panels with known answers: flattening, compensation, ARAP,
// decompensation, borders and cutting lines, mesh tools, panel distortion.
internal static class PatterningChecks
{
    private static int passed, failed;
    private static readonly List<Canvas> canvases = new List<Canvas>();

    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PATTERN PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("PATTERN FAIL " + name + ": " + e.GetBaseException().Message); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    private static void Near(double actual, double expected, double tolerance, string what)
        => Check(Math.Abs(actual - expected) <= tolerance, $"{what}: expected {expected}, got {actual}");

    // ---- Running a component on its own ----

    private static void Set<T>(GH_Component component, int input, params T[] values) where T : class, IGH_Goo
        => Canvas.Set(component, input, values);

    /// <summary>
    /// Solves the component in its own document; errors are expected only when asked.
    /// </summary>
    private static T Solve<T>(T component, bool expectError = false) where T : GH_Component
    {
        var canvas = new Canvas();
        canvases.Add(canvas);
        canvas.Add(component);
        canvas.Solve();
        var errors = component.RuntimeMessages(GH_RuntimeMessageLevel.Error);
        Check(expectError ? errors.Count > 0 : errors.Count == 0, component.Name + (expectError ? ": error expected" : ": " + string.Join("; ", errors)));
        return component;
    }

    private static List<IGH_Goo> Out(GH_Component component, int output = 0) => Canvas.Out(component, output);

    private static T Single<T>(GH_Component component, int output = 0) where T : class, IGH_Goo => Canvas.Single<T>(component, output);

    private static Mesh MeshOut(GH_Component component, int output = 0)
    {
        var mesh = Single<GH_Mesh>(component, output).Value;
        Check(mesh != null && mesh.IsValid && mesh.Vertices.Count > 0, component.Name + ": invalid or empty mesh");
        return mesh;
    }

    private static bool HasWarning(GH_Component component) => component.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0;

    // The mesh of the first input as the component received it: in a definition it is the same
    // object of the upstream output, so a component must not change it
    private static Mesh InputMesh(GH_Component component) => ((GH_Mesh)component.Params.Input[0].VolatileData.AllData(true).Single()).Value;

    private static double Area(Mesh mesh) => AreaMassProperties.Compute(mesh).Area;

    // ---- Panels ----

    // Square 2 x 2 from (1, 1) to (3, 3): one quad or two triangles split on the (1, 1)-(3, 3) diagonal
    private static Mesh Square(bool quad = true)
    {
        var m = new Mesh();
        m.Vertices.Add(1, 1, 0); m.Vertices.Add(3, 1, 0); m.Vertices.Add(3, 3, 0); m.Vertices.Add(1, 3, 0);
        if (quad) m.Faces.AddFace(0, 1, 2, 3);
        else { m.Faces.AddFace(0, 1, 2); m.Faces.AddFace(0, 2, 3); }
        m.Normals.ComputeNormals();
        return m;
    }

    // Saddle n x n with spacing 1 and rise 0.05 (x-2)^2 - 0.05 (y-2)^2: a doubly curved panel
    private static Mesh Saddle(int n = 4)
    {
        var m = new Mesh();
        for (int y = 0; y <= n; y++) for (int x = 0; x <= n; x++) m.Vertices.Add(x, y, 0.05 * (x - 2) * (x - 2) - 0.05 * (y - 2) * (y - 2));
        for (int y = 0; y < n; y++) for (int x = 0; x < n; x++) m.Faces.AddFace(y * (n + 1) + x, y * (n + 1) + x + 1, (y + 1) * (n + 1) + x + 1, (y + 1) * (n + 1) + x);
        m.Normals.ComputeNormals();
        return m;
    }

    // A beam with fixed length for the Compensation, made with the FeMM components
    private static IGH_Goo FixedBeam(Line line, double length)
    {
        var c = new Canvas(); canvases.Add(c);
        var chain = FormFindingChecks.Start(c);
        var fixedLength = c.Add(new Arachne.Grasshopper.Components.Attributes.FixedLengthComponent());
        Canvas.Link(fixedLength, 0, FormFindingChecks.Beams(chain, new[] { line }));
        Canvas.Link(fixedLength, 1, chain.LoadCase);
        Canvas.Set(fixedLength, 2, new GH_Number(length));
        c.Solve();
        return Canvas.Out(fixedLength).Single();
    }

    private static DecompensationDescriptor Descriptor(bool normalized, params double[] lengths)
    {
        var descriptor = new DecompensationDescriptor();
        foreach (double length in lengths)
            descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(normalized, length, 1, 1));
        return descriptor;
    }

    internal static int Run()
    {
        passed = failed = 0;
        GH_Document.EnableSolutions = true;

        // ---- Flattening (FeMM, TensileLib planar solver) ----

        Test("Flattening keeps a flat square: side 2, area 4, input untouched", () =>
        {
            var c = Solve(Flatten(Square()));
            var flat = MeshOut(c);
            Near(flat.Vertices.Point3dAt(0).DistanceTo(flat.Vertices.Point3dAt(1)), 2, 1e-4, "Side");
            Near(Area(flat), 4, 1e-4, "Area");
            Check(InputMesh(c).Faces.Count == 1 && InputMesh(c).Faces[0].IsQuad, "Input mesh triangulated");
        });

        Test("Flattening converged on the last allowed iteration gives no warning", () =>
        {
            var c = Flatten(Square()); Set(c, 2, new GH_Integer(1));
            Check(!HasWarning(Solve(c)), "Converged panel rejected at the iteration limit");
        });

        Test("Flattening at the iteration limit publishes the panel with a warning (FeMM)", () =>
        {
            var lifted = Square(); lifted.Vertices.SetVertex(2, 3, 3, 1);
            var c = Flatten(lifted); Set(c, 2, new GH_Integer(1)); Set(c, 3, new GH_Number(1e-12));
            Solve(c);
            Check(HasWarning(c) && Out(c).Count > 0, "Not converged flattening without warning or panel");
        });

        Test("Flattening of a curved panel with the default inputs: planar, small distortion", () =>
        {
            // With the FeMM solver tolerance 0 every curved panel failed with "Solver not converged"
            var curved = Saddle();
            var c = Solve(Flatten(curved));
            Check(!HasWarning(c), "Warnings: " + string.Join("; ", c.RuntimeMessages(GH_RuntimeMessageLevel.Warning)));
            var flat = MeshOut(c);
            Check(flat.Vertices.Count == curved.Vertices.Count && flat.Vertices.All(v => Math.Abs(v.Z) < 1e-9), "Not a planar panel with the same vertices");
            var distortion = Solve(Distortion(curved, flat));
            double max = Single<GH_Number>(distortion, 2).Value;
            Check(max > 0 && max < 5, "Largest length change " + max + "%");
        });

        // ---- Compensation ----

        Test("Compensation 0%: same area, input untouched", () =>
        {
            var c = Solve(Compensation(Square(), 0, 0));
            Near(Area(MeshOut(c)), 4, 1e-4, "Area");
            Check(InputMesh(c).Faces.Count == 1 && InputMesh(c).Faces[0].IsQuad, "Input mesh triangulated");
        });

        Test("Compensation 10% warp and 20% weft: sides 1.8 and 1.6, area 2.88", () =>
        {
            var result = MeshOut(Solve(Compensation(Square(), 10, 20)));
            Near(result.Vertices.Point3dAt(0).DistanceTo(result.Vertices.Point3dAt(1)), 1.8, 1e-3, "Warp side");
            Near(result.Vertices.Point3dAt(1).DistanceTo(result.Vertices.Point3dAt(2)), 1.6, 1e-3, "Weft side");
            Near(Area(result), 2.88, 1e-3, "Area");
        });

        Test("Compensation with an unreachable fixed length publishes the panel with a warning (FeMM)", () =>
        {
            var c = Compensation(Square(), 0, 0);
            Set(c, 5, new GH_Integer(1)); Set(c, 6, new GH_Number(1e-12));
            Set<IGH_Goo>(c, 4, FixedBeam(new Line(1, 1, 0, 3, 1, 0), 20));
            Solve(c);
            Check(HasWarning(c) && Out(c).Count > 0, "Not converged compensation without warning or panel");
        });

        Test("Compensation with an invalid solver tolerance: error and no panel", () =>
        {
            var c = Compensation(Square(), 10, 10); Set(c, 8, new GH_Number(-1));
            Solve(c, true);
            Check(Out(c).Count == 0, "Panel published after a solver error");
        });

        // ---- ARAP ----

        Test("Flattening ARAP of a flat panel: planar, same side lengths", () =>
        {
            var c = new FlatteningArapComponent(); Set(c, 0, new GH_Mesh(Square(false))); Set(c, 1, new GH_Integer(20)); Set(c, 2, new GH_Integer(20));
            var flat = MeshOut(Solve(c));
            Check(flat.Vertices.All(v => Math.Abs(v.Z) < 1e-7), "Not planar");
            Near(flat.Vertices.Point3dAt(0).DistanceTo(flat.Vertices.Point3dAt(1)), 2, 1e-6, "Side");
        });

        Test("Flattening ARAP keeps the quads of a vertical panel", () =>
        {
            var panel = Square(); panel.Transform(Transform.Rotation(Math.PI / 2, Vector3d.YAxis, Point3d.Origin));
            var c = new FlatteningArapComponent(); Set(c, 0, new GH_Mesh(panel));
            Check(MeshOut(Solve(c)).Faces[0].IsQuad, "Quad lost");
        });

        Test("Flattening ARAP without perimeter iterations: warning and no panel", () =>
        {
            var c = new FlatteningArapComponent(); Set(c, 0, new GH_Mesh(Square(false))); Set(c, 1, new GH_Integer(20)); Set(c, 2, new GH_Integer(0));
            Solve(c);
            Check(HasWarning(c) && Out(c).Count == 0, "Unsuccessful ARAP result published");
        });

        Test("Flattening ARAP rejects a negative tolerance: error and no panel", () =>
        {
            var c = new FlatteningArapComponent(); Set(c, 0, new GH_Mesh(Square())); Set(c, 3, new GH_Number(-1));
            Solve(c, true);
            Check(Out(c).Count == 0, "Invalid settings produced a panel");
        });

        Test("Deform ARAP keeps the pinned vertices", () =>
        {
            var c = new DeformArapComponent(); Set(c, 0, new GH_Mesh(Square(false))); Set(c, 1, new GH_Integer(20));
            Set(c, 2, new GH_Integer(0), new GH_Integer(1)); Set(c, 3, new GH_Point(new Point3d(1, 1, 0)), new GH_Point(new Point3d(3, 1, 0)));
            var mesh = MeshOut(Solve(c));
            Near(mesh.Vertices.Point3dAt(0).DistanceTo(new Point3d(1, 1, 0)), 0, 1e-5, "Pin 0");
            Near(mesh.Vertices.Point3dAt(1).DistanceTo(new Point3d(3, 1, 0)), 0, 1e-5, "Pin 1");
        });

        Test("Deform ARAP in 2D keeps translated pins", () =>
        {
            var panel = Square(false); panel.Translate(new Vector3d(-20, -30, -5));
            var c = new DeformArapComponent(); Set(c, 0, new GH_Mesh(panel)); Set(c, 1, new GH_Integer(20));
            Set(c, 2, new GH_Integer(0), new GH_Integer(1)); Set(c, 3, new GH_Point(new Point3d(-19, -29, 0)), new GH_Point(new Point3d(-17, -29, 0)));
            Set(c, 4, new GH_Boolean(true));
            var mesh = MeshOut(Solve(c));
            Near(mesh.Vertices.Point3dAt(0).DistanceTo(new Point3d(-19, -29, 0)), 0, 1e-5, "Pin 0");
            Near(mesh.Vertices.Point3dAt(1).DistanceTo(new Point3d(-17, -29, 0)), 0, 1e-5, "Pin 1");
        });

        Test("Deform ARAP rejects a negative pin index before computing", () =>
        {
            var c = new DeformArapComponent(); Set(c, 0, new GH_Mesh(Square(false))); Set(c, 2, new GH_Integer(-1)); Set(c, 3, new GH_Point(Point3d.Origin));
            Solve(c, true);
            Check(Out(c).Count == 0, "Invalid pins produced a panel");
        });

        Test("Deform ARAP pins each separate part: all 8 vertices kept", () =>
        {
            var panel = Square(); var second = Square(); second.Translate(5, 0, 0); panel.Append(second);
            var c = new DeformArapComponent(); Set(c, 0, new GH_Mesh(panel));
            Check(MeshOut(Solve(c)).Vertices.Count == 8, "Vertices lost");
        });

        Test("Deform ARAP keeps double precision vertices 1e8 from the origin", () =>
        {
            var panel = Square(); panel.Vertices.UseDoublePrecisionVertices = true;
            for (int i = 0; i < panel.Vertices.Count; i++) { var p = panel.Vertices.Point3dAt(i); panel.Vertices.SetVertex(i, p.X + 1e8 + 0.1, p.Y + 1e8 + 0.3, p.Z); }
            var c = new DeformArapComponent(); Set(c, 0, new GH_Mesh(panel));
            var output = MeshOut(Solve(c));
            Check(output.Vertices.UseDoublePrecisionVertices, "Single precision output");
            for (int i = 0; i < panel.Vertices.Count; i++)
                Near(output.Vertices.Point3dAt(i).DistanceTo(panel.Vertices.Point3dAt(i)), 0, 1e-7, "Vertex " + i);
        });

        Test("Deform ARAP with a pin position and no index pins the first vertex", () =>
        {
            var c = new DeformArapComponent(); Set(c, 0, new GH_Mesh(Square())); Set(c, 3, new GH_Point(new Point3d(0.1, 0.3, 0.7)));
            Near(MeshOut(Solve(c)).Vertices.Point3dAt(0).DistanceTo(new Point3d(0.1, 0.3, 0.7)), 0, 1e-12, "First vertex");
        });

        Test("The old native ARAP library is never loaded", () =>
        {
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
                Check(!process.Modules.Cast<System.Diagnostics.ProcessModule>().Any(m => string.Equals(m.ModuleName, "Arachne.IGL.dll", StringComparison.OrdinalIgnoreCase)), "Arachne.IGL.dll loaded");
        });

        // ---- Decompensation ----

        Test("Decompensation with a neutral descriptor keeps the curve (length 4)", () =>
        {
            var descriptor = new DecompensationDescriptorComponent(); for (int i = 2; i < 6; i++) Set(descriptor, i, new GH_Number(0));
            Solve(descriptor);
            var c = new DecompensationComponent();
            Set(c, 0, new GH_Curve(new PolylineCurve(new[] { new Point3d(1, 1, 0), new Point3d(3, 1, 0), new Point3d(3, 3, 0) })));
            Set<IGH_Goo>(c, 1, Single<GH_ObjectWrapper>(descriptor));
            var result = Single<GH_Curve>(Solve(c)).Value;
            Check(result.IsValid, "Invalid curve"); Near(result.GetLength(), 4, 1e-7, "Length");
        });

        Test("Extended descriptor: one point for each length", () =>
        {
            var c = new DecompensationDescriptorExtComponent();
            Set(c, 0, new GH_Number(0), new GH_Number(1)); Set(c, 1, new GH_Boolean(true), new GH_Boolean(true));
            Set(c, 2, new GH_Number(0), new GH_Number(0)); Set(c, 3, new GH_Number(0), new GH_Number(0));
            var descriptor = Single<GH_ObjectWrapper>(Solve(c)).Value as DecompensationDescriptor;
            Check(descriptor != null && descriptor.Points.Count == 2, "Descriptor " + descriptor?.Points.Count);
        });

        Test("Extended descriptor rejects lists of different lengths: warning, no descriptor", () =>
        {
            var c = new DecompensationDescriptorExtComponent();
            Set(c, 0, new GH_Number(0), new GH_Number(1)); Set(c, 1, new GH_Boolean(true));
            Set(c, 2, new GH_Number(0), new GH_Number(0)); Set(c, 3, new GH_Number(0), new GH_Number(0));
            Solve(c);
            Check(Out(c).Count == 0 && HasWarning(c), "Invalid lists accepted");
        });

        Test("Decompensation interpolates on the arc length (x = 0.9375)", () =>
        {
            var descriptor = new DecompensationDescriptor();
            descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(true, 0, 1, 1));
            descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(true, 1, 0.5, 1));
            var c = new DecompensationComponent();
            Set(c, 0, new GH_Curve(new PolylineCurve(new[] { new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(1, 3, 0) })));
            Set<IGH_Goo>(c, 1, new GH_ObjectWrapper(descriptor));
            Check(Single<GH_Curve>(Solve(c)).Value.TryGetPolyline(out var result), "Not a polyline");
            Near(result[1].X, 0.9375, 1e-7, "Corner (interpolated on the curve parameter it was not)");
        });

        Test("Decompensation does not change a reusable absolute descriptor", () =>
        {
            var descriptor = Descriptor(false, 0, 4);
            var c = new DecompensationComponent();
            Set(c, 0, new GH_Curve(new PolylineCurve(new[] { Point3d.Origin, new Point3d(4, 0, 0) })));
            Set<IGH_Goo>(c, 1, new GH_ObjectWrapper(descriptor));
            Solve(c);
            Check(!descriptor.Points[1].IsNormalized && descriptor.Points[1].Length == 4, "Input descriptor changed");
        });

        Test("Decompensation of a line with two equal breakpoints", () =>
        {
            var c = new DecompensationComponent();
            Set(c, 0, new GH_Curve(new LineCurve(Point3d.Origin, new Point3d(4, 0, 0))));
            Set<IGH_Goo>(c, 1, new GH_ObjectWrapper(Descriptor(true, 0, 0.5, 0.5, 1)));
            var result = Single<GH_Curve>(Solve(c)).Value;
            Check(result.IsValid, "Invalid curve at the breakpoint"); Near(result.GetLength(), 4, 1e-7, "Length");
        });

        // ---- Borders and cutting lines ----

        Test("Borders of a square: 4 sides of length 2", () =>
        {
            var c = new BordersComponent(); Set(c, 0, new GH_Mesh(Square()));
            var borders = Out(Solve(c)).Cast<GH_Curve>().Select(g => g.Value).ToList();
            Check(borders.Count == 4 && borders.All(b => Math.Abs(b.GetLength() - 2) < 1e-9), "Borders " + string.Join(", ", borders.Select(b => b.GetLength())));
        });

        Test("Order Borders: each border starts where the previous one ends", () =>
        {
            var borders = new BordersComponent(); Set(borders, 0, new GH_Mesh(Square()));
            var shuffled = Out(Solve(borders)).Cast<GH_Curve>().Reverse().ToArray();
            var c = new OrderBordersComponent(); Set(c, 0, shuffled);
            var ordered = Out(Solve(c)).Cast<GH_Curve>().Select(g => g.Value).ToList();
            Check(ordered.Count == 4, "Borders " + ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
                Near(ordered[i].PointAtEnd.DistanceTo(ordered[(i + 1) % ordered.Count].PointAtStart), 0, 1e-9, "Joint " + i);
        });

        Test("Split Mesh On Curves: a cut at x = 2 gives two panels of area 2", () =>
        {
            var c = new SplitMeshOnCurvesComponent(); Set(c, 0, new GH_Mesh(Square()));
            Set(c, 1, new GH_Curve(new LineCurve(new Point3d(2, 0, 0), new Point3d(2, 4, 0))));
            var panels = Out(Solve(c)).Cast<GH_Mesh>().Select(g => g.Value).ToList();
            Check(panels.Count == 2 && panels.All(p => Math.Abs(Area(p) - 2) < 1e-6), "Panels " + string.Join(", ", panels.Select(Area)));
        });

        Test("Mesh Divide in 2 parts: a central geodesic of length 2", () =>
        {
            var c = new MeshDivideComponent(); Set(c, 0, new GH_Mesh(Square())); Set(c, 1, new GH_Integer(2)); Set(c, 3, new GH_Number(0.25)); Set(c, 4, new GH_Integer(10));
            Near(Single<GH_Curve>(Solve(c)).Value.GetLength(), 2, 1e-3, "Length");
        });

        Test("Mesh Geodesic on a flat panel: a straight segment of length 2", () =>
        {
            var c = new MeshGeodesicComponent(); Set(c, 0, new GH_Mesh(Square()));
            Set(c, 1, new GH_Point(new Point3d(1, 2, 0))); Set(c, 2, new GH_Point(new Point3d(3, 2, 0))); Set(c, 3, new GH_Number(0.25)); Set(c, 4, new GH_Integer(10));
            Near(Single<GH_Curve>(Solve(c)).Value.GetLength(), 2, 1e-3, "Length");
        });

        Test("Geodesic Offsets of 0.5 on a flat panel: a strip of area 1", () =>
        {
            var c = new GeodesicOffsetsComponent(); Set(c, 0, new GH_Mesh(Square()));
            Set(c, 1, new GH_Curve(new LineCurve(new Point3d(2, 1, 0), new Point3d(2, 3, 0))));
            Set(c, 3, new GH_Number(0.5)); Set(c, 4, new GH_Number(0.01)); Set(c, 5, new GH_Number(0.25)); Set(c, 6, new GH_Integer(10));
            Solve(c);
            Check(Out(c).Count > 0, "No offset curve");
            Near(Area(MeshOut(c, 1)), 1, 0.03, "Strip area");
        });

        Test("Unweld along the diagonal: two separate triangles of area 2", () =>
        {
            var c = new UnweldMeshComponent(); Set(c, 0, new GH_Mesh(Square(false)));
            Set(c, 1, new GH_Curve(new LineCurve(new Point3d(1, 1, 0), new Point3d(3, 3, 0))));
            var parts = Out(Solve(c)).Cast<GH_Mesh>().Select(g => g.Value).ToList();
            Check(parts.Count == 2 && parts.All(p => Math.Abs(Area(p) - 2) < 1e-9), "Parts " + string.Join(", ", parts.Select(Area)));
        });

        Test("Line-Up of two panels: both kept, 1 apart along the base line", () =>
        {
            var c = new LineUpComponent(); Set<IGH_GeometricGoo>(c, 0, new GH_Mesh(Square()), new GH_Mesh(Square())); Set(c, 2, new GH_Number(1));
            var boxes = Out(Solve(c)).Cast<GH_Mesh>().Select(g => g.Value.GetBoundingBox(true)).OrderBy(b => b.Min.X).ToList();
            Check(boxes.Count == 2, "Panels " + boxes.Count);
            Near(boxes[1].Min.X - boxes[0].Max.X, 1, 1e-6, "Spacing");
        });

        Test("Mesh Remap between equal panels keeps the point", () =>
        {
            var c = new MeshRemapComponent(); Set(c, 0, new GH_Mesh(Square())); Set(c, 1, new GH_Mesh(Square()));
            Set<IGH_GeometricGoo>(c, 2, new GH_Point(new Point3d(2, 2, 0))); Set<IGH_GeometricGoo>(c, 3, new GH_Point(new Point3d(2, 2, 0)));
            Near(Single<GH_Point>(Solve(c)).Value.DistanceTo(new Point3d(2, 2, 0)), 0, 1e-6, "Point");
        });

        Test("Mesh Remap rejects panels with different faces: warning, no output", () =>
        {
            var c = new MeshRemapComponent(); Set(c, 0, new GH_Mesh(Square())); Set(c, 1, new GH_Mesh(Square(false)));
            Set<IGH_GeometricGoo>(c, 2, new GH_Point(new Point3d(2, 2, 0)));
            Solve(c);
            Check(HasWarning(c) && Out(c).Count == 0, "Incompatible panels remapped");
        });

        // ---- Mesh tools ----

        Test("Mesh Pipe: 4 sides at distance 1 from the axis, from z = 0 to 3", () =>
        {
            var c = new MeshPipeComponent(); Set(c, 0, new GH_Line(new Line(0, 0, 0, 0, 0, 3)));
            var pipe = MeshOut(Solve(c));
            Check(pipe.Vertices.All(v => Math.Abs(Math.Sqrt(v.X * v.X + v.Y * v.Y) - 1) < 1e-6 && (Math.Abs(v.Z) < 1e-6 || Math.Abs(v.Z - 3) < 1e-6)), "Vertices off the pipe");
        });

        Test("Mesh Clean welds the vertices closer than the length, on a copy", () =>
        {
            // Two quads side by side, with the shared side duplicated: one end exactly, the other 0.02 apart
            var input = new Mesh();
            foreach (var p in new[] { new Point3d(0, 0, 0), new Point3d(1, 0, 0), new Point3d(1, 1, 0), new Point3d(0, 1, 0),
                                      new Point3d(1, 0, 0), new Point3d(2, 0, 0), new Point3d(2, 1, 0), new Point3d(1.02, 1, 0) })
                input.Vertices.Add(p);
            input.Faces.AddFace(0, 1, 2, 3); input.Faces.AddFace(4, 5, 6, 7);
            var c = new MeshCleanComponent(); Set(c, 0, new GH_Mesh(input)); Set(c, 1, new GH_Number(0.1));
            Solve(c);
            var cleaned = Single<IGH_GeometricGoo>(c).ScriptVariable() as Mesh;
            Check(cleaned != null && cleaned.Vertices.Count == 6 && cleaned.Faces.Count == 2, $"Cleaned {cleaned?.Vertices.Count} vertices, {cleaned?.Faces.Count} faces");
            Check(InputMesh(c).Vertices.Count == 8, "Input mesh modified (FeMM welded it in place): " + InputMesh(c).Vertices.Count + " vertices");
        });

        Test("Subdivide Mesh: one quad in four, same area", () =>
        {
            var c = new SubdivideMeshComponent(); Set(c, 0, new GH_Mesh(Square()));
            var result = Single<IGH_GeometricGoo>(Solve(c)).ScriptVariable() as Mesh;
            Check(result != null && result.Faces.Count == 4, "Faces " + result?.Faces.Count);
            Near(Area(result), 4, 1e-9, "Area");
        });

        Test("Remesh keeps the area of the panel", () =>
        {
            var c = new RemeshComponent(); Set(c, 0, new GH_Mesh(Square())); Set(c, 1, new GH_Number(0.5));
            var mesh = Out(Solve(c)).OfType<GH_Mesh>().Single().Value;
            Check(mesh.IsValid && mesh.Faces.Count > 4, "Faces " + mesh.Faces.Count);
            Near(Area(mesh), 4, 1e-3, "Area");
        });

        Test("Components with a mesh input leave it as it was", () =>
        {
            var factories = new List<Func<GH_Component>>
            {
                () => new BordersComponent(),
                () => { var c = new SplitMeshOnCurvesComponent(); Set(c, 1, new GH_Curve(new LineCurve(new Point3d(2, 0, 0), new Point3d(2, 4, 0)))); return c; },
                () => { var c = new MeshDivideComponent(); Set(c, 1, new GH_Integer(2)); Set(c, 3, new GH_Number(0.25)); Set(c, 4, new GH_Integer(10)); return c; },
                () => { var c = new MeshGeodesicComponent(); Set(c, 1, new GH_Point(new Point3d(1, 2, 0))); Set(c, 2, new GH_Point(new Point3d(3, 2, 0))); Set(c, 3, new GH_Number(0.25)); Set(c, 4, new GH_Integer(10)); return c; },
                () => { var c = new GeodesicOffsetsComponent(); Set(c, 1, new GH_Curve(new LineCurve(new Point3d(2, 1, 0), new Point3d(2, 3, 0)))); Set(c, 3, new GH_Number(0.5)); Set(c, 4, new GH_Number(0.01)); Set(c, 5, new GH_Number(0.25)); Set(c, 6, new GH_Integer(10)); return c; },
                () => { var c = new UnweldMeshComponent(); Set(c, 1, new GH_Curve(new LineCurve(new Point3d(1, 1, 0), new Point3d(3, 3, 0)))); return c; },
                () => new MeshCleanComponent(),
                () => new SubdivideMeshComponent(),
                () => { var c = new RemeshComponent(); Set(c, 1, new GH_Number(0.5)); return c; },
                () => new FlatteningComponent(),
                () => { var c = new CompensationComponent(); Set(c, 1, new GH_Vector(Vector3d.XAxis)); Set(c, 2, new GH_Number(10)); Set(c, 3, new GH_Number(20)); return c; },
                () => new FlatteningArapComponent(),
                () => new DeformArapComponent(),
                () => { var c = new MeshRemapComponent(); Set(c, 1, new GH_Mesh(Square(false))); Set<IGH_GeometricGoo>(c, 2, new GH_Point(new Point3d(2, 2, 0))); return c; },
                () => { var c = new PanelDistortionComponent(); Set(c, 1, new GH_Mesh(Square(false))); return c; },
            };
            var changed = new List<string>();
            foreach (var factory in factories)
            {
                var c = factory();
                var original = Square(false);
                Set(c, 0, new GH_Mesh(original.DuplicateMesh()));
                var canvas = new Canvas(); canvases.Add(canvas); canvas.Add(c); canvas.Solve();
                var input = InputMesh(c);
                bool same = input.Vertices.Count == original.Vertices.Count && input.Faces.Count == original.Faces.Count &&
                    Enumerable.Range(0, original.Vertices.Count).All(i => input.Vertices.Point3dAt(i).DistanceTo(original.Vertices.Point3dAt(i)) == 0);
                if (!same) changed.Add(c.Name);
            }
            Check(changed.Count == 0, "Input changed by " + string.Join(", ", changed));
        });

        Test("Line-Up leaves its input panels where they were", () =>
        {
            var c = new LineUpComponent(); var panel = Square(); panel.Translate(new Vector3d(10, 20, 0));
            Set<IGH_GeometricGoo>(c, 0, new GH_Mesh(panel.DuplicateMesh())); Set(c, 2, new GH_Number(1));
            Solve(c);
            var input = ((GH_Mesh)c.Params.Input[0].VolatileData.AllData(true).Single()).Value;
            Near(input.Vertices.Point3dAt(0).DistanceTo(panel.Vertices.Point3dAt(0)), 0, 0, "Input panel moved");
        });

        // ---- Panel Distortion ----

        Test("Panel Distortion: 0 for a moved panel, 1% sides and 2.01% area for a panel 1% larger", () =>
        {
            var panel = Square();
            var moved = panel.DuplicateMesh(); moved.Translate(new Vector3d(10, 0, 0));
            var same = Solve(Distortion(panel, moved));
            Check(Out(same, 0).Count == 4 && Out(same, 1).Count == 4, "Edges " + Out(same, 0).Count);
            Near(Single<GH_Number>(same, 2).Value, 0, 1e-9, "Largest change"); Near(Single<GH_Number>(same, 3).Value, 0, 1e-9, "Area change");
            var scaled = panel.DuplicateMesh(); scaled.Transform(Transform.Scale(Point3d.Origin, 1.01));
            var larger = Solve(Distortion(panel, scaled));
            Near(Single<GH_Number>(larger, 2).Value, 1, 1e-9, "Largest change"); Near(Single<GH_Number>(larger, 3).Value, 2.01, 1e-9, "Area change");
        });

        Test("Panel Distortion rejects a panel with different vertices", () =>
        {
            Solve(Distortion(Saddle(), Square()), true);
        });

        Console.WriteLine("Patterning checks: " + passed + " passed, " + failed + " failed.");
        foreach (var canvas in canvases) canvas.Dispose();
        canvases.Clear();
        return failed;
    }

    private static FlatteningComponent Flatten(Mesh mesh)
    {
        var c = new FlatteningComponent(); Set(c, 0, new GH_Mesh(mesh));
        return c;
    }

    private static CompensationComponent Compensation(Mesh panel, double warp, double weft)
    {
        var c = new CompensationComponent(); Set(c, 0, new GH_Mesh(panel)); Set(c, 1, new GH_Vector(Vector3d.XAxis));
        Set(c, 2, new GH_Number(warp)); Set(c, 3, new GH_Number(weft));
        return c;
    }

    private static PanelDistortionComponent Distortion(Mesh panel, Mesh flat)
    {
        var c = new PanelDistortionComponent(); Set(c, 0, new GH_Mesh(panel)); Set(c, 1, new GH_Mesh(flat));
        return c;
    }
}
