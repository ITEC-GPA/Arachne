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
    /// <summary>A plate: a GPC <see cref="AreaElement"/> with its plate property (draft in the document units until Build Model).</summary>
    public class GH_Plate : GH_GeometricGoo<AreaElement>, IGH_BakeAwareData, IGH_PreviewData
    {
        public Guid Guid => Value?.Guid ?? Guid.Empty;
        public Point3d[] Points => Value == null ? new Point3d[0] : Value.Points.Select(ElementPreview.Rh).ToArray();
        public override bool IsValid => Value != null && Points.Length >= 3;
        public override string IsValidWhyNot => Value == null ? "No internal plate" : IsValid ? string.Empty : "Invalid plate";
        public override string TypeName => "Plate";
        public override string TypeDescription => "Define a single plate";
        public override BoundingBox Boundingbox => Value == null ? BoundingBox.Empty : new BoundingBox(Points);

        public GH_Plate() { }
        public GH_Plate(Point3d[] points, GH_PlateProperty property, GH_Group[] groups, Guid identity)
        {
            // Vertex nodes in the order of the input (a shape polygon may start from another vertex: the solver plate would change)
            Value = new AreaElement(identity, null, ModelValues.CopyValue(property?.Value));
            Value.ConnectNodes(points.Select((p, k) => new NodeElement(Arachne.Common.Analysis.InputIdentity.Of(identity, "vertex:" + k), new GPC.Geometry.Point3d(p.X, p.Y, p.Z))).ToArray());
            foreach (var group in groups ?? new GH_Group[0]) if (group?.Value != null && !Value.Groups.Values.Any(g => g.Guid == group.Value.Guid)) Value.AddGroup(ModelValues.CopyValue(group.Value));
        }
        public GH_Plate(AreaElement plate) { Value = plate == null ? null : ModelValues.CopyValue(plate); }
        public GH_Plate(GH_Plate plate) : this(plate?.Value) { }

        public BoundingBox ClippingBox => Boundingbox;
        public void DrawViewportMeshes(GH_PreviewMeshArgs args) { }
        public void DrawViewportWires(GH_PreviewWireArgs args) { if (Value != null) ElementPreview.Plate(Value, args.Pipeline, args.Color); }

        private Brep Shape()
        {
            var points = Points.ToList(); if (points.Count < 3) return null;
            return points.Count == 4 ? Brep.CreateFromCornerPoints(points[0], points[1], points[2], points[3], 1e-6) : Brep.CreateFromCornerPoints(points[0], points[1], points[2], 1e-6);
        }
        public override IGH_GeometricGoo Morph(SpaceMorph xmorph) { var brep = IsValid ? Shape() : null; if (brep == null) return null; xmorph.Morph(brep); return new GH_Brep(brep); }
        public override IGH_GeometricGoo Transform(Transform xform) { var brep = IsValid ? Shape() : null; if (brep == null) return null; brep.Transform(xform); return new GH_Brep(brep); }

        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = Guid.Empty;
            var points = Points; if (points.Length < 3) return false;
            var mesh = new Mesh(); mesh.Vertices.AddVertices(points);
            if (points.Length == 4) mesh.Faces.AddFace(0, 1, 2, 3); else mesh.Faces.AddFace(0, 1, 2);
            mesh.Normals.ComputeNormals();
            obj_guid = doc.Objects.AddMesh(mesh, att);
            return obj_guid != Guid.Empty;
        }

        public override bool CastTo<Q>(out Q target)
        {
            if (Value != null && typeof(Q) == typeof(GH_Curve))
            {
                var pline = new PolylineCurve(Points); pline.MakeClosed(0);
                target = (Q)(object)new GH_Curve(pline); return true;
            }
            if (IsValid && typeof(Q).IsAssignableFrom(typeof(GH_PlateResults))) { target = (Q)(object)new GH_PlateResults(this); return true; }
            target = default; return false;
        }

        public override bool CastFrom(object source)
        {
            if (source is AreaElement area) { Value = ModelValues.CopyValue(area); return true; }
            return false;
        }

        public override IGH_GeometricGoo DuplicateGeometry() => new GH_Plate(this);
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
            try { Value = reader.ItemExists("Data") ? ContractArchive.ReadValue<AreaElement>(reader.GetByteArray("Data")) : null; return true; }
            catch (Exception e) { reader.AddMessage("Plate: " + e.Message, GH_Message_Type.error); return false; }
        }

        public override string ToString()
        {
            if (Value == null) return "Null Plate";
            string text = "Arachne Plate" + (string.IsNullOrEmpty(Value.Name) ? "" : " #" + Value.Name);
            text += "\nGroups: " + string.Join(", ", Value.Groups.Values.Select(g => g.Name));
            text += "\nPoints: " + string.Join("; ", Points.Select(p => p.ToString()));
            text += "\nProperty: " + Value.PlateProperty?.Name;
            var label = ElementDrafts.Label(Value); if (label != null) text += "\nID: " + label;
            return text;
        }
    }
}
