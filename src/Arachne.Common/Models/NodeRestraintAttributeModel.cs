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

        public class AttributeData : IEquatable<AttributeData>
        {
            public bool Tx { get; set; }
            public bool Ty { get; set; }
            public bool Tz { get; set; }
            public bool Rx { get; set; }
            public bool Ry { get; set; }
            public bool Rz { get; set; }
            public Vector3d TransationalDisplacement { get; set; }
            public Vector3d RotationDisplacement { get; set; }

            public override bool Equals(object obj)
            {
                return Equals(obj as AttributeData);
            }

            public bool Equals(AttributeData other)
            {
                return other != null && Tx == other.Tx && Ty == other.Ty && Tz == other.Tz &&
                    Rx == other.Rx && Ry == other.Ry && Rz == other.Rz &&
                    TransationalDisplacement.Equals(other.TransationalDisplacement) && 
                    RotationDisplacement.Equals(other.RotationDisplacement);
            }

            public override int GetHashCode()
            {
                var hashCode = -1503506270;
                hashCode = hashCode * -1521134295 + Tx.GetHashCode();
                hashCode = hashCode * -1521134295 + Ty.GetHashCode();
                hashCode = hashCode * -1521134295 + Tz.GetHashCode();
                hashCode = hashCode * -1521134295 + Rx.GetHashCode();
                hashCode = hashCode * -1521134295 + Ry.GetHashCode();
                hashCode = hashCode * -1521134295 + Rz.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3d>.Default.GetHashCode(TransationalDisplacement);
                hashCode = hashCode * -1521134295 + EqualityComparer<Vector3d>.Default.GetHashCode(RotationDisplacement);
                return hashCode;
            }
        }

        public override string Description => "Node restraints";

        public NodeRestraintAttributeModel()
        {
            _value = new AttributeData()
            {
                TransationalDisplacement = Vector3d.Zero,
                RotationDisplacement = Vector3d.Zero
            };
        }

        public NodeRestraintAttributeModel(bool tx, bool ty, bool tz, bool rx, bool ry, bool rz)
        {
            _value = new AttributeData()
            {
                Tx = tx,
                Ty = ty,
                Tz = tz,
                Rx = rx,
                Ry = ry,
                Rz = rz,
                TransationalDisplacement = Vector3d.Zero,
                RotationDisplacement = Vector3d.Zero
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
                Rx = attr.Value.Rx,
                Ry = attr.Value.Ry,
                Rz = attr.Value.Rz,
                TransationalDisplacement = attr.Value.TransationalDisplacement,
                RotationDisplacement = attr.Value.RotationDisplacement
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
                if (_value.Tx)
                    display.DrawLine(node.Position, node.Position + new Vector3d(lr, 0, 0), Color.Magenta);
                if (_value.Ty)
                    display.DrawLine(node.Position, node.Position + new Vector3d(0, lr, 0), Color.Magenta);
                if (_value.Tz)
                    display.DrawLine(node.Position, node.Position + new Vector3d(0, 0, lr), Color.Magenta);
                if (_value.Rx)
                    display.DrawLine(node.Position, node.Position - new Vector3d(lr, 0, 0), Color.White);
                if (_value.Ry)
                    display.DrawLine(node.Position, node.Position - new Vector3d(0, lr, 0), Color.White);
                if (_value.Rz)
                    display.DrawLine(node.Position, node.Position - new Vector3d(0, 0, lr), Color.White);
            }
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeRestraintAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

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
                Rx = info.GetBoolean("Rx"),
                Ry = info.GetBoolean("Ry"),
                Rz = info.GetBoolean("Rz"),
                //TransationalDisplacement = (Vector3d)info.GetValue("TransationalDisplacement", typeof(Vector3d)),
                //RotationDisplacement = (Vector3d)info.GetValue("RotationDisplacement", typeof(Vector3d))
            };
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Tx", _value.Tx);
            info.AddValue("Ty", _value.Ty);
            info.AddValue("Tz", _value.Tz);
            info.AddValue("Rx", _value.Rx);
            info.AddValue("Ry", _value.Ry);
            info.AddValue("Rz", _value.Rz);
            //info.AddValue("TransationalDisplacement", _value.TransationalDisplacement, typeof(Vector3d));
            //info.AddValue("RotationDisplacement", _value.RotationDisplacement, typeof(Vector3d));
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
