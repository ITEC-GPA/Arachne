using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Model.Core;
using GPC.Model.Core.Identity;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.Models;
using G = GPC.Geometry;
using R = Rhino.Geometry;

namespace Arachne.Common.Analysis
{
    /// <summary>
    /// Build Model: the element drafts of Grasshopper (document units) become one GPC model in N and mm. Same rules as FeMM
    /// (Arachne 7b1108d, ModelHelper.BuildModel and ModelModel.Sync): elements with the same GUID are merged, the IDs are the Element
    /// Id labels or a numbering, nodes closer than the tolerance are merged, beam ends and plate vertices without a node get one, beams
    /// of zero length are dropped. The order of the elements is kept as the numbering of the model (solver order, outputs).
    /// </summary>
    public static class ModelBuilder
    {
        public static Model Build(IEnumerable<NodeElement> nodeDrafts, IEnumerable<BeamElement> beamDrafts, IEnumerable<AreaElement> plateDrafts,
            ModelUnits units, string name, Guid owner, double tolerance, List<string> messages = null)
        {
            if (owner == Guid.Empty) throw new ArgumentException("EmptyModelOwner");
            // 1. One element per GUID, in the order of the first occurrence; the later ones are merged into it.
            List<T> Distinct<T>(IEnumerable<T> drafts, out Dictionary<Guid, T> byGuid) where T : Element
            {
                var all = drafts.Where(d => d != null).ToList();
                var labels = new HashSet<string>(all.Select(ElementDrafts.Label).Where(l => l != null), StringComparer.Ordinal);
                byGuid = new Dictionary<Guid, T>(); var order = new List<T>(); int next = 1;
                foreach (var draft in all)
                {
                    if (byGuid.TryGetValue(draft.Guid, out var first)) { ElementDrafts.Merge(first, draft); continue; }
                    var copy = Detached(draft);
                    string label = ElementDrafts.Label(copy);
                    if (label != null) copy.Name = label;
                    else
                    {
                        while (labels.Contains(next.ToString(CultureInfo.InvariantCulture))) next++;
                        copy.Name = next.ToString(CultureInfo.InvariantCulture); next++;
                    }
                    byGuid.Add(draft.Guid, copy); order.Add(copy);
                }
                return order;
            }
            var nodeList = Distinct(nodeDrafts ?? Enumerable.Empty<NodeElement>(), out _);
            int nodeNumber = 1;
            {
                // The numbering of the nodes created below continues after the labelled and numbered nodes, as FeMM's n_node
                var labels = new HashSet<string>((nodeDrafts ?? Enumerable.Empty<NodeElement>()).Where(n => n != null).Select(ElementDrafts.Label).Where(l => l != null));
                foreach (var node in nodeList) if (ElementDrafts.Label(node) == null) { while (labels.Contains(nodeNumber.ToString(CultureInfo.InvariantCulture))) nodeNumber++; nodeNumber++; }
            }
            int Numeric(Element e) => int.TryParse(e.Name, out int id) ? id : int.MaxValue;
            var beams = Distinct(beamDrafts ?? Enumerable.Empty<BeamElement>(), out _).OrderBy(Numeric).ToList();
            var plates = Distinct(plateDrafts ?? Enumerable.Empty<AreaElement>(), out _).OrderBy(Numeric).ToList();

            // 2. Nodes closer than the tolerance (document units) are merged, in order, with Rhino's RTree as FeMM.
            var nodes = new List<NodeElement>(); var tree = new R.RTree();
            int Search(G.Point3d p)
            {
                bool found = false; int foundId = -1;
                tree.Search(new R.Sphere(new R.Point3d(p.X, p.Y, p.Z), tolerance), (sender, e) => { found = true; foundId = e.Id; });
                return found ? foundId : -1;
            }
            foreach (var node in nodeList)
            {
                int found = Search(node.Position);
                if (found < 0) { nodes.Add(node); tree.Insert(new R.Point3d(node.Position.X, node.Position.Y, node.Position.Z), nodes.Count - 1); }
                else ElementDrafts.Merge(nodes[found], node);
            }
            NodeElement At(G.Point3d p, Guid element, string endpoint)
            {
                int found = Search(p);
                if (found >= 0) return nodes[found];
                var created = new NodeElement(InputIdentity.Of(element, endpoint), p, null, nodeNumber.ToString(CultureInfo.InvariantCulture)); nodeNumber++;
                nodes.Add(created); tree.Insert(new R.Point3d(p.X, p.Y, p.Z), nodes.Count - 1);
                return created;
            }
            // 3. Beam ends and plate vertices on the nodes (snapped to their position) or on new nodes.
            var connected = new List<BeamElement>();
            foreach (var beam in beams)
            {
                var a = At(beam.StartPoint, beam.Guid, "I"); var b = At(beam.EndPoint, beam.Guid, "J");
                if (ReferenceEquals(a, b) || a.Position.X == b.Position.X && a.Position.Y == b.Position.Y && a.Position.Z == b.Position.Z) continue;
                beam.ConnectNodes(a, b); connected.Add(beam);
            }
            foreach (var plate in plates.ToArray())
            {
                var points = plate.Points;
                var vertices = new NodeElement[points.Length];
                for (int j = 0; j < points.Length; j++) vertices[j] = At(points[j], plate.Guid, "vertex:" + j);
                // The form finding uses plates of 3 or 4 vertices only: FeMM gave nodes to all the vertices and left the others out of the solver
                if (points.Length < 3 || points.Length > 4) { messages?.Add($"Plate n° {plates.IndexOf(plate)} have more than vertices"); plates.Remove(plate); continue; }
                if (vertices.Distinct().Count() != vertices.Length) throw new ArgumentException("A plate has two vertices on the same node: " + plate.Name);
                plate.ConnectNodes(vertices);
            }
            // FeMM merged elements with the same ID (ModelModel.Sync) but dropped them from its lists: two distinct elements with one
            // Element Id are refused here instead.
            void Unique(IEnumerable<Element> family, string kind)
            {
                var twice = family.GroupBy(e => e.Name, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);
                if (twice != null) throw new ArgumentException("Two " + kind + " have the same ID " + twice.Key + ": give them different Element Ids.");
            }
            Unique(nodes, "nodes"); Unique(connected, "beams"); Unique(plates, "plates");

            // The results of a previous form finding (decomposed models) are not inputs.
            foreach (var e in nodes.Cast<Element>().Concat(connected).Concat(plates)) if (e.Results.Count != 0) e.Results.Clear();
            // 4. N, mm. The properties are assigned after the assembly: FeMM allowed different properties with one name.
            var scale = new ElementDrafts.Scale(FormFindingUnits.From(units), true);
            foreach (var node in nodes) scale.Apply(node);
            foreach (var beam in connected) scale.Apply(beam);
            foreach (var plate in plates) scale.Apply(plate);
            // One instance per property identity (the drafts carry copies)
            var beamCanonical = new Dictionary<Guid, BeamProperty>(); var plateCanonical = new Dictionary<Guid, PlateProperty>();
            foreach (var beam in connected) if (beam.BeamProperty != null) { if (!beamCanonical.TryGetValue(beam.BeamProperty.Guid, out var p)) beamCanonical.Add(beam.BeamProperty.Guid, p = beam.BeamProperty); beam.BeamProperty = p; }
            foreach (var plate in plates) if (plate.PlateProperty != null) { if (!plateCanonical.TryGetValue(plate.PlateProperty.Guid, out var p)) plateCanonical.Add(plate.PlateProperty.Guid, p = plate.PlateProperty); plate.PlateProperty = p; }
            var beamProperties = connected.ToDictionary(b => b.Guid, b => b.BeamProperty);
            var plateProperties = plates.ToDictionary(p => p.Guid, p => p.PlateProperty);
            foreach (var beam in connected) beam.BeamProperty = null;
            foreach (var plate in plates) plate.PlateProperty = null;
            var sequence = new Dictionary<Guid, long>();
            void Number(IEnumerable<Element> family, EntityFamily kind)
            {
                int slot = 0;
                foreach (var e in family)
                {
                    e.Source = new SourceIdentity("Arachne", owner.ToString("D"), kind, slot.ToString(CultureInfo.InvariantCulture));
                    sequence[e.Guid] = slot++;
                }
            }
            Number(nodes, EntityFamily.Node); Number(connected, EntityFamily.Beam); Number(plates, EntityFamily.Shell);
            var elements = new List<Element>(nodes); elements.AddRange(connected); elements.AddRange(plates);
            var model = ModelAssembly.Assemble(elements, owner, name ?? "", null, null, true, e => sequence[e.Guid]);
            var registered = new HashSet<string>(StringComparer.Ordinal);
            void Register(ElementProperty property, IEnumerable<ElementProperty> all)
            {
                if (property == null || !registered.Add(property.Name ?? "")) return;
                // A name shared by different properties stays on the elements only.
                if (all.Where(p => p != null && p.Name == property.Name).Select(p => p.Guid).Distinct().Count() == 1) model.AddProperty(property);
            }
            var beamValues = beamProperties.Values.Cast<ElementProperty>().ToList();
            foreach (var beam in model.BeamElements.Values) { beam.BeamProperty = beamProperties[beam.Guid]; Register(beam.BeamProperty, beamValues); }
            var plateValues = plateProperties.Values.Cast<ElementProperty>().ToList();
            foreach (var area in model.AreaElements.Values) { area.PlateProperty = plateProperties[area.Guid]; Register(area.PlateProperty, plateValues); }
            return model;
        }

        /// <summary>A detached copy of a draft without the results of a previous form finding (they are not inputs). Its property is the
        /// one of the draft (immutable, shared: user decision of 4/11/2026); the scaling to N-mm replaces it with a new one.</summary>
        private static T Detached<T>(T draft) where T : Element
        {
            var copy = ModelValues.CopyValue(draft, Arachne.Common.Analysis.SharedCopies.Options);
            if (copy.Results.Count != 0) copy.Results.Clear();
            return copy;
        }
    }
}
