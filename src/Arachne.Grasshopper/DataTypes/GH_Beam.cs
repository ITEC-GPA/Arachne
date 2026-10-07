using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_Beam : GH_GeometricGoo<BeamModel>, IGH_BakeAwareData, IGH_PreviewData
    {
        protected Guid _guid;
        BoundingBox _boundingBox;

        #region Properties

        public Guid Guid => _guid;

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null)
                    return "No internal BeamModel instance";
                if (Value.IsValid)
                    return string.Empty;
                return "Invalid BeamModel instance";
            }
        }

        public override string TypeName => "Beam";

        public override string TypeDescription => "Define a single beam";

        public override BoundingBox Boundingbox
        {
            get
            {
                if (m_value != null && !_boundingBox.IsValid)
                {
                    _boundingBox = new BoundingBox();
                    foreach (Brep shape in m_value.Shapes)
                        _boundingBox.Union(shape.GetBoundingBox(true));
                }
                return _boundingBox;
            }
        }

        #endregion

        #region Constructors

        public GH_Beam()
        {
            _guid = Guid.NewGuid();
            _boundingBox = BoundingBox.Unset;
        }

        public GH_Beam(Point3d from, Point3d to, GH_BeamProperty bp, GH_Group[] groups = null)
            : this()
        {
            Value = new BeamModel(from, to, bp.Value);
            if (groups != null && groups.Length > 0)
            {
                foreach (GH_Group group in groups)
                {
                    Value.Groups.Add(group.Value);
                }
            }
        }

        public GH_Beam(BeamModel beam, Guid? guid = null)
            : this()
        {
            if (beam == null)
                beam = new BeamModel();
            Value = beam;
            if (guid.HasValue)
                _guid = guid.Value;
            //_beamProperty = new GH_BeamProperty(beam.BeamProperty);
            /*if (beam.Groups.Count > 0)
            {
                foreach (string group in beam.Groups)
                {
                    _groups.Add(new GH_Group(group));
                }
            }*/
        }

        /// <summary>
        /// Copy constructor. Use to duplicate the object to add data through the components.
        /// NOTE: It keeps the Guid original value
        /// </summary>
        /// <param name="beam">The beam to duplicate</param>
        public GH_Beam(GH_Beam beam)
        {
            _guid = beam._guid;
            //_beamProperty = beam._beamProperty;
            //_groups = beam._groups;
            Value = new BeamModel(beam.Value);
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

        // As for the nodes, a transformed beam becomes plain geometry (its axis line).
        public override IGH_GeometricGoo Morph(SpaceMorph xmorph)
        {
            if (Value == null || !Value.IsValid)
                return null;

            LineCurve line = new LineCurve(Value.PointFrom, Value.PointTo);
            xmorph.Morph(line);
            return new GH_Curve(line);
        }

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            if (Value == null || !Value.IsValid)
                return null;

            LineCurve line = new LineCurve(Value.PointFrom, Value.PointTo);
            line.Transform(xform);
            return new GH_Curve(line);
        }

        #endregion

        #region BakeAwareData methods

        // Arachne has no FeMM Rhino plug-in objects: the beam is baked as its axis line.
        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = doc.Objects.AddLine(Value.PointFrom, Value.PointTo, att);
            return obj_guid != Guid.Empty;
        }

        #endregion

        #region Casting methods

        public override bool CastTo<Q>(out Q target)
        {
            if (typeof(Q) == typeof(GH_Curve))
            {
                LineCurve line = new LineCurve(Value.PointFrom, Value.PointTo);
                target = (Q)Convert.ChangeType(new GH_Curve(line), typeof(Q));
                return true;
            }
            else if (typeof(Q) == typeof(GH_Brep))
            {
                if (Value.Shapes.Length == 1)
                {
                    Brep brep = Value.Shapes[0].DuplicateBrep();
                    target = (Q)Convert.ChangeType(new GH_Brep(brep), typeof(Q));
                    return true;
                }
                /*else
                {
                    GH_Structure<GH_Brep> breps = new GH_Structure<GH_Brep>();
                    for (int i = 0; i < Value.Shapes.Length; i++)
                    {
                        Brep brep = Value.Shapes[i].DuplicateBrep();
                        breps.Append(new GH_Brep(brep));
                    }
                    target = (Q)Convert.ChangeType(breps, typeof(Q));
                }*/
                target = default;
                return false;
            }
            else if (typeof(Q).IsAssignableFrom(typeof(GH_BeamResults)))
            {
                if (Value != null && Value.IsValid)
                {
                    GH_BeamResults results = new GH_BeamResults(this);
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
            GH_Beam copy = new GH_Beam
            {
                _guid = _guid,
                Value = Value
            };
            return copy;
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            var boundingbox = Boundingbox;
            boundingbox.Transform(xform);
            return boundingbox;
        }

        public override bool Write(GH_IWriter writer)
        {
            if (base.Write(writer))
            {
                try
                {
                    writer.SetGuid("Guid", _guid);
                    writer.SetByteArray("Data", SerializationHelper.ToBytes(Value));
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Beam.Write: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        public override bool Read(GH_IReader reader)
        {
            if (base.Read(reader))
            {
                try
                {
                    _guid = reader.GetGuid("Guid");
                    Value = SerializationHelper.FromBytes<BeamModel>(reader.GetByteArray("Data"));
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Beam.Read: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Beam";
            else
                return Value.ToString();
        }
    }
}
