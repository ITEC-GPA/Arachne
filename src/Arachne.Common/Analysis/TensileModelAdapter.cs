using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Core.Identity;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Loads.Assignments;
using GPC.Model.Models;
using GPC.Model.Restraints;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using GPC.Model.Sections;
using GPC.Model.Structure.Assignments;
using TensileLib.FormFinding;
using R = Rhino.Geometry;
using TBeam = TensileLib.Geometry.Beam;
using TModel = TensileLib.Geometry.Model;
using TNode = TensileLib.Geometry.Node;
using TPlate = TensileLib.Geometry.Plate;

namespace Arachne.Common.Analysis
{
    /// <summary>
    /// The GPC form-finding graph (N, mm, s, t) to a detached TensileLib model in the document units, and the solver results back to
    /// GPC. The assembly repeats the former one (Arachne 7b1108d, FormFindingHelper.AssemblyModel and FormFindComponent) operation by
    /// operation, with the same Rhino vector arithmetic, so that the solver receives the same numbers (bit for bit in N and mm).
    /// Every lookup inside the loops is a dictionary (review A1): linear in the number of elements.
    /// </summary>
    public static class TensileModelAdapter
    {
        public sealed class Exported
        {
            public TModel Solver { get; internal set; }
            public SolverIdentityMap Map { get; internal set; }
            /// <summary>Solver id of each node, beam and plate.</summary>
            public Dictionary<Guid, int> Ids { get; internal set; }
            public FormFindingUnits Units { get; internal set; }
            public List<string> Remarks { get; } = new List<string>();
            /// <summary>The diagnostics of <see cref="ModelPreflight.ValidateDomain"/> (no errors: an error stops the export).</summary>
            public List<GPC.Model.Core.Diagnostics.ModelDiagnostic> Diagnostics { get; } = new List<GPC.Model.Core.Diagnostics.ModelDiagnostic>();
            /// <summary>The plates generated from the beams (no plate in the graph), added to the graph as shells.</summary>
            public bool GeneratedPlates { get; internal set; }
        }

        private static R.Vector3d Rh(Vector3d v) => new R.Vector3d(v.X, v.Y, v.Z);
        private static Vector3d Gp(R.Vector3d v) => new Vector3d(v.X, v.Y, v.Z);

        /// <summary>The domain diagnostics of the graph (<see cref="ModelPreflight.ValidateDomain"/>): stages, moving loads, form-finding
        /// definition, elastic and membrane properties, and the inputs the selected method ignores.</summary>
        public static IReadOnlyList<GPC.Model.Core.Diagnostics.ModelDiagnostic> Preflight(Model model) => ModelPreflight.ValidateDomain(model);

        public static Exported Export(Model model, FormFindingUnits units, FormFindingInputBuilder.Input input = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            units = units ?? FormFindingUnits.Nmm;
            double length = units.LengthToMm, force = units.ForceToN;
            var ff = model.FormFinding ?? throw new ArgumentException("MissingFormFindingDefinition");
            if (model.VolumeElements.Count != 0 || model.ModelLoads.Count != 0 || model.Costrains.Count != 0 || model.Stages.Count != 0 || model.Combinations.Count != 0 || model.MovingLoads != null)
                throw new NotSupportedException("UnsupportedFormFindingModelInput");
            Plane plane = input?.SolverPlane;
            if (plane == null)
            {
                var cs = ff.ProjectionPlane ?? CoordinateSystem.Global;
                plane = new Plane(new Point3d(cs.Origin.X / length, cs.Origin.Y / length, cs.Origin.Z / length), cs.V3);
            }
            var native = new TModel();
            var result = new Exported { Solver = native, Ids = new Dictionary<Guid, int>(), Units = units };
            // Domain preflight: reading, copying and fingerprinting a GPC model no longer validate it (review L1, B1), so dangling
            // stage or form-finding references are found here, before the solver. Errors stop the run; the rest are messages.
            result.Diagnostics.AddRange(Preflight(model));
            var errors = result.Diagnostics.Where(d => d.Severity == GPC.Model.Core.Diagnostics.DiagnosticSeverity.Error).ToArray();
            if (errors.Length != 0) throw new InvalidOperationException("The model cannot be form found: " + string.Join("; ", errors.Select(d => d.Code + " (" + d.Message + ")")));
            var coordinateGoals = ff.CoordinateGoals.GroupBy(g => g.NodeGuid).ToDictionary(g => g.Key, g => g.First());
            var goals = ff.Goals.GroupBy(g => g.BeamGuid).ToDictionary(g => g.Key, g => g.ToList());
            int solverType = ff.Method == FormFindingMethod.LinearForceDensity ? 0 : ff.Method == FormFindingMethod.SteppedLinearForceDensity ? 1 : 2;

            // Nodes, in the order of the model (Build Model order). The first node at a point takes the elements that end there.
            var nodes = new Dictionary<Guid, TNode>();
            foreach (var node in model.NodesElements.Values)
            {
                if (node.Assignments.GroundSprings.Count != 0 || node.Assignments.Links.Count != 0 || node.Assignments.Mass != null || node.Assignments.Active != null || node.Assignments.Dofs != null)
                    throw new NotSupportedException("UnsupportedFormFindingNodeAssignment");
                int id = native.AddNewNode(node.Position.X / length, node.Position.Y / length, node.Position.Z / length);
                var n = native.Nodes.Last(); result.Ids.Add(node.Guid, id); nodes.Add(node.Guid, n);
                var restraint = node.Assignments.Restrains.FirstOrDefault();
                if (restraint?.Restrain != null)
                {
                    if (restraint.Case != null || restraint.Phase != null || restraint.FreedomCase != null
                        || restraint.Restrain.Restrains.Any(d => d.HasStiffness || d.HasImposedDisplacement))
                        throw new NotSupportedException("UnsupportedFormFindingRestraint");
                    bool Restrained(GeometryRestrain.DOF dof) => restraint.Restrain.Restrains.Any(d => d.Dof == dof && d.IsRestrained);
                    n.Tx = Restrained(GeometryRestrain.DOF.DX); n.Ty = Restrained(GeometryRestrain.DOF.DY); n.Tz = Restrained(GeometryRestrain.DOF.DZ);
                    // Restraints along the axes of a plane (TensileLib 2.4)
                    if (!ElementDrafts.IsGlobalRestraint(restraint.Restrain))
                        n.SetRestraintAxes(restraint.Restrain.CoordinateSystem.V1, restraint.Restrain.CoordinateSystem.V2);
                }
                var loads = node.Loads.Values.OfType<PointLoad>().ToList();
                if (loads.Count > 0)
                {
                    // Sequential sum, in the document units
                    var sum = new R.Vector3d();
                    foreach (var p in loads)
                    {
                        if (p.M1 != 0 || p.M2 != 0 || p.M3 != 0) throw new NotSupportedException("FormFindingDoesNotSupportNodalMoments");
                        var g = p.ToGlobal();
                        sum += new R.Vector3d(g.F1 / force, g.F2 / force, g.F3 / force);
                    }
                    n.SetForce(sum.X, sum.Y, sum.Z);
                }
                if (coordinateGoals.TryGetValue(node.Guid, out var cg))
                {
                    if (cg.Weights.X != 0) n.FixNLCoordX(cg.Weights.X);
                    if (cg.Weights.Y != 0) n.FixNLCoordY(cg.Weights.Y);
                    if (cg.Weights.Z != 0) n.FixNLCoordZ(cg.Weights.Z);
                }
            }

            // Beams; the edges of the plates are the first beam of each pair of nodes.
            var edges = new Dictionary<(int, int), TBeam>();
            foreach (var beam in model.BeamElements.Values)
            {
                if (beam.NodeI == null || beam.NodeJ == null) throw new ArgumentException("ExplicitBeamConnectivityRequired");
                var node1 = nodes[beam.NodeI.Guid]; var node2 = nodes[beam.NodeJ.Guid];
                int id = native.AddNewBeam(node1.Id, node2.Id, plane);
                var b = native.Beams.Last(); result.Ids.Add(beam.Guid, id);
                if (!edges.ContainsKey((node1.Id, node2.Id))) edges.Add((node1.Id, node2.Id), b);
                if (!edges.ContainsKey((node2.Id, node1.Id))) edges.Add((node2.Id, node1.Id), b);
                Mass(b, beam, units);
                var from = beam.NodeI.Position; var to = beam.NodeJ.Position;
                var v = new R.Point3d(to.X / length, to.Y / length, to.Z / length) - new R.Point3d(from.X / length, from.Y / length, from.Z / length);
                foreach (var a in beam.Assignments.Loads) ApplyLoad(b, a, v, units);
                if (!goals.TryGetValue(beam.Guid, out var beamGoals)) continue;
                var primary = beamGoals.FirstOrDefault(g => ElementDrafts.IsPrimary(g.Kind));
                if (primary != null)
                {
                    switch (primary.Kind)
                    {
                        case FormFindingGoalKind.ForceDensity: b.TargetForceDensity = primary.Target / units.ForceDensityToNPerMm; break;
                        case FormFindingGoalKind.CableForce: b.TargetAxialForce = primary.Target / force; break;
                        case FormFindingGoalKind.MembraneForcePerWidth:
                            b.TargetForceAreaDensity = primary.Target / units.ForceDensityToNPerMm;
                            b.MembraneType = primary.Direction == FabricDirection.Warp ? TBeam.MembraneTypes.Warp : primary.Direction == FabricDirection.Weft ? TBeam.MembraneTypes.Weft : TBeam.MembraneTypes.Undefined;
                            break;
                        case FormFindingGoalKind.CurvatureRatio: b.TargetCurvatureRatio = primary.Target; b.CurveChord = primary.ChordLengthMm.Value / length; break;
                    }
                    b.Project = primary.ProjectToPlane;
                    if (primary.Weight > 0) b.FixNLForceDensity(primary.Weight);
                    if (input != null && input.MultipleForceDensities.Contains(beam.Guid)) result.Remarks.Add($"Beam {b} have multiple force density");
                }
                var fixedLength = beamGoals.FirstOrDefault(g => g.Kind == FormFindingGoalKind.Length);
                if (fixedLength != null) b.FixNLLength(fixedLength.Target / length, fixedLength.Weight);
                var axialForce = beamGoals.FirstOrDefault(g => g.Kind == FormFindingGoalKind.AxialForce);
                if (axialForce != null) { b.TargetAxialForce = axialForce.Target / force; b.FixNLAxialForce(axialForce.Weight); }
            }

            // Explicit plates (Create mesh), then pressure, gravity and plane; without plates TensileLib generates them from the beams.
            foreach (var area in model.AreaElements.Values)
            {
                if (area.Nodes.Count != 3 && area.Nodes.Count != 4) throw new NotSupportedException("FormFindingRequiresLinearShell");
                var ns = area.Nodes.Select(n => nodes[n.Guid]).ToArray();
                var bb = new List<TBeam>();
                for (int i = 0; i < ns.Length; i++)
                    if (edges.TryGetValue((ns[i].Id, ns[(i + 1) % ns.Length].Id), out var edge)) bb.Add(edge);
                var plate = new TPlate(ns, bb.ToArray(), plane) { Id = native.Plates.Count + 1 };
                Weight(plate, area.PlateProperty, units);
                native.Plates.Add(plate); result.Ids.Add(area.Guid, plate.Id);
            }
            native.Pressure = ff.Pressure / units.PressureToMPa;
            native.Gravity = ff.Gravity / length;
            native.Plane = plane;
            if (native.Plates.Count == 0)
            {
                native.GeneratePlatesFromBeams();
                var byId = model.NodesElements.Values.ToDictionary(n => result.Ids[n.Guid]);
                var generatedProperty = input?.GeneratedPlateProperty;
                foreach (var plate in native.Plates)
                {
                    Weight(plate, generatedProperty, units);
                    var corners = new[] { plate.Node1, plate.Node2, plate.Node3, plate.Node4 }.Where(n => n != null).Select(n => byId[n.Id]).ToArray();
                    var area = new AreaElement(InputIdentity.Of(model.Guid, "generated-area:" + plate.Id), null, null, null, plate.Id.ToString(System.Globalization.CultureInfo.InvariantCulture), plate.Id);
                    area.ConnectNodes(corners);
                    area.Source = new SourceIdentity("Arachne", model.Guid.ToString("D"), EntityFamily.Shell, "generated:" + plate.Id);
                    model.AreaElements.Add(area); result.Ids.Add(area.Guid, plate.Id);
                }
                result.GeneratedPlates = true;
            }
            if (native.Gravity == 0 && native.Plates.Any(p => p.WeightMultiplier > 0))
                result.Remarks.Add("The plates have a weight but Gravity is 0: the self weight is not applied.");
            // The graph is a copy of the Build Model output with the shells the solver generated: its identity map is rebuilt.
            ModelAssembly.RefreshIdentityMap(model);
            result.Map = SolverIdentityMap.Create(model, "TensileLib-2.5.0.1", result.Ids);
            return result;
        }

        // Self weight of the beams: section area x density of the material, in the document units as the former assembly
        // (the property of the beam); without a property, the line mass of the analysis profile.
        private static void Mass(TBeam target, BeamElement beam, FormFindingUnits units)
        {
            double length = units.LengthToMm;
            if (beam.BeamProperty is ElasticBeamProperty property && property.Section is Section section && property.Material != null)
            {
                target.Area = length == 1 ? section.Area : section.Area / (length * length);
                target.Density = units.SolverDensity(property.Material.Density);
                return;
            }
            if (beam.BeamProperty != null) throw new NotSupportedException("UnsupportedFormFindingBeamProperty: " + beam.BeamProperty.GetType().Name);
            var profile = beam.Assignments.AnalysisProfile ?? throw new ArgumentException("MissingFormFindingBeamProperty");
            profile.Validate();
            target.Area = 1; target.Density = units.SolverDensity(profile.At(0).MassPerLength / (length * length));
        }

        // Weight of the plates: thickness x density; for a membrane the mass per area of its data sheet (FeMM: the thickness cancels out).
        private static void Weight(TPlate plate, PlateProperty property, FormFindingUnits units)
        {
            switch (property)
            {
                case null: return;
                case ElasticPlateProperty elastic:
                    double thickness = elastic.MembraneThickness / units.LengthToMm;
                    if (elastic.Material == null || !(thickness > 0)) return;
                    plate.MemThick = thickness; plate.Density = units.SolverDensity(elastic.Material.Density);
                    return;
                case MembranePlateProperty membrane:
                    if (!(membrane.MassPerArea > 0)) return;
                    plate.MemThick = 1; plate.Density = units.SolverAreaDensity(membrane.MassPerArea);
                    return;
                default: throw new NotSupportedException("UnsupportedFormFindingPlateProperty: " + property.GetType().Name);
            }
        }

        // Loads: each load is applied as its resultant force on the beam, at its distance from the first node (TensileLib keeps it as a
        // fraction of the length), with the arithmetic of the former assembly; moments are not applied, as before.
        private const string BeamLoadPrefix = "Arachne distributed load a=";
        /// <summary>The source record of a distributed beam load: the distances a and b as typed (round-trip text).</summary>
        public static string BeamLoadRecord(double a, double b)
            => BeamLoadPrefix + a.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + " b=" + b.ToString("R", System.Globalization.CultureInfo.InvariantCulture);
        private static double DistanceFromEnd(BeamLoadAssignment a)
        {
            var record = a.SourceRecord;
            int at = record == null || !record.StartsWith(BeamLoadPrefix, StringComparison.Ordinal) ? -1 : record.IndexOf(" b=", StringComparison.Ordinal);
            if (at > 0 && double.TryParse(record.Substring(at + 3), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double b) && 1 - b == a.End)
                return b;
            return 1 - a.End;
        }

        private static void ApplyLoad(TBeam target, BeamLoadAssignment a, R.Vector3d v, FormFindingUnits units)
        {
            if (a == null || a.Eccentricity != null || a.IsEquivalentNodalRepresentation || a.Start < 0 || a.End > 1 || a.End < a.Start)
                throw new NotSupportedException("UnsupportedFormFindingBeamLoad");
            if (v.Length == 0) return;
            double force = units.ForceToN;
            if (a.Concentrated != null)
            {
                var p = a.Concentrated.ToGlobal();
                if (p.F1 == 0 && p.F2 == 0 && p.F3 == 0) return; // a moment
                target.AddLoad(p.F1 / force, p.F2 / force, p.F3 / force, v.Length * a.Start);
                return;
            }
            if (a.StartIntensity == null || a.EndIntensity == null) throw new ArgumentException("MissingBeamLoadIntensities");
            var s = a.StartIntensity.ToGlobal(); var e = a.EndIntensity.ToGlobal();
            if (s.F1 == 0 && s.F2 == 0 && s.F3 == 0 && e.F1 == 0 && e.F2 == 0 && e.F3 == 0 && (s.M1 != 0 || s.M2 != 0 || s.M3 != 0 || e.M1 != 0 || e.M2 != 0 || e.M3 != 0)) return;
            double perLength = units.ForceDensityToNPerMm;
            var pa = new R.Vector3d(s.F1 / perLength, s.F2 / perLength, s.F3 / perLength);
            var pb = new R.Vector3d(e.F1 / perLength, e.F2 / perLength, e.F3 / perLength);
            // The scalar intensities PA and PB along the load direction: the component along the largest axis (exact for loads along
            // a global axis or the gravity, as the components give).
            double Scalar(R.Vector3d q, R.Vector3d direction)
            {
                double ax = Math.Abs(direction.X), ay = Math.Abs(direction.Y), az = Math.Abs(direction.Z);
                return ax >= ay && ax >= az ? q.X / direction.X : ay >= az ? q.Y / direction.Y : q.Z / direction.Z;
            }
            R.Vector3d dir;
            bool projected = a.LengthConvention == BeamLoadLengthConvention.ProjectedLength;
            if (projected) dir = Rh(a.ProjectionPlaneNormal ?? throw new ArgumentException("MissingProjectionPlaneNormal"));
            else
            {
                var basis = pa.Length >= pb.Length ? pa : pb;
                if (basis.Length == 0) return;
                dir = basis; dir.Unitize();
                if (Math.Abs(dir.X) == 1 || Math.Abs(dir.Y) == 1 || Math.Abs(dir.Z) == 1) dir = new R.Vector3d(Math.Round(dir.X), Math.Round(dir.Y), Math.Round(dir.Z));
            }
            double PA = Scalar(pa, dir), PB = Scalar(pb, dir);
            double L = v.Length;
            double A = a.Start, B = DistanceFromEnd(a);
            double l = L - L * A - L * B;
            double d, q;
            if (PA == PB) { d = L * A + l / 2; q = PA * l; }
            else
            {
                double sum = PA + PB;
                d = L * A + (sum != 0 ? l * (PA + 2 * PB) / (3 * sum) : l / 2);
                q = sum * l / 2;
            }
            if (projected)
            {
                double cos = (v / v.Length) * dir;
                q *= Math.Sqrt(Math.Max(0, 1 - cos * cos));
            }
            var load = dir * q;
            target.AddLoad(load.X, load.Y, load.Z, d);
        }

        public sealed class Outcome
        {
            public Solver.ResultCodes Status;
            /// <summary>False when the solver stopped at its iteration limit: the reactions are not stored (FeMM).</summary>
            public bool Converged;
            /// <summary>The error of Linear step by step when its outer loop does not converge: no result (FeMM).</summary>
            public string Error;
        }

        /// <summary>The solvers as the former Form Find: Linear, Linear step by step (outer loop on the force densities) and Non-Linear.</summary>
        public static Outcome Solve(TModel native, FormFindingDefinition ff)
        {
            if (!Enum.IsDefined(typeof(FormFindingMethod), ff.Method)) throw new NotSupportedException("UnknownFormFindingMethod");
            int maxIterNumber = ff.MaxIterations; double tolerance = ff.RelativeTolerance;
            if (ff.Method != FormFindingMethod.SteppedLinearForceDensity)
            {
                ForceDensitySolver solver = ff.Method == FormFindingMethod.LinearForceDensity
                    ? (ForceDensitySolver)new LinearForceDensitySolver(native, maxIterNumber, tolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations)
                    : new NonLinearForceDensitySolver(native, maxIterNumber, tolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations);
                var status = solver.SolveForceDensity();
                return new Outcome { Status = status, Converged = status != Solver.ResultCodes.NotconvergedAtMaxIterationNumber };
            }
            var fixedAxialForceBeam = native.Beams.Where(j => j.HasNLFixedAxialForce).ToList();
            var fixedLengthBeam = native.Beams.Where(j => j.HasNLFixedLength).ToList();
            if (fixedLengthBeam.Any(b => !(b.NLFixedLength > 0))) throw new ArgumentException("The fixed lengths must be greater than 0");
            if (fixedAxialForceBeam.Any(b => b.TargetAxialForce == 0)) throw new ArgumentException("The fixed axial forces must be different from 0");
            var stepped = new LinearForceDensitySolver(native, maxIterNumber, tolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations);
            Solver.ResultCodes ret; bool exit; int count = 0;
            do
            {
                exit = true; count++;
                ret = stepped.SolveForceDensity();
                // The next steps go on from the force densities just found
                foreach (var beam in native.Beams) beam.TargetForceDensity = beam.ForceDensity;
                foreach (var bb in fixedLengthBeam)
                    if (Math.Abs((bb.Length - bb.NLFixedLength) / bb.NLFixedLength) > tolerance)
                    { exit = false; bb.TargetForceDensity = bb.TargetForceDensity * bb.Length / bb.NLFixedLength; }
                foreach (var bb in fixedAxialForceBeam)
                    if (Math.Abs((bb.AxialForce - bb.TargetAxialForce) / bb.TargetAxialForce) > tolerance)
                    {
                        if (bb.AxialForce == 0) throw new InvalidOperationException("A beam with fixed axial force has no force: its force density cannot be updated");
                        exit = false; bb.TargetForceDensity = bb.TargetForceDensity * bb.TargetAxialForce / bb.AxialForce;
                    }
                if (!exit && count >= maxIterNumber)
                    return new Outcome { Status = Solver.ResultCodes.NotconvergedAtMaxIterationNumber, Converged = false, Error = "Not converged. The maximum number of iteration has been reached." };
            } while (!exit);
            return new Outcome { Status = ret, Converged = ret != Solver.ResultCodes.NotconvergedAtMaxIterationNumber };
        }

        /// <summary>FeMM ComputeReactions: at a restrained node the reaction balances the beam forces and the loads the solver applied to it,
        /// along the restrained directions only; null for the nodes without restraints. Rhino vector arithmetic, as before.</summary>
        public static R.Vector3d?[] Reactions(TModel native)
        {
            var nodes = native.Nodes.ToList();
            var indexes = new Dictionary<int, int>(nodes.Count);
            var sums = new R.Vector3d[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i]; indexes.Add(node.Id, i);
                sums[i] = Rh(node.NodeForce) + Rh(node.BeamSelfWeightForce) + Rh(node.PlateSelfWeightForce) + Rh(node.BeamLoadForce) + Rh(node.PressureForce);
            }
            foreach (var beam in native.Beams)
            {
                var axis = new R.Vector3d(beam.Node2.X - beam.Node1.X, beam.Node2.Y - beam.Node1.Y, beam.Node2.Z - beam.Node1.Z);
                double l = axis.Length;
                if (l == 0) continue;
                var pull = axis * (beam.AxialForce / l);
                sums[indexes[beam.Node1.Id]] += pull; sums[indexes[beam.Node2.Id]] -= pull;
            }
            var reactions = new R.Vector3d?[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (!node.Tx && !node.Ty && !node.Tz) continue;
                reactions[i] = Rh(node.RestrainedPart(Gp(-sums[i])));
            }
            return reactions;
        }

        private static OriginalResultData Original(FormFindingUnits units, string order, params double[] values) => new OriginalResultData
        { Values = values.Select(v => (double?)v).ToArray(), ComponentOrder = order, Convention = "TensileLib document units", ReaderVersion = "Arachne.FormFinding/1", LengthToMm = units.LengthToMm };

        /// <summary>The results of the run on the graph (form-finding dataset, semantics FormFinding): final coordinates (as displacements
        /// from the input geometry, with the exact solver coordinates), the pre-loads of the beams with an objective, in its case, the
        /// pressure forces in their case and, when converged, the support reactions.</summary>
        /// <remarks>The dataset is bound to the fingerprint of the exported graph (the solver map of <paramref name="exported"/>): the
        /// model must be the one exported, unchanged. No analysis snapshot is captured: a form-found shape is a new design input, not an
        /// analysis for verification, and the snapshot (a second image of the whole input in the model) cost 59 of the 65 s of this step
        /// at 10,000 nodes. The results stay decoded: the projection reads them and the sidecar encodes them once.</remarks>
        public static void Attach(Model model, Exported exported, Outcome outcome, FormFindingInputBuilder.Input input)
        {
            if (model == null || exported?.Map == null || outcome == null || input == null) throw new ArgumentNullException();
            if (model.Guid != exported.Map.ModelGuid || model.Datasets.ContainsKey("form-finding") || model.Analysis != null) throw new InvalidOperationException("StaleFormFindingResult");
            string fingerprint = exported.Map.AnalysisFingerprint;
            var units = exported.Units; double length = units.LengthToMm, force = units.ForceToN;
            model.Datasets.Add("form-finding", new AnalysisDataset { Id = "form-finding", Program = "TensileLib", SolverVersion = "2.5.0.1", AnalysisId = "FormFinding/" + outcome.Status,
                InputFingerprint = fingerprint, NormalizedUnits = "N,mm,s,tonne", Semantics = AnalysisSemantics.FormFinding });
            ComponentAvailability[] Translations() => new[] { ComponentAvailability.Available, ComponentAvailability.Available, ComponentAvailability.Available, ComponentAvailability.NotApplicable, ComponentAvailability.NotApplicable, ComponentAvailability.NotApplicable };
            ResultState State(OriginalResultData original, ComponentAvailability[] components) => new ResultState { DatasetId = "form-finding", InputFingerprint = fingerprint,
                Step = "equilibrium", Semantics = AnalysisSemantics.FormFinding, SourceRecord = "TensileLib form-finding: " + outcome.Status, Components = components, Original = original };
            var native = exported.Solver;
            var reactions = outcome.Converged ? Reactions(native) : null;
            var resultCase = input.ReactionCase;
            int index = 0;
            foreach (var node in model.NodesElements.Values)
            {
                var n = native.Nodes[exported.Ids[node.Guid]];
                var samples = new List<INodeResultLocation>();
                var displacement = State(Original(units, "X,Y,Z", n.X, n.Y, n.Z), Translations()); displacement.SourceDisplacementReference = "initial";
                samples.Add(new NodeResultDisplacement(resultCase, new ResultDisplacement(n.X * length - node.Position.X, n.Y * length - node.Position.Y, n.Z * length - node.Position.Z, 0, 0, 0)) { State = displacement });
                var p = new R.Vector3d(n.PressureX, n.PressureY, n.PressureZ);
                if (p.Length != 0)
                {
                    if (input.PressureCase == null) throw new InvalidOperationException("MissingSavedPressureCase");
                    samples.Add(new NodeResultForces(input.PressureCase, new ResultBeamForces(p.Z * force, p.X * force, p.Y * force, 0, 0, 0, CoordinateSystem.Global))
                    { Kind = NodalForceKind.AppliedPressure, Body = ActionBody.OnNode, State = State(Original(units, "X,Y,Z", p.X, p.Y, p.Z), Translations()) });
                }
                if (reactions != null && reactions[index].HasValue)
                {
                    var r = reactions[index].Value;
                    samples.Add(new NodeResultForces(resultCase, new ResultBeamForces(r.Z * force, r.X * force, r.Y * force, 0, 0, 0, CoordinateSystem.Global))
                    { Kind = NodalForceKind.SupportReaction, Body = ActionBody.OnNode, State = State(Original(units, "X,Y,Z", r.X, r.Y, r.Z), Translations()) });
                }
                node.AddResult(new NodeResult(samples));
                index++;
            }
            foreach (var beam in model.BeamElements.Values)
            {
                if (!input.PreloadCases.TryGetValue(beam.Guid, out var preloadCase)) continue;
                var b = native.Beams[exported.Ids[beam.Guid]];
                var axis = new Vector3d(b.Node2.X - b.Node1.X, b.Node2.Y - b.Node1.Y, b.Node2.Z - b.Node1.Z);
                var axes = Axes(axis, new Point3d(b.Node1.X * length, b.Node1.Y * length, b.Node1.Z * length));
                var components = new[] { ComponentAvailability.Available, ComponentAvailability.NotApplicable, ComponentAvailability.NotApplicable, ComponentAvailability.NotApplicable, ComponentAvailability.NotApplicable, ComponentAvailability.NotApplicable };
                StationResultBeamForces Station(double at) => new StationResultBeamForces(preloadCase, new ResultBeamForces(b.AxialForce * force, 0, 0, 0, 0, 0, axes), at)
                { State = State(Original(units, "N", b.AxialForce), components) };
                beam.Results.Add(new BeamResult(new[] { Station(0), Station(1) }));
            }
        }

        private static CoordinateSystem Axes(Vector3d axis, Point3d origin)
        {
            var axial = new Vector3d(axis.X, axis.Y, axis.Z); axial.Unitize();
            var transverse = Math.Abs(axial.Z) < .9 ? new Vector3d(0, 0, 1).CrossProduct(axial) : new Vector3d(1, 0, 0).CrossProduct(axial); transverse.Unitize();
            return new CoordinateSystem(origin, transverse, axial.CrossProduct(transverse));
        }
    }
}
