using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Arachne.Grasshopper.Components.Analysis;
using Arachne.Grasshopper.Components.Attributes;
using Arachne.Grasshopper.Components.Models;
using Arachne.Grasshopper.Components.Patterning;
using Arachne.Grasshopper.DataTypes;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// Form finding examples built with the FeMM components (examples/01 and 03).
internal static class FormFindingExample
{
    internal static void Write(string root)
    {
        Save(root, "01_SaddleNet", SaddleNet, 25, 40);
        Save(root, "03_CableNet", CableNet, 0, 0);
        Icons.WriteOverview(root, InventoryChecks.Objects());
    }

    private static T Place<T>(Canvas c, T obj, float x, float y) where T : IGH_DocumentObject
    {
        obj.Attributes.Pivot = new PointF(x, y);
        return obj;
    }

    // Places the remaining objects (sections, materials...) on a row
    private static void Arrange(Canvas c, float y)
    {
        float x = 60;
        foreach (var o in c.Document.Objects.Where(o => o.Attributes.Pivot == PointF.Empty))
        {
            o.Attributes.Pivot = new PointF(x, y);
            x += 220;
        }
    }

    private static GH_Panel Note(Canvas c, string text, float x, float y, float width, float height)
    {
        var note = c.Add(new GH_Panel { UserText = text }, x, y);
        note.Attributes.Bounds = new RectangleF(x, y, width, height);
        return note;
    }

    private static GH_BooleanToggle AutoRun(Canvas c, FormFindComponent ff, float x, float y)
    {
        var toggle = c.Add(new GH_BooleanToggle { Value = true, NickName = "Auto Run" }, x, y);
        ff.Params.Input[13].AddSource(toggle);
        return toggle;
    }

    // Grid lines of a square saddle, with the border lifted at the corners.
    private static void SaddleNet(Canvas c)
    {
        var chain = FormFindingChecks.Start(c);
        var (lines, border) = FormFindingChecks.SaddleGrid(4, 10000, 2000);
        var lineParam = c.Add(new Param_Curve { NickName = "Cavi" }, 60, 120);
        lineParam.SetPersistentData(lines.Select(l => new GH_Curve(new LineCurve(l))));
        var borderParam = c.Add(new Param_Point { NickName = "Vincoli" }, 60, 420);
        borderParam.SetPersistentData(border.Select(p => new GH_Point(p)));

        var beam = c.Add(new BeamComponent(), 300, 140); beam.Params.Input[0].AddSource(lineParam); Canvas.Link(beam, 1, chain.Property);
        var fd = FormFindingChecks.ForceDensity(chain, beam, 1);
        var node = c.Add(new NodeComponent(), 300, 420); node.Params.Input[0].AddSource(borderParam);
        var restraints = c.Add(new NodeRestraintsComponent(), 520, 420); Canvas.Link(restraints, 0, node);
        Canvas.Set<IGH_Goo>(restraints, 1, new GH_Boolean(true), new GH_Boolean(true), new GH_Boolean(true));
        var build = FormFindingChecks.Build(chain, fd, restraints);
        var check = c.Add(new CheckModelComponent(), 1000, 480); Canvas.Link(check, 0, build);
        var ff = c.Add(new FormFindComponent()); Canvas.Link(ff, 0, build);
        AutoRun(c, ff, 980, 640);
        var forces = c.Add(new BeamForcesComponent(), 1260, 200); Canvas.Link(forces, 0, ff);
        var reactions = c.Add(new SupportReactionsComponent(), 1260, 400); Canvas.Link(reactions, 0, ff);

        Place(c, chain.LoadCase, 60, 260); Place(c, chain.Property, 60, 600);
        Arrange(c, 760);
        Place(c, fd, 520, 140); Place(c, build, 760, 260); Place(c, ff, 1000, 260);
        Note(c, "ARACHNE | 01 Sella (N, mm)\nRete 10 x 10 m, 4 x 4 campi, bordo vincolato con quote +/-2 m agli angoli.\n" +
                "Beam (proprieta Cable, sezione piena D 10 mm, acciaio) + Force Density 1 N/mm, caso di carico FF.\n" +
                "Build Model in N/mm -> Check Model -> Form Find lineare -> Beam Forces (tiri, N) e Support Reactions (N).\n" +
                "Auto Run attivo: disattivarlo e usare il pulsante Run.", 60, 860, 680, 120);
    }

    // Cable Net on a saddle perimeter: warp and weft cables, restrained border.
    private static void CableNet(Canvas c)
    {
        var chain = FormFindingChecks.Start(c);
        var perimeter = new PolylineCurve(new[] { new Point3d(0, 0, 1500), new Point3d(12000, 0, -1500), new Point3d(12000, 8000, 1500), new Point3d(0, 8000, -1500), new Point3d(0, 0, 1500) });
        var border = c.Add(new Param_Curve { NickName = "Perimetro" }, 60, 160);
        border.SetPersistentData(perimeter.DuplicateSegments().Select(s => new GH_Curve(s)));
        var net = c.Add(new CableNetComponent(), 300, 200);
        net.Params.Input[0].AddSource(border); net.Params.Input[2].AddSource(border);
        Canvas.Set(net, 3, new GH_Number(1000)); Canvas.Set(net, 4, new GH_Number(1000));

        var warp = c.Add(new BeamComponent(), 560, 80); Canvas.Link(warp, 0, net, 2); Canvas.Link(warp, 1, chain.Property);
        var weft = c.Add(new BeamComponent(), 560, 260); Canvas.Link(weft, 0, net, 3); Canvas.Link(weft, 1, chain.Property);
        var edges = c.Add(new BeamComponent(), 560, 440); Canvas.Link(edges, 0, net, 0); Canvas.Link(edges, 1, chain.Property);
        edges.Params.Input[0].DataMapping = GH_DataMapping.Flatten; warp.Params.Input[0].DataMapping = GH_DataMapping.Flatten; weft.Params.Input[0].DataMapping = GH_DataMapping.Flatten;
        var fdWarp = FormFindingChecks.ForceDensity(chain, warp, 2); Place(c, fdWarp, 800, 80);
        var fdWeft = FormFindingChecks.ForceDensity(chain, weft, 1); Place(c, fdWeft, 800, 260);
        var fdEdges = FormFindingChecks.ForceDensity(chain, edges, 1); Place(c, fdEdges, 800, 440);
        var node = c.Add(new NodeComponent(), 560, 640); Canvas.Link(node, 0, net, 4);
        var restraints = c.Add(new NodeRestraintsComponent(), 800, 640); Canvas.Link(restraints, 0, node);
        Canvas.Set<IGH_Goo>(restraints, 1, new GH_Boolean(true), new GH_Boolean(true), new GH_Boolean(true));
        var build = FormFindingChecks.Build(chain, fdWarp, fdWeft, fdEdges, restraints); Place(c, build, 1060, 300);
        var ff = c.Add(new FormFindComponent(), 1300, 300); Canvas.Link(ff, 0, build);
        AutoRun(c, ff, 1280, 560);
        Place(c, chain.LoadCase, 300, 520); Place(c, chain.Property, 300, 680);
        Arrange(c, 820);
        Note(c, "ARACHNE | 03 Cable Net (N, mm)\nPerimetro a sella 12 x 8 m, maglia 1 x 1 m. Ordito q = 2 N/mm, trama q = 1 N/mm, bordo vincolato.\n" +
                "Le linee di bordo vincolate (Restraint edges) entrano come Beam per chiudere le maglie della mesh.\n" +
                "Build Model in N/mm -> Form Find lineare con mesh.", 60, 900, 660, 100);
    }

    private static void Save(string root, string name, Action<Canvas> build, int nodes, int beams)
    {
        using (var c = new Canvas())
        {
            build(c);
            c.Solve(); Verify(c.Document, name, nodes, beams);
            foreach (string extension in new[] { ".ghx", ".gh" })
            {
                string path = Path.Combine(root, "examples", name + extension);
                var archive = new GH_Archive();
                if (!archive.AppendObject(c.Document, "Definition") || !archive.WriteToFile(path, true, false)) throw new IOException("Cannot save " + path);
                var restored = new GH_Archive();
                using (var reopened = new GH_Document { Enabled = true })
                {
                    if (!restored.ReadFromFile(path) || !restored.ExtractObject(reopened, "Definition") || reopened.ObjectCount != c.Document.ObjectCount)
                        throw new IOException("Cannot reopen " + path);
                    reopened.NewSolution(false); Verify(reopened, name, nodes, beams);
                }
            }
            Console.WriteLine($"Example {name}: {c.Document.ObjectCount} objects, GH and GHX reopened and recalculated.");
        }
    }

    private static void Verify(GH_Document document, string name, int nodes, int beams)
    {
        foreach (var component in document.Objects.OfType<GH_Component>())
            if (component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0)
                throw new InvalidOperationException(name + " / " + component.Name + ": " + string.Join("; ", component.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        var ff = document.Objects.OfType<FormFindComponent>().Single();
        var model = ff.Params.Output[0].VolatileData.AllData(true).OfType<GH_Model>().SingleOrDefault()?.Value;
        var mesh = ff.Params.Output[1].VolatileData.AllData(true).OfType<GH_Mesh>().SingleOrDefault()?.Value;
        if (model == null || mesh == null || mesh.Faces.Count == 0) throw new InvalidOperationException(name + ": Form Find produced no result.");
        if (nodes > 0 && (model.Nodes.Count != nodes || model.Beams.Count != beams))
            throw new InvalidOperationException($"{name}: {model.Nodes.Count} nodes / {model.Beams.Count} beams.");
    }
}
