using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Arachne.Common.Analysis;
using Arachne.Common.Helpers;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.Models;
using GPC.Model.Restraints;
using GPC.Model.Structure.Assignments;
using G = GPC.Geometry;
using R = Rhino.Geometry;

// Scale of the GPC path (reviews A1 and A2): Build Model, analysis graph, TensileLib export and solve, results, projection, copy and
// GPC archive of a triangulated net of k x k nodes (3 bars and 2 explicit membrane triangles per node). Writes MIGRATION_SCALE_OUTPUT.
internal static class MigrationScale
{
    /// <summary>A Build Model output in N and mm: k x k nodes 1 m apart, the perimeter restrained, horizontal, vertical and diagonal
    /// cables with a force density, and two membrane triangles per square.</summary>
    internal static GPC.Model.Models.Model Grid(int k, out int nodes, out int bars, out int triangles)
    {
        var owner = Guid.Parse("7b0c2a55-0000-4000-8000-00000000a000");
        var cases = InputIdentity.LoadCase("", "FF");
        var steel = new GPC.Model.Materials.Material("Steel", 210000, 0.3, 7.849e-9, 1.17e-5);
        var calc = new Arachne.Common.Geometry.SectionCalculator { Type = Arachne.Common.Geometry.SectionCalculator.Kind.SolidCircle, D = 10 }; calc.Calculate();
        var property = new ElasticBeamProperty("Cable", steel, calc.ToSection());
        var fabric = new MembranePlateProperty("Fabric") { MassPerArea = 1.05e-9 };
        var points = new G.Point3d[k, k]; var nodeList = new List<NodeElement>(); var beamList = new List<BeamElement>(); var plateList = new List<AreaElement>();
        for (int y = 0; y < k; y++)
            for (int x = 0; x < k; x++)
            {
                bool edge = x == 0 || y == 0 || x == k - 1 || y == k - 1;
                points[x, y] = new G.Point3d(1000.0 * x, 1000.0 * y, edge ? 200 * Math.Sin(0.3 * x) : 0);
                if (!edge) continue;
                var node = new NodeElement(InputIdentity.Of(owner, "node:" + x + ":" + y), points[x, y]);
                node.Assignments.Restrains.Add(new RestrainAssignment { Restrain = new NodeRestrain(node, new List<DofRestrain> {
                    new DofRestrain(GeometryRestrain.DOF.DX, true), new DofRestrain(GeometryRestrain.DOF.DY, true), new DofRestrain(GeometryRestrain.DOF.DZ, true) }) });
                nodeList.Add(node);
            }
        void Beam(G.Point3d a, G.Point3d b, string key)
        {
            var beam = new BeamElement(InputIdentity.Of(owner, key), a, b, property);
            beam.Assignments.Formulation = BeamFormulation.Cable;
            beam.Attributes.Add(new FormFindingGoalAttribute(new FormFindingGoal { Kind = FormFindingGoalKind.ForceDensity, Target = 1, Weight = 1 }, cases));
            beamList.Add(beam);
        }
        void Triangle(G.Point3d a, G.Point3d b, G.Point3d c, string key)
        {
            var id = InputIdentity.Of(owner, key);
            var area = new AreaElement(id, null, fabric);
            area.ConnectNodes(new[] { a, b, c }.Select((p, i) => new NodeElement(InputIdentity.Of(id, "vertex:" + i), p)).ToArray());
            plateList.Add(area);
        }
        for (int y = 0; y < k; y++)
            for (int x = 0; x < k; x++)
            {
                if (x > 0) Beam(points[x - 1, y], points[x, y], "h:" + x + ":" + y);
                if (y > 0) Beam(points[x, y - 1], points[x, y], "v:" + x + ":" + y);
                if (x > 0 && y > 0)
                {
                    Beam(points[x - 1, y - 1], points[x, y], "d:" + x + ":" + y);
                    Triangle(points[x - 1, y - 1], points[x, y - 1], points[x, y], "t1:" + x + ":" + y);
                    Triangle(points[x - 1, y - 1], points[x, y], points[x - 1, y], "t2:" + x + ":" + y);
                }
            }
        var model = ModelBuilder.Build(nodeList, beamList, plateList, ModelUnits.Nmm, "Scale", owner, 0.001);
        nodes = model.NodesElements.Count; bars = model.BeamElements.Count; triangles = model.AreaElements.Count;
        return model;
    }

    internal static int Run()
    {
        // MIGRATION_SCALE_DUMP: the GPC archive (XML) of a net of 3 x 3 nodes, before and after the form finding, to read what it holds
        string dump = Environment.GetEnvironmentVariable("MIGRATION_SCALE_DUMP");
        if (!string.IsNullOrEmpty(dump))
        {
            var small = Grid(3, out _, out _, out _);
            var smallInput = FormFindingInputBuilder.Build(small, ModelUnits.Nmm, 0, new FormFindingOptions(), R.Plane.WorldXY);
            var smallExport = TensileModelAdapter.Export(smallInput.Model, smallInput.Units, smallInput);
            TensileModelAdapter.Attach(smallInput.Model, smallExport, TensileModelAdapter.Solve(smallExport.Solver, smallInput.Model.FormFinding), smallInput);
            using (var file = File.Create(dump + ".model.xml")) GPC.Model.Persistence.ModelArchive.SaveDocument(small, file);
            using (var file = File.Create(dump + ".analysis.xml")) GPC.Model.Persistence.ModelArchive.SaveDocument(smallInput.Model, file);
            return 0;
        }
        string path = Environment.GetEnvironmentVariable("MIGRATION_SCALE_OUTPUT");
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("Set MIGRATION_SCALE_OUTPUT to a new CSV file.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using (var output = new StreamWriter(new FileStream(path, FileMode.CreateNew)))
        {
            output.WriteLine("nodes,bars,triangles,operation,seconds,bytes");
            Grid(5, out _, out _, out _);
            foreach (int k in (Environment.GetEnvironmentVariable("MIGRATION_SCALE_SIZES") ?? "32,100").Split(',').Select(int.Parse))
            {
                int n = 0, b = 0, t = 0; long bytes = 0;
                void Measure(string name, Action action)
                {
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    var watch = Stopwatch.StartNew(); action(); watch.Stop();
                    output.WriteLine(string.Join(",", n, b, t, name, watch.Elapsed.TotalSeconds.ToString("F4", CultureInfo.InvariantCulture), bytes)); output.Flush();
                    Console.WriteLine($"SCALE {n}/{b}/{t} {name}: {watch.Elapsed.TotalSeconds:F3} s {(bytes > 0 ? bytes + " bytes" : "")}");
                    bytes = 0;
                }
                GPC.Model.Models.Model model = null; FormFindingInputBuilder.Input input = null; TensileModelAdapter.Exported exported = null; TensileModelAdapter.Outcome outcome = null;
                Measure("build-model", () => model = Grid(k, out n, out b, out t));
                Measure("analysis-graph", () => input = FormFindingInputBuilder.Build(model, ModelUnits.Nmm, 0, new FormFindingOptions(), R.Plane.WorldXY));
                // parts of the export, measured alone: domain preflight, identity map, fingerprint of the analysis input
                Measure("export: preflight (ValidateDomain)", () => TensileModelAdapter.Preflight(input.Model));
                Measure("export: identity map refresh", () => ModelAssembly.RefreshIdentityMap(ModelValues.Copy(input.Model)));
                Measure("export: analysis fingerprint", () => input.Model.AnalysisFingerprint());
                Measure("export (A1)", () => exported = TensileModelAdapter.Export(input.Model, input.Units, input));
                Measure("solve-linear", () => outcome = TensileModelAdapter.Solve(exported.Solver, input.Model.FormFinding));
                Measure("attach-results", () => TensileModelAdapter.Attach(input.Model, exported, outcome, input));
                // The sidecar of the Form Find (AnalysisDocumentState): prepare at the end of the run, save with the document, open
                var sidecar = new GPC.Model.Persistence.ExternalModelArchive(); GPC.Model.Persistence.ExternalModelReference reference = null;
                string file = Path.Combine(Path.GetTempPath(), "arachne-scale-" + Guid.NewGuid().ToString("N") + GPC.Model.Persistence.ExternalModelArchive.Extension);
                try
                {
                    Measure("sidecar: prepare", () => reference = sidecar.Prepare(input.Model, exported.Map));
                    Measure("sidecar: save", () => { sidecar.SaveSnapshot(file, new[] { reference }); bytes = new FileInfo(file).Length; });
                    Measure("sidecar: open", () => sidecar.Open(reference));
                }
                catch (Exception e) { Console.WriteLine("SCALE sidecar failed: " + e.GetBaseException().Message); output.WriteLine(string.Join(",", n, b, t, "sidecar failed: " + e.GetBaseException().Message.Replace(',', ';'), 0, 0)); }
                finally { try { File.Delete(file); } catch (IOException) { } }
                FormFindingOutputProjection.Output view = null;
                Measure("project-outputs", () => view = FormFindingOutputProjection.Project(model, input.Model, new FormFindingOptions(), ModelUnits.Nmm));
                Measure("copy-model (GH Duplicate)", () => ModelValues.Copy(model));
                byte[] archive = null, archived = null;
                byte[] rawModel = null;
                Measure("archive-model: GPC ModelArchive alone (not deflated)", () => { using (var raw = new MemoryStream()) { GPC.Model.Persistence.ModelArchive.Save(model, raw); rawModel = raw.ToArray(); bytes = raw.Length; } });
                foreach (var level in new[] { System.IO.Compression.CompressionLevel.Fastest, System.IO.Compression.CompressionLevel.Optimal })
                    Measure("archive-model: deflate " + level + " of the GPC bytes", () =>
                    {
                        using (var packed = new MemoryStream())
                        {
                            using (var deflate = new System.IO.Compression.DeflateStream(packed, level, true)) deflate.Write(rawModel, 0, rawModel.Length);
                            bytes = packed.Length;
                        }
                    });
                Measure("archive-model", () => { archive = ContractArchive.Model(model); bytes = archive.Length; });
                Measure("read-model", () => ContractArchive.ReadModel(archive));
                Measure("archive-form-found-model", () => { archived = ContractArchive.Model(view.Model); bytes = archived.Length; });
                Measure("read-form-found-model", () => ContractArchive.ReadModel(archived));
            }
        }
        return 0;
    }
}
