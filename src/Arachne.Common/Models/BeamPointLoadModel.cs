using System;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using Arachne.Common.Helpers;
using System.Collections.Generic;
using Rhino.Collections;

namespace Arachne.Common.Models
{
    [Serializable]
    public class BeamPointLoadModel : LoadModel<BeamPointLoadModel.LoadData>, IEquatable<BeamPointLoadModel>
    {
        public enum LoadType
        {
            Force,
            Moment
        }

        public class LoadData : IEquatable<LoadData>
        {
            /// <summary>
            /// NULL for local system
            /// </summary>
            public CoordinateSystemModel CoordinateSystem { get; set; }
            public Vector3d P { get; set; }
            public double A { get; set; }

            public override bool Equals(object obj)
            {
                return Equals(obj as LoadData);
            }

            public bool Equals(LoadData other)
            {
                return other != null &&
                       EqualityComparer<CoordinateSystemModel>.Default.Equals(CoordinateSystem, other.CoordinateSystem) &&
                       P.Equals(other.P) &&
                       A == other.A;
            }

            public override int GetHashCode()
            {
                var hashCode = 918855109;
                hashCode = hashCode * 183552263 + EqualityComparer<CoordinateSystemModel>.Default.GetHashCode(CoordinateSystem);
                hashCode = hashCode * 183552263 + EqualityComparer<Vector3d>.Default.GetHashCode(P);
                hashCode = hashCode * 183552263 + A.GetHashCode();
                return hashCode;
            }
        }

        protected static bool _isVisible = true;
        protected static double _scale = 1;
        protected static Color _color = Color.IndianRed;
        protected LoadType _loadType;

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

        public override string Description => "Beam Point Load";

        public LoadType PointLoadType
        {
            get => _loadType;
            set => _loadType = value;
        }

        public BeamPointLoadModel()
        {
        }

        public BeamPointLoadModel(LoadCaseModel loadCase, LoadData value, LoadType type)
            : base (loadCase, value)
        {
            _loadType = type;
        }

        public BeamPointLoadModel(BeamPointLoadModel load)
        {
            _loadCase = load.LoadCase;
            _loadType = load._loadType;
            _value = new LoadData()
            {
                CoordinateSystem = load.Value.CoordinateSystem != null ? new CoordinateSystemModel(load.Value.CoordinateSystem) : null,
                P = new Vector3d(load.Value.P),
                A = load.Value.A
            };
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "BeamPointLoadModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected BeamPointLoadModel(SerializationInfo info, StreamingContext context)
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
            _value = new LoadData()
            {
                // TODO: CoordinateSystem
                P = (Vector3d)info.GetValue("Force", typeof(Vector3d)),
                A = info.GetDouble("A")
            };
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("LoadType", _loadType, typeof(LoadType));
            // TODO: info.AddValue("CoordinateSystem"
            info.AddValue("Force", _value.P, typeof(Vector3d));
            info.AddValue("A", _value.A);
        }

        #endregion

        public override void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
            if (!IsVisible)
                return;

            BeamModel beam = (BeamModel)element;
            if (beam != null)
            {
                if (_value.P.Length == 0 || _value.P == Vector3d.Unset)
                    return;

                Vector3d vec = beam.PointTo - beam.PointFrom;
                Vector3d uvec_h = new Vector3d(vec);
                uvec_h.Unitize();

                Vector3d v1, v2, v3;
                if (_value.CoordinateSystem == null) // Local system
                {
                    beam.GetLocalAxes(true, out v1, out v2, out v3, out Transform transform);
                    v2 = -v2;
                }
                else if (_value.CoordinateSystem.Name == CoordinateSystemModel.Global.Name)
                {
                    v1 = Vector3d.XAxis;
                    v2 = Vector3d.YAxis;
                    v3 = Vector3d.ZAxis;
                }
                else //  Custom system
                {
                    // TODO: gestire ucs
                    return;
                }

                Point3d pt = beam.PointFrom + uvec_h * (_value.A * vec.Length);
                double scale = DrawingHelper.GetScaleFactor(display.Viewport, display.Viewport.CameraTarget/*pt*/) * Scale;
                if (_loadType == LoadType.Force)
                {
                    if (_value.P.X != 0)
                        display.DrawArrow(new Line(pt + v1 * -_value.P.X / 1000 * scale, pt), Color, 0, DrawingHelper.ArrowSize);
                    if (_value.P.Y != 0)
                        display.DrawArrow(new Line(pt + v2 * _value.P.Y / 1000 * scale, pt), Color, 0, DrawingHelper.ArrowSize);
                    if (_value.P.Z != 0)
                        display.DrawArrow(new Line(pt + v3 * -_value.P.Z / 1000 * scale, pt), Color, 0, DrawingHelper.ArrowSize);
                }
                else
                {
                    if (_value.P.X != 0)
                    {
                        Plane plane = new Plane(pt, v1);
                        Circle circle = new Circle(plane, _value.P.X / 1000.0 * scale);
                        display.DrawCircle(circle, Color);
                    }
                    if (_value.P.Y != 0)
                    {
                        Plane plane = new Plane(pt, v2);
                        Circle circle = new Circle(plane, _value.P.Y / 1000.0 * scale);
                        display.DrawCircle(circle, Color);
                    }
                    if (_value.P.Z != 0)
                    {
                        Plane plane = new Plane(pt, v3);
                        Circle circle = new Circle(plane, _value.P.Z / 1000.0 * scale);
                        display.DrawCircle(circle, Color);
                    }
                }
            }
        }

        public static bool operator ==(BeamPointLoadModel pl1, BeamPointLoadModel pl2)
        {
            if (ReferenceEquals(pl1, pl2))
                return true;
            if (pl1 is null)
                return false;
            return pl1.Equals(pl2);
        }

        public static bool operator !=(BeamPointLoadModel pl1, BeamPointLoadModel pl2)
        {
            return !(pl1 == pl2);
        }

        public bool Equals(BeamPointLoadModel other)
        {
            return !(other is null) && LoadCase == other.LoadCase && PointLoadType == other.PointLoadType &&
                Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is BeamPointLoadModel pl)
                return Equals(pl);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 117305057;
            hashCode = hashCode * 174287473 + LoadCase.GetHashCode();
            hashCode = hashCode * 174287473 + PointLoadType.GetHashCode();
            hashCode = hashCode * 174287473 + Value.GetHashCode();

            return hashCode;
        }

        public override string ToString()
        {
            string text = $"FeMM Point {_loadType} load: ";
            text += $"\n - Load Case: {_loadCase}, ";
            text += $"\n - P1={_value.P.X}, P2={_value.P.Y}, P3={_value.P.Z}, a={_value.A}\n - ";
            if (_value.CoordinateSystem == null)
                text += " Local";
            else
                text += " " + _value.CoordinateSystem.Name;
            return text;
        }
    }
}
