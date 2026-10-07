using Arachne.Common.Models;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Common.Helpers
{
    /// <summary>
    /// Checks a model before the form finding. The errors make the solver fail (singular
    /// system) or stop; the warnings point out probable mistakes.
    /// </summary>
    public static class ModelChecks
    {
        public class Report
        {
            public List<string> Errors { get; } = new List<string>();

            public List<string> Warnings { get; } = new List<string>();

            /// <summary>
            /// The groups of nodes connected by beams that carry a force.
            /// </summary>
            public int Parts { get; set; }

            public bool IsValid => Errors.Count == 0;
        }

        private const int MaxListedIds = 10;

        public static Report Check(ModelModel model, double tolerance)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));

            Report report = new Report();
            List<NodeModel> nodes = model.Nodes;
            List<BeamModel> beams = model.Beams;

            if (beams.Count == 0)
                report.Errors.Add("The model has no beams.");

            RTree tree = new RTree();
            for (int i = 0; i < nodes.Count; i++)
                tree.Insert(nodes[i].Position, i);

            int FindNode(Point3d point)
            {
                int found = -1;
                double best = double.MaxValue;
                tree.Search(new Sphere(point, tolerance), (sender, e) =>
                {
                    double distance = nodes[e.Id].Position.DistanceTo(point);
                    if (distance < best)
                    {
                        best = distance;
                        found = e.Id;
                    }
                });
                return found;
            }

            // Restraints: the form finding reads the first restraint of each node
            NodeRestraintAttributeModel.AttributeData[] restraints = new NodeRestraintAttributeModel.AttributeData[nodes.Count];
            List<string> multipleRestraints = new List<string>();
            for (int i = 0; i < nodes.Count; i++)
            {
                restraints[i] = NodeRestraintAttributeModel.GetRestraint(nodes[i]);
                if (nodes[i].Attributes.Count(a => a.GetType() == typeof(NodeRestraintAttributeModel)) > 1)
                    multipleRestraints.Add(nodes[i].NodeId);
            }
            bool Tx(int i) => restraints[i] != null && restraints[i].Tx;
            bool Ty(int i) => restraints[i] != null && restraints[i].Ty;
            bool Tz(int i) => restraints[i] != null && restraints[i].Tz;

            // Beams: only the beams with a force density, a fixed axial force or a fixed length
            // enter the solver system and connect their nodes
            int[] parent = Enumerable.Range(0, nodes.Count).ToArray();
            int Root(int i)
            {
                while (parent[i] != i)
                {
                    parent[i] = parent[parent[i]];
                    i = parent[i];
                }
                return i;
            }

            bool[] connected = new bool[nodes.Count];
            Dictionary<(int, int), string> joined = new Dictionary<(int, int), string>();
            List<string> withoutForce = new List<string>();
            List<string> notPositive = new List<string>();
            List<string> multipleDensities = new List<string>();
            foreach (BeamModel beam in beams)
            {
                List<ForceDensityModel> densities = beam.Loads.Where(l => l.GetType() == typeof(ForceDensityModel)).Cast<ForceDensityModel>().ToList();
                List<FixedAxialForceModel> axialForces = beam.Loads.Where(l => l.GetType() == typeof(FixedAxialForceModel)).Cast<FixedAxialForceModel>().ToList();
                List<FixedLengthModel> lengths = beam.Loads.Where(l => l.GetType() == typeof(FixedLengthModel)).Cast<FixedLengthModel>().ToList();

                if (densities.Count > 1)
                    multipleDensities.Add(beam.BeamId);
                if (densities.Count > 0 && densities[0].Value <= 0)
                    notPositive.Add(beam.BeamId);
                if (axialForces.Count > 0 && axialForces[0].Value == 0)
                    report.Errors.Add($"Beam {beam.BeamId} has a fixed axial force equal to 0.");
                if (lengths.Count > 0 && !(lengths[0].Value > 0))
                    report.Errors.Add($"Beam {beam.BeamId} has a fixed length not greater than 0.");

                bool carriesForce = (densities.Count > 0 && densities[0].Value != 0) ||
                    (axialForces.Count > 0 && axialForces[0].Value != 0) || lengths.Count > 0;
                if (!carriesForce)
                    withoutForce.Add(beam.BeamId);

                if (beam.PointFrom.DistanceTo(beam.PointTo) <= tolerance)
                {
                    report.Errors.Add($"Beam {beam.BeamId} has zero length.");
                    continue;
                }

                int a = FindNode(beam.PointFrom);
                int b = FindNode(beam.PointTo);
                if (a < 0 || b < 0)
                {
                    report.Errors.Add($"Beam {beam.BeamId} has an end without node: build the model with the Build Model component.");
                    continue;
                }
                if (a == b)
                {
                    report.Errors.Add($"Beam {beam.BeamId} starts and ends on node {nodes[a].NodeId}.");
                    continue;
                }

                (int, int) key = a < b ? (a, b) : (b, a);
                if (joined.TryGetValue(key, out string other))
                    report.Warnings.Add($"Beams {other} and {beam.BeamId} join the same nodes.");
                else
                    joined.Add(key, beam.BeamId);

                if (carriesForce)
                {
                    connected[a] = true;
                    connected[b] = true;
                    int ra = Root(a);
                    int rb = Root(b);
                    if (ra != rb)
                        parent[ra] = rb;
                }
            }

            if (withoutForce.Count > 0)
                report.Warnings.Add($"{withoutForce.Count} beams have no force density, fixed axial force or fixed length and carry no force: {List(withoutForce)}.");
            if (notPositive.Count > 0)
                report.Warnings.Add($"{notPositive.Count} beams have a force density value not greater than 0 (compression or no force): {List(notPositive)}.");
            if (multipleDensities.Count > 0)
                report.Warnings.Add($"{multipleDensities.Count} beams have more than one force density, the form finding uses the first: {List(multipleDensities)}.");
            if (multipleRestraints.Count > 0)
                report.Warnings.Add($"{multipleRestraints.Count} nodes have more than one restraint, the form finding uses the first: {List(multipleRestraints)}.");

            List<string> isolated = new List<string>();
            List<string> isolatedFixed = new List<string>();
            Dictionary<int, List<int>> parts = new Dictionary<int, List<int>>();
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!connected[i])
                {
                    if (Tx(i) && Ty(i) && Tz(i))
                        isolatedFixed.Add(nodes[i].NodeId);
                    else
                        isolated.Add(nodes[i].NodeId);
                    continue;
                }
                int root = Root(i);
                if (!parts.TryGetValue(root, out List<int> part))
                    parts.Add(root, part = new List<int>());
                part.Add(i);
            }
            report.Parts = parts.Count;

            // Supports: every part needs a restraint along each axis, otherwise its position
            // along that axis is undetermined and the solver system is singular
            if (nodes.Count > 0 && !Enumerable.Range(0, nodes.Count).Any(i => Tx(i) || Ty(i) || Tz(i)))
            {
                report.Errors.Add("No node is restrained: add the supports with the Node Restraints component.");
            }
            else
            {
                if (isolated.Count > 0)
                    report.Errors.Add($"{isolated.Count} nodes have no beam that carries a force and are not fully restrained: {List(isolated)}.");
                if (isolatedFixed.Count > 0)
                    report.Warnings.Add($"{isolatedFixed.Count} restrained nodes have no beam that carries a force: {List(isolatedFixed)}.");

                foreach (List<int> part in parts.Values)
                {
                    // The part can move along the directions that no restraint holds; with the
                    // axes of a plane they can be inclined
                    List<Vector3d> free = FreeDirections(part.Where(i => restraints[i] != null).SelectMany(i => restraints[i].RestrainedDirections()));
                    if (free.Count > 0)
                        report.Errors.Add($"The part with node {nodes[part[0]].NodeId} ({part.Count} nodes) has no restraint along {string.Join(", ", free.Select(Name))}.");
                }
            }

            return report;
        }

        private const double DirectionTolerance = 1e-9;

        /// <summary>
        /// An orthonormal basis of the directions perpendicular to all the restrained ones:
        /// X, Y and Z when they are among them, in this order
        /// </summary>
        private static List<Vector3d> FreeDirections(IEnumerable<Vector3d> restrained)
        {
            // Gram-Schmidt on the restrained directions, then on X, Y, Z
            List<Vector3d> basis = new List<Vector3d>();
            void Add(Vector3d direction)
            {
                foreach (Vector3d other in basis)
                    direction -= other * (direction * other);
                if (direction.Length > DirectionTolerance)
                {
                    direction.Unitize();
                    basis.Add(direction);
                }
            }
            foreach (Vector3d direction in restrained)
                Add(direction);
            int restrainedCount = basis.Count;
            foreach (Vector3d axis in new[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis })
                Add(axis);
            return basis.Skip(restrainedCount).ToList();
        }

        // X, Y, Z or the components of an inclined direction
        private static string Name(Vector3d direction)
        {
            string[] names = { "X", "Y", "Z" };
            for (int i = 0; i < 3; i++)
            {
                if (Math.Abs(Math.Abs(direction[i]) - 1) <= DirectionTolerance)
                    return names[i];
            }
            string Component(double value) => (Math.Abs(value) < 5e-4 ? 0 : value).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
            return $"({Component(direction.X)}, {Component(direction.Y)}, {Component(direction.Z)})";
        }

        private static string List(List<string> ids)
        {
            string text = string.Join(", ", ids.Take(MaxListedIds));
            if (ids.Count > MaxListedIds)
                text += ", ...";
            return text;
        }
    }
}
