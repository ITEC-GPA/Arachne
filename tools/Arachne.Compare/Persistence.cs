using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// GH file size and save/open times of an internalized Build Model output and of a document with a Form Find run,
// for a triangulated net of k x k nodes (about 3 bars and 2 triangles per node). Plug-in independent.
internal static class Persistence
{
    internal static void Run(string work, string output)
    {
        Directory.CreateDirectory(work);
        using (var report = new StreamWriter(output, false))
        {
            report.WriteLine("nodes,beams,build_s,duplicate_s,model_gh_bytes,model_save_s,model_open_s,model_reopened_ok,ff_s,ff_gh_bytes,ff_sidecar_bytes,ff_save_s,ff_open_s");
            foreach (int k in (Environment.GetEnvironmentVariable("ARACHNE_IO_SIZES") ?? "32,100").Split(',').Select(int.Parse))
            {
                var line = Measure(work, k);
                report.WriteLine(line); report.Flush();
                Console.WriteLine("IO " + line);
            }
        }
    }

    // The solve time of every component (GH ProcessorTime), to see where a solution goes.
    private static void Times(Doc d, string what, int k)
    {
        foreach (var component in d.Document.Objects.OfType<GH_Component>())
        {
            Console.WriteLine("TIME " + k * k + " " + what + " " + component.Name + ": " + component.ProcessorTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture) + " s");
            foreach (var level in new[] { GH_RuntimeMessageLevel.Error, GH_RuntimeMessageLevel.Warning })
                foreach (var message in component.RuntimeMessages(level))
                    Console.WriteLine("MESSAGE " + k * k + " " + what + " " + component.Name + " " + level + ": " + message);
        }
    }

    private static string Measure(string work, int k)
    {
        var lines = new List<Line>(); var border = new List<Point3d>();
        double step = 1000;
        Point3d P(int x, int y) => new Point3d(x * step, y * step, (x == 0 || y == 0 || x == k - 1 || y == k - 1) ? 0.2 * step * Math.Sin(x * 0.3) : 0);
        for (int y = 0; y < k; y++)
            for (int x = 0; x < k; x++)
            {
                if (x == 0 || y == 0 || x == k - 1 || y == k - 1) border.Add(P(x, y));
                if (x > 0) lines.Add(new Line(P(x - 1, y), P(x, y)));
                if (y > 0) lines.Add(new Line(P(x, y - 1), P(x, y)));
                if (x > 0 && y > 0) lines.Add(new Line(P(x - 1, y - 1), P(x, y)));
            }
        using (var d = new Doc())
        {
            var lc = d.Add(Id.LoadCase); d.Set(lc, 0, new GH_String("FF"));
            var section = d.Add(Id.SolidCircle); d.Set(section, 0, new GH_Number(10));
            var material = d.Add(Id.Steel); d.Set(material, 0, new GH_String("Steel")); d.Set(material, 1, new GH_Integer(0));
            var property = d.Add(Id.BeamProperty); d.Set(property, 0, new GH_String("Cable")); d.Set(property, 1, new GH_Integer(1));
            Doc.Link(property, 2, section); Doc.Link(property, 3, material);
            var beams = d.Add(Id.Beam); d.Set(beams, 0, lines.Select(l => (IGH_Goo)new GH_Curve(new LineCurve(l))).ToArray()); Doc.Link(beams, 1, property);
            var fd = d.Add(Id.ForceDensity); Doc.Link(fd, 0, beams); d.Set(fd, 1, new GH_Integer(0)); Doc.Link(fd, 2, lc); d.Set(fd, 3, new GH_Number(1)); d.Set(fd, 4, new GH_Boolean(false)); d.Set(fd, 5, new GH_Number(1));
            var nodes = d.Add(Id.Node); d.Set(nodes, 0, border.Select(p => (IGH_Goo)new GH_Point(p)).ToArray());
            var supports = d.Add(Id.Restraints); Doc.Link(supports, 0, nodes); d.Set(supports, 1, new GH_Boolean(true), new GH_Boolean(true), new GH_Boolean(true));
            var build = d.Add(Id.BuildModel); Doc.Link(build, 0, fd); Doc.Link(build, 0, supports); build.Params.Input[0].DataMapping = GH_DataMapping.Flatten;
            var clock = Stopwatch.StartNew();
            d.Solve();
            double buildTime = clock.Elapsed.TotalSeconds;
            Times(d, "build", k);
            var model = build.Params.Output[0].VolatileData.AllData(true).Single();
            clock.Restart(); var copy = model.Duplicate(); double duplicate = clock.Elapsed.TotalSeconds;

            // Internalized model in a parameter of a new document
            string modelPath = Path.Combine(work, "model-" + k + ".gh");
            double modelSave;
            using (var store = new GH_Document())
            {
                var param = new Param_GenericObject(); param.CreateAttributes(); param.PersistentData.Append(copy); store.AddObject(param, false);
                clock.Restart(); bool saved = new GH_DocumentIO(store).SaveQuiet(modelPath); modelSave = clock.Elapsed.TotalSeconds;
                if (!saved) throw new InvalidOperationException("Save failed");
            }
            // ARACHNE_IO_REOPEN=0 skips reopening the internalized model (master 7b1108d at 60,025 nodes: 24 minutes, then not read back);
            // open time and the reopened flag are then left empty.
            double modelOpen = double.NaN; bool? ok = null;
            if (Environment.GetEnvironmentVariable("ARACHNE_IO_REOPEN") != "0")
            {
                clock.Restart(); var reopen = new GH_DocumentIO(); bool opened = reopen.Open(modelPath); modelOpen = clock.Elapsed.TotalSeconds;
                // A model the plug-in cannot read back is dropped by GH (empty parameter): reported as False, the run goes on
                // (master 7b1108d at 60,025 nodes: the BinaryFormatter archive of FeMM is not read back).
                var reopened = opened ? reopen.Document?.Objects.OfType<Param_GenericObject>().SingleOrDefault()?.PersistentData.AllData(true).ToList() : null;
                ok = reopened != null && reopened.Count == 1 && reopened[0] != null && reopened[0].IsValid;
                reopen.Document?.Dispose();
            }

            // Form Find in the same document, saved with its results
            var options = d.Add(Id.SolverOptions); d.Set(options, 4, new GH_Integer(20)); d.Set(options, 9, new GH_Boolean(true));
            var ff = d.Add(Id.FormFind); Doc.Link(ff, 0, build); d.Set(ff, 1, new GH_Integer(0)); Doc.Link(ff, 2, options); d.Set(ff, 4, new GH_Boolean(true));
            clock.Restart(); d.Solve(); double ffTime = clock.Elapsed.TotalSeconds;
            Times(d, "form-find", k);
            string ffPath = Path.Combine(work, "formfind-" + k + ".gh");
            foreach (var old in Directory.GetFiles(work, "formfind-" + k + ".*")) File.Delete(old);
            clock.Restart(); bool ffSaved = new GH_DocumentIO(d.Document).SaveQuiet(ffPath); double ffSave = clock.Elapsed.TotalSeconds;
            if (!ffSaved) throw new InvalidOperationException("Form Find save failed");
            long sidecar = Directory.GetFiles(work, "formfind-" + k + ".*").Where(f => !f.EndsWith(".gh", StringComparison.OrdinalIgnoreCase)).Sum(f => new FileInfo(f).Length);
            clock.Restart(); var reopenFf = new GH_DocumentIO(); reopenFf.Open(ffPath); double ffOpen = clock.Elapsed.TotalSeconds; reopenFf.Document?.Dispose();
            var inv = CultureInfo.InvariantCulture;
            return string.Join(",", new object[] { k * k, lines.Count, buildTime.ToString("F3", inv), duplicate.ToString("F3", inv), new FileInfo(modelPath).Length,
                modelSave.ToString("F3", inv), double.IsNaN(modelOpen) ? "" : modelOpen.ToString("F3", inv), ok?.ToString() ?? "", ffTime.ToString("F3", inv), new FileInfo(ffPath).Length, sidecar, ffSave.ToString("F3", inv), ffOpen.ToString("F3", inv) });
        }
    }
}
