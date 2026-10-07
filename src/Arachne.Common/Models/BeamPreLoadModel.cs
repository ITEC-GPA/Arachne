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
    public class BeamPreLoadModel : LoadModel<double>, IEquatable<BeamPreLoadModel>
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

        public enum LoadType
        {
            Tension,
            Strain
        }
        protected LoadType _loadType;

        public override string Description => "Beam Pre Load";

        public LoadType PreLoadType
        {
            get => _loadType;
            set => _loadType = value;
        }

        public BeamPreLoadModel()
        {
        }

        public BeamPreLoadModel(LoadCaseModel loadCase, double value, LoadType type)
            : base(loadCase, value)
        {
            _loadType = type;
        }

        public BeamPreLoadModel(BeamPreLoadModel pl)
        {
            _loadCase = pl._loadCase;
            _value = pl._value;
            _loadType = pl._loadType;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "BeamPreLoadModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected BeamPreLoadModel(SerializationInfo info, StreamingContext context)
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
            _loadType = (LoadType)info.GetValue("LoadType", typeof(LoadType));
            _value = info.GetDouble("Value");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("LoadType", _loadType, typeof(LoadType));
            info.AddValue("Value", _value);
        }

        #endregion

        public override void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
            if (!IsVisible)
                return;

            BeamModel beam = (BeamModel)element;
            if (beam != null)
            {
                if (_value == 0)
                    return;

                double scale = DrawingHelper.GetScaleFactor(display.Viewport, display.Viewport.CameraTarget);
                if (scale < 30)
                {
                    Vector3d vec = beam.PointTo - beam.PointFrom;
                    Vector3d uvec_h = new Vector3d(vec);
                    uvec_h.Unitize();
                    /*
                    double scale = DrawingHelper.GetScaleFactor(display.Viewport, beam.PointFrom + uvec_h * vec.Length);                
                    display.DrawArrow(new Line(beam.PointFrom + uvec_h * (vec.Length * scale), beam.PointFrom), color, 
                        0, DrawingHelper.ArrowSize);
                    display.DrawArrow(new Line(beam.PointTo - uvec_h * (vec.Length * scale), beam.PointTo), color, 
                        0, DrawingHelper.ArrowSize);                
                    */

                    display.DrawArrow(new Line(beam.PointFrom + uvec_h * (vec.Length / 10) * Scale, beam.PointFrom), Color, 0, DrawingHelper.ArrowSize);
                    display.DrawArrow(new Line(beam.PointTo - uvec_h * (vec.Length / 10) * Scale, beam.PointTo), Color, 0, DrawingHelper.ArrowSize);
                }
            }
        }

        public static bool operator ==(BeamPreLoadModel pl1, BeamPreLoadModel pl2)
        {
            if (ReferenceEquals(pl1, pl2))
                return true;
            if (pl1 is null)
                return false;
            return pl1.Equals(pl2);
        }

        public static bool operator !=(BeamPreLoadModel pl1, BeamPreLoadModel pl2)
        {
            return !(pl1 == pl2);
        }

        public bool Equals(BeamPreLoadModel other)
        {
            return !(other is null) && LoadCase == other.LoadCase && PreLoadType == other.PreLoadType && 
                Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (obj is BeamPreLoadModel pl)
                return Equals(pl);
            return base.Equals(obj);
        }
        
        public override int GetHashCode()
        {
            var hashCode = 93666149;
            hashCode = hashCode * 131462983 + LoadCase.GetHashCode();
            hashCode = hashCode * 131462983 + PreLoadType.GetHashCode();
            hashCode = hashCode * 131462983 + Value.GetHashCode();

            return hashCode;
        }

        public override string ToString()
        {
            string text = $"FeMM Beam Pre {_loadType} load: ";
            text += $"Load Case: {_loadCase}, ";
            text += $"V={_value}";
            return text;
        }
    }
}
