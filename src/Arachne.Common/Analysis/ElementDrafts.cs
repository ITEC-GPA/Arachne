using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Attributes;
using GPC.Model.Core;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Loads.Assignments;
using GPC.Model.Restraints;
using GPC.Model.Structure.Assignments;
using GSection = GPC.Model.Sections.Section;

namespace Arachne.Common.Analysis
{
    /// <summary>Stable identities of the Grasshopper inputs: owner (component instance) and key (output address, topology).</summary>
    public static class InputIdentity
    {
        private static readonly Guid Definitions = new Guid("6a2c1f3e-9d4b-4f17-8b7a-2e5d0c9a4f61");
        public static Guid Of(Guid owner, string key)
        {
            if (owner == Guid.Empty) throw new ArgumentException("EmptyInputOwner");
            using (var sha = SHA256.Create()) return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes(owner.ToString("D") + "\n" + key)).Take(16).ToArray());
        }
        /// <summary>A load case is identified by its id and name, as in FeMM (two Load Case components with the same name give the same case).</summary>
        public static LoadCaseBase LoadCase(string id, string name) => new LoadCaseBase(Of(Definitions, "case:" + (id ?? "") + "\n" + (name ?? "")), name ?? "");
        /// <summary>A group is identified by its full name (parent path), as in FeMM.</summary>
        public static Group Group(string name, Group parent) => GPC.Model.Attributes.Group.Create(Of(Definitions, "group:" + FullName(parent) + "/" + name), name, parent);
        public static string FullName(Group group) => group == null ? "" : FullName(group.Parent) + "/" + group.Name;
    }

    /// <summary>
    /// The GPC elements of the Grasshopper data before Build Model ("drafts"): their quantities are in the document units chosen in
    /// Build Model, which is the unit boundary (correzioni-P2A.md). Helpers to read their Arachne inputs, to merge two drafts of one
    /// identity as FeMM merged its elements, and to scale them to N-mm (Build Model) and back (Decompose Model).
    /// </summary>
    public static class ElementDrafts
    {
        public static string Label(Element element) => element?.Attributes.Values.OfType<ElementLabelAttribute>().FirstOrDefault()?.Label;
        public static bool HasLabel(Element element, string label) => Label(element) == label && label != null;
        public static IEnumerable<FormFindingGoalAttribute> Goals(Element element) => element.Attributes.Values.OfType<FormFindingGoalAttribute>();
        public static RestrainAssignment Restraint(NodeElement node) => node.Assignments.Restrains.FirstOrDefault();

        /// <summary>The kinds of FeMM's Force Density (Force density, Cable, Membrane, Curvature): one primary objective per beam.</summary>
        public static bool IsPrimary(FormFindingGoalKind kind) => kind == FormFindingGoalKind.ForceDensity || kind == FormFindingGoalKind.CableForce
            || kind == FormFindingGoalKind.MembraneForcePerWidth || kind == FormFindingGoalKind.CurvatureRatio;

        // ---- Content keys: the value equality of FeMM's loads and attributes (Merge kept one copy of equal items) ----
        private static string N(double v) => BitConverter.DoubleToInt64Bits(v).ToString("X16", CultureInfo.InvariantCulture);
        private static string V(Vector3d v) => v == null ? "-" : N(v.X) + "," + N(v.Y) + "," + N(v.Z);
        private static string P(Point3d p) => p == null ? "-" : N(p.X) + "," + N(p.Y) + "," + N(p.Z);
        private static string Case(Load load) => load?.LoadCase == null ? "-" : load.LoadCase.Guid.ToString("N");
        private static string Cs(CoordinateSystem cs) => cs == null ? "-" : P(cs.Origin) + "|" + V(cs.V1) + "|" + V(cs.V2);
        public static string Key(Load load)
        {
            if (load is PointLoad p) return "P:" + Case(p) + ":" + N(p.F1) + "," + N(p.F2) + "," + N(p.F3) + "," + N(p.M1) + "," + N(p.M2) + "," + N(p.M3);
            return load.GetType().Name + ":" + load.Guid;
        }
        public static string Key(BeamLoadAssignment a)
        {
            string Line(LineLoad l) => l == null ? "-" : Case(l) + ":" + N(l.F1) + "," + N(l.F2) + "," + N(l.F3) + "," + N(l.M1) + "," + N(l.M2) + "," + N(l.M3);
            return N(a.Start) + "," + N(a.End) + ":" + (a.Concentrated == null ? "-" : Key(a.Concentrated)) + ":" + Line(a.StartIntensity) + ":" + Line(a.EndIntensity)
                + ":" + a.LengthConvention + ":" + V(a.ProjectionPlaneNormal);
        }
        public static string Key(GPC.Model.Attributes.Attribute attribute)
        {
            switch (attribute)
            {
                case ElementLabelAttribute l: return "L:" + l.Label;
                case FormFindingCoordinateAttribute c: return "C:" + V(c.Weights);
                case FormFindingGoalAttribute g:
                    // FeMM's equality of Force Density ignored the membrane type; the other objectives compared case, value and weight.
                    return "G:" + g.Goal.Kind + ":" + (g.LoadCase == null ? "-" : g.LoadCase.Guid.ToString("N")) + ":" + N(g.Goal.Target) + ":" + N(g.Goal.Weight)
                        + ":" + g.Goal.ProjectToPlane + ":" + (g.Goal.ChordLengthMm.HasValue ? N(g.Goal.ChordLengthMm.Value) : "-");
                default: return attribute.GetType().Name + ":" + attribute.Guid;
            }
        }
        public static string Key(RestrainAssignment r)
            => Cs(r.Restrain?.CoordinateSystem) + ":" + string.Join(",", (r.Restrain?.Restrains ?? new List<DofRestrain>()).Select(d => d.Dof + "=" + d.IsRestrained));

        /// <summary>FeMM's ElementModel.Merge: groups, loads, attributes and results of <paramref name="other"/> that the target does not
        /// have (by value) are added, in their order. <paramref name="other"/> is not changed.</summary>
        public static void Merge(Element target, Element other)
        {
            other = ModelValues.CopyValue(other, ModelCopyOptions.ShareProperties);
            foreach (var group in other.Groups.Values) if (!target.Groups.Values.Any(g => g.Guid == group.Guid)) target.AddGroup(group);
            var loads = new HashSet<string>(target.Loads.Values.Select(Key));
            foreach (var load in other.Loads.Values.ToArray())
                if (loads.Add(Key(load))) target.Loads.Add(Fresh(load, (target as NodeElement)?.Position));
            var attributes = new HashSet<string>(target.Attributes.Values.Select(Key));
            foreach (var attribute in other.Attributes.Values.ToArray()) if (attributes.Add(Key(attribute))) target.Attributes.Add(Fresh(attribute));
            if (target is NodeElement t && other is NodeElement o)
            {
                var restraints = new HashSet<string>(t.Assignments.Restrains.Select(Key));
                foreach (var r in o.Assignments.Restrains)
                    if (restraints.Add(Key(r)))
                        t.Assignments.Restrains.Add(new RestrainAssignment { Restrain = Rebind(r.Restrain, t) });
            }
            if (target is BeamElement tb && other is BeamElement ob)
            {
                var beamLoads = new HashSet<string>(tb.Assignments.Loads.Select(Key));
                foreach (var a in ob.Assignments.Loads) if (beamLoads.Add(Key(a))) tb.Assignments.Loads.Add(a);
            }
            var results = new HashSet<GPC.Model.Results.Storage.ElementResult>(target.Results);
            foreach (var result in other.Results) if (!results.Contains(result)) target.AddResult(result);
        }

        // The items of another element keep its ids: they enter the collections of the target as new items.
        private static Load Fresh(Load load, Point3d at)
        {
            if (load is PointLoad p) return new PointLoad(p.F1, p.F2, p.F3, p.M1, p.M2, p.M3, at ?? p.Point, p.LoadCase, p.CoordinateSystem ?? CoordinateSystem.Global);
            throw new NotSupportedException("UnsupportedArachneLoad: " + load.GetType().Name);
        }
        private static GPC.Model.Attributes.Attribute Fresh(GPC.Model.Attributes.Attribute attribute)
        {
            switch (attribute)
            {
                case ElementLabelAttribute l: return new ElementLabelAttribute(l.Label);
                case FormFindingCoordinateAttribute c: return new FormFindingCoordinateAttribute(c.Weights);
                case FormFindingGoalAttribute g: return new FormFindingGoalAttribute(g.Goal, g.LoadCase);
                default: throw new NotSupportedException("UnsupportedArachneAttribute: " + attribute.GetType().Name);
            }
        }

        public static NodeRestrain Rebind(NodeRestrain restrain, NodeElement node)
        {
            var dofs = ModelValues.CopyValue(restrain.Restrains.ToList());
            var cs = restrain.CoordinateSystem;
            return IsGlobalRestraint(restrain) ? new NodeRestrain(node, dofs) : new NodeRestrain(node, new CoordinateSystem(node.Position, cs.V1, cs.V2), dofs);
        }
        /// <summary>True when the restraint acts along the global axes (FeMM: no coordinate system given).</summary>
        // A copy of CoordinateSystem.Global is another instance: the global system is recognized by its name and axes. A plane given to
        // Restraints is never global, even along the world axes (FeMM then used the axes of the plane).
        public static bool IsGlobalRestraint(NodeRestrain restrain)
        {
            var cs = restrain.CoordinateSystem;
            if (cs == null || ReferenceEquals(cs, CoordinateSystem.Global)) return true;
            var g = CoordinateSystem.Global;
            return cs.Name == g.Name && cs.V1.X == 1 && cs.V1.Y == 0 && cs.V1.Z == 0 && cs.V2.X == 0 && cs.V2.Y == 1 && cs.V2.Z == 0;
        }

        // ---- Units: drafts in the document units <-> N, mm ----

        public sealed class Scale
        {
            public readonly double L, F;
            public readonly bool Identity;
            private readonly Dictionary<Guid, BeamProperty> beams = new Dictionary<Guid, BeamProperty>();
            private readonly Dictionary<Guid, PlateProperty> plates = new Dictionary<Guid, PlateProperty>();
            private readonly Dictionary<Guid, LoadCaseBase> cases = new Dictionary<Guid, LoadCaseBase>();
            /// <summary>The document units to N-mm (forward) or back.</summary>
            public Scale(FormFindingUnits units, bool toMm)
            { L = toMm ? units.LengthToMm : 1 / units.LengthToMm; F = toMm ? units.ForceToN : 1 / units.ForceToN; Identity = units.LengthToMm == 1 && units.ForceToN == 1; }
            public double Length(double v) => Identity ? v : v * L;
            public Point3d Point(Point3d p) => Identity ? p : new Point3d(p.X * L, p.Y * L, p.Z * L);
            public Point2d Point(Point2d p) => Identity ? p : new Point2d(p.X * L, p.Y * L);

            public void Apply(NodeElement node)
            {
                if (Identity) return;
                node.Position = Point(node.Position);
                foreach (var r in node.Assignments.Restrains)
                    if (r.Restrain != null && !IsGlobalRestraint(r.Restrain)) r.Restrain = Rebind(r.Restrain, node);
                foreach (var load in node.Loads.Values) Apply(load);
            }
            public void Apply(Load load)
            {
                if (Identity) return;
                if (load is PointLoad p) { p.F1 *= F; p.F2 *= F; p.F3 *= F; p.M1 *= F * L; p.M2 *= F * L; p.M3 *= F * L; p.Point = Point(p.Point); }
                else if (load is LineLoad l) { l.F1 *= F / L; l.F2 *= F / L; l.F3 *= F / L; l.M1 *= F; l.M2 *= F; l.M3 *= F; l.Line = new Line3d(Point(l.Line.Start), Point(l.Line.End)); }
                else throw new NotSupportedException("UnsupportedArachneLoad: " + load.GetType().Name);
            }
            /// <summary>The beam's own quantities (loads, offset, objectives, property); its nodes are scaled with the nodes.</summary>
            public void Apply(BeamElement beam)
            {
                if (Identity) return;
                if (beam.NodeI == null) { beam.StartPoint = Point(beam.StartPoint); beam.EndPoint = Point(beam.EndPoint); }
                foreach (var a in beam.Assignments.Loads)
                {
                    if (a.Concentrated != null) Apply(a.Concentrated);
                    if (a.StartIntensity != null) Apply(a.StartIntensity);
                    if (a.EndIntensity != null && !ReferenceEquals(a.EndIntensity, a.StartIntensity)) Apply(a.EndIntensity);
                }
                if (beam.Assignments.SectionCentroidOffset != null)
                    beam.Assignments.SectionCentroidOffset = new Vector2d(beam.Assignments.SectionCentroidOffset.X * L, beam.Assignments.SectionCentroidOffset.Y * L);
                foreach (var g in Goals(beam)) Apply(g.Goal);
                beam.BeamProperty = Property(beam.BeamProperty);
            }
            /// <summary>A whole draft (beam with its own end nodes, plate with its vertices), e.g. a decomposed element.</summary>
            public void Apply(BeamElement beam, bool withNodes)
            {
                if (Identity) return;
                if (withNodes && beam.NodeI != null) { Apply(beam.NodeI); if (!ReferenceEquals(beam.NodeI, beam.NodeJ)) Apply(beam.NodeJ); }
                Apply(beam);
            }
            public void Apply(AreaElement area, bool withNodes)
            {
                if (Identity) return;
                if (withNodes) foreach (var node in area.Nodes.Distinct()) Apply(node);
                Apply(area);
            }
            public void Apply(FormFindingGoal goal)
            {
                if (Identity) return;
                switch (goal.Kind)
                {
                    case FormFindingGoalKind.ForceDensity: case FormFindingGoalKind.MembraneForcePerWidth: goal.Target *= F / L; break;
                    case FormFindingGoalKind.CableForce: case FormFindingGoalKind.AxialForce: goal.Target *= F; break;
                    case FormFindingGoalKind.Length: goal.Target *= L; break;
                }
                if (goal.ChordLengthMm.HasValue) goal.ChordLengthMm = goal.ChordLengthMm.Value * L;
            }
            public void Apply(AreaElement area)
            {
                if (Identity) return;
                if (area.Nodes.Count == 0) area.Shape = new Shape(new Polygon3d(area.Points.Select(Point)));
                area.PlateProperty = Property(area.PlateProperty);
            }
            public BeamProperty Property(BeamProperty property)
            {
                if (Identity || property == null) return property;
                if (beams.TryGetValue(property.Guid, out var done)) return done;
                if (!(property is ElasticBeamProperty elastic)) throw new NotSupportedException("UnsupportedArachneBeamProperty: " + property.GetType().Name);
                // Copy on modify: a new property (same identity, the one of every element that cites the original in these drafts)
                // with a new section; the material, unchanged, is shared.
                var scaled = ModelValues.CopyValue(elastic, ModelCopyOptions.ShareProperties);
                scaled.Section = Section(elastic.Section as GSection);
                beams.Add(property.Guid, scaled); return scaled;
            }
            public PlateProperty Property(PlateProperty property)
            {
                if (Identity || property == null) return property;
                if (plates.TryGetValue(property.Guid, out var done)) return done;
                PlateProperty scaled = ModelValues.CopyValue(property, ModelCopyOptions.ShareProperties);
                if (scaled is ElasticPlateProperty elastic) { elastic.BendingThickness *= L; elastic.MembraneThickness *= L; }
                else if (!(scaled is MembranePlateProperty)) throw new NotSupportedException("UnsupportedArachnePlateProperty: " + property.GetType().Name);
                plates.Add(property.Guid, scaled); return scaled;
            }
            public GSection Section(GSection s)
            {
                if (s == null) throw new NotSupportedException("UnsupportedArachneSection");
                double l2 = L * L, l3 = l2 * L, l4 = l2 * l2;
                return new GSection(Outline(s.Shape), s.Area * l2, s.Jxx * l4, s.Jyy * l4, s.Jxy * l4, s.J11 * l4, s.J22 * l4, s.Jt * l4,
                    Point(s.Centroid), s.AngleX1, s.Wel1 * l3, s.Wel2 * l3, s.WelX * l3, s.WelY * l3, s.Name);
            }
            private Shape2d Outline(Shape2d shape)
            {
                if (shape == null) return null;
                Polygon2d Poly(Polygon2d p) => new Polygon2d(p.Select(Point));
                return new Shape2d(Poly(shape.Fill2d), shape.Holes2d?.Select(Poly).ToArray(), shape.Childs2d?.Select(Outline).ToArray());
            }
        }
    }
}
