using Arachne.Common.Analysis;
using GPC.Model.Analysis;
using GPC.Model.Elements;
using GPC.Model.Restraints;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using GModel = GPC.Model.Models.Model;

namespace Arachne.Common.Helpers
{
    /// <summary>
    /// Checks a model before the form finding. The errors make the solver fail (singular system) or stop; the warnings point out
    /// probable mistakes. Same checks and messages as before, on the GPC model of Build Model (lengths in the document units).
    /// </summary>
    public static class ModelChecks
    {
        public class Report
        {
            public List<string> Errors { get; } = new List<string>();
            public List<string> Warnings { get; } = new List<string>();
            /// <summary>The groups of nodes connected by beams that carry a force.</summary>
            public int Parts { get; set; }
            public bool IsValid => Errors.Count == 0;
        }

        private const int MaxListedIds = 10;

        public static Report Check(GModel model, ModelUnits units, double tolerance)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var u = FormFindingUnits.From(units);
            var report = new Report();
            var nodes = model.NodesElements.Values.ToList();
            var beams = model.BeamElements.Values.ToList();
            var index = new Dictionary<Guid, int>();
            for (int i = 0; i < nodes.Count; i++) index[nodes[i].Guid] = i;
            Point3d At(NodeElement n) => new Point3d(n.Position.X / u.LengthToMm, n.Position.Y / u.LengthToMm, n.Position.Z / u.LengthToMm);

            if (beams.Count == 0) report.Errors.Add("The model has no beams.");

            // Restraints: the form finding reads the first restraint of each node
            var restraints = new NodeRestrain[nodes.Count];
            var multipleRestraints = new List<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                restraints[i] = nodes[i].Assignments.Restrains.FirstOrDefault()?.Restrain;
                if (nodes[i].Assignments.Restrains.Count > 1) multipleRestraints.Add(nodes[i].Name);
            }
            bool Restrained(int i, GeometryRestrain.DOF dof) => restraints[i] != null && restraints[i].Restrains.Any(d => d.Dof == dof && d.IsRestrained);
            bool Tx(int i) => Restrained(i, GeometryRestrain.DOF.DX);
            bool Ty(int i) => Restrained(i, GeometryRestrain.DOF.DY);
            bool Tz(int i) => Restrained(i, GeometryRestrain.DOF.DZ);
            IEnumerable<Vector3d> Directions(int i)
            {
                var r = restraints[i];
                var axes = ElementDrafts.IsGlobalRestraint(r) ? new[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis }
                    : new[] { r.CoordinateSystem.V1, r.CoordinateSystem.V2, r.CoordinateSystem.V3 }.Select(v => new Vector3d(v.X, v.Y, v.Z)).ToArray();
                var restrained = new[] { Tx(i), Ty(i), Tz(i) };
                return axes.Where((axis, k) => restrained[k]);
            }

            // Beams: only the beams with a force density, a fixed axial force or a fixed length enter the solver system and connect their nodes
            int[] parent = Enumerable.Range(0, nodes.Count).ToArray();
            int Root(int i) { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; }
            var connected = new bool[nodes.Count];
            var joined = new Dictionary<(int, int), string>();
            var withoutForce = new List<string>(); var notPositive = new List<string>(); var multipleDensities = new List<string>();
            foreach (var beam in beams)
            {
                var goals = ElementDrafts.Goals(beam).ToList();
                var densities = goals.Where(g => ElementDrafts.IsPrimary(g.Goal.Kind)).ToList();
                var axialForces = goals.Where(g => g.Goal.Kind == FormFindingGoalKind.AxialForce).ToList();
                var lengths = goals.Where(g => g.Goal.Kind == FormFindingGoalKind.Length).ToList();
                if (densities.Count > 1) multipleDensities.Add(beam.Name);
                if (densities.Count > 0 && densities[0].Goal.Target <= 0) notPositive.Add(beam.Name);
                if (axialForces.Count > 0 && axialForces[0].Goal.Target == 0) report.Errors.Add($"Beam {beam.Name} has a fixed axial force equal to 0.");
                if (lengths.Count > 0 && !(lengths[0].Goal.Target > 0)) report.Errors.Add($"Beam {beam.Name} has a fixed length not greater than 0.");
                bool carriesForce = (densities.Count > 0 && densities[0].Goal.Target != 0) || (axialForces.Count > 0 && axialForces[0].Goal.Target != 0) || lengths.Count > 0;
                if (!carriesForce) withoutForce.Add(beam.Name);
                if (beam.NodeI == null || beam.NodeJ == null || !index.ContainsKey(beam.NodeI.Guid) || !index.ContainsKey(beam.NodeJ.Guid))
                { report.Errors.Add($"Beam {beam.Name} has an end without node: build the model with the Build Model component."); continue; }
                if (At(beam.NodeI).DistanceTo(At(beam.NodeJ)) <= tolerance) { report.Errors.Add($"Beam {beam.Name} has zero length."); continue; }
                int a = index[beam.NodeI.Guid], b = index[beam.NodeJ.Guid];
                if (a == b) { report.Errors.Add($"Beam {beam.Name} starts and ends on node {nodes[a].Name}."); continue; }
                var key = a < b ? (a, b) : (b, a);
                if (joined.TryGetValue(key, out string other)) report.Warnings.Add($"Beams {other} and {beam.Name} join the same nodes.");
                else joined.Add(key, beam.Name);
                if (carriesForce)
                {
                    connected[a] = true; connected[b] = true;
                    int ra = Root(a), rb = Root(b);
                    if (ra != rb) parent[ra] = rb;
                }
            }

            if (withoutForce.Count > 0) report.Warnings.Add($"{withoutForce.Count} beams have no force density, fixed axial force or fixed length and carry no force: {List(withoutForce)}.");
            if (notPositive.Count > 0) report.Warnings.Add($"{notPositive.Count} beams have a force density value not greater than 0 (compression or no force): {List(notPositive)}.");
            if (multipleDensities.Count > 0) report.Warnings.Add($"{multipleDensities.Count} beams have more than one force density, the form finding uses the first: {List(multipleDensities)}.");
            if (multipleRestraints.Count > 0) report.Warnings.Add($"{multipleRestraints.Count} nodes have more than one restraint, the form finding uses the first: {List(multipleRestraints)}.");

            var isolated = new List<string>(); var isolatedFixed = new List<string>();
            var parts = new Dictionary<int, List<int>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!connected[i]) { if (Tx(i) && Ty(i) && Tz(i)) isolatedFixed.Add(nodes[i].Name); else isolated.Add(nodes[i].Name); continue; }
                int root = Root(i);
                if (!parts.TryGetValue(root, out var part)) parts.Add(root, part = new List<int>());
                part.Add(i);
            }
            report.Parts = parts.Count;

            // Supports: every part needs a restraint along each axis, otherwise its position along that axis is undetermined
            if (nodes.Count > 0 && !Enumerable.Range(0, nodes.Count).Any(i => Tx(i) || Ty(i) || Tz(i)))
                report.Errors.Add("No node is restrained: add the supports with the Node Restraints component.");
            else
            {
                if (isolated.Count > 0) report.Errors.Add($"{isolated.Count} nodes have no beam that carries a force and are not fully restrained: {List(isolated)}.");
                if (isolatedFixed.Count > 0) report.Warnings.Add($"{isolatedFixed.Count} restrained nodes have no beam that carries a force: {List(isolatedFixed)}.");
                foreach (var part in parts.Values)
                {
                    var free = FreeDirections(part.Where(i => restraints[i] != null).SelectMany(Directions));
                    if (free.Count > 0) report.Errors.Add($"The part with node {nodes[part[0]].Name} ({part.Count} nodes) has no restraint along {string.Join(", ", free.Select(Name))}.");
                }
            }
            return report;
        }

        private const double DirectionTolerance = 1e-9;

        /// <summary>An orthonormal basis of the directions perpendicular to all the restrained ones: X, Y and Z when they are among them, in this order</summary>
        private static List<Vector3d> FreeDirections(IEnumerable<Vector3d> restrained)
        {
            var basis = new List<Vector3d>();
            void Add(Vector3d direction)
            {
                foreach (var other in basis) direction -= other * (direction * other);
                if (direction.Length > DirectionTolerance) { direction.Unitize(); basis.Add(direction); }
            }
            foreach (var direction in restrained) Add(direction);
            int restrainedCount = basis.Count;
            foreach (var axis in new[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis }) Add(axis);
            return basis.Skip(restrainedCount).ToList();
        }

        // X, Y, Z or the components of an inclined direction
        private static string Name(Vector3d direction)
        {
            string[] names = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++) if (Math.Abs(Math.Abs(direction[i]) - 1) <= DirectionTolerance) return names[i];
            string Component(double value) => (Math.Abs(value) < 5e-4 ? 0 : value).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return $"({Component(direction.X)}, {Component(direction.Y)}, {Component(direction.Z)})";
        }

        private static string List(List<string> ids)
        {
            string text = string.Join(", ", ids.Take(MaxListedIds));
            if (ids.Count > MaxListedIds) text += ", ...";
            return text;
        }
    }
}
