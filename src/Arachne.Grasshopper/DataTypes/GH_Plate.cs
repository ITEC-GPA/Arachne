using Arachne.Common.Models;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_Plate : GH_GeometricGoo<PlateModel>, IGH_BakeAwareData, IGH_PreviewData
    {
        protected Guid _guid;
        protected GH_PlateProperty _plateProperty;
        protected List<GH_Group> _groups;

        #region Properties

        public Guid Guid => _guid;

        public GH_PlateProperty PlateProperty => _plateProperty;

        public List<GH_Group> Groups => _groups;

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null)
                    return "No internal PlateModel instance";
                if (Value.IsValid)
                    return string.Empty;
                return "Invalid PlateModel instance";
            }
        }

        public override string TypeName => "Plate";

        public override string TypeDescription => "Define a single plate";

        public override BoundingBox Boundingbox
        {
            get
            {
                if (Value == null)
                    return BoundingBox.Empty;
                if (!Value.IsValid)
                    return BoundingBox.Empty;
                return Value.Shape.GetBoundingBox(true);
            }
        }

        #endregion

        #region Constructors

        public GH_Plate()
        {
            _guid = Guid.NewGuid();
            _groups = new List<GH_Group>();
        }

        public GH_Plate(Point3d[] points, GH_PlateProperty pp, GH_Group[] groups = null)
            : this()
        {
            Value = new PlateModel(points, pp.Value);
            if (groups != null)
            {
                foreach (GH_Group group in groups)
                {
                    Value.Groups.Add(group.Value);
                    _groups.Add(group);
                }
            }
        }

        public GH_Plate(PlateModel plate, Guid? guid = null)
            : this()
        {
            if (plate == null)
                plate = new PlateModel();
            Value = plate;
            if (guid.HasValue)
                _guid = guid.Value;
            /*_plateProperty = new GH_PlateProperty(plate.PlateProperty);
            if (plate.Groups.Count > 0)
            {
                foreach (string group in plate.Groups)
                {
                    _groups.Add(new GH_Group(group));
                }
            }*/
        }

        /// <summary>
        /// Copy constructor. Use to duplicate the object to add data through the components.
        /// NOTE: It keeps the Guid original value
        /// </summary>
        /// <param name="plate">The plate to duplicate</param>
        public GH_Plate(GH_Plate plate)
        {
            _guid = plate._guid;
            _plateProperty = plate._plateProperty;
            _groups = plate._groups;
            Value = new PlateModel(plate.Value);
        }

        #endregion

        #region PreviewData methods

        public BoundingBox ClippingBox => Boundingbox;

        public void DrawViewportMeshes(GH_PreviewMeshArgs args)
        {
        }

        public void DrawViewportWires(GH_PreviewWireArgs args)
        {
            Value.DrawWireframe(args.Pipeline, args.Viewport, args.Color);
            if (Settings.Instance.ShowSolids)
                Value.DrawSolid(args.Pipeline, args.Viewport, args.Color);
            if (Settings.Instance.ShowAttributes)
                Value.DrawAttributes(args.Pipeline, args.Viewport);
        }

        #endregion

        #region Transformation methods

        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            if (Value == null)
                return null;
            if (!Value.IsValid)
                return null;

            Brep brep = Value.Shape.DuplicateBrep();
            xmorph.Morph(brep);
            return new GH_Brep(brep);
        }

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            //It's debatable whether you should maintain a BoatShell through transformations. 
            //It might not be easy/make sense to apply scaling/rotations, shears etc.
            //In this example, I'll convert the shell to a brep.
            //Perhaps you will want to check for translations/rotations only, operations that make sense.
            if (Value == null)
                return null;
            if (!Value.IsValid)
                return null;

            Brep brep = Value.Shape.DuplicateBrep();
            brep.Transform(xform);
            return new GH_Brep(brep);
        }

        #endregion

        #region BakeAwareData methods

        // Arachne has no FeMM Rhino plug-in objects: the plate is baked as a mesh face.
        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = Guid.Empty;
            if (Value == null || Value.Points.Count < 3)
                return false;

            Mesh mesh = new Mesh();
            mesh.Vertices.AddVertices(Value.Points);
            if (Value.Points.Count == 4)
                mesh.Faces.AddFace(0, 1, 2, 3);
            else
                mesh.Faces.AddFace(0, 1, 2);
            mesh.Normals.ComputeNormals();
            obj_guid = doc.Objects.AddMesh(mesh, att);
            return obj_guid != Guid.Empty;
        }

        #endregion

        #region Casting methods

        public override bool CastTo<Q>(out Q target)
        {
            if (typeof(Q) == typeof(GH_Curve))
            {
                PolylineCurve pline = new PolylineCurve(Value.Points);
                pline.MakeClosed(0);
                target = (Q)Convert.ChangeType(new GH_Curve(pline), typeof(Q));
                return true;
            }
            else if (typeof(Q) == typeof(GH_Brep))
            {
                target = (Q)Convert.ChangeType(new GH_Brep(Value.Shape), typeof(Q));
                return true;
            }
            else if (typeof(Q).IsAssignableFrom(typeof(GH_PlateResults)))
            {
                if (Value != null && Value.IsValid)
                {
                    GH_PlateResults results = new GH_PlateResults(this);
                    target = (Q)Convert.ChangeType(results, typeof(Q));
                    return true;
                }
            }

            target = default;
            return false;
        }

        #endregion

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            GH_Plate copy = new GH_Plate
            {
                _guid = _guid,
                _plateProperty = _plateProperty,
                _groups = _groups,
                Value = Value
            };
            return copy;
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            if (Value == null)
                return BoundingBox.Empty;
            if (!Value.IsValid)
                return BoundingBox.Empty;
            return Value.Shape.GetBoundingBox(xform);
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Plate";
            else
                return Value.ToString();
        }
    }
}
