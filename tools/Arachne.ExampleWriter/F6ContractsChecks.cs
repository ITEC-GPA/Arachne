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
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static int Run()
    {
        int failed = 0;
        foreach (var test in new Action[] { GhDataCarryGpcObjects, GhDataArchives, LegacyPayloadsRejected, UnitBoundary, DecomposeAndRebuild, PreflightDiagnostics,
            DocumentSidecars, SavedOutputRestoration, LinearExport, SharedProperties })
            try { test(); Console.WriteLine("F6 PASS " + test.Method.Name); }
            catch (Exception ex) { failed++; Console.WriteLine("F6 FAIL " + test.Method.Name + ": " + ex); }
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
            // Lane L5: written as version 2 (the compact GPC archive); version 1 (XML archive, deflated, F6 development builds) still read.
            var written = ContractArchive.Model(model.Value);
            Check(written[7] == 2 && System.Text.Encoding.ASCII.GetString(written, 9, 6) == "GPCMOD", "GH data are compact GPC archives (version 2)");
            byte[] Version1(byte kind, Action<System.IO.Stream> write)
            {
                using (var output = new System.IO.MemoryStream())
                {
                    output.Write(System.Text.Encoding.ASCII.GetBytes("ARCGPC3"), 0, 7); output.WriteByte(1); output.WriteByte(kind);
                    using (var deflate = new System.IO.Compression.DeflateStream(output, System.IO.Compression.CompressionLevel.Optimal, true)) write(deflate);
                    return output.ToArray();
                }
            }
            var oldModel = ContractArchive.ReadModel(Version1(2, s => GPC.Model.Persistence.ModelArchive.Save(model.Value, s)));
            Check(oldModel.AnalysisFingerprint() == model.Value.AnalysisFingerprint(), "Version 1 model archive still read");
            var oldMaterial = ContractArchive.ReadMember<GPC.Model.Materials.Material>(Version1(3, s => GPC.Model.Persistence.ModelValueArchive.Save(steel, s)));
            Check(oldMaterial.Name == "S355" && oldMaterial.ElasticModulusTension == 210000, "Version 1 material archive still read");
            var oldBeam = ContractArchive.ReadValue<BeamElement>(Version1(1, s => GPC.Model.Persistence.ModelValueArchive.Save(beam.Value, s)));
            Check(oldBeam.Guid == beam.Value.Guid && ElementDrafts.Goals(oldBeam).Single().Goal.Target == 1, "Version 1 beam archive still read");
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
