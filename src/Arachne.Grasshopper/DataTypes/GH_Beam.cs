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
    /// <summary>A beam: a GPC <see cref="BeamElement"/> with its property (draft in the document units until Build Model), its
    /// loads (<see cref="GPC.Model.Loads.Assignments.BeamLoadAssignment"/>) and its form-finding objectives
    /// (<see cref="GPC.Model.Analysis.FormFindingGoalAttribute"/>).</summary>
    public class GH_Beam : GH_GeometricGoo<BeamElement>, IGH_BakeAwareData, IGH_PreviewData
    {
        public Guid Guid => Value?.Guid ?? Guid.Empty;
        public Point3d PointFrom => Value == null ? Point3d.Unset : ElementPreview.Rh(Value.StartPoint);
        public Point3d PointTo => Value == null ? Point3d.Unset : ElementPreview.Rh(Value.EndPoint);
        public override bool IsValid => Value != null && PointFrom.IsValid && PointTo.IsValid && Value.BeamProperty != null;
        public override string IsValidWhyNot => Value == null ? "No internal beam" : IsValid ? string.Empty : "Invalid beam";
        public override string TypeName => "Beam";
        public override string TypeDescription => "Define a single beam";
        public override BoundingBox Boundingbox => Value == null ? BoundingBox.Empty : new BoundingBox(new[] { PointFrom, PointTo });

        public GH_Beam() { }
        public GH_Beam(Point3d from, Point3d to, GH_BeamProperty property, GH_Group[] groups, Guid identity)
        {
            Value = new BeamElement(identity, new GPC.Geometry.Point3d(from.X, from.Y, from.Z), new GPC.Geometry.Point3d(to.X, to.Y, to.Z), ModelValues.CopyValue(property.Value));
            Value.Assignments.Formulation = property.Formulation;
            foreach (var group in groups ?? new GH_Group[0]) if (group?.Value != null && !Value.Groups.Values.Any(g => g.Guid == group.Value.Guid)) Value.AddGroup(ModelValues.CopyValue(group.Value));
        }
        /// <summary>A detached copy of <paramref name="beam"/> (same identity).</summary>
        public GH_Beam(BeamElement beam) { Value = beam == null ? null : ModelValues.CopyValue(beam); }
        public GH_Beam(GH_Beam beam) : this(beam?.Value) { }

        public BoundingBox ClippingBox => Boundingbox;
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
        public void DrawViewportWires(GH_PreviewWireArgs args) { if (Value != null) ElementPreview.Beam(Value, args.Pipeline, args.Color); }

        // As for the nodes, a transformed beam becomes plain geometry (its axis line).
        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            if (!IsValid) return null;
            var line = new LineCurve(PointFrom, PointTo); xmorph.Morph(line); return new GH_Curve(line);
        }
        public override IGH_GeometricGoo Transform(Transform xform)
        {
            if (!IsValid) return null;
            var line = new LineCurve(PointFrom, PointTo); line.Transform(xform); return new GH_Curve(line);
        }

        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = doc.Objects.AddLine(PointFrom, PointTo, att);
            return obj_guid != Guid.Empty;
        }

        public override bool CastTo<Q>(out Q target)
        {
            if (Value != null && typeof(Q) == typeof(GH_Curve)) { target = (Q)(object)new GH_Curve(new LineCurve(PointFrom, PointTo)); return true; }
            if (Value != null && typeof(Q) == typeof(GH_Brep))
            {
                var shapes = ElementPreview.Shapes(Value);
                if (shapes.Length == 1) { target = (Q)(object)new GH_Brep(shapes[0]); return true; }
            }
            if (IsValid && typeof(Q).IsAssignableFrom(typeof(GH_BeamResults))) { target = (Q)(object)new GH_BeamResults(this); return true; }
            target = default; return false;
        }

        public override bool CastFrom(object source)
        {
            if (source is BeamElement beam) { Value = ModelValues.CopyValue(beam); return true; }
            return false;
        }

        public override IGH_GeometricGoo DuplicateGeometry() => new GH_Beam(this);
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
            try { Value = reader.ItemExists("Data") ? ContractArchive.ReadValue<BeamElement>(reader.GetByteArray("Data")) : null; return true; }
            catch (Exception e) { reader.AddMessage("Beam: " + e.Message, GH_Message_Type.error); return false; }
        }

        public override string ToString()
        {
            if (Value == null) return "Null Beam";
            string text = "Arachne Beam" + (string.IsNullOrEmpty(Value.Name) ? "" : " #" + Value.Name);
            text += "\nGroups: " + string.Join(", ", Value.Groups.Values.Select(g => g.Name));
            text += $"\nFrom {PointFrom} to {PointTo}";
            text += "\nProperty: " + Value.BeamProperty?.Name + " (" + Value.Assignments.Formulation + ")";
            var label = ElementDrafts.Label(Value); if (label != null) text += "\nID: " + label;
            foreach (var goal in ElementDrafts.Goals(Value)) text += $"\n{goal.Goal.Kind}: {goal.Goal.Target}, weight {goal.Goal.Weight}, load case {goal.LoadCase?.Name}";
            if (Value.Assignments.Loads.Count > 0) text += "\nLoads: " + Value.Assignments.Loads.Count;
            if (Value.Results.Count > 0) text += "\nWith results";
            return text;
        }
    }
}
