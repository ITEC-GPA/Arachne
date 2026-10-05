using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;

namespace Arachne.Core
{
    public static class ModelBuilder
    {
        public static FormModel Build(IEnumerable<Cable> cables, IEnumerable<NodeCondition> conditions = null,
            IEnumerable<Point3d[]> faces = null, double tolerance = 1e-6)
        {
            Validation.Positive(tolerance, "Merge tolerance");
            if (cables == null) throw new ArgumentNullException(nameof(cables));
            var nodes = new List<FormNode>();
            var edges = new List<FormEdge>();
            var topology = new List<int[]>();
            var usedEdges = new HashSet<(int, int)>();
            var buckets = new Dictionary<(long, long, long), List<int>>();
            (long, long, long) Cell(Point3d p) => (Coordinate(p.X), Coordinate(p.Y), Coordinate(p.Z));
            long Coordinate(double value)
            {
                double scaled = Math.Floor(value / tolerance);
                if (Math.Abs(scaled) > 9e18) throw new ArgumentException("Coordinates are too large for the merge tolerance.");
                return (long)scaled;
            }
            int Find(Point3d point)
            {
                Validation.Point(point);
                var cell = Cell(point);
                for (int x=-1; x<=1; x++) for (int y=-1; y<=1; y++) for (int z=-1; z<=1; z++)
                    if (buckets.TryGetValue((cell.Item1+x, cell.Item2+y, cell.Item3+z), out var indices))
                        foreach (int index in indices)
                            if (Validation.Distance(nodes[index].Position, point) <= tolerance) return index;
                return -1;
            }
            int GetOrAdd(Point3d point)
            {
                int index = Find(point);
                if (index >= 0) return index;
                index = nodes.Count;
                nodes.Add(new FormNode { Position = new Point3d(point) });
                var cell = Cell(point);
                if (!buckets.TryGetValue(cell, out var bucket)) buckets.Add(cell, bucket = new List<int>());
                bucket.Add(index);
                return index;
            }
            foreach (var cable in cables)
            {
                if (cable == null) throw new ArgumentException("Null cable.");
                Validation.Point(cable.Start); Validation.Point(cable.End);
                Validation.Positive(cable.Target, "Cable target");
                if (!Enum.IsDefined(typeof(TargetKind), cable.TargetKind)) throw new ArgumentException("Unknown target type.");
                if (!Enum.IsDefined(typeof(MembraneDirection), cable.Direction)) throw new ArgumentException("Unknown membrane direction.");
                Validation.NonNegative(cable.TargetWeight, "Target weight");
                Validation.NonNegative(cable.Area, "Area"); Validation.NonNegative(cable.Density, "Density");
                if (cable.TargetKind == TargetKind.Curvature) Validation.Positive(cable.Chord, "Chord");
                if (cable.FixedLength.HasValue) Validation.Positive(cable.FixedLength.Value, "Fixed length");
                if (cable.FixedForce.HasValue) Validation.Positive(cable.FixedForce.Value, "Fixed force");
                if (cable.FixedLength.HasValue || cable.FixedForce.HasValue) Validation.Positive(cable.ConstraintWeight, "Constraint weight");
                foreach (var load in cable.Loads)
                {
                    if (load == null) throw new ArgumentException("Null cable load.");
                    Validation.Vector(load.Force); Validation.Finite(load.Fraction, "Load position");
                    if (load.Fraction < 0 || load.Fraction > 1) throw new ArgumentException("Cable load position must be in [0,1].");
                }
                int a = GetOrAdd(cable.Start), b = GetOrAdd(cable.End);
                if (a == b) throw new ArgumentException("Cable collapses at the merge tolerance.");
                if (!usedEdges.Add((Math.Min(a,b), Math.Max(a,b)))) throw new ArgumentException("Duplicate cable between the same nodes.");
                edges.Add(new FormEdge(a, b, cable.Copy()));
            }
            if (edges.Count == 0) throw new ArgumentException("At least one cable is required.");
            foreach (var condition in conditions ?? Enumerable.Empty<NodeCondition>())
            {
                if (condition == null) throw new ArgumentException("Null node condition.");
                int index = Find(condition.Position);
                if (index < 0) throw new ArgumentException("A node condition does not match any cable endpoint.");
                Validation.Vector(condition.Force); Validation.Vector(condition.PositionWeight);
                Validation.NonNegative(condition.PositionWeight.X, "Position weight X");
                Validation.NonNegative(condition.PositionWeight.Y, "Position weight Y");
                Validation.NonNegative(condition.PositionWeight.Z, "Position weight Z");
                var node = nodes[index];
                node.FixX |= condition.FixX; node.FixY |= condition.FixY; node.FixZ |= condition.FixZ;
                // Deterministic accumulation: no parallel writes to the same vector.
                node.Force = new Vector3d(node.Force.X + condition.Force.X, node.Force.Y + condition.Force.Y, node.Force.Z + condition.Force.Z);
                Validation.Vector(node.Force);
                node.PositionWeight = new Vector3d(Math.Max(node.PositionWeight.X, condition.PositionWeight.X), Math.Max(node.PositionWeight.Y, condition.PositionWeight.Y), Math.Max(node.PositionWeight.Z, condition.PositionWeight.Z));
            }
            var usedFaces = new HashSet<string>();
            foreach (var face in faces ?? Enumerable.Empty<Point3d[]>())
            {
                if (face == null || (face.Length != 3 && face.Length != 4)) throw new ArgumentException("Faces must have 3 or 4 vertices.");
                var indices = face.Select(Find).ToArray();
                if (indices.Any(i => i < 0)) throw new ArgumentException("Face vertices must match cable endpoints.");
                if (indices.Distinct().Count() != indices.Length) throw new ArgumentException("Degenerate face.");
                if (!usedFaces.Add(string.Join(",", indices.OrderBy(i => i)))) throw new ArgumentException("Duplicate face.");
                topology.Add(indices);
            }
            return new FormModel(nodes, edges, topology, tolerance);
        }
    }
}
