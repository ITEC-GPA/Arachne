using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Arachne.Common.Models;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Core.Identity;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Loads.Assignments;
using GPC.Model.Models;
using GPC.Model.Restraints;
using GPC.Model.Structure.Assignments;
using G = GPC.Geometry;
using R = Rhino.Geometry;

namespace Arachne.Common.Analysis
{
    /// <summary>Application input boundary. Produces a detached, normalized GPC analysis graph;
    /// Rhino preview data and inactive compatibility controls remain in their wrappers.</summary>
    public static class FormFindingInputBuilder
    {
        public sealed class Input
        {
            public Model Model { get; internal set; }
            public LoadCaseBase ResultCase { get; internal set; }
            public LoadCaseBase PressureCase { get; internal set; }
            public FormFindingUnits Units { get; internal set; }
            public List<string> Diagnostics { get; } = new List<string>();
        }
        public static Guid Identity(Guid owner, string key)
        {
            if (owner == Guid.Empty) throw new ArgumentException("EmptyInputOwner");
            using (var sha = SHA256.Create()) return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes(owner.ToString("D") + "\n" + key)).Take(16).ToArray());
        }
        private static G.Vector3d Vector(R.Vector3d value) => new G.Vector3d(value.X, value.Y, value.Z);
        private static R.Vector3d Global(CoordinateSystemModel axes, R.Vector3d value)
        {
            if (axes == null) return value;
            if (!axes.GetAxes(out var x, out var y, out var z)) throw new ArgumentException("InvalidLoadCoordinateSystem");
            return x * value.X + y * value.Y + z * value.Z;
        }
        public static Input Build(ModelModel source, R.Plane plane, int solverType, SolverOptionsModel options,
            Guid owner, IDictionary<ElementModel, Guid> identities = null)
        {
            if (source == null || options == null) throw new ArgumentNullException();
            if (solverType < 0 || solverType > 2) throw new ArgumentOutOfRangeException(nameof(solverType));
            var input = new Input { Units = FormFindingUnits.From(source.Units) };
            var u = input.Units;
            G.Point3d Point(R.Point3d p) => new G.Point3d(p.X * u.LengthToMm, p.Y * u.LengthToMm, p.Z * u.LengthToMm);
            Guid Key(ElementModel wrapper, string slot) => identities != null && identities.TryGetValue(wrapper, out var id) ? id : Identity(owner, slot);
            var ff = ModelValues.CopyValue(options.Definition);
            ff.Goals.Clear(); ff.CoordinateGoals.Clear(); ff.SurfaceMasses.Clear();
            ff.Method = solverType == 0 ? FormFindingMethod.LinearForceDensity : solverType == 1 ? FormFindingMethod.SteppedLinearForceDensity : FormFindingMethod.NonlinearForceDensity;
            ff.Gravity *= u.LengthToMm; ff.Pressure *= u.PressureToMPa;
            if (plane == R.Plane.Unset) plane = R.Plane.WorldXY;
            var projection = new G.Plane(Point(plane.Origin), Vector(plane.Normal));
            ff.ProjectionPlane = new G.CoordinateSystem(projection.P1, projection.P2, projection.P3);
            var elements = new List<Element>();
            var nodes = new List<NodeElement>();
            var beams = new List<BeamElement>();
            var points = new Dictionary<(double, double, double), NodeElement>();
            var cases = new Dictionary<string, LoadCaseBase>(StringComparer.Ordinal);
            var caseIds = new Dictionary<string, string>(StringComparer.Ordinal);
            var usedCases = new HashSet<LoadCaseModel>();
            LoadCaseBase Case(LoadCaseModel sourceCase)
            {
                if (sourceCase == null || string.IsNullOrWhiteSpace(sourceCase.Name)) throw new ArgumentException("MissingFormFindingLoadCase");
                if (caseIds.TryGetValue(sourceCase.Name, out var previousId) && previousId != sourceCase.Id)
                    throw new ArgumentException("ConflictingLoadCaseName: " + sourceCase.Name);
                caseIds[sourceCase.Name] = sourceCase.Id;
                if (!cases.TryGetValue(sourceCase.Name, out var item))
                    cases.Add(sourceCase.Name, item = new LoadCaseBase(Identity(owner, "case:" + sourceCase.Name), sourceCase.Name));
                return item;
            }
            bool Applies(LoadModel load)
            {
                bool included = options.LoadCases == null || options.LoadCases.Count == 0 || options.LoadCases.Any(c => c == load.LoadCase);
                if (included) usedCases.Add(load.LoadCase);
                return included;
            }
            void Register(Element element, EntityFamily family, int slot)
            {
                element.Source = new SourceIdentity("Arachne", owner.ToString("D"), family, slot.ToString(System.Globalization.CultureInfo.InvariantCulture));
                elements.Add(element);
            }
            NodeElement AddNode(R.Point3d p, Guid id, NodeElement original = null)
            {
                var node = original == null ? new NodeElement(id, Point(p)) : ModelValues.CopyValue(original);
                if (node.Guid != id) throw new ArgumentException("ConflictingNodeWrapperIdentity");
                node.Position = Point(p);
                Register(node, EntityFamily.Node, nodes.Count); nodes.Add(node);
                if (!points.ContainsKey((p.X, p.Y, p.Z))) points.Add((p.X, p.Y, p.Z), node);
                return node;
            }
            NodeElement At(R.Point3d p, Guid element, string endpoint) => points.TryGetValue((p.X, p.Y, p.Z), out var node)
                ? node : AddNode(p, Identity(element, endpoint));
            for (int i = 0; i < source.Nodes.Count; i++)
            {
                var old = source.Nodes[i];
                var id = identities != null && identities.TryGetValue(old, out var bound) ? bound : old.Identity;
                var node = AddNode(old.Position, id, old.Input);
                if (old.HasAttribute(typeof(NodeRestraintAttributeModel)))
                {
                    var r = NodeRestraintAttributeModel.GetRestraint(old);
                    var axes = r.Axes();
                    node.Assignments.Restrains.Add(new RestrainAssignment { Restrain = new NodeRestrain(node,
                        new G.CoordinateSystem(node.Position, Vector(axes[0]), Vector(axes[1])),
                        new[] { r.Tx, r.Ty, r.Tz }.Select((fixedDof, dof) => new DofRestrain((GeometryRestrain.DOF)dof, fixedDof)).ToList()) });
                }
                foreach (var load in old.Loads)
                {
                    if (load is NodePressureLoadModel) { input.Diagnostics.Add("The pressure forces of a previous form finding are results; only the current pressure is applied."); continue; }
                    if (load is NodeForceLoadModel force && Applies(load))
                    {
                        var physicalLoad = ModelValues.CopyValue(force.Input);
                        physicalLoad.F1 *= u.ForceToN; physicalLoad.F2 *= u.ForceToN; physicalLoad.F3 *= u.ForceToN;
                        physicalLoad.Point = node.Position; physicalLoad.LoadCase = Case(load.LoadCase);
                        node.Loads.Add(physicalLoad);
                    }
                    else if (!(load is NodeForceLoadModel)) throw new NotSupportedException("UnsupportedFormFindingNodeLoad: " + load.GetType().Name);
                }
                if (old.HasAttribute(typeof(NodeWeightAttributeModel)))
                {
                    var weights = NodeWeightAttributeModel.GetWeight(old);
                    if (solverType == 2) ff.CoordinateGoals.Add(new FormFindingCoordinateGoal { NodeGuid = node.Guid, Weights = Vector(weights) });
                    else if (weights != R.Vector3d.Zero) input.Diagnostics.Add("Coordinate objectives are ignored by the selected linear method.");
                }
            }
            for (int i = 0; i < source.Beams.Count; i++)
            {
                var old = source.Beams[i]; var id = Key(old, "beam:" + i);
                var beam = new BeamElement(id, Point(old.PointFrom), Point(old.PointTo), null);
                beam.ConnectNodes(At(old.PointFrom, id, "I"), At(old.PointTo, id, "J"));
                beam.Assignments.Formulation = BeamFormulation.Cable;
                if (old.BeamProperty?.Material == null) throw new ArgumentException("MissingFormFindingBeamProperty");
                double mass = old.BeamProperty.SectionArea * u.LengthToMm * u.LengthToMm * old.BeamProperty.Material.Density;
                beam.Assignments.AnalysisProfile = new BeamAnalysisProfile();
                foreach (double station in new[] { 0.0, 1.0 }) beam.Assignments.AnalysisProfile.Stations.Add(new BeamAnalysisStation
                    { Station = station, EA = 0, EI1 = 0, EI2 = 0, GA1 = 0, GA2 = 0, GJ = 0, MassPerLength = mass });
                var densities = old.Loads.OfType<ForceDensityModel>().ToArray();
                if (densities.Length > 1) input.Diagnostics.Add("Multiple force density objectives: the first is used, as in the previous component.");
                var primary = densities.Length == 0 ? new FormFindingGoal { Kind = FormFindingGoalKind.ForceDensity, Target = 0, Weight = 0 }
                    : ModelValues.CopyValue(densities[0].Goal);
                if (densities.Length == 0 && old.Loads.OfType<FixedAxialForceModel>().FirstOrDefault() is FixedAxialForceModel onlyAxial)
                { primary.Kind = FormFindingGoalKind.CableForce; primary.Target = onlyAxial.Value; }
                primary.BeamGuid = id;
                if (primary.Kind == FormFindingGoalKind.ForceDensity || primary.Kind == FormFindingGoalKind.MembraneForcePerWidth) primary.Target *= u.ForceDensityToNPerMm;
                else if (primary.Kind == FormFindingGoalKind.CableForce) primary.Target *= u.ForceToN;
                if (primary.ChordLengthMm.HasValue) primary.ChordLengthMm *= u.LengthToMm;
                ff.Goals.Add(primary);
                foreach (var family in new[] { typeof(FixedLengthModel), typeof(FixedAxialForceModel) })
                {
                    var goals = old.Loads.Where(l => l.GetType() == family).ToArray();
                    if (goals.Length == 0) continue;
                    if (solverType == 0) { input.Diagnostics.Add("Length and axial-force objectives are ignored by the linear method."); continue; }
                    if (goals.Length > 1) input.Diagnostics.Add("Multiple " + family.Name + " objectives: the first is used.");
                    var goal = ModelValues.CopyValue(goals[0] is FixedLengthModel length ? length.Goal : ((FixedAxialForceModel)goals[0]).Goal);
                    goal.BeamGuid = id; goal.Target *= goal.Kind == FormFindingGoalKind.Length ? u.LengthToMm : u.ForceToN; ff.Goals.Add(goal);
                }
                foreach (var load in old.Loads.Where(l => !(l is ForceDensityModel) && !(l is FixedLengthModel) && !(l is FixedAxialForceModel)).Where(Applies))
                {
                    if (load is BeamPreLoadModel) { input.Diagnostics.Add("Beam preloads from a previous form finding are results and are not reapplied."); continue; }
                    if (load is BeamPointLoadModel point)
                    {
                        if (point.PointLoadType != BeamPointLoadModel.LoadType.Force) throw new NotSupportedException("FormFindingDoesNotSupportBeamMoments");
                        var p = Global(point.Value.CoordinateSystem, point.Value.P);
                        beam.Assignments.Loads.Add(new BeamLoadAssignment { Start = point.Value.A, End = point.Value.A,
                            Concentrated = new PointLoad(p.X * u.ForceToN, p.Y * u.ForceToN, p.Z * u.ForceToN, 0, 0, 0, beam.StartPoint, Case(load.LoadCase)),
                            LengthConvention = BeamLoadLengthConvention.ActualLength });
                    }
                    else if (load is BeamDistributedLoadModel distributed)
                    {
                        var d = distributed.Value;
                        if (distributed.DistributedLoadType != BeamDistributedLoadModel.LoadType.Force) throw new NotSupportedException("FormFindingDoesNotSupportBeamMoments");
                        if (d.LoadSchema != BeamDistributedLoadModel.LoadSchema.Uniform && d.LoadSchema != BeamDistributedLoadModel.LoadSchema.Keystone)
                            throw new NotSupportedException("UnsupportedFormFindingLoadSchema: " + d.LoadSchema);
                        R.Vector3d direction; bool projected = false;
                        switch (d.LoadDirection)
                        {
                            case BeamDistributedLoadModel.LoadDirection.X_Projected: projected = true; goto case BeamDistributedLoadModel.LoadDirection.X;
                            case BeamDistributedLoadModel.LoadDirection.Y_Projected: projected = true; goto case BeamDistributedLoadModel.LoadDirection.Y;
                            case BeamDistributedLoadModel.LoadDirection.Z_Projected: projected = true; goto case BeamDistributedLoadModel.LoadDirection.Z;
                            case BeamDistributedLoadModel.LoadDirection.Gravity_Projected: projected = true; goto case BeamDistributedLoadModel.LoadDirection.Gravity;
                            case BeamDistributedLoadModel.LoadDirection.X: direction = Global(d.CoordinateSystem, R.Vector3d.XAxis); break;
                            case BeamDistributedLoadModel.LoadDirection.Y: direction = Global(d.CoordinateSystem, R.Vector3d.YAxis); break;
                            case BeamDistributedLoadModel.LoadDirection.Z: direction = Global(d.CoordinateSystem, R.Vector3d.ZAxis); break;
                            case BeamDistributedLoadModel.LoadDirection.Gravity: direction = -R.Vector3d.ZAxis; break;
                            default: throw new NotSupportedException("UnknownFormFindingLoadDirection");
                        }
                        var start = Vector(direction) * (d.PA * u.ForceDensityToNPerMm);
                        var end = Vector(direction) * ((d.LoadSchema == BeamDistributedLoadModel.LoadSchema.Uniform ? d.PA : d.PB) * u.ForceDensityToNPerMm);
                        beam.Assignments.Loads.Add(new BeamLoadAssignment { Start = d.A, End = 1 - d.B,
                            StartIntensity = new LineLoad(start, new G.Vector3d(0, 0, 0), beam.Line, Case(load.LoadCase)),
                            EndIntensity = new LineLoad(end, new G.Vector3d(0, 0, 0), beam.Line, Case(load.LoadCase)),
                            LengthConvention = projected ? BeamLoadLengthConvention.ProjectedLength : BeamLoadLengthConvention.ActualLength,
                            ProjectionPlaneNormal = projected ? Vector(direction) : null });
                    }
                    else throw new NotSupportedException("UnsupportedFormFindingBeamLoad: " + load.GetType().Name);
                }
                Register(beam, EntityFamily.Beam, i); beams.Add(beam);
            }
            double SurfaceMass(PlatePropertyModel property) => property?.Material == null || !(property.Thickness > 0) ? 0
                : property.Material.Kind == MaterialModel.MaterialKind.Membrane ? property.Material.AreaDensity
                : property.Thickness * u.LengthToMm * property.Material.Density;
            void AddArea(Guid id, NodeElement[] connectivity, PlatePropertyModel property)
            {
                var area = new AreaElement(id, null, null); area.ConnectNodes(connectivity);
                Register(area, EntityFamily.Shell, ff.SurfaceMasses.Count + 1);
                ff.SurfaceMasses.Add(new FormFindingSurfaceMass { AreaGuid = id, MassPerArea = SurfaceMass(property) });
            }
            if (options.CreateMesh)
                for (int i = 0; i < source.Plates.Count; i++)
                {
                    var old = source.Plates[i]; var id = Key(old, "area:" + i);
                    if (old.Points.Count != 3 && old.Points.Count != 4) throw new NotSupportedException("FormFindingRequiresLinearShell");
                    AddArea(id, old.Points.Select((p, slot) => At(p, id, "vertex:" + slot)).ToArray(), old.PlateProperty);
                }
            if (ff.SurfaceMasses.Count == 0)
            {
                // Reuse TensileLib's topology algorithm only; this temporary graph carries no
                // loads, material, goals or solver state. The discovered faces enter GPC before export.
                var topology = new TensileLib.Geometry.Model { Plane = projection };
                var ids = nodes.ToDictionary(n => n.Guid, n => topology.AddNewNode(n.Position.X, n.Position.Y, n.Position.Z));
                foreach (var beam in beams) topology.AddNewBeam(ids[beam.NodeI.Guid], ids[beam.NodeJ.Guid], projection);
                var bySolverId = nodes.ToDictionary(n => ids[n.Guid]);
                topology.GeneratePlatesFromBeams();
                foreach (var face in topology.Plates)
                {
                    var connectivity = new[] { face.Node1, face.Node2, face.Node3, face.Node4 }.Where(n => n != null).Select(n => bySolverId[n.Id]).ToArray();
                    AddArea(Identity(owner, "generated-area:" + string.Join("/", connectivity.Select(n => n.Guid.ToString("N")).OrderBy(x => x, StringComparer.Ordinal))), connectivity, options.PlateProperty);
                }
            }
            input.Model = ModelAssembly.Assemble(elements, owner, source.Name ?? "", supportsWarpedQuads: true);
            input.Model.FormFinding = ff;
            if (ff.Pressure != 0)
            {
                input.PressureCase = Case(options.PressureLoadCase);
                if (!input.Model.LoadCases.Values.Any(c => ReferenceEquals(c, input.PressureCase))) input.Model.LoadCases.Add(input.PressureCase);
            }
            var resultName = "Form finding";
            while (cases.ContainsKey(resultName)) resultName += " (result)";
            input.ResultCase = new LoadCaseBase(Identity(owner, "case:form-finding-result"), resultName);
            input.Model.LoadCases.Add(input.ResultCase);
            ff.Validate(input.Model);
            if (ff.Gravity == 0 && ff.SurfaceMasses.Any(m => m.MassPerArea > 0)) input.Diagnostics.Add("The plates have a mass but Gravity is 0: self weight is not applied.");
            foreach (var lc in (options.LoadCases ?? new List<LoadCaseModel>()).Where(c => !usedCases.Contains(c))) input.Diagnostics.Add("No node or beam load in the load case " + lc.Name + ".");
            if (source.Units == ModelModel.ModelUnits.IPS) input.Diagnostics.Add("IPS self weight uses force/acceleration mass consistently; the former lbm-as-inertial-mass convention is corrected.");
            return input;
        }
    }
}
