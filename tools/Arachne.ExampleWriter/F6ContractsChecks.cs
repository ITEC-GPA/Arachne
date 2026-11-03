using System;
using System.Linq;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Common.Analysis;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Models;
using GPC.Model.Restraints;
using GPC.Model.Structure.Assignments;
using G = GPC.Geometry;
using R = Rhino.Geometry;

internal static class F6ContractsChecks
{
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    internal static int Run()
    {
        int failed = 0;
        foreach (var test in new Action[] { ArchiveGraph, NodeAndLoadStorage, NodeConstraintStorage, BeamPointLoadStorage, GoalOwnership, WrapperPersistence, ProductionIdentity, ProductionUnits, IpsSelfWeight, DistributedLoadMoment, PersistencePathOrdering, DocumentSidecars, SavedOutputRestoration, SolverOptionsOwnership, InvalidArchives, FutureWrapperSchema, LegacyMigration, IndependentGpcSolver })
            try { test(); Console.WriteLine("F6 PASS " + test.Method.Name); }
            catch (Exception ex) { failed++; Console.WriteLine("F6 FAIL " + test.Method.Name + ": " + ex); }
        return failed;
    }
    private static void ArchiveGraph()
    {
        var load = new LoadCaseModel("Q", "Load");
        var a = new NodeModel("1", new R.Point3d(0, 0, 0));
        var b = new NodeModel("2", new R.Point3d(100, 0, 0));
        a.Loads.Add(new NodeForceLoadModel(load, new R.Vector3d(1, 2, 3)));
        b.Loads.Add(new NodeForceLoadModel(load, new R.Vector3d(4, 5, 6)));
        var model = new ModelModel(ModelModel.ModelUnits.Nmm);
        model.AddElements(new ElementModel[] { a, b, new BeamModel("B", a.Position, b.Position) });
        model.AddLoadCases(new[] { load });
        var copy = SerializationHelper.Clone(model);
        Check(copy.Nodes.Count == 2 && copy.Beams.Count == 1 && copy.GetNode("1") != null, "Indexes rebuilt");
        Check(ReferenceEquals(copy.Nodes[0].Loads[0].LoadCase, copy.Nodes[1].Loads[0].LoadCase), "Shared cases retained");
        Check(!ReferenceEquals(load, copy.Nodes[0].Loads[0].LoadCase), "Case detached");
        copy.Nodes[0].Loads[0].LoadCase.Name = "changed";
        copy.Nodes[0].Position = new R.Point3d(10, 20, 30);
        Check(load.Name == "Load" && a.Position == R.Point3d.Origin, "Branch mutation isolated");
    }
    private static T StoredInput<T>(System.Runtime.Serialization.ISerializable wrapper)
    {
        var data = new System.Runtime.Serialization.SerializationInfo(wrapper.GetType(), new System.Runtime.Serialization.FormatterConverter());
        wrapper.GetObjectData(data, new System.Runtime.Serialization.StreamingContext());
        return (T)data.GetValue("Input", typeof(T));
    }
    private static void NodeAndLoadStorage()
    {
        var node = new NodeModel("N", new R.Point3d(10, 20, -0.0));
        var identity = Guid.NewGuid(); node.BindInputIdentity(identity);
        var physical = StoredInput<NodeElement>(node);
        Check(physical.Guid == identity && physical.Name == "N" && physical.Position.Y == 20, "Node owns GPC input");
        node.Position = new R.Point3d(40, 50, -0.0);
        Check(physical.Position.X == 40, "Rhino coordinates are a view of the GPC node");
        var lc = new LoadCaseModel("12", "Wind");
        lc.BindInputIdentity(Guid.NewGuid());
        var force = new NodeForceLoadModel(lc, new R.Vector3d(1, -2, 3)); node.Loads.Add(force);
        var physicalForce = StoredInput<PointLoad>(force);
        var physicalCase = StoredInput<LoadCaseBase>(lc);
        Check(ReferenceEquals(physicalForce.LoadCase, physicalCase), "Load references the case input, not a second physical definition");
        force.Value = new R.Vector3d(5, 6, 7);
        Check(physicalForce.F1 == 5 && physicalForce.F3 == 7, "Force vector writes GPC components");
        var clone = new NodeModel(node);
        Check(clone.Identity == node.Identity && BitConverter.DoubleToInt64Bits(clone.Position.Z) == BitConverter.DoubleToInt64Bits(node.Position.Z), "Copy retains node identity and signed zero");
        ((NodeForceLoadModel)clone.Loads[0]).Value = R.Vector3d.Zero;
        clone.Loads[0].LoadCase.Name = "copy";
        Check(force.Value.X == 5 && lc.Name == "Wind", "Copied node owns force and case");
        var chunk = new GH_IO.Serialization.GH_LooseChunk("case");
        Check(new Arachne.Grasshopper.DataTypes.GH_LoadCase(lc).Write(chunk), "Write full load case");
        var read = new GH_IO.Serialization.GH_LooseChunk("case"); read.Deserialize_Binary(chunk.Serialize_Binary());
        var restored = new Arachne.Grasshopper.DataTypes.GH_LoadCase();
        Check(restored.Read(read) && restored.Value.Id == "12" && restored.Value.Identity == lc.Identity && restored.Value.Name == "Wind", "Case ID and identity survive internalization");
        var old = new GH_IO.Serialization.GH_LooseChunk("legacy"); old.SetString("name", "Old case");
        var migrated1 = new Arachne.Grasshopper.DataTypes.GH_LoadCase(); var migrated2 = new Arachne.Grasshopper.DataTypes.GH_LoadCase();
        Check(migrated1.Read(old) && migrated2.Read(old) && migrated1.Value.Identity == migrated2.Value.Identity, "Legacy case migration is deterministic");
        var assigned = StoredInput<NodeElement>(clone); assigned.Loads.Add(StoredInput<PointLoad>((NodeForceLoadModel)clone.Loads[0]));
        bool refused = false; try { clone.BindInputIdentity(Guid.NewGuid()); } catch (InvalidOperationException) { refused = true; }
        Check(refused && clone.Identity == identity, "Cannot reidentify a node carrying domain assignments");
    }
    private static void NodeConstraintStorage()
    {
        var axes = CoordinateSystemModel.FromPlane(new R.Plane(new R.Point3d(7, 8, 9), R.Vector3d.YAxis, -R.Vector3d.XAxis));
        var restraint = new NodeRestraintAttributeModel(true, false, true, axes);
        axes.Origin = R.Point3d.Origin;
        Check(restraint.Value.CoordinateSystem.Origin.X == 7, "Restraint owns its axes");
        var data = new System.Runtime.Serialization.SerializationInfo(restraint.GetType(), new System.Runtime.Serialization.FormatterConverter());
        restraint.GetObjectData(data, new System.Runtime.Serialization.StreamingContext());
        var dofs = (System.Collections.Generic.List<DofRestrain>)data.GetValue("Dofs", typeof(System.Collections.Generic.List<DofRestrain>));
        Check(dofs.Count == 3 && dofs[0].Dof == GeometryRestrain.DOF.DX && dofs[0].IsRestrained && !dofs[1].IsRestrained, "GPC translation restraints stored");
        restraint.Value.Ty = true;
        Check(dofs[1].IsRestrained, "Boolean controls view the GPC degrees of freedom");
        var copied = new NodeRestraintAttributeModel(restraint);
        copied.Value.Tx = false; copied.Value.CoordinateSystem.Origin = R.Point3d.Origin;
        Check(restraint.Value.Tx && restraint.Value.CoordinateSystem.Origin.X == 7, "Restraint copies are independent");
        var restored = SerializationHelper.Clone(restraint);
        Check(restored.Equals(restraint), "GPC restraint and local axes survive persistence");
        var weight = new NodeWeightAttributeModel(new R.Vector3d(2, 3, -0.0));
        weight.Goal.Weights.X = 4;
        Check(weight.Value.X == 4, "Coordinate controls view the GPC goal");
        var weightCopy = SerializationHelper.Clone(weight); weightCopy.Value = R.Vector3d.Zero;
        Check(weight.Value.X == 4 && BitConverter.DoubleToInt64Bits(weight.Value.Z) == long.MinValue, "Goal copy is detached and preserves negative zero");
        var node = new NodeModel("N", new R.Point3d(1, 2, 3)); node.Attributes.Add(restraint); node.Attributes.Add(weight);
        var model = new ModelModel(ModelModel.ModelUnits.Nmm); model.AddElements(new[] { node });
        var options = new SolverOptionsModel { CreateMesh = false };
        var input = FormFindingInputBuilder.Build(model, R.Plane.WorldXY, 2, options, model.InputOwner, model.InputIdentities);
        var physical = input.Model.NodesElements.Values.Single();
        Check(physical.Assignments.Restrains.Single().Restrain.Restrains.Count == 3 && input.Model.FormFinding.CoordinateGoals.Single().NodeGuid == physical.Guid, "Builder uses GPC restraints and binds coordinate goal identity");
        input.Model.FormFinding.CoordinateGoals.Single().Weights.X = 10;
        physical.Assignments.Restrains.Single().Restrain.Restrains[0].IsRestrained = false;
        Check(weight.Value.X == 4 && restraint.Value.Tx, "Assembled graph does not alias wrapper inputs");
        var linear = FormFindingInputBuilder.Build(model, R.Plane.WorldXY, 0, options, model.InputOwner, model.InputIdentities);
        Check(linear.Model.FormFinding.CoordinateGoals.Count == 0 && linear.Diagnostics.Any(d => d.Contains("Coordinate objectives")), "Inactive nonlinear goal is diagnosed for linear solver");
        // Exercise old schema constructors without rewriting the frozen NRBF fixtures.
        System.Runtime.Serialization.SerializationInfo Legacy(Type type, string key, int version)
        {
            var info = new System.Runtime.Serialization.SerializationInfo(type, new System.Runtime.Serialization.FormatterConverter());
            info.AddValue("AttributeModel_SERIALIZATION_RELEASE", 1); info.AddValue("AttributeModel<T>_SERIALIZATION_RELEASE", 1); info.AddValue(key, version); return info;
        }
        object Read(Type type, System.Runtime.Serialization.SerializationInfo info) => type.GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
            new[] { typeof(System.Runtime.Serialization.SerializationInfo), typeof(System.Runtime.Serialization.StreamingContext) }, null).Invoke(new object[] { info, new System.Runtime.Serialization.StreamingContext() });
        var oldRestraint = Legacy(typeof(NodeRestraintAttributeModel), "NodeRestraintAttributeModel_SERIALIZATION_RELEASE", 2);
        oldRestraint.AddValue("Tx", true); oldRestraint.AddValue("Ty", false); oldRestraint.AddValue("Tz", true); oldRestraint.AddValue("CoordinateSystem", axes, typeof(CoordinateSystemModel));
        var migrated = (NodeRestraintAttributeModel)Read(typeof(NodeRestraintAttributeModel), oldRestraint);
        Check(migrated.Value.Tx && !migrated.Value.Ty && migrated.Value.Tz, "Legacy restraint flags migrate to GPC degrees of freedom");
        var oldWeight = Legacy(typeof(NodeWeightAttributeModel), "NodeWeightAttributeModel_SERIALIZATION_RELEASE", 1); oldWeight.AddValue("Value", new R.Vector3d(3, 5, 7));
        Check(((NodeWeightAttributeModel)Read(typeof(NodeWeightAttributeModel), oldWeight)).Goal.Weights.Y == 5, "Legacy coordinate weights migrate to GPC goal");
    }
    private static void BeamPointLoadStorage()
    {
        var axes = CoordinateSystemModel.FromPlane(new R.Plane(R.Point3d.Origin, R.Vector3d.YAxis, -R.Vector3d.XAxis), "Rotated");
        var lc = new LoadCaseModel("P", "Point force");
        var original = new BeamPointLoadModel(lc, new BeamPointLoadModel.LoadData { CoordinateSystem = axes, P = new R.Vector3d(2, 3, 5), A = .25 }, BeamPointLoadModel.LoadType.Force);
        var physical = StoredInput<GPC.Model.Loads.Assignments.BeamLoadAssignment>(original);
        original.Value.P = new R.Vector3d(7, 11, 13); original.Value.A = .4;
        Check(physical.Concentrated.F2 == 11 && physical.Start == .4 && physical.End == .4, "Point components and station are GPC-backed");
        var restored = SerializationHelper.Clone(original);
        Check(restored.Value.CoordinateSystem.Equals(axes) && !restored.RequiresCoordinateSystemRecovery, "Point-load axes survive persistence");
        var copy = new BeamPointLoadModel(original); copy.PointLoadType = BeamPointLoadModel.LoadType.Moment; copy.LoadCase.Name = "copy";
        var moment = StoredInput<GPC.Model.Loads.Assignments.BeamLoadAssignment>(copy).Concentrated;
        Check(moment.M1 == 7 && moment.M2 == 11 && moment.F1 == 0 && original.PointLoadType == BeamPointLoadModel.LoadType.Force && lc.Name == "Point force", "Type changes move components and copy owns its case");
        Check(SerializationHelper.Clone(copy).Value.P.Z == 13, "Moment components persist without reinterpretation");
        var beam = new BeamModel("B", new R.Point3d(0, 0, 0), new R.Point3d(2, 0, 0))
        { BeamProperty = new BeamPropertyModel("P") { Material = new MaterialModel("M") { Density = 0 }, Section = new SectionModel("S") { SectionArea = .01 } } }; beam.Loads.Add(restored);
        var model = new ModelModel(ModelModel.ModelUnits.SI); model.AddElements(new[] { beam }); model.AddLoadCases(new[] { lc });
        var input = FormFindingInputBuilder.Build(model, R.Plane.WorldXY, 0, new SolverOptionsModel { CreateMesh = false }, model.InputOwner, model.InputIdentities);
        var actual = input.Model.BeamElements.Values.Single().Assignments.Loads.Single();
        Check(actual.Concentrated.F1 == -11 && actual.Concentrated.F2 == 7 && actual.Concentrated.F3 == 13 && actual.Start == .4, "Custom axes resolve to the independent analytical global vector");
        actual.Concentrated.F1 = 99;
        Check(restored.Value.P.X == 7 && physical.Concentrated.F1 == 7, "Assembly owns its point load");
        var info = new System.Runtime.Serialization.SerializationInfo(typeof(BeamPointLoadModel), new System.Runtime.Serialization.FormatterConverter());
        info.AddValue("LoadModel_SERIALIZATION_RELEASE", 1); info.AddValue("LoadModel<T>_SERIALIZATION_RELEASE", 1); info.AddValue("LoadCase", lc);
        info.AddValue("BeamPointLoadModel_SERIALIZATION_RELEASE", 1); info.AddValue("LoadType", BeamPointLoadModel.LoadType.Force); info.AddValue("Force", R.Vector3d.XAxis); info.AddValue("A", .5);
        var legacy = (BeamPointLoadModel)typeof(BeamPointLoadModel).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null,
            new[] { typeof(System.Runtime.Serialization.SerializationInfo), typeof(System.Runtime.Serialization.StreamingContext) }, null).Invoke(new object[] { info, new System.Runtime.Serialization.StreamingContext() });
        Check(legacy.RequiresCoordinateSystemRecovery && SerializationHelper.Clone(legacy).RequiresCoordinateSystemRecovery, "Missing legacy axes remain explicit through a resave");
        beam.Loads.Clear(); beam.Loads.Add(legacy);
        bool refused = false;
        try { FormFindingInputBuilder.Build(model, R.Plane.WorldXY, 0, new SolverOptionsModel { CreateMesh = false }, model.InputOwner, model.InputIdentities); }
        catch (InvalidOperationException e) { refused = e.Message.Contains("LegacyPointLoadCoordinateSystemWasNotSaved"); }
        Check(refused, "Ambiguous legacy load cannot silently change its force direction");
        legacy.Value.CoordinateSystem = CoordinateSystemModel.Global;
        Check(!legacy.RequiresCoordinateSystemRecovery, "Explicit axis assignment recovers the legacy load");
    }
    private static void GoalOwnership()
    {
        var lc = new LoadCaseModel("Q", "Pretension");
        var original = new ForceDensityModel(lc, 25, ForceDensityModel.ForceDensityTypes.Membrane, false, 3, 40, ForceDensityModel.MembraneTypes.Weft);
        var copy = new ForceDensityModel(original);
        Check(copy.Goal.Kind == FormFindingGoalKind.MembraneForcePerWidth && copy.Goal.Direction == FabricDirection.Weft, "GPC membrane goal retained");
        copy.Goal.Target = 31; copy.LoadCase.Name = "copy";
        Check(original.Value == 25 && lc.Name == "Pretension", "Goal and case independent");
        copy.ForceDensityTipe = ForceDensityModel.ForceDensityTypes.Curvature;
        Check(copy.Goal.ChordLengthMm == 40 && copy.Goal.Direction == FabricDirection.Unspecified, "Only active goal fields enter the domain");
        var restored = SerializationHelper.Clone(copy);
        restored.ForceDensityTipe = ForceDensityModel.ForceDensityTypes.Membrane;
        Check(restored.MembraneType == ForceDensityModel.MembraneTypes.Weft && !restored.Goal.ChordLengthMm.HasValue, "Inactive UI choices survive round trip");
        var node = new NodeModel("n", R.Point3d.Origin);
        node.Loads.Add(original);
        node.Attributes.Add(new NodeWeightAttributeModel(new R.Vector3d(1, 2, 3)));
        var branch = new NodeModel(node);
        ((ForceDensityModel)branch.Loads[0]).Value = 99;
        ((NodeWeightAttributeModel)branch.Attributes[0]).Value = new R.Vector3d(9, 9, 9);
        Check(original.Value == 25 && ((NodeWeightAttributeModel)node.Attributes[0]).Value.X == 1, "Component branch detached");
    }
    private static void WrapperPersistence()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas); canvas.Solve();
            var property = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_BeamProperty>(chain.Property);
            var propertyCopy = (Arachne.Grasshopper.DataTypes.GH_BeamProperty)property.Duplicate();
            propertyCopy.Value.Material.Density *= 2;
            Check(propertyCopy.Value.Material.Density == 2 * property.Value.Material.Density, "Property branch owns its material");
            var archive = new GH_IO.Serialization.GH_LooseChunk("F6");
            Check(property.Write(archive.CreateChunk("property")), "Write complete beam property");
            var read = new GH_IO.Serialization.GH_LooseChunk("F6"); read.Deserialize_Binary(archive.Serialize_Binary());
            var restored = new Arachne.Grasshopper.DataTypes.GH_BeamProperty();
            Check(restored.Read(read.FindChunk("property")), "Read complete beam property");
            Check(restored.Value.SectionArea == property.Value.SectionArea && restored.Value.Material.Density == property.Value.Material.Density, "Section and material survive internalization");
            var legacy = new GH_IO.Serialization.GH_LooseChunk("old"); legacy.SetString("Id", "P"); legacy.SetString("Name", "Incomplete");
            Check(!new Arachne.Grasshopper.DataTypes.GH_BeamProperty().Read(legacy), "Incomplete legacy property cannot silently acquire a default section");
            var plate = new Arachne.Grasshopper.DataTypes.GH_Plate(new PlateModel(new[] { R.Point3d.Origin, new R.Point3d(100, 0, 0), new R.Point3d(0, 100, 0) },
                new PlatePropertyModel("P") { Thickness = 1, Material = property.Value.Material }));
            var plateCopy = (Arachne.Grasshopper.DataTypes.GH_Plate)plate.DuplicateGeometry();
            plateCopy.Value.Points[0] = new R.Point3d(1, 1, 1); plateCopy.Value.PlateProperty.Thickness = 2;
            Check(plateCopy.Guid == plate.Guid && plate.Value.Points[0] == R.Point3d.Origin && plate.Value.PlateProperty.Thickness == 1, "Geometric copy retains identity and owns coordinates and property");
            var plateChunk = new GH_IO.Serialization.GH_LooseChunk("plate"); Check(plate.Write(plateChunk), "Write plate");
            var plateRead = new GH_IO.Serialization.GH_LooseChunk("plate"); plateRead.Deserialize_Binary(plateChunk.Serialize_Binary());
            var restoredPlate = new Arachne.Grasshopper.DataTypes.GH_Plate(); Check(restoredPlate.Read(plateRead), "Read plate");
            Check(restoredPlate.Guid == plate.Guid && restoredPlate.Value.Points.Count == 3 && restoredPlate.Value.PlateProperty.Thickness == 1, "Plate identity, topology and property survive round trip");
        }
    }
    private static void ProductionIdentity()
    {
        using (var canvas = new Canvas())
        {
            var chain = FormFindingChecks.Start(canvas);
            var endpoints = new[] { new R.Point3d(-100, 0, 0), new R.Point3d(100, 0, 0) };
            var beams = FormFindingChecks.Beams(chain, new[] { new R.Line(endpoints[0], R.Point3d.Origin), new R.Line(R.Point3d.Origin, endpoints[1]) });
            var build = FormFindingChecks.Build(chain, FormFindingChecks.ForceDensity(chain, beams, 1), FormFindingChecks.Supports(chain, endpoints));
            var solve = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0);
            canvas.Solve();
            var original = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(build);
            var ids = original.Guids.Values.ToArray();
            var owner = original.Value.InputOwner;
            var run = solve.ReadAnalysis(0);
            Check(run.Model.Guid == owner && run.Model.Analysis.InputFingerprint == run.Model.AnalysisFingerprint(), "Production analysis bound to actual input");
            Check(run.Model.AllElements.Any(e => e.Results.Count > 0), "Production GPC results attached");
            beams.ExpireSolution(false); build.ExpireSolution(false); solve.ExpireSolution(false); canvas.Solve();
            var recalculated = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(build);
            Check(owner == recalculated.Value.InputOwner && ids.OrderBy(x => x).SequenceEqual(recalculated.Guids.Values.OrderBy(x => x)), "Component identities stable on recomputation");
            var copy = (Arachne.Grasshopper.DataTypes.GH_Model)recalculated.Duplicate();
            Check(copy.Guids.Keys.All(k => copy.Value.Elements.Contains(k)) && copy.Guids.Keys.All(k => !recalculated.Value.Elements.Contains(k)), "Copied registry keys point to copied elements");
            var restored = SerializationHelper.Clone(recalculated.Value);
            Check(restored.InputOwner == owner && restored.InputIdentities.Count == ids.Length && restored.InputIdentities.Keys.All(k => restored.Elements.Contains(k)), "Persistent model and element identities");
        }
    }
    private static void ProductionUnits()
    {
        foreach (var units in new[] { ModelModel.ModelUnits.Nmm, ModelModel.ModelUnits.SI, ModelModel.ModelUnits.Knm })
        using (var canvas = new Canvas())
        {
            var scale = FormFindingUnits.From(units);
            var chain = FormFindingChecks.Start(canvas, diameter: 10 / scale.LengthToMm);
            var ends = new[] { new R.Point3d(-1000 / scale.LengthToMm, 0, 0), new R.Point3d(1000 / scale.LengthToMm, 0, 0) };
            var beams = FormFindingChecks.Beams(chain, new[] { new R.Line(ends[0], R.Point3d.Origin), new R.Line(R.Point3d.Origin, ends[1]) });
            var middle = FormFindingChecks.NodeLoad(chain, FormFindingChecks.Nodes(chain, new[] { R.Point3d.Origin }), new R.Vector3d(0, 0, -10 / scale.ForceToN));
            var build = FormFindingChecks.Build(chain, units, FormFindingChecks.ForceDensity(chain, beams, .1 / scale.ForceDensityToNPerMm), FormFindingChecks.Supports(chain, ends), middle);
            var solver = (Arachne.Grasshopper.Components.Analysis.FormFindComponent)FormFindingChecks.FormFind(chain, build, 0, gravity: 9806.65 / scale.LengthToMm);
            canvas.Solve();
            var original = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(build);
            var options = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_SolverOptions>(FormFindingChecks.Options(solver)).Value;
            var baseline = LegacyFormFindingBaseline.AssemblyModel(original.Value, R.Plane.WorldXY, 0, options, out _);
            var input = FormFindingInputBuilder.Build(original.Value, R.Plane.WorldXY, 0, options, original.Value.InputOwner, original.Guids);
            var actual = TensileModelAdapter.Export(input.Model, input.Units);
            var oldStatus = TensileModelAdapter.Solve(baseline, input.Model.FormFinding);
            var newStatus = TensileModelAdapter.Solve(actual.Solver, input.Model.FormFinding);
            Check(oldStatus == newStatus, "Same solver convergence in " + units);
            foreach (var old in baseline.Nodes)
            {
                var current = actual.Solver.Nodes[old.Id];
                Check(Math.Abs(current.Z - old.Z) <= 1e-9 * Math.Max(1, Math.Abs(old.Z)), "Legacy solver geometry in " + units);
            }
            foreach (var old in baseline.Beams)
                Check(Math.Abs(actual.Solver.Beams[old.Id].AxialForce - old.AxialForce) <= 1e-9 * Math.Max(1, Math.Abs(old.AxialForce)), "Legacy axial force in " + units);
            var saved = solver.ReadAnalysis(0);
            Check(Math.Abs(saved.Model.NodesElements.Values.Min(n => n.Position.X) + 1000) < 1e-9, "Archived positions normalized to mm in " + units);
            Check(saved.Model.Analysis.InputFingerprint == saved.Model.AnalysisFingerprint(), "Archived input fingerprint in " + units);
        }
    }
    private static void IpsSelfWeight()
    {
        using (var canvas = new Canvas())
        {
            var scale = FormFindingUnits.From(ModelModel.ModelUnits.IPS);
            var chain = FormFindingChecks.Start(canvas, diameter: 10 / scale.LengthToMm);
            var ends = new[] { new R.Point3d(-1000 / scale.LengthToMm, 0, 0), new R.Point3d(1000 / scale.LengthToMm, 0, 0) };
            var beams = FormFindingChecks.Beams(chain, new[] { new R.Line(ends[0], R.Point3d.Origin), new R.Line(R.Point3d.Origin, ends[1]) });
            var build = FormFindingChecks.Build(chain, ModelModel.ModelUnits.IPS, FormFindingChecks.ForceDensity(chain, beams, .1 / scale.ForceDensityToNPerMm), FormFindingChecks.Supports(chain, ends));
            var solver = FormFindingChecks.FormFind(chain, build, 0, maxIterations: 200, tolerance: 1e-10, gravity: 9806.65 / scale.LengthToMm);
            canvas.Solve();
            var solved = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(solver);
            var property = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_BeamProperty>(chain.Property).Value;
            double massPerMm = property.SectionArea * scale.LengthToMm * scale.LengthToMm * property.Material.Density;
            // Symmetric two-cable equilibrium: 2 q h = m g sqrt(L²+h²).
            double ratio = massPerMm * 9806.65 / .2;
            double expected = 1000 * ratio / Math.Sqrt(1 - ratio * ratio);
            double actual = -solved.Value.Nodes.Min(n => n.Position.Z) * scale.LengthToMm;
            Check(Math.Abs(actual - expected) <= 1e-7 * expected, "IPS inertial mass gives analytical sag in mm: " + actual + " vs " + expected);
        }
    }
    private static void DistributedLoadMoment()
    {
        var a = new NodeElement(new G.Point3d(0, 0, 0));
        var b = new NodeElement(new G.Point3d(1200, 0, 0));
        foreach (var node in new[] { a, b })
            node.Assignments.Restrains.Add(new RestrainAssignment { Restrain = new NodeRestrain(node, G.CoordinateSystem.Global,
                new[] { new DofRestrain(GeometryRestrain.DOF.DX, true), new DofRestrain(GeometryRestrain.DOF.DY, ReferenceEquals(node, a)), new DofRestrain(GeometryRestrain.DOF.DZ, true) }.ToList()) });
        var beam = new BeamElement(a.Position, b.Position, null); beam.ConnectNodes(a, b);
        beam.Assignments.Formulation = BeamFormulation.Cable;
        beam.Assignments.AnalysisProfile = new BeamAnalysisProfile();
        foreach (double station in new[] { 0.0, 1.0 }) beam.Assignments.AnalysisProfile.Stations.Add(new BeamAnalysisStation
            { Station = station, EA = 0, EI1 = 0, EI2 = 0, GJ = 0, GA1 = 0, GA2 = 0, MassPerLength = 0 });
        var load = new LoadCaseBase("Changing sign");
        beam.Assignments.Loads.Add(new GPC.Model.Loads.Assignments.BeamLoadAssignment
        {
            Start = 0, End = 1, LengthConvention = GPC.Model.Loads.Assignments.BeamLoadLengthConvention.ActualLength,
            StartIntensity = new LineLoad(0, 0, 1, 0, 0, 0, beam.Line, load),
            EndIntensity = new LineLoad(0, 0, -1, 0, 0, 0, beam.Line, load)
        });
        var model = ModelAssembly.Assemble(new Element[] { a, b, beam }, Guid.NewGuid());
        model.FormFinding = new FormFindingDefinition();
        model.FormFinding.Goals.Add(new FormFindingGoal { BeamGuid = beam.Guid, Kind = FormFindingGoalKind.ForceDensity, Target = 1, Weight = 0 });
        var exported = TensileModelAdapter.Export(model);
        var status = TensileModelAdapter.Solve(exported.Solver, model.FormFinding);
        Check(status == TensileLib.FormFinding.Solver.ResultCodes.Converged, "Zero-resultant load equilibrium");
        var first = exported.Solver.Nodes[exported.Ids[a.Guid]].BeamLoadForce.Z;
        var last = exported.Solver.Nodes[exported.Ids[b.Guid]].BeamLoadForce.Z;
        Check(Math.Abs(first - 200) < 1e-9 && Math.Abs(last + 200) < 1e-9, "Zero resultant retains the exact first moment -240000 N mm");
    }
    private sealed class PathProbe : Grasshopper.Kernel.GH_Component
    {
        internal string PathSeen; internal string ArchiveSeen;
        public PathProbe() : base("Path probe", "Probe", "Test only", "Tests", "Tests") { }
        public override Guid ComponentGuid => new Guid("5d768a1a-8e1d-42eb-b126-8d72287b07fb");
        protected override void RegisterInputParams(GH_InputParamManager manager) { }
        protected override void RegisterOutputParams(GH_OutputParamManager manager) { }
        protected override void SolveInstance(Grasshopper.Kernel.IGH_DataAccess access) { }
        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        { PathSeen = OnPingDocument()?.FilePath; ArchiveSeen = writer.ArchiveLocation; return base.Write(writer); }
    }
    private static void PersistencePathOrdering()
    {
        string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arachne-f6-path-" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            using (var document = new Grasshopper.Kernel.GH_Document())
            {
                var probe = new PathProbe(); probe.CreateAttributes(); document.AddObject(probe, false);
                var io = new Grasshopper.Kernel.GH_DocumentIO { Document = document };
                string first = System.IO.Path.Combine(directory, "first.ghx");
                Check(io.SaveQuiet(first), "Save first document");
                Check(string.IsNullOrEmpty(probe.PathSeen) && probe.ArchiveSeen == first, "Writer ArchiveLocation gives the first destination independently of Document.FilePath");
                string second = System.IO.Path.Combine(directory, "second.ghx");
                Check(io.SaveQuiet(second), "Save As document");
                Check(string.IsNullOrEmpty(probe.PathSeen) && probe.ArchiveSeen == second, "Writer ArchiveLocation gives the Save As destination independently of Document.FilePath");
            }
        }
        finally { System.IO.Directory.Delete(directory, true); }
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
                var expected = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(solve).Value;
                Check(expected.Nodes.SelectMany(n => n.Loads).OfType<NodePressureLoadModel>().Any(), "Pressure fixture produces nodal forces");
                var reference = solve.AnalysisReferences[0];
                Canvas.Set(solve, 4, new Grasshopper.Kernel.Types.GH_Boolean(false));
                string path = System.IO.Path.Combine(directory, "pressure.gh");
                Check(new Grasshopper.Kernel.GH_DocumentIO { Document = canvas.Document }.SaveQuiet(path), "Save manual-run document");
                var io = new Grasshopper.Kernel.GH_DocumentIO(); Check(io.Open(path), "Reopen manual-run document");
                using (io.Document)
                {
                    var restored = io.Document.Objects.OfType<Arachne.Grasshopper.Components.Analysis.FormFindComponent>().Single();
                    io.Document.Enabled = true; io.Document.NewSolution(false);
                    var actual = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(restored).Value;
                    Check(restored.Message == "Restored" && restored.AnalysisReferences[0].ManifestAddress == reference.ManifestAddress,
                        "Outputs restored from the saved run with Auto Run disabled");
                    Check(actual.Nodes.Count == expected.Nodes.Count && actual.Beams.Count == expected.Beams.Count, "Saved output topology retained");
                    for (int i = 0; i < actual.Nodes.Count; i++)
                    {
                        Check(actual.Nodes[i].Position == expected.Nodes[i].Position, "Exact final solver coordinates retained");
                        var oldPressure = expected.Nodes[i].Loads.OfType<NodePressureLoadModel>().SingleOrDefault();
                        var pressure = actual.Nodes[i].Loads.OfType<NodePressureLoadModel>().SingleOrDefault();
                        Check((pressure == null) == (oldPressure == null) && (pressure == null || pressure.Value == oldPressure.Value), "Saved pressure forces retained");
                        var oldReaction = expected.Nodes[i].Results.OfType<NodeReactResultModel>().SingleOrDefault();
                        var reaction = actual.Nodes[i].Results.OfType<NodeReactResultModel>().SingleOrDefault();
                        Check((reaction == null) == (oldReaction == null) && (reaction == null || reaction.Force == oldReaction.Force), "Saved reactions retained");
                    }
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
    private static void SolverOptionsOwnership()
    {
        var lc = new LoadCaseModel("P", "Pressure");
        var options = new SolverOptionsModel { Pressure = 0.125, PressureLoadCase = lc };
        options.LoadCases.Add(lc);
        options.Definition.Method = FormFindingMethod.NonlinearForceDensity;
        var copy = new SolverOptionsModel(options);
        Check(copy.Definition.Method == options.Definition.Method && copy.Definition.Pressure == options.Pressure, "Full GPC settings retained");
        Check(ReferenceEquals(copy.PressureLoadCase, copy.LoadCases[0]), "Selection aliases retained");
        copy.Definition.Pressure = 1; copy.LoadCases[0].Name = "changed";
        Check(options.Pressure == 0.125 && lc.Name == "Pressure", "Settings and selection isolated");
    }
    private static void InvalidArchives()
    {
        bool rejected = false;
        try { SerializationHelper.FromBytes<object>(new byte[] { 0, 1, 0, 0, 0 }); }
        catch (System.Runtime.Serialization.SerializationException) { rejected = true; }
        Check(rejected, "Legacy bytes never execute a formatter");
        rejected = false;
        try { SerializationHelper.ToBytes(new System.IO.FileInfo("unregistered")); }
        catch (System.Runtime.Serialization.SerializationException) { rejected = true; }
        Check(rejected, "Unregistered payload rejected");
    }
    private static void IndependentGpcSolver()
    {
        var load = new LoadCaseBase("Applied");
        var nodes = new[] { new NodeElement(Guid.NewGuid(), new G.Point3d(-500,0,0)), new NodeElement(Guid.NewGuid(), new G.Point3d(500,0,0)), new NodeElement(Guid.NewGuid(), new G.Point3d(0,0,-100)) };
        for (int i = 0; i < 3; i++) nodes[i].Assignments.Restrains.Add(new RestrainAssignment { Restrain = new NodeRestrain(nodes[i], G.CoordinateSystem.Global,
            Enumerable.Range(0,3).Select(j => new DofRestrain((GeometryRestrain.DOF)j, i < 2 || j < 2)).ToList()) });
        nodes[2].Loads.Add(new PointLoad(0,0,-10,0,0,0,nodes[2].Position,load));
        var beams = new[] { new BeamElement(Guid.NewGuid(),nodes[0].Position,nodes[2].Position,null), new BeamElement(Guid.NewGuid(),nodes[1].Position,nodes[2].Position,null) };
        for (int i = 0; i < 2; i++)
        {
            beams[i].ConnectNodes(nodes[i],nodes[2]); beams[i].Assignments.Formulation = BeamFormulation.Cable;
            beams[i].Assignments.AnalysisProfile = new BeamAnalysisProfile();
            foreach (double station in new[] { 0d, 1d }) beams[i].Assignments.AnalysisProfile.Stations.Add(
                new BeamAnalysisStation { Station=station, EA=0, EI1=0, EI2=0, GJ=0, GA1=0, GA2=0, MassPerLength=0 });
        }
        var model = ModelAssembly.Assemble(nodes.Cast<Element>().Concat(beams),Guid.NewGuid(),"F6 GPC");
        model.FormFinding = new FormFindingDefinition();
        foreach (var beam in beams) model.FormFinding.Goals.Add(new FormFindingGoal { BeamGuid=beam.Guid,Kind=FormFindingGoalKind.ForceDensity,Target=.01,Weight=0 });
        string fingerprint = model.AnalysisFingerprint();
        var run = TensileModelAdapter.Export(model);
        var status = TensileModelAdapter.Solve(run.Solver,model.FormFinding);
        Check(status == TensileLib.FormFinding.Solver.ResultCodes.Converged, "Converged");
        Check(Math.Abs(run.Solver.Nodes[run.Ids[nodes[2].Guid]].Z+500)<1e-7,"Analytical sag");
        TensileModelAdapter.Attach(model,run,status);
        Check(model.AnalysisFingerprint()==fingerprint,"Input unchanged");
        Check(model.Datasets.ContainsKey("form-finding"),"Result dataset attached");
    }
    private static void FutureWrapperSchema()
    {
        var data = SerializationHelper.ToBytes(new NodeModel("n", R.Point3d.Origin));
        System.Xml.Linq.XDocument document;
        using (var reader = System.Xml.XmlDictionaryReader.CreateBinaryReader(data, 7, data.Length - 7, System.Xml.XmlDictionaryReaderQuotas.Max))
            document = System.Xml.Linq.XDocument.Load(reader);
        document.Descendants().Single(e => e.Name.LocalName == "NodeModel_SERIALIZATION_RELEASE").Value = "999";
        using (var stream = new System.IO.MemoryStream())
        {
            stream.Write(data, 0, 7);
            using (var writer = System.Xml.XmlDictionaryWriter.CreateBinaryWriter(stream, null, null, false)) document.WriteTo(writer);
            bool rejected = false;
            try { SerializationHelper.FromBytes<NodeModel>(stream.ToArray()); }
            catch (System.Runtime.Serialization.SerializationException) { rejected = true; }
            Check(rejected, "Future wrapper schema is rejected at the document boundary");
        }
    }
    private static void LegacyMigration()
    {
        byte[] Read(string name) => System.IO.File.ReadAllBytes(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fixtures","Legacy",name));
        var model = SerializationHelper.FromBytes<ModelModel>(Read("model-v5.nrbf"));
        Check(model.Name=="Legacy NRBF"&&model.Nodes.Count==2&&model.Beams.Count==1,"Legacy model and indexes");
        Check(ReferenceEquals(model.Nodes[0].Loads[0].LoadCase,model.Beams[0].Loads[0].LoadCase),"Legacy shared cases");
        Check(ReferenceEquals(model.Nodes[0].Groups[0],model.Beams[0].Groups[0]),"Legacy shared groups");
        var goal=model.Beams[0].Loads.OfType<ForceDensityModel>().Single();
        Check(goal.MembraneType==ForceDensityModel.MembraneTypes.Weft&&goal.Value==.2&&goal.Weight==3,"Legacy fabric goal");
        Check(model.Beams[0].Loads.OfType<FixedLengthModel>().Single().Goal.Target==100,"Legacy goal enters GPC");
        var reopened=SerializationHelper.Clone(model);
        Check(reopened.GetNode("2")!=null&&reopened.Beams[0].Loads.Count==3,"Legacy model resaved in new format");
        var options=SerializationHelper.FromBytes<SolverOptionsModel>(Read("solver-options-v03.nrbf"));
        Check(options.MaxIterations==37&&options.SolverMaxIterations==71&&options.Pressure==.125&&!options.CreateMesh,"Legacy solver settings");
        Check(ReferenceEquals(options.LoadCases[0],options.PressureLoadCase),"Legacy selection alias");
        Check(BitConverter.DoubleToInt64Bits(options.Gravity)==long.MinValue,"Legacy negative zero preserved");
        var saved=SerializationHelper.Clone(options);
        Check(BitConverter.DoubleToInt64Bits(saved.Gravity)==long.MinValue,"Negative zero preserved after migration");
    }
}
