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
    // https://api.ning.com/files/xorPYQRorQU*NhhPVfRpB6fFFUY*MNAdaUuNvl3280KJlbIveuzziw9C20corPfCIrwXFvI*F07PGI0*rGuZ0QJtB*T5m-Mp/Boat.cs

    public class GH_Node : GH_GeometricGoo<NodeModel>, IGH_BakeAwareData, IGH_PreviewData
    {
        protected Guid _guid;

        #region Properties

        public Guid Guid => _guid;

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string IsValidWhyNot
        {
            get
            {
                if (Value == null)
                    return "No internal NodeModel instance";
                if (Value.IsValid)
                    return string.Empty;
                return "Invalid NodeModel instance";
            }
        }

        public override string TypeName => "Node";

        public override string TypeDescription => "Defines a single node";

        public override BoundingBox Boundingbox
        {
            get
            {
                if (Value == null)
                    return BoundingBox.Empty;
                if (Value.Position == Point3d.Unset)
                    return BoundingBox.Empty;
                return new BoundingBox(Value.Position, Value.Position);
            }
        }

        #endregion

        #region Constructors

        public GH_Node()
        {
            _guid = Guid.NewGuid();
        }

        public GH_Node(Point3d point)
            : this()
        {
            Value = new NodeModel(point);
        }

        public GH_Node(NodeModel node, Guid? guid = null)
            : this()
        {
            if (node == null)
                node = new NodeModel();
            Value = new NodeModel(node);
            if (guid.HasValue)
                _guid = guid.Value;
        }

        /// <summary>
        /// Copy constructor. Use to duplicate the object to add data through the components.
        /// NOTE: It keeps the Guid original value
        /// </summary>
        /// <param name="node">The node to duplicate</param>
        public GH_Node(GH_Node node)
        {
            _guid = node.Guid;
            Value = new NodeModel(node.Value);
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
            if (Value.Position == Point3d.Unset)
                return null;

            Point point = new Point(Value.Position);
            xmorph.Morph(point);
            return new GH_Node(point.Location);
        }

        public override IGH_GeometricGoo Transform(Transform xform)
        {
            //It's debatable whether you should maintain a BoatShell through transformations. 
            //It might not be easy/make sense to apply scaling/rotations, shears etc.
            //In this example, I'll convert the shell to a brep.
            //Perhaps you will want to check for translations/rotations only, operations that make sense.
            if (Value == null)
                return null;
            if (Value.Position == Point3d.Unset)
                return null;

            GH_Point point = new GH_Point(Value.Position);
            point.Transform(xform);
            return point;
        }

        #endregion

        #region BakeAwareData methods

        // Arachne has no FeMM Rhino plug-in objects: the node is baked as a point.
        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = doc.Objects.AddPoint(Value.Position, att);
            return obj_guid != Guid.Empty;
        }

        #endregion

        #region Casting methods

        public override bool CastTo<Q>(out Q target)
        {
            try
            {
                if (Value != null && Value.IsValid)
                {
                    if (typeof(Q).IsAssignableFrom(typeof(GH_Point)))
                    {

                        GH_Point point = new GH_Point(Value.Position);
                        target = (Q)Convert.ChangeType(point, typeof(Q));
                        return true;
                    }
                    else if (typeof(Q).IsAssignableFrom(typeof(GH_NodeResults)))
                    {
                        GH_NodeResults results = new GH_NodeResults(this);
                        target = (Q)Convert.ChangeType(results, typeof(Q));
                        return true;
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            target = default;
            return false;
        }

        public override bool CastFrom(object source)
        {
            if (source == null) { return false; }

            if (typeof(NodeModel).IsAssignableFrom(source.GetType()))
            {
                Value = (NodeModel)source;
                return true;
            }

            return false;
        }

        #endregion

        public override IGH_GeometricGoo DuplicateGeometry()
        {
            GH_Node copy = new GH_Node
            {
                _guid = _guid,
                Value = Value
            };
            return copy;
        }

        public override BoundingBox GetBoundingBox(Transform xform)
        {
            if (Value == null)
                return BoundingBox.Empty;
            if (Value.Position == Point3d.Unset)
                return BoundingBox.Empty;
            BoundingBox bb = Boundingbox;
            bb.Transform(xform);
            return bb;
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
                    System.Diagnostics.Debug.WriteLine("GH_Node.Write: {0}", e.Message);
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
                    Value = SerializationHelper.FromBytes<NodeModel>(reader.GetByteArray("Data"));
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Node.Read: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Node";
            else
                return Value.ToString();
        }
    }
}
