using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// The same definitions on any Arachne build: components by GUID, inputs and outputs by index.
internal sealed class Doc : IDisposable
{
    public readonly GH_Document Document = new GH_Document { Enabled = true };
    public GH_Component Add(string guid)
    {
        var obj = Grasshopper.Instances.ComponentServer.EmitObject(new Guid(guid)) as GH_Component ?? throw new InvalidOperationException("Missing component " + guid);
        obj.CreateAttributes(); Document.AddObject(obj, false); return obj;
    }
    public void Set(GH_Component target, int input, params IGH_Goo[] values)
    {
        var source = new Param_GenericObject(); source.CreateAttributes();
        foreach (var value in values) source.PersistentData.Append(value);
        Document.AddObject(source, false);
        target.Params.Input[input].RemoveAllSources();
        target.Params.Input[input].AddSource(source);
    }
    public static void Link(GH_Component target, int input, GH_Component source, int output = 0) => target.Params.Input[input].AddSource(source.Params.Output[output]);
    public void Solve() => Document.NewSolution(false);
    public void Dispose() => Document.Dispose();
}

internal static class Id
{
    public const string LoadCase = "dd9360b8-20ab-4a62-aa15-bbad93f0f9b6", SolidCircle = "3562882c-f91f-445b-be98-2494b937fd07", HollowCircle = "540b17d6-3c75-457f-8e5b-6b76f349b363",
        SolidRect = "31479893-1941-41e3-99d8-238efc1bcff6", HollowRect = "c37923d7-be85-4846-92f3-79ae601f4f52", ISection = "79118195-ca0f-409f-ac01-55b72161131a",
        TSection = "4573bce8-dc25-4b73-a0d5-cdce1f86846f", CSection = "d915ff63-5959-4f99-ac4a-8c3d7fb20713", Angle = "ea358ee1-c6ec-4849-9411-77fa20cc0b8c",
        GenericSection = "794e34ed-861f-4f67-8466-cf74fda4d459", BrepSection = "b105ee76-1e25-432e-a2b6-8b0ae1b7ed54", SectionProperties = "1da0a1a0-214f-4a76-9f34-b50fadca708d",
        Steel = "d6938aa3-6c82-4648-8d33-f61505ddd262", Concrete = "76802e52-c065-47ff-9c04-97094ab9edc8", Aluminium = "9baa5979-36ea-4247-baff-7d9b4e8a832d",
        CustomMaterial = "7d402b00-2590-463f-950a-acf6d0e970f4", Membrane = "3653a652-deb4-4695-b52d-1fce5edfe629",
        BeamProperty = "d44afeb3-cd24-42bf-a5d5-45a14cee69f3", PlateProperty = "77ce7fd4-99f4-4bc1-9d3f-5c6ce0955ab1",
        Beam = "03f091b7-2779-4f4b-ae65-9953fe26a366", Node = "2d7a3c66-282f-4bbf-a11e-db55827e3224", Plate = "ad3fea4a-1c4b-4923-bb06-3e58ae1f3247",
        Restraints = "70c7c8e2-0ed7-4e5b-95db-9b8837813feb", NodeLoad = "705f086f-9f47-46f9-92b0-9c20e9115891", ForceDensity = "969824ee-693f-42be-977b-4f977b7af300",
        FixedLength = "cb4a245d-aec7-441d-b7a2-0e98aae9e065", FixedAxialForce = "b0574cd0-d4c8-44b1-a85c-1658dd4fbfae", FixedCompensation = "2a20c8a1-9837-4b21-a0ec-70d22e92bb6e",
        CurvatureCable = "92d964b3-ad4d-4c30-a1c6-8e37ac0a7b85", MembraneDensity = "6176f23d-8401-4c8d-ae54-ed469c846fcf", KeepPosition = "a5502518-f224-4d21-a275-705cbc41eda5",
        ElementId = "92cb7203-eb55-4cc4-a322-8e4ccd2bed08", BeamPointLoad = "7f58c892-a469-400c-bc46-e2c9c60ad280", BeamDistributedLoad = "19f2292f-70a7-4fce-bc0d-2f8923829b69",
        BuildModel = "36226b88-8b00-470b-abf2-26f40b07f0ca", SolverOptions = "4b18d9c1-d734-40c5-99d8-9d53333a76d5", FormFind = "f4c3d571-98d3-4bc9-926e-b3315a1444f3",
        BeamForces = "5f9a26f9-c55b-496a-8153-459d8e9a56db", SupportReactions = "2df8465b-e430-4c4c-b077-6f64ee5150e9", Decompose = "c4146124-3da2-491d-96e4-7e0f565ece4b",
        CheckModel = "6b9c4718-1b7d-485e-9293-31637d077e35", NodeFilter = "f322b12d-ddb7-4287-aaa6-67813e6cc864", BeamFilter = "8d3cb7f2-7de6-4594-a437-9756dc36b47c",
        PlateFilter = "087ad97f-842c-41f7-826b-1a96656ffe77", CurveToMesh = "923d40fe-63a7-4d04-84f3-6b8f825cabb9", CableNet = "cb2b48b5-63b8-4af9-8f54-75ff5d506e5f";
}

internal static class Scenarios
{
    private sealed class Chain { public Doc Doc; public GH_Component LoadCase, Property; }
    private static GH_Number N(double v) => new GH_Number(v);
    private static GH_Integer I(int v) => new GH_Integer(v);
    private static GH_Boolean B(bool v) => new GH_Boolean(v);
    private static GH_String S(string v) => new GH_String(v);

    private static Chain Start(Doc d, string loadCase = "FF", double diameter = 10)
    {
        var lc = d.Add(Id.LoadCase); d.Set(lc, 0, S(loadCase));
        var section = d.Add(Id.SolidCircle); d.Set(section, 0, N(diameter));
        var material = d.Add(Id.Steel); d.Set(material, 0, S("Steel")); d.Set(material, 1, I(0));
        var property = d.Add(Id.BeamProperty); d.Set(property, 0, S("Cable")); d.Set(property, 1, I(1));
        Doc.Link(property, 2, section); Doc.Link(property, 3, material);
        return new Chain { Doc = d, LoadCase = lc, Property = property };
    }
    private static GH_Component Beams(Chain c, IEnumerable<Line> lines)
    {
        var beam = c.Doc.Add(Id.Beam);
        c.Doc.Set(beam, 0, lines.Select(l => (IGH_Goo)new GH_Curve(new LineCurve(l))).ToArray());
        Doc.Link(beam, 1, c.Property); return beam;
    }
    private static GH_Component ForceDensity(Chain c, GH_Component beams, double value, int type = 0, double weight = 1, bool project = false)
    {
        var fd = c.Doc.Add(Id.ForceDensity);
        Doc.Link(fd, 0, beams); c.Doc.Set(fd, 1, I(type)); Doc.Link(fd, 2, c.LoadCase); c.Doc.Set(fd, 3, N(value)); c.Doc.Set(fd, 4, B(project)); c.Doc.Set(fd, 5, N(weight));
        return fd;
    }
    private static GH_Component Goal(Chain c, string guid, GH_Component beams, double value, double? weight = null)
    {
        var g = c.Doc.Add(guid); Doc.Link(g, 0, beams); Doc.Link(g, 1, c.LoadCase); c.Doc.Set(g, 2, N(value));
        if (weight.HasValue) c.Doc.Set(g, 3, N(weight.Value));
        return g;
    }
    private static GH_Component Nodes(Chain c, IEnumerable<Point3d> points)
    { var node = c.Doc.Add(Id.Node); c.Doc.Set(node, 0, points.Select(p => (IGH_Goo)new GH_Point(p)).ToArray()); return node; }
    private static GH_Component Supports(Chain c, IEnumerable<Point3d> points, bool tx = true, bool ty = true, bool tz = true, Plane? plane = null)
    {
        var r = c.Doc.Add(Id.Restraints); Doc.Link(r, 0, Nodes(c, points)); c.Doc.Set(r, 1, B(tx), B(ty), B(tz));
        if (plane.HasValue) c.Doc.Set(r, 2, new GH_Plane(plane.Value));
        return r;
    }
    private static GH_Component NodeLoad(Chain c, GH_Component nodes, Vector3d force, GH_Component loadCase = null)
    { var l = c.Doc.Add(Id.NodeLoad); Doc.Link(l, 0, nodes); Doc.Link(l, 1, loadCase ?? c.LoadCase); c.Doc.Set(l, 2, new GH_Vector(force)); return l; }
    private static GH_Component Build(Chain c, int units, params GH_Component[] elements)
    {
        var build = c.Doc.Add(Id.BuildModel);
        foreach (var e in elements) Doc.Link(build, 0, e);
        build.Params.Input[0].DataMapping = GH_DataMapping.Flatten;
        c.Doc.Set(build, 2, I(units)); return build;
    }
    private static GH_Component Build(Chain c, params GH_Component[] elements) => Build(c, 2, elements);
    private sealed class FormFinding { public GH_Component FormFind, Options; }
    private static FormFinding FormFind(Chain c, GH_Component model, int solverType, int maxIterations = 20, double tolerance = 1e-4, double gravity = 0, int modelOutput = 0)
    {
        var options = c.Doc.Add(Id.SolverOptions);
        c.Doc.Set(options, 0, N(gravity)); c.Doc.Set(options, 4, I(maxIterations)); c.Doc.Set(options, 5, N(tolerance));
        var ff = c.Doc.Add(Id.FormFind);
        Doc.Link(ff, 0, model, modelOutput); c.Doc.Set(ff, 1, I(solverType)); Doc.Link(ff, 2, options); c.Doc.Set(ff, 4, B(true));
        return new FormFinding { FormFind = ff, Options = options };
    }
    private static GH_Component After(Chain c, string guid, GH_Component source) { var a = c.Doc.Add(guid); Doc.Link(a, 0, source); return a; }

    private static (List<Line> lines, List<Point3d> border) SaddleGrid(int n, double span, double rise, double dx = 0)
    {
        var p = new Point3d[n + 1, n + 1]; var lines = new List<Line>(); var border = new List<Point3d>();
        for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                bool edge = x == 0 || x == n || y == 0 || y == n;
                p[x, y] = new Point3d(dx + span * x / n, span * y / n, edge ? rise * (2.0 * x / n - 1) * (2.0 * y / n - 1) : 0);
                if (edge) border.Add(p[x, y]);
                if (x > 0) lines.Add(new Line(p[x - 1, y], p[x, y]));
                if (y > 0) lines.Add(new Line(p[x, y - 1], p[x, y]));
            }
        return (lines, border);
    }

    // Every form-found model: the solver outputs, the beam forces, the reactions and the decomposed elements.
    private static void Results(Dump dump, Chain c, string key, FormFinding run)
    {
        var forces = After(c, Id.BeamForces, run.FormFind);
        var reactions = After(c, Id.SupportReactions, run.FormFind);
        var parts = After(c, Id.Decompose, run.FormFind);
        c.Doc.Solve();
        dump.Outputs(key + ".ff", run.FormFind, 1);
        dump.Text(key + ".ff.models", run.FormFind.Params.Output[0].VolatileData.DataCount.ToString());
        dump.Outputs(key + ".forces", forces);
        dump.Outputs(key + ".reactions", reactions);
        dump.Outputs(key + ".parts", parts);
    }

    private static readonly Point3d A = new Point3d(0, 0, 0), M = new Point3d(1, 0, 0), B2 = new Point3d(2, 0, 0);

    private static void Case(Dump dump, string name, Action<Doc, Dump> body)
    {
        dump.Scenario(name);
        try { using (var d = new Doc()) body(d, dump); }
        catch (Exception e) { dump.Text("exception", e.GetType().Name + ": " + e.GetBaseException().Message); Console.WriteLine("  EXCEPTION " + e.GetBaseException().Message); }
    }

    internal static void Run(Dump dump)
    {
        Case(dump, "linear saddle 4x4", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10, 2);
            Results(o, c, "saddle", FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border)), 0));
        });
        Case(dump, "linear node load between two cables", (d, o) =>
        {
            var c = Start(d);
            var beams = ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 2);
            Results(o, c, "cables", FormFind(c, Build(c, beams, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), 0));
        });
        Case(dump, "stepped fixed length", (d, o) =>
        {
            var c = Start(d);
            var first = ForceDensity(c, Beams(c, new[] { new Line(A, M) }), 1);
            var fixedLength = Goal(c, Id.FixedLength, first, 0.6);
            var second = ForceDensity(c, Beams(c, new[] { new Line(M, B2) }), 1);
            Results(o, c, "stepped", FormFind(c, Build(c, fixedLength, second, Supports(c, new[] { A, B2 })), 1, 100, 1e-6));
        });
        Case(dump, "stepped impossible fixed length", (d, o) =>
        {
            var c = Start(d);
            var fixedLength = Goal(c, Id.FixedLength, ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 1), 0.5);
            Results(o, c, "impossible", FormFind(c, Build(c, fixedLength, Supports(c, new[] { A, B2 })), 1, 10, 1e-6));
        });
        Case(dump, "stepped fixed axial force with load", (d, o) =>
        {
            var c = Start(d);
            var axial = Goal(c, Id.FixedAxialForce, ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 1), 4);
            Results(o, c, "stepped-axial", FormFind(c, Build(c, axial, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), 1, 50, 1e-6));
        });
        Case(dump, "nonlinear fixed axial force", (d, o) =>
        {
            var c = Start(d);
            var axial = Goal(c, Id.FixedAxialForce, ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 1, 0, 0), 4);
            Results(o, c, "nonlinear", FormFind(c, Build(c, axial, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), 2, 200, 1e-8));
        });
        Case(dump, "linear with fixed axial force and length (ignored inputs)", (d, o) =>
        {
            var c = Start(d);
            var beams = ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 1);
            var axial = Goal(c, Id.FixedAxialForce, beams, 4, 2);
            var length = Goal(c, Id.FixedLength, axial, 1.5, 3);
            Results(o, c, "linear-ignored", FormFind(c, Build(c, length, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), 0));
        });
        Case(dump, "nonlinear fixed length and keep position", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 4000, 800);
            var beams = ForceDensity(c, Beams(c, lines), 1, 0, 1);
            var length = Goal(c, Id.FixedLength, beams, 900, 5);
            var free = Nodes(c, new[] { new Point3d(2000, 2000, 0) });
            var keep = d.Add(Id.KeepPosition); Doc.Link(keep, 0, free); d.Set(keep, 1, new GH_Vector(new Vector3d(0, 0, 10)));
            Results(o, c, "keep", FormFind(c, Build(c, length, keep, Supports(c, border)), 2, 100, 1e-8));
        });
        Case(dump, "pressure with load case", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10, 2);
            var wind = d.Add(Id.LoadCase); d.Set(wind, 0, S("Wind"));
            var run = FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border)), 0);
            d.Set(run.Options, 1, N(0.05)); Doc.Link(run.Options, 2, wind);
            Results(o, c, "pressure", run);
        });
        Case(dump, "form found again with pressure", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10000, 2000);
            var wind = d.Add(Id.LoadCase); d.Set(wind, 0, S("Wind"));
            var build = Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border));
            FormFinding Find(GH_Component model, bool pressure)
            {
                var run = FormFind(c, model, 0, 100, 1e-10);
                if (pressure) { d.Set(run.Options, 1, N(1e-4)); Doc.Link(run.Options, 2, wind); }
                return run;
            }
            var pressed = Find(build, true);
            Results(o, c, "pressed", pressed);
            Results(o, c, "again", Find(pressed.FormFind, true));
            Results(o, c, "relaxed", Find(pressed.FormFind, false));
        });
        Case(dump, "pressure without load case", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10, 2);
            var run = FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border)), 0);
            d.Set(run.Options, 1, N(0.05));
            Results(o, c, "nopressurecase", run);
        });
        Case(dump, "form find without options", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10, 2);
            var ff = d.Add(Id.FormFind); Doc.Link(ff, 0, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border))); d.Set(ff, 4, B(true));
            Results(o, c, "defaults", new FormFinding { FormFind = ff });
        });
        Case(dump, "load case selection", (d, o) =>
        {
            var c = Start(d);
            var other = d.Add(Id.LoadCase); d.Set(other, 0, S("Other"));
            var beams = ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 2);
            var middle = Nodes(c, new[] { M });
            var selected = NodeLoad(c, middle, new Vector3d(0, 0, -4));
            var ignored = NodeLoad(c, middle, new Vector3d(0, 0, 40), other);
            var run = FormFind(c, Build(c, beams, Supports(c, new[] { A, B2 }), selected, ignored), 0);
            Doc.Link(run.Options, 3, c.LoadCase);
            Results(o, c, "selection", run);
        });
        Case(dump, "generated plates weight: none, membrane, steel", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(2, 4000, 0);
            var build = Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border));
            var membrane = d.Add(Id.Membrane); d.Set(membrane, 0, S("PVC/PES")); d.Set(membrane, 1, N(1050)); d.Set(membrane, 2, N(4200)); d.Set(membrane, 3, N(4000));
            var steel = d.Add(Id.Steel); d.Set(steel, 0, S("Steel")); d.Set(steel, 1, I(0));
            FormFinding Find(GH_Component material)
            {
                var run = FormFind(c, build, 0, 100, 1e-10, 9806.65);
                if (material != null)
                {
                    var property = d.Add(Id.PlateProperty); d.Set(property, 0, S("Plates")); d.Set(property, 1, N(1)); Doc.Link(property, 2, material);
                    Doc.Link(run.Options, 10, property);
                }
                return run;
            }
            Results(o, c, "bare", Find(null)); Results(o, c, "fabric", Find(membrane)); Results(o, c, "steel", Find(steel));
        });
        Case(dump, "explicit plates with membrane property and gravity", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(2, 4000, 500);
            var mesh = new Mesh();
            for (int y = 0; y <= 2; y++) for (int x = 0; x <= 2; x++) mesh.Vertices.Add(2000 * x, 2000 * y, (x == 1 && y == 1) ? 0 : 500 * (x - 1) * (y - 1));
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) mesh.Faces.AddFace(y * 3 + x, y * 3 + x + 1, y * 3 + x + 4, y * 3 + x + 3);
            var membrane = d.Add(Id.Membrane); d.Set(membrane, 0, S("PTFE")); d.Set(membrane, 1, N(1500)); d.Set(membrane, 2, N(5000)); d.Set(membrane, 3, N(4500));
            var property = d.Add(Id.PlateProperty); d.Set(property, 0, S("Fabric")); d.Set(property, 1, N(1)); Doc.Link(property, 2, membrane);
            var plates = d.Add(Id.Plate); d.Set(plates, 0, new GH_Mesh(mesh)); Doc.Link(plates, 1, property);
            var run = FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), plates, Supports(c, border)), 2, 100, 1e-8, 9806.65);
            Results(o, c, "explicit-plates", run);
        });
        Case(dump, "explicit quad plates, one per face, with membrane and steel weight", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(2, 4000, 500);
            double Z(int x, int y) => (x == 1 && y == 1) ? 0 : 500 * (x - 1) * (y - 1);
            var faces = new List<IGH_Goo>();
            for (int y = 0; y < 2; y++)
                for (int x = 0; x < 2; x++)
                {
                    var face = new Mesh();
                    face.Vertices.Add(2000 * x, 2000 * y, Z(x, y)); face.Vertices.Add(2000 * (x + 1), 2000 * y, Z(x + 1, y));
                    face.Vertices.Add(2000 * (x + 1), 2000 * (y + 1), Z(x + 1, y + 1)); face.Vertices.Add(2000 * x, 2000 * (y + 1), Z(x, y + 1));
                    face.Faces.AddFace(0, 1, 2, 3); faces.Add(new GH_Mesh(face));
                }
            var membrane = d.Add(Id.Membrane); d.Set(membrane, 0, S("PTFE")); d.Set(membrane, 1, N(1500)); d.Set(membrane, 2, N(5000)); d.Set(membrane, 3, N(4500));
            var steel = d.Add(Id.Steel); d.Set(steel, 0, S("Steel")); d.Set(steel, 1, I(1));
            foreach (var (material, key, thickness) in new[] { (membrane, "quad-fabric", 1.0), (steel, "quad-steel", 2.0) })
            {
                var property = d.Add(Id.PlateProperty); d.Set(property, 0, S("Plates")); d.Set(property, 1, N(thickness)); Doc.Link(property, 2, material);
                var plates = d.Add(Id.Plate); d.Set(plates, 0, faces.ToArray()); Doc.Link(plates, 1, property);
                foreach (int solver in new[] { 0, 2 })
                    Results(o, c, key + solver, FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), plates, Supports(c, border)), solver, 100, 1e-8, 9806.65));
            }
        });
        Case(dump, "membrane force densities warp and weft", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 4000, 800);
            var warp = Beams(c, lines.Where((l, i) => i % 2 == 0)); var weft = Beams(c, lines.Where((l, i) => i % 2 == 1));
            GH_Component Membrane(GH_Component beams, int type) { var m = d.Add(Id.MembraneDensity); Doc.Link(m, 0, beams); Doc.Link(m, 1, c.LoadCase); d.Set(m, 2, N(2)); d.Set(m, 3, I(type)); return m; }
            Results(o, c, "membrane", FormFind(c, Build(c, Membrane(warp, 0), Membrane(weft, 1), Supports(c, border)), 2, 100, 1e-8));
        });
        Case(dump, "curvature cable", (d, o) =>
        {
            var c = Start(d);
            var points = Enumerable.Range(0, 7).Select(i => new Point3d(1000 * i, 0, 0)).ToList();
            var beams = Beams(c, points.Zip(points.Skip(1), (p, q) => new Line(p, q)));
            var curve = d.Add(Id.CurvatureCable); Doc.Link(curve, 0, beams); Doc.Link(curve, 1, c.LoadCase); d.Set(curve, 2, N(6000)); d.Set(curve, 3, N(10));
            var load = NodeLoad(c, Nodes(c, points.Skip(1).Take(5)), new Vector3d(0, 0, -100));
            Results(o, c, "curvature", FormFind(c, Build(c, curve, Supports(c, new[] { points[0], points[6] }), load), 2, 100, 1e-8));
        });
        Case(dump, "fixed compensation", (d, o) =>
        {
            var c = Start(d);
            var beams = ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 1);
            var comp = Goal(c, Id.FixedCompensation, beams, 2, 1);
            Results(o, c, "compensation", FormFind(c, Build(c, comp, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), 2, 100, 1e-8));
        });
        Case(dump, "beam loads keystone, projected, point", (d, o) =>
        {
            var c = Start(d);
            var flat = ForceDensity(c, Beams(c, new[] { new Line(A, M) }), 0.1);
            var inclined = ForceDensity(c, Beams(c, new[] { new Line(M, new Point3d(2, 0, 1)) }), 0.1);
            var last = ForceDensity(c, Beams(c, new[] { new Line(new Point3d(2, 0, 1), new Point3d(3, 0, 0)) }), 0.1);
            var keystone = d.Add(Id.BeamDistributedLoad);
            Doc.Link(keystone, 0, flat); Doc.Link(keystone, 1, c.LoadCase); d.Set(keystone, 2, I(1)); d.Set(keystone, 4, I(6)); d.Set(keystone, 5, N(1)); d.Set(keystone, 6, N(3)); d.Set(keystone, 7, N(0.1)); d.Set(keystone, 8, N(0.2));
            var projected = d.Add(Id.BeamDistributedLoad);
            Doc.Link(projected, 0, inclined); Doc.Link(projected, 1, c.LoadCase); d.Set(projected, 2, I(0)); d.Set(projected, 4, I(7)); d.Set(projected, 5, N(1));
            var point = d.Add(Id.BeamPointLoad);
            Doc.Link(point, 0, last); Doc.Link(point, 1, c.LoadCase); d.Set(point, 3, N(0.2)); d.Set(point, 5, N(-1)); d.Set(point, 6, N(0.25));
            Results(o, c, "beamloads", FormFind(c, Build(c, keystone, projected, point, Supports(c, new[] { A, new Point3d(3, 0, 0) })), 0, 100, 1e-10));
        });
        Case(dump, "far from the origin", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10000, 2000, 1e6);
            var wind = d.Add(Id.LoadCase); d.Set(wind, 0, S("Wind"));
            var run = FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border)), 0);
            d.Set(run.Options, 1, N(1e-4)); Doc.Link(run.Options, 2, wind);
            Results(o, c, "far", run);
        });
        Case(dump, "backstay and two nets", (d, o) =>
        {
            var c = Start(d); var g1 = SaddleGrid(2, 4000, 1000); var g2 = SaddleGrid(2, 4000, 1000, 10000);
            var lines = new List<Line> { new Line(new Point3d(0, 0, 1000), new Point3d(-3000, 0, 0)), new Line(new Point3d(4000, 2000, 0), new Point3d(10000, 2000, 0)) };
            lines.AddRange(g1.lines); lines.AddRange(g2.lines);
            Results(o, c, "backstay", FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, g1.border.Concat(g2.border).Concat(new[] { new Point3d(-3000, 0, 0) }))), 0));
        });
        // IPS on its own: by the user decision of 4/11/2026 the solver masses of IPS are in lbf·s²/in (pound-mass / 386.09), not the
        // pound-mass of master 7b1108d, so this scenario differs from master by design; Nmm, SI and kNm stay bit-identical.
        foreach (var (name, systems) in new[] {
            ("self weight in Nmm, SI, kNm", new[] { (2, 1.0, "nmm"), (1, 0.001, "si"), (0, 0.001, "knm") }),
            ("self weight in IPS (pound-mass, differs from master by decision)", new[] { (3, 1 / 25.4, "ips") }) })
        Case(dump, name, (d, o) =>
        {
            foreach (var (units, metres, label) in systems)
            {
                var c = Start(d, "FF", 10 * metres);
                var p0 = new Point3d(0, 0, 0); var p1 = new Point3d(1000 * metres, 0, 0); var p2 = new Point3d(2000 * metres, 0, 0);
                var beams = ForceDensity(c, Beams(c, new[] { new Line(p0, p1), new Line(p1, p2) }), 0.1 / metres);
                Results(o, c, label, FormFind(c, Build(c, units, beams, Supports(c, new[] { p0, p2 }), NodeLoad(c, Nodes(c, new[] { p1 }), new Vector3d(0, 0, -1))), 0, 100, 1e-12, 9806.65 * metres));
            }
        });
        Case(dump, "restraints along inclined plane and line", (d, o) =>
        {
            var c = Start(d);
            var inclined = new Plane(new Point3d(2, 0, 0), new Vector3d(1, 0, 1));
            var line = new Plane(new Point3d(0, 0, 0), new Vector3d(0, 0, 1), new Vector3d(0, 1, 0));
            var beams = ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 1);
            var fixedEnd = Supports(c, new[] { A }, false, true, true, line);
            var slider = Supports(c, new[] { B2 }, false, false, true, inclined);
            var other = Supports(c, new[] { B2 }, false, true, false);
            Results(o, c, "inclined", FormFind(c, Build(c, beams, fixedEnd, slider, other, NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0.5, 0.25, -4))), 0, 100, 1e-10));
        });
        Case(dump, "restraints turned about Z", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(4, 10, 2);
            var turned = new Plane(Point3d.Origin, new Vector3d(Math.Cos(0.3), Math.Sin(0.3), 0), new Vector3d(-Math.Sin(0.3), Math.Cos(0.3), 0));
            Results(o, c, "turned", FormFind(c, Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border, true, true, true, turned)), 0));
        });
        Case(dump, "element id, filters, beam forces ids", (d, o) =>
        {
            var c = Start(d);
            var beams = Beams(c, new[] { new Line(A, M), new Line(M, B2) });
            var named = d.Add(Id.ElementId); Doc.Link(named, 0, beams); d.Set(named, 1, S("C1"), S("C2"));
            var fd = ForceDensity(c, named, 1);
            var supports = Supports(c, new[] { A, B2 });
            var namedNodes = d.Add(Id.ElementId); Doc.Link(namedNodes, 0, supports); d.Set(namedNodes, 1, S("S1"), S("S2"));
            var build = Build(c, fd, namedNodes, NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -1)));
            var run = FormFind(c, build, 0);
            var nodeFilter = d.Add(Id.NodeFilter); Doc.Link(nodeFilter, 0, namedNodes); d.Set(nodeFilter, 1, S("S2"));
            var beamFilter = d.Add(Id.BeamFilter); Doc.Link(beamFilter, 0, fd); d.Set(beamFilter, 1, S("C1"));
            Results(o, c, "ids", run);
            o.Outputs("ids.nodefilter", nodeFilter); o.Outputs("ids.beamfilter", beamFilter);
        });
        Case(dump, "check model", (d, o) =>
        {
            var c = Start(d); var (lines, border) = SaddleGrid(2, 4000, 1000); var other = SaddleGrid(2, 4000, 1000, 10000);
            GH_Component CheckOf(params GH_Component[] elements) => After(c, Id.CheckModel, Build(c, elements));
            var valid = CheckOf(ForceDensity(c, Beams(c, lines), 1), Supports(c, border));
            var noSupports = CheckOf(ForceDensity(c, Beams(c, lines), 1));
            var freePart = CheckOf(ForceDensity(c, Beams(c, lines.Concat(other.lines)), 1), Supports(c, border));
            var noForce = CheckOf(Beams(c, lines), Supports(c, border));
            d.Solve();
            o.Outputs("valid", valid); o.Outputs("nosupports", noSupports); o.Outputs("freepart", freePart); o.Outputs("noforce", noForce);
        });
        Case(dump, "cable net component", (d, o) =>
        {
            var c = Start(d);
            var perimeter = new PolylineCurve(new[] { new Point3d(0, 0, 1.5), new Point3d(12, 0, -1.5), new Point3d(12, 8, 1.5), new Point3d(0, 8, -1.5), new Point3d(0, 0, 1.5) });
            var border = perimeter.DuplicateSegments().Select(s => (IGH_Goo)new GH_Curve(s)).ToArray();
            var net = d.Add(Id.CableNet); d.Set(net, 0, border); d.Set(net, 2, border); d.Set(net, 3, N(1)); d.Set(net, 4, N(1));
            GH_Component BeamsOf(int output) { var b = d.Add(Id.Beam); Doc.Link(b, 0, net, output); Doc.Link(b, 1, c.Property); b.Params.Input[0].DataMapping = GH_DataMapping.Flatten; return b; }
            var node = d.Add(Id.Node); Doc.Link(node, 0, net, 4);
            var restraints = d.Add(Id.Restraints); Doc.Link(restraints, 0, node); d.Set(restraints, 1, B(true), B(true), B(true));
            var build = Build(c, ForceDensity(c, BeamsOf(2), 2), ForceDensity(c, BeamsOf(3), 1), ForceDensity(c, BeamsOf(0), 1), restraints);
            Results(o, c, "net", FormFind(c, build, 0));
        });
        Case(dump, "section properties", (d, o) =>
        {
            var sections = new List<(string key, GH_Component c)>();
            GH_Component Section(string guid, params double[] values) { var s = d.Add(guid); for (int i = 0; i < values.Length; i++) d.Set(s, i, N(values[i])); return s; }
            sections.Add(("solidcircle", Section(Id.SolidCircle, 10)));
            sections.Add(("hollowcircle", Section(Id.HollowCircle, 100, 5)));
            sections.Add(("solidrect", Section(Id.SolidRect, 200, 100)));
            sections.Add(("hollowrect", Section(Id.HollowRect, 200, 100, 8, 6)));
            sections.Add(("i", Section(Id.ISection, 150, 120, 300, 12, 10, 7, 15)));
            sections.Add(("t", Section(Id.TSection, 120, 10, 100, 8)));
            var cs = Section(Id.CSection, 80, 8, 200, 6); d.Set(cs, 4, B(false)); d.Set(cs, 5, N(0)); sections.Add(("c", cs));
            var angle = Section(Id.Angle, 80, 8, 100, 8); d.Set(angle, 4, B(true)); d.Set(angle, 5, N(10)); sections.Add(("angle", angle));
            sections.Add(("generic", Section(Id.GenericSection, 1000, 2e6, 1e6, 5e5, 0, 0, 800, 800, 4e4, 3e4, 0, 0, 0)));
            var outputs = sections.Select(s => (s.key, p: After(new Chain { Doc = d }, Id.SectionProperties, s.c))).ToList();
            d.Solve();
            foreach (var s in outputs) o.Outputs(s.key, s.p);
            foreach (var s in sections) o.Messages(s.key + ".section", s.c);
        });
        Case(dump, "membrane and plate property values", (d, o) =>
        {
            var c = Start(d);
            var membrane = d.Add(Id.Membrane); d.Set(membrane, 0, S("PVC/PES")); d.Set(membrane, 1, N(1050)); d.Set(membrane, 2, N(4200)); d.Set(membrane, 3, N(4000)); d.Set(membrane, 4, N(600));
            var (lines, border) = SaddleGrid(2, 4000, 0);
            var build = Build(c, ForceDensity(c, Beams(c, lines), 1), Supports(c, border));
            var property = d.Add(Id.PlateProperty); d.Set(property, 0, S("Fabric")); d.Set(property, 1, N(0.8)); Doc.Link(property, 2, membrane);
            var run = FormFind(c, build, 0, 100, 1e-10, 9806.65); Doc.Link(run.Options, 10, property);
            Results(o, c, "fabric", run);
        });
        // User decision 4/11/2026 («cable e fixed axial force possono convivere»): a Cable force density and a Fixed Axial Force on the same
        // bars, with the three solvers. Master sets the cable force as axial target, then the fixed axial force replaces it (also with the
        // linear solver, whose message says that it ignores it); the order of the components does not matter.
        Case(dump, "cable and fixed axial force on one bar", (d, o) =>
        {
            var c = Start(d);
            foreach (var (solver, iterations, tolerance) in new[] { (0, 20, 1e-4), (1, 50, 1e-6), (2, 200, 1e-8) })
            {
                var cable = ForceDensity(c, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 3, 1, 1);
                var axial = Goal(c, Id.FixedAxialForce, cable, 4, 2);
                Results(o, c, "cable-axial" + solver, FormFind(c, Build(c, axial, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), solver, iterations, tolerance));
            }
            var first = Goal(c, Id.FixedAxialForce, Beams(c, new[] { new Line(A, M), new Line(M, B2) }), 4, 2);
            var after = ForceDensity(c, first, 3, 1, 1);
            Results(o, c, "axial-cable2", FormFind(c, Build(c, after, Supports(c, new[] { A, B2 }), NodeLoad(c, Nodes(c, new[] { M }), new Vector3d(0, 0, -4))), 2, 200, 1e-8));
        });
    }
}
