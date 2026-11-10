using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Arachne.Common.Analysis;
using Arachne.Common.Helpers;
using Arachne.Grasshopper.DataTypes;
using GH_IO.Serialization;
using GH_Mesh = Grasshopper.Kernel.Types.GH_Mesh;
using GH_Number = Grasshopper.Kernel.Types.GH_Number;
using GH_String = Grasshopper.Kernel.Types.GH_String;
using GPC.Model.Models;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Elements;
using GPC.Model.Loads;
using G = GPC.Geometry;
using R = Rhino.Geometry;

// Contracts of the GPC migration (correzioni-P2A.md): GH data carry GPC objects, their copies and archives, the refused legacy
// payloads (A3), the unit boundary of Build Model, the domain preflight, the sidecars of the Form Find and the linear export (A1).
internal static class F6ContractsChecks
{
    internal static SuiteReport Summary { get; private set; }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static int Run()
    {
        int failed = 0;
        Summary = new SuiteReport { Name = "GpcContracts" };
        foreach (var test in new Action[] { GhDataCarryGpcObjects, GhDataArchives, LegacyPayloadsRejected, FailedReadsClearPopulatedValues, UnitBoundary, DecomposeAndRebuild, PreflightDiagnostics,
            DocumentSidecars, SaveImmediatelyAfterSolve, BackgroundAnalysisAccessRejected, SavedOutputRestoration, StandardGeometrySavedOutputRestoration, CrossRuntimeCircularOutlines, LinearExport, SharedProperties })
            try { test(); Summary.Passed++; Console.WriteLine("F6 PASS " + test.Method.Name); }
            catch (Exception ex) { failed++; Summary.Failed++; Console.WriteLine("F6 FAIL " + test.Method.Name + ": " + ex); }
        return failed;
    }

    private static T RoundTrip<T>(T goo) where T : Grasshopper.Kernel.Types.IGH_Goo, new()
    {
        var chunk = new GH_LooseChunk("check");
        Check(goo.Write(chunk.CreateChunk("item")), "Write " + typeof(T).Name);
        var read = new GH_LooseChunk("check"); read.Deserialize_Binary(chunk.Serialize_Binary());
        var restored = new T(); Check(restored.Read(read.FindChunk("item")), "Read " + typeof(T).Name);
        return restored;
    }

    private static void GhDataCarryGpcObjects()
    {
        var node = new GH_Node(new R.Point3d(1, 2, -0.0), Guid.Parse("00000000-0000-0000-0000-00000000a001"));
        Check(node.Value is NodeElement && node.Guid == node.Value.Guid, "Node carries a GPC NodeElement with its identity");
        var copy = (GH_Node)node.Duplicate();
        copy.Value.Position = new G.Point3d(5, 5, 5);
        Check(node.Position.X == 1 && copy.Guid == node.Guid && BitConverter.DoubleToInt64Bits(node.Value.Position.Z) == long.MinValue, "Detached copy, same identity, negative zero kept");
        var lc1 = new GH_LoadCase("Wind"); var lc2 = new GH_LoadCase("Wind");
        Check(lc1.Value.Guid == lc2.Value.Guid, "Load cases are their name, as in FeMM");
        var parent = new GH_Group("Net", null); var child = new GH_Group("Warp", parent.Value);
        Check(child.Value.Parent?.Name == "Net" && new GH_Group("Warp", parent.Value).Value.Guid == child.Value.Guid, "Groups are their full name");
    }

    private static void GhDataArchives()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas);
            var beams = FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, new[] { new R.Line(R.Point3d.Origin, new R.Point3d(1000, 0, 0)) }), 1);
            var supports = FormFindingChecks.Supports(chain, new[] { R.Point3d.Origin, new R.Point3d(1000, 0, 0) });
            var build = FormFindingChecks.Build(chain, beams, supports, FormFindingChecks.NodeLoad(chain, FormFindingChecks.Nodes(chain, new[] { new R.Point3d(1000, 0, 0) }), new R.Vector3d(0, 0, -1)));
            var run = FormFindingChecks.FormFind(chain, build, 0);
            canvas.Solve();
            var model = Canvas.Single<GH_Model>(build);
            var model2 = RoundTrip(model);
            Check(model2.Units == model.Units && model2.Value.AnalysisFingerprint() == model.Value.AnalysisFingerprint(), "Model archive keeps the fingerprint and the units");
            var beam = Canvas.Single<GH_Beam>(beams); var beam2 = RoundTrip(beam);
            Check(beam2.Guid == beam.Guid && ElementDrafts.Goals(beam2.Value).Single().Goal.Target == 1 && beam2.Value.BeamProperty is GPC.Model.ElementProperties.ElasticBeamProperty, "Beam archive");
            var node = (GH_Node)Canvas.Out(supports, 0)[0]; var node2 = RoundTrip(node);
            Check(node2.Guid == node.Guid && node2.Value.Assignments.Restrains.Count == 1, "Node archive");
            var property = Canvas.Single<GH_BeamProperty>(chain.Property); var property2 = RoundTrip(property);
            Check(property2.Formulation == property.Formulation && ((GPC.Model.Sections.Section)property2.Value.Section).Area == ((GPC.Model.Sections.Section)property.Value.Section).Area, "Beam property archive");
            var section = new GH_BeamSection((GPC.Model.Sections.Section)property.Value.Section);
            Check(RoundTrip(section).Value.Area == section.Value.Area, "Section archive");
            var options = Canvas.Single<GH_SolverOptions>(FormFindingChecks.Options(run)); var options2 = RoundTrip(options);
            Check(options2.Value.MaxIterations == options.Value.MaxIterations && options2.Value.CreateMesh == options.Value.CreateMesh, "Solver options archive");
            var output = Canvas.Single<GH_Model>(run); var output2 = RoundTrip(output);
            Check(output2.Value.AllElements.Any(e => e.Results.Count > 0) && output2.Value.NodesElements.Values.Select(n => FormFindingOutputProjection.Position(n, output2.Scale))
                .SequenceEqual(output.Value.NodesElements.Values.Select(n => FormFindingOutputProjection.Position(n, output.Scale))), "Form-found model archive keeps results and exact positions");
            var lc = RoundTrip(new GH_LoadCase("Q")); Check(lc.Value.Name == "Q", "Load case archive");
            var cs = RoundTrip(GH_CoordinateSystem.FromPlane(new R.Plane(R.Point3d.Origin, R.Vector3d.XAxis, R.Vector3d.ZAxis))); Check(cs.Value.V2.Z == 1, "Coordinate system archive");
            var group = RoundTrip(new GH_Group("Warp", new GH_Group("Net", null).Value)); Check(group.Value.Name == "Warp" && group.Value.Parent?.Name == "Net", "Group archive");
            // data carried by a base type: the concrete GPC type comes back (closed list, chosen by the reader)
            var steel = new GPC.Model.Materials.Material("S355", 210000, 0.3, 7.85e-9, 1.2e-5);
            var fabric = new GPC.Model.ElementProperties.MembranePlateProperty("PVC") { MassPerArea = 1.05e-9, StrengthWarp = 84 };
            var material = RoundTrip(new GH_Material(steel)); Check(material.Material?.ElasticModulusTension == 210000 && material.Material.Name == "S355", "Material archive");
            var membrane = RoundTrip(new GH_Material(fabric)); Check(membrane.Membrane?.MassPerArea == 1.05e-9 && membrane.Membrane.StrengthWarp == 84, "Membrane material archive");
            var elastic = new GPC.Model.ElementProperties.ElasticPlateProperty("Plate", steel, 0.8, 0.8);
            var plate = RoundTrip(new GH_PlateProperty(elastic)); Check(plate.Value is GPC.Model.ElementProperties.ElasticPlateProperty e && e.MembraneThickness == 0.8 && e.Material.Name == "S355", "Elastic plate property archive");
            var plate2 = RoundTrip(new GH_PlateProperty(fabric)); Check(plate2.Value is GPC.Model.ElementProperties.MembranePlateProperty m && m.MassPerArea == 1.05e-9, "Membrane plate property archive");
            var withPlate = new GH_SolverOptions(options.Value.Copy()); withPlate.Value.PlateProperty = elastic;
            Check(RoundTrip(withPlate).Value.PlateProperty is GPC.Model.ElementProperties.ElasticPlateProperty, "Solver options archive with a plate property");
            bool refused = false;
            try { ContractArchive.ReadMember<GPC.Model.ElementProperties.PlateProperty>(ContractArchive.Member(steel)); } catch (System.Runtime.Serialization.SerializationException) { refused = true; }
            Check(refused, "A material is not read as a plate property");
            var written = ContractArchive.Model(model.Value);
            Check(written[7] == 2 && System.Text.Encoding.ASCII.GetString(written, 9, 6) == "GPCMOD", "GH data use current compact GPC archives");
            void RejectVersion(byte[] data, Action<byte[]> read)
            {
                foreach (byte version in new byte[] { 0, 1, 3, 255 })
                {
                    var invalid = (byte[])data.Clone(); invalid[7] = version;
                    bool rejected = false;
                    try { read(invalid); }
                    catch (System.Runtime.Serialization.SerializationException ex) { rejected = ex.Message.StartsWith("UnsupportedArachneArchiveVersion"); }
                    Check(rejected, "Unsupported archive version must be rejected: " + version);
                }
            }
            RejectVersion(written, data => ContractArchive.ReadModel(data));
            RejectVersion(ContractArchive.Member(steel), data => ContractArchive.ReadMember<GPC.Model.Materials.Material>(data));
            RejectVersion(ContractArchive.Value(beam.Value), data => ContractArchive.ReadValue<BeamElement>(data));
        }
    }

    private static void LegacyPayloadsRejected()
    {
        // A BinaryFormatter (NRBF) header and an ARCGPC2 archive of the F6 development builds
        var nrbf = new byte[] { 0, 1, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 1, 0, 0, 0, 0, 0, 0, 0, 12, 2, 0, 0, 0 };
        var arcgpc2 = System.Text.Encoding.ASCII.GetBytes("ARCGPC2").Concat(new byte[] { 1, 2, 3 }).ToArray();
        foreach (var data in new[] { nrbf, arcgpc2 })
        {
            Check(ContractArchive.IsLegacy(data), "Legacy payload recognized");
            var chunk = new GH_LooseChunk("check"); chunk.CreateChunk("item").SetByteArray("Data", data);
            var read = new GH_LooseChunk("check"); read.Deserialize_Binary(chunk.Serialize_Binary());
            var node = new GH_Node();
            Check(!node.Read(read.FindChunk("item")) && node.Value == null, "Legacy node payload refused without executing a formatter");
            bool explained = false;
            try { ContractArchive.ReadValue<NodeElement>(data); } catch (System.Runtime.Serialization.SerializationException e) { explained = e.Message == ContractArchive.LegacyMessage; }
            Check(explained, "Legacy payload refused with the actionable message");
        }
        var model = new GH_LooseChunk("check"); model.CreateChunk("item").SetByteArray("Data", nrbf);
        var modelRead = new GH_LooseChunk("check"); modelRead.Deserialize_Binary(model.Serialize_Binary());
        Check(!new GH_Model().Read(modelRead.FindChunk("item")), "Legacy model payload refused");
    }

    // User decision of 4/11/2026 («Condividi proprietà», lane L9): the copies of the GH data share the immutable properties, materials
    // and sections; elements and models stay copies. Downstream (attributes, Build Model in two units, Decompose, Form Find with a plate
    // property, Duplicate and edits of the copies) nothing changes the data upstream: a property that has to change is a new one.
    private static void SharedProperties()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas); var grid = FormFindingChecks.SaddleGrid(2, 4000, 1000);
            var beams = FormFindingChecks.Beams(chain, grid.lines);
            var fd = FormFindingChecks.ForceDensity(chain, beams, 1);
            // restraints along the axes of an inclined plane (all three translations: the same form, coordinate systems off the global axes)
            var supports = FormFindingChecks.Supports(chain, grid.border, true, true, true, new R.Plane(new R.Point3d(10, 20, 30), new R.Vector3d(1, 0, 0.25), new R.Vector3d(0, 1, 0)));
            // two plate properties made from one membrane material, on explicit plates
            var membrane = canvas.Add(new Arachne.Grasshopper.Components.Materials.MaterialMembraneComponent());
            Canvas.Set(membrane, 0, new GH_String("PTFE")); Canvas.Set(membrane, 1, new GH_Number(1500)); Canvas.Set(membrane, 2, new GH_Number(5000)); Canvas.Set(membrane, 3, new GH_Number(4500));
            Grasshopper.Kernel.GH_Component PlateProperty(string name)
            {
                var p = canvas.Add(new Arachne.Grasshopper.Components.ElementProperties.PlatePropertyComponent());
                Canvas.Set(p, 0, new GH_String(name)); Canvas.Set(p, 1, new GH_Number(1)); Canvas.Link(p, 2, membrane); return p;
            }
            var propertyA = PlateProperty("A"); var propertyB = PlateProperty("B");
            Grasshopper.Kernel.GH_Component Plate(Grasshopper.Kernel.GH_Component property, int x)
            {
                var face = new R.Mesh();
                face.Vertices.Add(2000 * x, 0, 0); face.Vertices.Add(2000 * (x + 1), 0, 0); face.Vertices.Add(2000 * (x + 1), 2000, 0); face.Vertices.Add(2000 * x, 2000, 0);
                face.Faces.AddFace(0, 1, 2, 3);
                var plate = canvas.Add(new Arachne.Grasshopper.Components.Models.PlateComponent()); Canvas.Set(plate, 0, new GH_Mesh(face)); Canvas.Link(plate, 1, property); return plate;
            }
            var plates = new[] { Plate(propertyA, 0), Plate(propertyB, 1) };
            var build = FormFindingChecks.Build(chain, fd, supports);
            var buildKnm = FormFindingChecks.Build(chain, ModelUnits.Knm, fd, supports);
            var buildPlates = FormFindingChecks.Build(chain, plates);
            var decompose = canvas.Add(new Arachne.Grasshopper.Components.Models.DecomposeModelComponent()); Canvas.Link(decompose, 0, build);
            var run = FormFindingChecks.FormFind(chain, build, 0);
            Canvas.Link(FormFindingChecks.Options(run), 10, propertyA); // the plates generated by the form finding
            canvas.Solve();
            foreach (var component in new[] { build, buildKnm, buildPlates, decompose, run }) Check(!Canvas.HasErrors(component), component.Name + ": " + Canvas.Messages(component));

            var property = Canvas.Single<GH_BeamProperty>(chain.Property).Value;
            var section = property.Section; var steel = property.Material;
            var fabric = Canvas.Single<GH_Material>(membrane).Value;
            var a = Canvas.Single<GH_PlateProperty>(propertyA).Value; var b = Canvas.Single<GH_PlateProperty>(propertyB).Value;
            string Print(object value) => ModelValues.Fingerprint(new[] { value });
            // A coordinate system bit for bit: origin, axes, name, and what its cached components give (ToGlobal/ToLocal of a point)
            string Bits(double v) => BitConverter.DoubleToInt64Bits(v).ToString("X16");
            string Xyz(double x, double y, double z) => Bits(x) + Bits(y) + Bits(z);
            string Frame(G.CoordinateSystem cs)
            {
                var g = cs.ToGlobal(new G.Point3d(1.5, -2.5, 3.25)); var l = cs.ToLocal(new G.Point3d(1.5, -2.5, 3.25));
                return cs.Name + "|" + Xyz(cs.Origin.X, cs.Origin.Y, cs.Origin.Z) + Xyz(cs.V1.X, cs.V1.Y, cs.V1.Z) + Xyz(cs.V2.X, cs.V2.Y, cs.V2.Z) + Xyz(cs.V3.X, cs.V3.Y, cs.V3.Z)
                    + Xyz(g.X, g.Y, g.Z) + Xyz(l.X, l.Y, l.Z);
            }
            IEnumerable<G.CoordinateSystem> Frames(Element e) => new[] { e.CoordinateSystem }
                .Concat(e is NodeElement n ? n.Assignments.Restrains.Select(r => r.Restrain?.CoordinateSystem) : Enumerable.Empty<G.CoordinateSystem>())
                .Concat(e.Loads.Values.Select(x => x.CoordinateSystem)).Where(cs => cs != null);
            Element[] Elements() => Canvas.Out(beams).Concat(Canvas.Out(fd)).Select(g => (Element)((GH_Beam)g).Value)
                .Concat(Canvas.Out(supports).Select(g => (Element)((GH_Node)g).Value)).Concat(plates.Select(p => (Element)Canvas.Single<GH_Plate>(p).Value)).ToArray();
            string[] Upstream() => new object[] { property, section, steel, fabric, a, b }.Select(Print)
                .Concat(Canvas.Out(beams).Concat(Canvas.Out(fd)).Select(g => Print(((GH_Beam)g).Value)))
                .Concat(plates.Select(p => Print(Canvas.Single<GH_Plate>(p).Value)))
                .Concat(Elements().SelectMany(Frames).Select(Frame))
                .Concat(new[] { Canvas.Single<GH_Model>(build).Value.AnalysisFingerprint(), Canvas.Single<GH_Model>(buildPlates).Value.AnalysisFingerprint() }).ToArray();
            var before = Upstream();
            var restrained = Canvas.Out(supports).Cast<GH_Node>().ToArray();
            Check(restrained.Length > 0 && restrained.All(x => x.Value.Assignments.Restrains.Single().Restrain.CoordinateSystem.V1.Z != 0), "Restraints in the axes of the plane");

            // Shared instances
            var fdBeams = Canvas.Out(fd).Cast<GH_Beam>().ToArray();
            Check(Canvas.Out(beams).Cast<GH_Beam>().Concat(fdBeams).All(x => ReferenceEquals(x.Value.BeamProperty, property)) && ReferenceEquals(Canvas.Single<GH_BeamProperty>(chain.Property).Duplicate() is GH_BeamProperty d ? d.Value : null, property),
                "Beam, Force Density and the copies of the property share the property of Beam Property");
            var model = Canvas.Single<GH_Model>(build).Value;
            Check(ReferenceEquals(model.BeamProperties["Cable"], property) && model.BeamElements.Values.All(x => ReferenceEquals(x.BeamProperty, property)), "Build Model (N-mm) registers and cites the shared property");
            var knm = Canvas.Single<GH_Model>(buildKnm).Value;
            var scaled = (GPC.Model.ElementProperties.ElasticBeamProperty)knm.BeamProperties["Cable"];
            Check(!ReferenceEquals(scaled, property) && ReferenceEquals(scaled.Material, steel) && !ReferenceEquals(scaled.Section, section) && knm.BeamElements.Values.All(x => ReferenceEquals(x.BeamProperty, scaled)),
                "Build Model (kN-m) cites a new, scaled property sharing the material");
            Check(Canvas.Out(decompose, 1).Cast<GH_Beam>().All(x => ReferenceEquals(x.Value.BeamProperty, property)), "Decompose shares the property");
            // Shared coordinate systems: the copies of the beams (Force Density), Decompose and the restraints of its nodes
            var beamOutputs = Canvas.Out(beams).Cast<GH_Beam>().ToDictionary(x => x.Guid);
            Check(fdBeams.All(x => ReferenceEquals(x.Value.CoordinateSystem, beamOutputs[x.Guid].Value.CoordinateSystem)), "Force Density shares the coordinate systems of the beams");
            var modelBeams = model.BeamElements.Values.ToDictionary(x => x.Guid); var modelNodes = model.NodesElements.Values.ToDictionary(x => x.Guid);
            Check(Canvas.Out(decompose, 1).Cast<GH_Beam>().All(x => ReferenceEquals(x.Value.CoordinateSystem, modelBeams[x.Guid].CoordinateSystem))
                && Canvas.Out(decompose, 0).Cast<GH_Node>().Where(x => x.Value.Assignments.Restrains.Count != 0)
                    .All(x => ReferenceEquals(x.Value.Assignments.Restrains[0].Restrain.CoordinateSystem, modelNodes[x.Guid].Assignments.Restrains[0].Restrain.CoordinateSystem)),
                "Decompose shares the coordinate systems of the elements and of the restraints");
            var output = Canvas.Single<GH_Model>(run).Value;
            Check(output.BeamElements.Values.All(x => ReferenceEquals(x.BeamProperty, property)) && output.AreaElements.Count > 0 && output.AreaElements.Values.All(x => ReferenceEquals(x.PlateProperty, a)),
                "The form-found model shares the beam property and gives the plate property of the options to the generated plates");
            // Plate Property: a new property (name and identity) from the membrane, which stays as it was
            var plateModel = Canvas.Single<GH_Model>(buildPlates).Value;
            Check(a.Guid != b.Guid && a.Guid != fabric.Guid && b.Guid != fabric.Guid && fabric.Name == "PTFE", "Plate Property gives each name its own property");
            Check(plateModel.PlateProperties.Count == 2 && plateModel.AreaElements.Values.Select(x => x.PlateProperty.Name).OrderBy(n => n).SequenceEqual(new[] { "A", "B" }),
                "Two plate properties of one membrane keep their names in Build Model");

            // Copies and edits downstream, as a component makes them: the copy of the model is a real copy, a changed property is new
            var copy = (GH_Model)Canvas.Single<GH_Model>(build).Duplicate();
            Check(!ReferenceEquals(copy.Value, model) && copy.Value.AllElements.Zip(model.AllElements, (x, y) => !ReferenceEquals(x, y) && x.Guid == y.Guid).All(z => z)
                && ReferenceEquals(copy.Value.BeamProperties["Cable"], property) && copy.Value.BeamElements.Values.All(x => ReferenceEquals(x.BeamProperty, property)), "Duplicate: a real copy of the model sharing its properties");
            Check(copy.Value.AllElements.Zip(model.AllElements, (x, y) => ReferenceEquals(x.CoordinateSystem, y.CoordinateSystem)).All(z => z), "Duplicate shares the coordinate systems");
            foreach (var node in copy.Value.NodesElements.Values) node.Position = new G.Point3d(node.Position.X, node.Position.Y, node.Position.Z + 1);
            foreach (var beam in copy.Value.BeamElements.Values) beam.Attributes.Clear();
            // a changed coordinate system is a new one: the beam axes turned, the restraints in other axes
            foreach (var beam in copy.Value.BeamElements.Values) beam.CoordinateSystem = new G.CoordinateSystem(beam.StartPoint, new G.Vector3d(0, 0, 1), new G.Vector3d(1, 0, 0));
            foreach (var node in copy.Value.NodesElements.Values)
                foreach (var r in node.Assignments.Restrains)
                    r.Restrain = new GPC.Model.Restraints.NodeRestrain(node, new G.CoordinateSystem(node.Position, new G.Vector3d(0, 1, 0), new G.Vector3d(0, 0, 1)), r.Restrain.Restrains.ToList());
            var stiffer = ModelValues.CopyValue(property); stiffer.ShearArea1 = 1;
            copy.Value.BeamElements.Values.First().BeamProperty = stiffer;
            var beamCopy = (GH_Beam)fdBeams[0].Duplicate();
            Check(ReferenceEquals(beamCopy.Value.BeamProperty, property) && !ReferenceEquals(beamCopy.Value, fdBeams[0].Value), "Duplicate of a beam: a copy sharing the property");
            beamCopy.Value.Attributes.Clear(); beamCopy.Value.StartPoint = new G.Point3d(-1, -1, -1);
            // the downstream components again (Build Model, Decompose, Form Find)
            foreach (var component in new[] { build, buildKnm, buildPlates }) component.ExpireSolution(false);
            canvas.Solve();
            Check(!Canvas.HasErrors(run) && Canvas.Single<GH_Model>(run).Value.AllElements.Any(e => e.Results.Count > 0), "Form Find again");
            var after = Upstream();
            Check(before.SequenceEqual(after), "The data upstream are unchanged after the edits downstream: " + string.Join(", ", Enumerable.Range(0, before.Length).Where(i => before[i] != after[i])));
            Check(property.ShearArea1 == 0 && ((GPC.Model.Sections.Section)section).Area == Math.PI * 25 && steel.ElasticModulusTension == 210000, "The shared values are those of their components");
        }
    }

    // Use a fresh populated instance for every failure; testing an empty goo hides stale-value regressions.
    private static void FailedReadsClearPopulatedValues()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas);
            var beams = FormFindingChecks.Beams(chain, new[] { new R.Line(R.Point3d.Origin, new R.Point3d(1000, 0, 0)) });
            var build = FormFindingChecks.Build(chain, beams);
            canvas.Solve();
            var steel = new GPC.Model.Materials.Material("S355", 210000, 0.3, 7.85e-9, 1.2e-5);
            var plateProperty = new GH_PlateProperty(new GPC.Model.ElementProperties.ElasticPlateProperty("Plate", steel, 1, 1));
            var plate = new GH_Plate(new[] { R.Point3d.Origin, new R.Point3d(1000, 0, 0), new R.Point3d(0, 1000, 0) }, plateProperty, null, Guid.NewGuid());

            void Verify<T>(T source, string key, Func<T, bool> cleared) where T : Grasshopper.Kernel.Types.IGH_Goo
            {
                var valid = new GH_LooseChunk("valid");
                Check(source.IsValid && source.Write(valid), typeof(T).Name + " populated fixture");
                byte[] payload = valid.GetByteArray(key);
                var unknown = (byte[])payload.Clone(); unknown[7] = 255;
                var version1 = (byte[])payload.Clone(); version1[7] = 1;
                var cases = new[]
                {
                    (name: "invalid header", data: new byte[] { 42, 43, 44 }, field: key),
                    (name: "truncated header", data: payload.Take(8).ToArray(), field: key),
                    (name: "truncated body", data: payload.Take(10).ToArray(), field: key),
                    (name: "unknown version", data: unknown, field: key),
                    (name: "version 1", data: version1, field: key),
                    (name: "NRBF", data: new byte[] { 0, 1, 0, 0, 0, 0xFF, 0xFF, 0xFF, 0xFF, 1, 0, 0, 0, 0, 0, 0, 0 }, field: "Data"),
                    (name: "ARCGPC2", data: System.Text.Encoding.ASCII.GetBytes("ARCGPC2").Concat(new byte[] { 1, 2, 3 }).ToArray(), field: "Data")
                };
                foreach (var item in cases)
                {
                    var target = (T)source.Duplicate();
                    Check(target.IsValid, typeof(T).Name + " starts populated: " + item.name);
                    var chunk = new GH_LooseChunk("invalid"); chunk.SetByteArray(item.field, item.data);
                    // Both old and incoming units are nondefault: failure must reset them, not retain either.
                    if (target is GH_Model model) { model.Units = ModelUnits.Knm; chunk.SetInt32("Units", (int)ModelUnits.SI); }
                    var reader = new GH_LooseChunk("invalid"); reader.Deserialize_Binary(chunk.Serialize_Binary());
                    Check(!target.Read(reader), typeof(T).Name + " rejects " + item.name);
                    Check(cleared(target) && !target.IsValid, typeof(T).Name + " clears its previous value after " + item.name);
                    // A failed read must not prevent a later valid read on the same instance.
                    Check(target.Read(valid) && target.IsValid, typeof(T).Name + " recovers after " + item.name);
                }
            }

            Verify(new GH_Node(R.Point3d.Origin, Guid.NewGuid()), "Data", x => x.Value == null);
            Verify(Canvas.Single<GH_Model>(build), "Model", x => x.Value == null && x.Units == ModelUnits.Nmm);
            Verify(new GH_Material(steel), "Data", x => x.Value == null);
            Verify(plateProperty, "Data", x => x.Value == null);
            Verify(Canvas.Single<GH_Beam>(beams), "Data", x => x.Value == null);
            Verify(plate, "Data", x => x.Value == null);
            Verify(new GH_SolverOptions(new FormFindingOptions()), "Settings", x => x.Value == null);

            var invalidUnits = new GH_LooseChunk("model");
            invalidUnits.SetByteArray("Model", ContractArchive.Model(Canvas.Single<GH_Model>(build).Value));
            invalidUnits.SetInt32("Units", int.MaxValue);
            var populated = (GH_Model)Canvas.Single<GH_Model>(build).Duplicate(); populated.Units = ModelUnits.Knm;
            Check(!populated.Read(invalidUnits) && populated.Value == null && populated.Units == ModelUnits.Nmm,
                "Invalid units clear a populated model and reset units even when model bytes are valid");
        }
    }

    private static void UnitBoundary()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas, "FF", 0.01);
            var beams = FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, new[] { new R.Line(R.Point3d.Origin, new R.Point3d(2, 0, 0)) }), 0.5);
            var load = FormFindingChecks.NodeLoad(chain, FormFindingChecks.Nodes(chain, new[] { new R.Point3d(2, 0, 0) }), new R.Vector3d(0, 0, -3));
            var build = FormFindingChecks.Build(chain, ModelUnits.Knm, beams, load);
            canvas.Solve();
            var model = Canvas.Single<GH_Model>(build);
            var end = model.Value.NodesElements.Values.Single(n => n.Position.X != 0);
            Check(end.Position.X == 2000 && model.Units == ModelUnits.Knm, "Build Model converts the positions to mm");
            Check(end.Loads.Values.OfType<PointLoad>().Single().F3 == -3000, "kN to N");
            var goal = ElementDrafts.Goals(model.Value.BeamElements.Values.Single()).Single();
            Check(goal.Goal.Target == 0.5, "kN/m = N/mm");
            var section = ((GPC.Model.ElementProperties.ElasticBeamProperty)model.Value.BeamElements.Values.Single().BeamProperty).Section as GPC.Model.Sections.Section;
            Check(Math.Abs(section.Area - Math.PI * 25) < 1e-9, "Section area in mm2 (10 mm diameter)");
        }
    }

    private static void DecomposeAndRebuild()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas); var grid = FormFindingChecks.SaddleGrid(2, 4000, 1000);
            var build = FormFindingChecks.Build(chain, ModelUnits.SI, FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, grid.lines), 1), FormFindingChecks.Supports(chain, grid.border));
            var decompose = canvas.Add(new Arachne.Grasshopper.Components.Models.DecomposeModelComponent()); Canvas.Link(decompose, 0, build);
            var rebuild = canvas.Add(new Arachne.Grasshopper.Components.Models.BuildModelComponent());
            foreach (int i in new[] { 0, 1, 2 }) Canvas.Link(rebuild, 0, decompose, i);
            rebuild.Params.Input[0].DataMapping = Grasshopper.Kernel.GH_DataMapping.Flatten; Canvas.Set(rebuild, 2, new Grasshopper.Kernel.Types.GH_Integer((int)ModelUnits.SI));
            canvas.Solve();
            var first = Canvas.Single<GH_Model>(build).Value; var second = Canvas.Single<GH_Model>(rebuild).Value;
            Check(first.NodesElements.Values.Select(n => n.Guid).SequenceEqual(second.NodesElements.Values.Select(n => n.Guid)), "Identities kept by Decompose and Build Model");
            Check(first.NodesElements.Values.Zip(second.NodesElements.Values, (a, b) => a.Position.X == b.Position.X && a.Position.Z == b.Position.Z).All(x => x), "Positions kept through the document units");
        }
    }

    private static void PreflightDiagnostics()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas);
            var ends = new[] { new R.Point3d(0, 0, 0), new R.Point3d(2000, 0, 0) };
            var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, new[] { new R.Line(ends[0], ends[1]) }), 1), FormFindingChecks.Supports(chain, ends));
            canvas.Solve();
            var model = Canvas.Single<GH_Model>(build);
            var input = FormFindingInputBuilder.Build(model.Value, model.Units, 0, new FormFindingOptions(), R.Plane.WorldXY);
            // A dangling objective (its beam is not in the graph) is found before the solver
            input.Model.FormFinding.Goals.Add(new FormFindingGoal { BeamGuid = Guid.NewGuid(), Kind = FormFindingGoalKind.ForceDensity, Target = 1, Weight = 0 });
            bool stopped = false;
            try { TensileModelAdapter.Export(input.Model, input.Units, input); } catch (InvalidOperationException e) { stopped = e.Message.Contains("InvalidFormFinding"); }
            Check(stopped, "Preflight error stops the export with its code");
            var linear = FormFindingInputBuilder.Build(model.Value, model.Units, 0, new FormFindingOptions(), R.Plane.WorldXY);
            Check(TensileModelAdapter.Export(linear.Model, linear.Units, linear).Diagnostics.All(d => d.Severity != GPC.Model.Core.Diagnostics.DiagnosticSeverity.Error), "Valid model: no preflight error");
        }
    }

    private static void DocumentSidecars()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arachne-f6-document-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            using (var canvas = new Canvas())
            {
                var chain = FormFindingChecks.Start(canvas);
                var ends = new[] { new R.Point3d(-100, 0, 0), new R.Point3d(100, 0, 0) };
                var beams = FormFindingChecks.Beams(chain, new[] { new R.Line(ends[0], R.Point3d.Origin), new R.Line(R.Point3d.Origin, ends[1]) });
                var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, beams, 1), FormFindingChecks.Supports(chain, ends));
                var solve = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0);
                canvas.Solve();
                var expected = solve.ReadAnalysis(0); var fingerprint = expected.Model.AnalysisFingerprint();
                string first = System.IO.Path.Combine(directory, "first.ghx");
                var io = new Grasshopper.Kernel.GH_DocumentIO { Document = canvas.Document };
                Check(io.SaveQuiet(first), "Save GH document with sidecar");
                var reference = solve.AnalysisReferences[0]; string sidecar = reference.ResolvePath(first);
                Check(System.IO.File.Exists(sidecar) && reference.ArchiveFile.Contains(solve.InstanceGuid.ToString("N")), "Each component owns a distinct sidecar");
                byte[] saved = System.IO.File.ReadAllBytes(sidecar); var stamp = System.IO.File.GetLastWriteTimeUtc(sidecar);
                Check(io.SaveQuiet(first) && saved.SequenceEqual(System.IO.File.ReadAllBytes(sidecar)) && stamp == System.IO.File.GetLastWriteTimeUtc(sidecar), "Unchanged GH save leaves sidecar bytes and timestamp unchanged");
                string second = System.IO.Path.Combine(directory, "second.gh");
                Check(io.SaveQuiet(second), "GH binary Save As");
                Check(solve.AnalysisReferences[0].ResolvePath(second) != sidecar && System.IO.File.Exists(sidecar), "Save As rebases references and preserves the original sidecar");
                var reopened = new Grasshopper.Kernel.GH_DocumentIO(); Check(reopened.Open(second), "Open saved GH document");
                using (reopened.Document)
                {
                    var component = reopened.Document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                    var actual = component.ReadAnalysis(0);
                    Check(actual.Model.AnalysisFingerprint() == fingerprint && actual.Model.AllElements.Any(e => e.Results.Count > 0), "GH reopen reads the saved GPC analysis without solving");
                    Check(actual.SolverMap != null, "Solver identities survive GH reopen");
                    reopened.Document.Enabled = true; reopened.Document.NewSolution(false);
                    Check(Canvas.Out(component).Count == 1, "Reopened auto-run document produces output");
                    Check(component.AnalysisReferences.Count == 1 && component.ReadAnalysis(0).Model.AnalysisFingerprint() == fingerprint,
                        "Recalculation after reopen replaces the saved iteration without duplicating slots");
                }
                var clipboard = new GH_IO.Serialization.GH_LooseChunk("clipboard"); Check(solve.Write(clipboard), "Write portable clipboard/undo archive");
                var bytes = clipboard.Serialize_Binary(); var reader = new GH_IO.Serialization.GH_LooseChunk("clipboard"); reader.Deserialize_Binary(bytes);
                var copied = new Arachne.Grasshopper.Components.Analysis.FormFindComponent(); copied.CreateAttributes();
                Check(copied.Read(reader) && copied.ReadAnalysis(0).Model.AnalysisFingerprint() == fingerprint, "Clipboard carries an independent GPC analysis");
                // Moving both files preserves the relative reference. A missing sidecar must not run a solver.
                string moved = System.IO.Path.Combine(directory, "moved"); System.IO.Directory.CreateDirectory(moved);
                string movedDocument = System.IO.Path.Combine(moved, "renamed.gh"); System.IO.File.Copy(second, movedDocument);
                var secondRef = solve.AnalysisReferences[0]; System.IO.File.Copy(secondRef.ResolvePath(second), secondRef.ResolvePath(movedDocument));
                var movedIo = new Grasshopper.Kernel.GH_DocumentIO(); Check(movedIo.Open(movedDocument), "Open moved document");
                using (movedIo.Document)
                {
                    var component = movedIo.Document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                    Check(component.ReadAnalysis(0).Model.AnalysisFingerprint() == fingerprint, "Relative sidecar survives moving the project");
                }
                System.IO.File.Delete(secondRef.ResolvePath(movedDocument));
                var missingIo = new Grasshopper.Kernel.GH_DocumentIO(); Check(missingIo.Open(movedDocument), "Definition opens without its results file");
                using (missingIo.Document)
                {
                    var component = missingIo.Document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                    bool rejected = false; try { component.ReadAnalysis(0); } catch (System.IO.InvalidDataException) { rejected = true; }
                    Check(rejected, "Missing numerical blocks are diagnosed explicitly");
                }
                // Start from independent saved definitions: neither case may benefit from cached numerical blocks.
                foreach (bool corrupt in new[] { false, true })
                {
                    string caseName = corrupt ? "corrupt" : "missing";
                    string caseDirectory = System.IO.Path.Combine(directory, caseName);
                    System.IO.Directory.CreateDirectory(caseDirectory);
                    string damagedDocument = System.IO.Path.Combine(caseDirectory, "definition.gh");
                    System.IO.File.Copy(second, damagedDocument);
                    string damagedSidecar = secondRef.ResolvePath(damagedDocument);
                    if (corrupt) System.IO.File.WriteAllBytes(damagedSidecar, new byte[] { 42, 0, 13, 10 });

                    // Keep the archive's file location and inspect writer messages from the actual definition save.
                    var archive = new GH_Archive();
                    Check(archive.ReadFromFile(damagedDocument), caseName + ": read definition archive");
                    using (var document = new Grasshopper.Kernel.GH_Document { Enabled = false })
                    {
                        Check(archive.ExtractObject(document, "Definition"), caseName + ": open without numerical blocks");
                        var component = document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                        Check(component.AnalysisReferences.Count == 1 && component.AnalysisReferences[0] != null,
                            caseName + ": saved reference exists before save validation");
                        int objects = document.ObjectCount;
                        archive.GetRootNode.RemoveChunk("Definition");
                        archive.ClearMessages();
                        // Do not call ReadAnalysis or NewSolution: Write itself must detect the unavailable blocks.
                        Check(archive.AppendObject(document, "Definition"), caseName + ": serialize definition without rerunning");
                        Check(archive.Messages.Any(m => m.Type == GH_Message_Type.warning
                            && m.Message.IndexOf("run", StringComparison.OrdinalIgnoreCase) >= 0
                            && (m.Message.IndexOf("not saved", StringComparison.OrdinalIgnoreCase) >= 0
                                || m.Message.IndexOf("discard", StringComparison.OrdinalIgnoreCase) >= 0)),
                            caseName + ": explicit warning that the saved run was discarded; references="
                                + string.Join(",", component.AnalysisReferences.Select(r => r == null ? "null" : "present"))
                                + "; messages=" + string.Join(" | ", archive.Messages.Select(m => m.Type + ": " + m.Message)));
                        Check(!archive.Messages.Any(m => m.Type == GH_Message_Type.error), caseName + ": no fatal write error");
                        Check(component.AnalysisReferences.All(r => r == null), caseName + ": unavailable run discarded in memory");
                        Check(archive.WriteToFile(damagedDocument, true, false), caseName + ": definition save succeeds");

                        var restoredArchive = new GH_Archive();
                        Check(restoredArchive.ReadFromFile(damagedDocument), caseName + ": reread saved definition");
                        using (var restoredDocument = new Grasshopper.Kernel.GH_Document { Enabled = false })
                        {
                            Check(restoredArchive.ExtractObject(restoredDocument, "Definition"), caseName + ": reopen without solving");
                            Check(restoredDocument.ObjectCount == objects, caseName + ": definition objects retained");
                            var restored = restoredDocument.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                            Check(restored.AnalysisReferences.Count == 0, caseName + ": reopened definition has no saved analysis");
                            bool absent = false;
                            try { restored.ReadAnalysis(0); }
                            catch (InvalidOperationException e) { absent = e.Message == "NoSavedFormFindingAnalysis"; }
                            Check(absent, caseName + ": discarded run cannot be reopened");
                        }
                    }
                }
            }
        }
        finally { System.IO.Directory.Delete(directory, true); }
    }

    private static void SaveImmediatelyAfterSolve()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arachne-f6-immediate-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            using (var canvas = new Canvas())
            {
                var chain = FormFindingChecks.Start(canvas); var grid = FormFindingChecks.SaddleGrid(2, 400, 100);
                var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, grid.lines), 1),
                    FormFindingChecks.Supports(chain, grid.border));
                var solve = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0);
                string path = System.IO.Path.Combine(directory, "immediate.gh");
                var io = new Grasshopper.Kernel.GH_DocumentIO { Document = canvas.Document };
                canvas.Solve();
                // This must remain the first operation after Solve: no reference accessor or ReadAnalysis wait.
                Check(io.SaveQuiet(path), "Save immediately after Solve waits for background preparation");
                Check(!Canvas.HasErrors(solve) && Canvas.Out(solve).Count == 1, "Immediate-save fixture solved successfully");
                var expected = solve.ReadAnalysis(0);
                var reference = solve.AnalysisReferences[0];
                Check(reference != null && System.IO.File.Exists(reference.ResolvePath(path)), "Immediate save wrote its sidecar");
                var archive = new GH_Archive(); Check(archive.ReadFromFile(path), "Read immediate save");
                using (var document = new Grasshopper.Kernel.GH_Document { Enabled = false })
                {
                    Check(archive.ExtractObject(document, "Definition"), "Open immediate save without solving");
                    var restored = document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                    Check(restored.AnalysisReferences.Count == 1 && restored.AnalysisReferences[0] != null, "Immediate save retained its run");
                    var actual = restored.ReadAnalysis(0);
                    Check(actual.Model.AnalysisFingerprint() == expected.Model.AnalysisFingerprint()
                        && actual.SolverMap != null && actual.Model.AllElements.Any(e => e.Results.Count > 0),
                        "Immediate save restores the analysis, solver map and results without rerunning");
                }
            }
        }
        finally { System.IO.Directory.Delete(directory, true); }
    }

    private static void BackgroundAnalysisAccessRejected()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas); var grid = FormFindingChecks.SaddleGrid(2, 400, 100);
            var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, grid.lines), 1),
                FormFindingChecks.Supports(chain, grid.border));
            var solve = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0);
            canvas.Solve();
            // Confirm a valid run on the owner thread, so "no saved analysis" cannot satisfy this test.
            var expected = solve.ReadAnalysis(0).Model.AnalysisFingerprint();
            Exception failure = null;
            var worker = new System.Threading.Thread(() =>
            {
                try { solve.ReadAnalysis(0); }
                catch (Exception e) { failure = e; }
            }) { IsBackground = true };
            worker.Start();
            Check(worker.Join(TimeSpan.FromSeconds(10)), "Background ReadAnalysis rejects promptly without deadlocking the owner thread");
            Check(failure is InvalidOperationException && failure.Message != "NoSavedFormFindingAnalysis",
                "Background ReadAnalysis rejects access to an existing run with InvalidOperationException");
            Check(solve.ReadAnalysis(0).Model.AnalysisFingerprint() == expected, "Rejected background access leaves the owner-thread run intact");
        }
    }

    private static void CrossRuntimeCircularOutlines()
    {
        // Independent baselines: SHA-256 of little-endian binary64 X,Y coordinates in contour order,
        // generated by the pinned GPC Polygon2d(diameter, 64, center) on .NET Framework 4.8 x64.
        // Include the concentric hole after the outside, when present. Do not regenerate per runtime:
        // same-runtime archive round-trips cannot detect the Math.Sin/Cos differences that broke saved examples.
        var fixtures = new[]
        {
            (d: .01, t: 0.0, hash: "B3C8CEE9DA080B22C363E6EF7D339FFB2C75859635AE1DADF5927F289C6B0A70"),
            (d: .01, t: .001, hash: "98940FF3CABFA5B85B704D822DAFD47253D96AEFD345EFE7E5DC3245EF0631AE"),
            (d: 10.0, t: 0.0, hash: "A6830E0BC5AC61C83882B980F604956A3C25320FAB811F1542B6448DC81DB502"),
            (d: 10.0, t: 1.0, hash: "C498B8DF7F58FCFD0CE02BE2269BD1678D8E8706E34D4B3B3CDC4FD9A1944D35"),
            (d: 42.5, t: 0.0, hash: "B786D8BB36E9C7C0022DB309063124A8F2F929BACFA9C508C948DF4574EC47C3"),
            (d: 42.5, t: 4.25, hash: "69C8319A5E1296F634392FC27D445E0D7D3210C87A2F027C546B1C7BF092EA3F"),
            (d: 10000.0, t: 0.0, hash: "874D05AF381A337F5DA404117E68C9FBB0DAAD47107B9D9B73A18092AFDE5F64"),
            (d: 10000.0, t: 1000.0, hash: "A0BB7C2C34E709147229944B4EE9F6DF6221712A3F744087E72A9F11513ADA4B")
        };
        GPC.Model.Sections.Section Section(double d, double t)
        {
            var calculator = new Arachne.Common.Geometry.SectionCalculator
            {
                Type = t == 0 ? Arachne.Common.Geometry.SectionCalculator.Kind.SolidCircle : Arachne.Common.Geometry.SectionCalculator.Kind.HollowCircle,
                D = d, T = t
            };
            calculator.Calculate(); return calculator.ToSection();
        }
        string OutlineHash(GPC.Model.Sections.Section section)
        {
            Check(section.Shape != null, "Circular section retains its outline");
            using (var stream = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(stream))
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                foreach (var polygon in new[] { section.Shape.Fill2d }.Concat(section.Shape.Holes2d ?? new G.Polygon2d[0]))
                {
                    Check(polygon.Count == 64, "Circular contour retains all 64 vertices");
                    foreach (var p in polygon.Points) { writer.Write(p.X); writer.Write(p.Y); }
                }
                writer.Flush();
                return BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", "");
            }
        }
        foreach (var fixture in fixtures)
        {
            var section = Section(fixture.d, fixture.t);
            Check(OutlineHash(section) == fixture.hash, "Cross-runtime outline bits: D=" + fixture.d + ", T=" + fixture.t);
            var restored = RoundTrip(new GH_BeamSection(section)).Value;
            Check(OutlineHash(restored) == fixture.hash && restored.Area == section.Area && restored.J11 == section.J11 && restored.J22 == section.J22,
                "Archive preserves exact circular outline and engineering properties");
        }
        var original = Section(10, 1);
        string fingerprint = ModelValues.Fingerprint(new[] { original });
        double nextDiameter = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(10.0) + 1);
        Check(ModelValues.Fingerprint(new[] { Section(nextDiameter, 1) }) != fingerprint, "One-ULP physical diameter change still changes the fingerprint");
        // Section/shape accessors return defensive copies: construct a section with the edited contour explicitly.
        var outline = original.Shape;
        var points = outline.Fill2d.Points;
        var vertex = points[21];
        vertex.Y = BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(vertex.Y) + 1);
        var edited = new GPC.Model.Sections.Section(new G.Shape2d(new G.Polygon2d(points), outline.Holes2d, outline.Childs2d),
            original.Area, original.Jxx, original.Jyy, original.Jxy, original.J11, original.J22, original.Jt,
            original.Centroid, original.AngleX1, original.Wel1, original.Wel2, original.WelX, original.WelY, original.Name);
        Check(OutlineHash(edited) != OutlineHash(original), "Contour perturbation actually reaches the reconstructed section");
        Check(ModelValues.Fingerprint(new[] { edited }) != fingerprint, "One-ULP contour edit is still included in the fingerprint");
        Check(OutlineHash(original) == fixtures[3].hash, "Editing a copied section leaves the source outline unchanged");
    }

    private static void StandardGeometrySavedOutputRestoration()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arachne-f6-standard-geometry-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            using (var canvas = new Canvas())
            {
                var chain = FormFindingChecks.Start(canvas);
                var grid = FormFindingChecks.SaddleGrid(4, 10000, 2000);
                var curves = canvas.Add(new Grasshopper.Kernel.Parameters.Param_Curve());
                curves.SetPersistentData(grid.lines.Select(l => new Grasshopper.Kernel.Types.GH_Curve(new R.LineCurve(l))));
                var points = canvas.Add(new Grasshopper.Kernel.Parameters.Param_Point());
                points.SetPersistentData(grid.border.Select(p => new Grasshopper.Kernel.Types.GH_Point(p)));
                var beams = canvas.Add(new Arachne.Grasshopper.Components.Models.BeamComponent());
                Canvas.Link(beams, 0, curves); Canvas.Link(beams, 1, chain.Property);
                var nodes = canvas.Add(new Arachne.Grasshopper.Components.Models.NodeComponent()); Canvas.Link(nodes, 0, points);
                var supports = canvas.Add(new Arachne.Grasshopper.Components.Attributes.NodeRestraintsComponent());
                Canvas.Link(supports, 0, nodes);
                Canvas.Set<Grasshopper.Kernel.Types.IGH_Goo>(supports, 1, new Grasshopper.Kernel.Types.GH_Boolean(true), new Grasshopper.Kernel.Types.GH_Boolean(true), new Grasshopper.Kernel.Types.GH_Boolean(true));
                var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, beams, 1), supports);
                var solve = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0);
                canvas.Solve();
                var source = Canvas.Single<GH_Model>(build);
                bool NegativeZero(double x) => BitConverter.DoubleToInt64Bits(x) == long.MinValue;
                Check(source.Value.NodesElements.Values.Any(n => NegativeZero(n.Position.Z)), "Standard geometry fixture contains negative zero coordinates");
                var sourceFingerprint = source.Value.AnalysisFingerprint();
                var options = Canvas.Single<GH_SolverOptions>(FormFindingChecks.Options(solve)).Value;
                var input = FormFindingInputBuilder.Build(source.Value, source.Units, 0, options, R.Plane.WorldXY);
                Check(input.Model.NodesElements.Values.All(n => !NegativeZero(n.Position.X) && !NegativeZero(n.Position.Y) && !NegativeZero(n.Position.Z)),
                    "Analysis input canonicalizes signed zero coordinates");
                var positive = (GH_Model)source.Duplicate();
                foreach (var node in positive.Value.NodesElements.Values)
                {
                    var p = node.Position; node.Position = new G.Point3d(p.X == 0 ? 0.0 : p.X, p.Y == 0 ? 0.0 : p.Y, p.Z == 0 ? 0.0 : p.Z);
                }
                string ExportFingerprint(GH_Model value)
                {
                    var prepared = FormFindingInputBuilder.Build(value.Value, value.Units, 0, options, R.Plane.WorldXY);
                    return TensileModelAdapter.Export(prepared.Model, prepared.Units, prepared).Map.AnalysisFingerprint;
                }
                string expectedFingerprint = ExportFingerprint(source);
                Check(ExportFingerprint(positive) == expectedFingerprint, "Signed zero alone does not change the solver input fingerprint");
                Check(source.Value.AnalysisFingerprint() == sourceFingerprint && source.Value.NodesElements.Values.Any(n => NegativeZero(n.Position.Z)),
                    "Analysis preparation preserves the source geometry and its exact fingerprint");
                var changed = (GH_Model)positive.Duplicate();
                var changedNode = changed.Value.NodesElements.Values.First(n => n.Position.Z == 0);
                var old = changedNode.Position; changedNode.Position = new G.Point3d(old.X, old.Y, 1e-9);
                Check(ExportFingerprint(changed) != expectedFingerprint, "A nonzero coordinate change is not rounded away by canonicalization");

                var expected = ModelView.Of(Canvas.Single<GH_Model>(solve));
                Canvas.Set(solve, 4, new Grasshopper.Kernel.Types.GH_Boolean(false));
                solve.ExpireSolution(false); canvas.Solve();
                Check(solve.Message == "Restored", "Standard geometry restores before saving with Auto Run disabled");
                var reference = solve.AnalysisReferences[0];
                foreach (string extension in new[] { ".gh", ".ghx" })
                {
                    string path = System.IO.Path.Combine(directory, "saddle" + extension);
                    Check(new Grasshopper.Kernel.GH_DocumentIO { Document = canvas.Document }.SaveQuiet(path), "Save standard geometry " + extension);
                    var archive = new GH_Archive(); Check(archive.ReadFromFile(path), "Read standard geometry " + extension);
                    using (var document = new Grasshopper.Kernel.GH_Document { Enabled = false })
                    {
                        Check(archive.ExtractObject(document, "Definition"), "Extract standard geometry " + extension);
                        var restored = document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                        document.Enabled = true; document.NewSolution(false);
                        Check(restored.Message == "Restored" && restored.AnalysisReferences[0].ManifestAddress == reference.ManifestAddress,
                            "Standard geometry restores the original run without solving " + extension + ": " + Canvas.Messages(restored));
                        var actual = ModelView.Of(Canvas.Single<GH_Model>(restored));
                        Check(actual.Nodes.Count == expected.Nodes.Count && actual.Beams.Count == expected.Beams.Count
                            && actual.Nodes.Zip(expected.Nodes, (a, b) => a.Position == b.Position && a.Reactions.SequenceEqual(b.Reactions)).All(x => x),
                            "Standard geometry restores exact positions and reactions " + extension);
                    }
                }
            }
        }
        finally { System.IO.Directory.Delete(directory, true); }
    }

    private static void SavedOutputRestoration()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arachne-f6-restore-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            using (var canvas = new Canvas())
            {
                var chain = FormFindingChecks.Start(canvas); var grid = FormFindingChecks.SaddleGrid(2, 400, 100);
                var beams = FormFindingChecks.Beams(chain, grid.lines);
                var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, beams, 1), FormFindingChecks.Supports(chain, grid.border));
                var solve = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0);
                var options = FormFindingChecks.Options(solve);
                Canvas.Set(options, 1, new Grasshopper.Kernel.Types.GH_Number(.0001)); Canvas.Link(options, 2, chain.LoadCase);
                canvas.Solve();
                var expected = ModelView.Of(Canvas.Single<GH_Model>(solve));
                var expectedMesh = Canvas.Single<Grasshopper.Kernel.Types.GH_Mesh>(solve, 1).Value;
                Check(expected.Nodes.Any(n => n.Loads.Any(l => l.IsPressure)), "Pressure fixture produces nodal forces");
                var reference = solve.AnalysisReferences[0];
                Canvas.Set(solve, 4, new Grasshopper.Kernel.Types.GH_Boolean(false));
                string path = System.IO.Path.Combine(directory, "pressure.gh");
                Check(new Grasshopper.Kernel.GH_DocumentIO { Document = canvas.Document }.SaveQuiet(path), "Save manual-run document");
                var io = new Grasshopper.Kernel.GH_DocumentIO(); Check(io.Open(path), "Reopen manual-run document");
                using (io.Document)
                {
                    var restored = io.Document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                    io.Document.Enabled = true; io.Document.NewSolution(false);
                    var actual = ModelView.Of(Canvas.Single<GH_Model>(restored));
                    Check(restored.Message == "Restored" && restored.AnalysisReferences[0].ManifestAddress == reference.ManifestAddress, "Outputs restored from the saved run with Auto Run disabled");
                    Check(actual.Nodes.Count == expected.Nodes.Count && actual.Beams.Count == expected.Beams.Count, "Saved output topology retained");
                    for (int i = 0; i < actual.Nodes.Count; i++)
                    {
                        Check(actual.Nodes[i].Position == expected.Nodes[i].Position, "Exact final solver coordinates retained");
                        var oldPressure = expected.Nodes[i].Loads.SingleOrDefault(l => l.IsPressure); var pressure = actual.Nodes[i].Loads.SingleOrDefault(l => l.IsPressure);
                        Check((pressure == null) == (oldPressure == null) && (pressure == null || pressure.Value == oldPressure.Value), "Saved pressure forces retained");
                        Check(actual.Nodes[i].Reactions.SequenceEqual(expected.Nodes[i].Reactions), "Saved reactions retained");
                    }
                    var mesh = Canvas.Single<Grasshopper.Kernel.Types.GH_Mesh>(restored, 1).Value;
                    Check(mesh.Vertices.ToPoint3dArray().SequenceEqual(expectedMesh.Vertices.ToPoint3dArray()) && mesh.Faces.Count == expectedMesh.Faces.Count, "Mesh restored");
                    var changed = FormFindingChecks.Options(restored);
                    Canvas.Set(changed, 1, new Grasshopper.Kernel.Types.GH_Number(.0002)); changed.ExpireSolution(false); io.Document.NewSolution(false);
                    Check(Canvas.Out(restored).Count == 0 && restored.Message == "Inputs changed", "Changed engineering input cannot receive stale output");
                    Check(restored.AnalysisReferences[0].ManifestAddress == reference.ManifestAddress, "Historical run remains available after input changes");
                    Canvas.Set(changed, 1, new Grasshopper.Kernel.Types.GH_Number(.0001)); changed.ExpireSolution(false); io.Document.NewSolution(false);
                    Check(Canvas.Out(restored).Count == 1 && restored.Message == "Restored", "Returning to the saved input restores output without a new run");
                }
            }
        }
        finally { System.IO.Directory.Delete(directory, true); }
    }

    /// <summary>Review A1: the export is linear. 10,000 nodes, 29,601 bars and 19,602 triangles; the time is compared with a quarter of
    /// the model (a quadratic export takes 16 times as long, a linear one about 4).</summary>
    private static void LinearExport()
    {
        double Seconds(int k, out int nodes, out int bars, out int triangles)
        {
            var model = MigrationScale.Grid(k, out nodes, out bars, out triangles);
            var input = FormFindingInputBuilder.Build(model, ModelUnits.Nmm, 0, new FormFindingOptions(), R.Plane.WorldXY);
            var watch = Stopwatch.StartNew();
            var exported = TensileModelAdapter.Export(input.Model, input.Units, input);
            watch.Stop();
            Check(exported.Solver.Plates.Count == triangles && exported.Solver.Beams.Count == bars, "Export entities");
            return watch.Elapsed.TotalSeconds;
        }
        Seconds(20, out _, out _, out _);
        double small = Seconds(50, out _, out _, out _), large = Seconds(100, out int n, out int b, out int t);
        Console.WriteLine($"F6 NOTE linear export: {n} nodes, {b} bars, {t} triangles in {large:F3} s (2,500 nodes: {small:F3} s)");
        Check(large < 8 * Math.Max(small, 0.05), $"Export not linear: {small:F3} s -> {large:F3} s");
    }
}
