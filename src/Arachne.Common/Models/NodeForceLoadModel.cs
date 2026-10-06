using System;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using Arachne.Common.Helpers;
using System.Collections.Generic;

namespace Arachne.Common.Models
{
    [Serializable]
    public class NodeForceLoadModel : LoadModel<Vector3d>, IEquatable<NodeForceLoadModel>
    {
        protected static bool _isVisible = true;
        protected static double _scale = 1;
        protected static Color _color = Color.IndianRed;

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

        public static Color Color
        {
            get => _color;
            set => _color = value;
        }

        public override string Description => "Node Force Load";

        public NodeForceLoadModel()
        {
        }

        public NodeForceLoadModel(LoadCaseModel loadCase, Vector3d value)
            : base(loadCase, value)
        {
        }

        public NodeForceLoadModel(NodeForceLoadModel fl)
        {
            _loadCase = fl._loadCase;
            _value = new Vector3d(fl._value);
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeForceLoadModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected NodeForceLoadModel(SerializationInfo info, StreamingContext context)
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
            double x = info.GetDouble("X");
            double y = info.GetDouble("Y");
            double z = info.GetDouble("Z");
            _value = new Vector3d(x, y, z);
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("X", _value.X);
            info.AddValue("Y", _value.Y);
            info.AddValue("Z", _value.Z);
        }

        #endregion

        public override void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
            if (!IsVisible)
                return;

            NodeModel node = (NodeModel)element;
            if (node != null)
            {
                double scale = DrawingHelper.GetScaleFactor(display.Viewport, node.Position) * Scale;

                if (_value.X != 0)
                    display.DrawArrow(new Line(node.Position + Vector3d.XAxis * -_value.X * scale, node.Position),
                        Color, 0, DrawingHelper.ArrowSize);
                if (_value.Y != 0)
                    display.DrawArrow(new Line(node.Position + Vector3d.YAxis * -_value.Y * scale, node.Position),
                        Color, 0, DrawingHelper.ArrowSize);
                if (_value.Z != 0)
                    display.DrawArrow(new Line(node.Position + Vector3d.ZAxis * -_value.Z * scale, node.Position),
                        Color, 0, DrawingHelper.ArrowSize);
                //if (_value.Length != 0)
                //    display.DrawArrow(new Line(node.Position + _value * scale, node.Position), color, 0, 0.3);
            }
        }

        public static bool operator ==(NodeForceLoadModel m1, NodeForceLoadModel m2)
        {
            if (ReferenceEquals(m1, m2))
                return true;
            return !(m1 is null) && m1.Equals(m2);
        }

        public static bool operator !=(NodeForceLoadModel m1, NodeForceLoadModel m2)
        {
            return !(m1 == m2);
        }

        public bool Equals(NodeForceLoadModel other)
        {
            return !(other is null) && LoadCase == other.LoadCase && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is NodeForceLoadModel model)
                return Equals(model);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 105453869;
            hashCode = hashCode * 12137471 + LoadCase.GetHashCode();
            hashCode = hashCode * 12137471 + EqualityComparer<Vector3d>.Default.GetHashCode(Value);

            return hashCode;
        }

        public override string ToString()
        {
            string text = $"FeMM Node Force load: ";
            text += $"Load Case: {_loadCase}, ";
            text += $"V={_value}";
            return text;
        }
    }
}
