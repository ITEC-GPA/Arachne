using System;
using System.Linq;
using Arachne.Common.Analysis;
using Arachne.Common.Helpers;
using Arachne.Grasshopper.Helpers;
using GH_IO.Serialization;
using GPC.Model.Core;
using GPC.Model.Elements;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A node: a GPC <see cref="NodeElement"/> (draft in the document units until Build Model). Its GUID is the identity
    /// that Build Model uses to merge the copies of a node coming through different components.</summary>
    public class GH_Node : GH_GeometricGoo<NodeElement>, IGH_BakeAwareData, IGH_PreviewData
    {
        public Guid Guid => Value?.Guid ?? Guid.Empty;
        public Point3d Position => Value == null ? Point3d.Unset : ElementPreview.Rh(Value.Position);
        public override bool IsValid => Value != null && Position.IsValid;
        public override string IsValidWhyNot => Value == null ? "No internal node" : IsValid ? string.Empty : "Invalid node";
        public override string TypeName => "Node";
        public override string TypeDescription => "Defines a single node";
        public override BoundingBox Boundingbox => IsValid ? new BoundingBox(Position, Position) : BoundingBox.Empty;

        public GH_Node() { }
        public GH_Node(Point3d point, Guid identity) { Value = new NodeElement(identity, new GPC.Geometry.Point3d(point.X, point.Y, point.Z)); }
        /// <summary>A detached copy of <paramref name="node"/> (same identity).</summary>
        public GH_Node(NodeElement node) { Value = node == null ? null : ModelValues.CopyValue(node, ModelCopyOptions.ShareProperties); }
        public GH_Node(GH_Node node) : this(node?.Value) { }

        public BoundingBox ClippingBox => Boundingbox;
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
        public void DrawViewportWires(GH_PreviewWireArgs args) { if (Value != null) ElementPreview.Node(Value, args.Pipeline, args.Color); }

        // A transformed node becomes plain geometry, as before.
        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            if (!IsValid) return null;
            var point = new Point(Position); xmorph.Morph(point);
            return new GH_Node(point.Location, Guid.NewGuid());
        }
        public override IGH_GeometricGoo Transform(Transform xform)
        {
            if (!IsValid) return null;
            var point = new GH_Point(Position); point.Transform(xform); return point;
        }

        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = doc.Objects.AddPoint(Position, att);
            return obj_guid != Guid.Empty;
        }

        public override bool CastTo<Q>(out Q target)
        {
            if (IsValid && typeof(Q).IsAssignableFrom(typeof(GH_Point))) { target = (Q)(object)new GH_Point(Position); return true; }
            if (IsValid && typeof(Q).IsAssignableFrom(typeof(GH_NodeResults))) { target = (Q)(object)new GH_NodeResults(this); return true; }
            target = default; return false;
        }

        public override bool CastFrom(object source)
        {
            if (source is NodeElement node) { Value = ModelValues.CopyValue(node, ModelCopyOptions.ShareProperties); return true; }
            return false;
        }

        public override IGH_GeometricGoo DuplicateGeometry() => new GH_Node(this);
        public override BoundingBox GetBoundingBox(Transform xform) { var bb = Boundingbox; bb.Transform(xform); return bb; }

        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            if (Value != null) writer.SetByteArray("Data", ContractArchive.Value(Value));
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            try { Value = reader.ItemExists("Data") ? ContractArchive.ReadValue<NodeElement>(reader.GetByteArray("Data")) : null; return true; }
            catch (Exception e) { reader.AddMessage("Node: " + e.Message, GH_Message_Type.error); return false; }
        }

        public override string ToString()
        {
            if (Value == null) return "Null Node";
            string text = "Arachne Node" + (string.IsNullOrEmpty(Value.Name) ? "" : " #" + Value.Name);
            text += "\nGroups: " + string.Join(", ", Value.Groups.Values.Select(g => g.Name));
            text += "\nPosition " + Position;
            var label = ElementDrafts.Label(Value); if (label != null) text += "\nID: " + label;
            if (Value.Assignments.Restrains.Count > 0) text += "\nRestraints: " + string.Join("; ", Value.Assignments.Restrains.Select(r => string.Join(" ", r.Restrain.Restrains.Where(d => d.IsRestrained).Select(d => d.Dof))));
            foreach (var load in Value.Loads.Values.OfType<GPC.Model.Loads.PointLoad>()) text += $"\nNode force: {load.LoadCase?.Name}, ({load.F1}, {load.F2}, {load.F3})";
            if (Value.Results.Count > 0) text += "\nWith results";
            return text;
        }
    }
}
