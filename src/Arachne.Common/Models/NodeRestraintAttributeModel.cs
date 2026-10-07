using System;
using System.Linq;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using Arachne.Common.Helpers;
using System.Collections.Generic;

namespace Arachne.Common.Models
{
    [Serializable]
    public class NodeRestraintAttributeModel : AttributeModel<NodeRestraintAttributeModel.AttributeData>, IEquatable<NodeRestraintAttributeModel>
    {
        protected static bool _isVisible = true;
        protected static double _scale = 1;

        public static bool IsVisible
        {
            get => _isVisible;
            set => _isVisible = value;
        }

        public static double Scale
        {
            get => _scale;
            set => _scale = value;
        }

        // The rotations of FeMM are not kept: the form finding does not use them
        public class AttributeData : IEquatable<AttributeData>
        {
            public bool Tx { get; set; }
            public bool Ty { get; set; }
            public bool Tz { get; set; }

            /// <summary>
            /// The axes of the restraints: X, Y and Z of the coordinate system (of a plane).
            /// Null: the global axes.
            /// </summary>
            public CoordinateSystemModel CoordinateSystem { get; set; }

            /// <summary>
            /// The unit vectors along which Tx, Ty and Tz act: the axes of the coordinate
            /// system, or the global axes if there is none (or it does not define a plane)
            /// </summary>
            public Vector3d[] Axes()
            {
                if (CoordinateSystem != null && CoordinateSystem.GetAxes(out Vector3d x, out Vector3d y, out Vector3d z))
                    return new[] { x, y, z };
                return new[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis };
            }

            /// <summary>
            /// The unit vectors of the restrained directions
            /// </summary>
            public IEnumerable<Vector3d> RestrainedDirections()
            {
                Vector3d[] axes = Axes();
                bool[] restrained = { Tx, Ty, Tz };
                return axes.Where((axis, i) => restrained[i]);
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as AttributeData);
            }

            public bool Equals(AttributeData other)
            {
                return other != null && Tx == other.Tx && Ty == other.Ty && Tz == other.Tz &&
                    EqualityComparer<CoordinateSystemModel>.Default.Equals(CoordinateSystem, other.CoordinateSystem);
            }

            public override int GetHashCode()
            {
                var hashCode = -1503506270;
                hashCode = hashCode * -1521134295 + Tx.GetHashCode();
                hashCode = hashCode * -1521134295 + Ty.GetHashCode();
                hashCode = hashCode * -1521134295 + Tz.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<CoordinateSystemModel>.Default.GetHashCode(CoordinateSystem);
                return hashCode;
            }
        }

        public override string Description => "Node restraints";

        public NodeRestraintAttributeModel()
        {
            _value = new AttributeData();
        }

        public NodeRestraintAttributeModel(bool tx, bool ty, bool tz, CoordinateSystemModel coordinateSystem = null)
        {
            _value = new AttributeData()
            {
                Tx = tx,
                Ty = ty,
                Tz = tz,
                CoordinateSystem = coordinateSystem
            };
        }

        public NodeRestraintAttributeModel(AttributeData value)
            : base(value)
        {
        }

        public NodeRestraintAttributeModel(NodeRestraintAttributeModel attr)
        {
            _value = new AttributeData()
            {
                Tx = attr.Value.Tx,
                Ty = attr.Value.Ty,
                Tz = attr.Value.Tz,
                CoordinateSystem = attr.Value.CoordinateSystem != null ? new CoordinateSystemModel(attr.Value.CoordinateSystem) : null
            };
        }

        public override void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
            if (!IsVisible)
                return;

            NodeModel node = (NodeModel)element;
            if (node != null)
            {
                double lr = DrawingHelper.GetScaleFactor(display.Viewport, node.Position) * Scale;
                // Along the axes of the restraints
                Vector3d[] axes = _value.Axes();
                if (_value.Tx)
                    display.DrawLine(node.Position, node.Position + axes[0] * lr, Color.Magenta);
                if (_value.Ty)
                    display.DrawLine(node.Position, node.Position + axes[1] * lr, Color.Magenta);
                if (_value.Tz)
                    display.DrawLine(node.Position, node.Position + axes[2] * lr, Color.Magenta);
            }
        }

        public override string ToString()
        {
            string text = "Restraints " + (_value.Tx ? "Tx " : "") + (_value.Ty ? "Ty " : "") + (_value.Tz ? "Tz " : "");
            return text + (_value.CoordinateSystem == null ? "(global axes)" : $"(axes of {_value.CoordinateSystem.Name})");
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeRestraintAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        // 2: no rotations, coordinate system of the restraints
        private int SERIALIZATION_RELEASE = 2;

		protected NodeRestraintAttributeModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            int savedRelease;
            try
            {
                savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);
            }
            catch
            {
                savedRelease = 0;
            }
            _value = new AttributeData()
            {
                Tx = info.GetBoolean("Tx"),
                Ty = info.GetBoolean("Ty"),
                Tz = info.GetBoolean("Tz"),
                // Release 1 had the rotations (Rx, Ry, Rz): they are not read
                CoordinateSystem = savedRelease >= 2 ? (CoordinateSystemModel)info.GetValue("CoordinateSystem", typeof(CoordinateSystemModel)) : null
            };
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Tx", _value.Tx);
            info.AddValue("Ty", _value.Ty);
            info.AddValue("Tz", _value.Tz);
            info.AddValue("CoordinateSystem", _value.CoordinateSystem, typeof(CoordinateSystemModel));
        }

        #endregion

        public static bool operator ==(NodeRestraintAttributeModel nr1, NodeRestraintAttributeModel nr2)
        {
            if (ReferenceEquals(nr1, nr2))
                return true;
            if (nr1 is null)
                return false;
            return nr1.Equals(nr2);
        }

        public static bool operator !=(NodeRestraintAttributeModel nr1, NodeRestraintAttributeModel nr2)
        {
            return !(nr1 == nr2);
        }

        public bool Equals(NodeRestraintAttributeModel other)
        {
            return !(other is null) && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is NodeRestraintAttributeModel nr)
                return Equals(nr);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            int hashCode = 33973657;
            hashCode = hashCode * 139859669 + Value.GetHashCode();

            return hashCode;
        }

        public static AttributeData GetRestraint(NodeModel node)
        {
            if (node.HasAttribute(typeof(NodeRestraintAttributeModel)))
            {
                return node.Attributes.Where(n => n.GetType() == typeof(NodeRestraintAttributeModel)).
                    Cast<NodeRestraintAttributeModel>().First().Value;
            }
            return null;
        }
    }
}
