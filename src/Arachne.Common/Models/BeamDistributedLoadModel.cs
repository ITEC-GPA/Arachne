using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using Arachne.Common.Helpers;

namespace Arachne.Common.Models
{
    [Serializable]
    public class BeamDistributedLoadModel : LoadModel<BeamDistributedLoadModel.LoadData>, IEquatable<BeamDistributedLoadModel>
    {
        public enum LoadType
        {
            Undefined = -1,
            Force,
            Moment
        }

        public enum LoadSchema
        {
            Undefined = -1,
            Uniform,
            Keystone,
            Triangular,
            [Description("Triangular at end 1 + Keystone")]
            TriangularEnd1_Keystone,
            [Description("Keystone + Triangular at end 2")]
            Keystone_TriangularEnd2,
            [Description("Triangular at ends + Keystone")]
            TriangularEnds_Keystone
        }

        public enum LoadDirection
        {
            Undefined = -1,
            X,
            Y,
            Z,
            X_Projected,
            Y_Projected,
            Z_Projected,
            Gravity,
            Gravity_Projected
        }

        public class LoadData : IEquatable<LoadData>
        {
            public LoadSchema LoadSchema { get; set; }
            /// <summary>
            /// NULL for local system
            /// </summary>
            public CoordinateSystemModel CoordinateSystem { get; set; }
            public LoadDirection LoadDirection { get; set; }
            public double P1 { get; set; }
            public double PA { get; set; }
            public double PB { get; set; }
            public double P2 { get; set; }
            public double A { get; set; }
            public double B { get; set; }

            public LoadData()
            {
                LoadSchema = LoadSchema.Undefined;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as LoadData);
            }

            public bool Equals(LoadData other)
            {
                return other != null &&
                       LoadSchema == other.LoadSchema &&
                       EqualityComparer<CoordinateSystemModel>.Default.Equals(CoordinateSystem, other.CoordinateSystem) &&
                       LoadDirection == other.LoadDirection &&
                       P1 == other.P1 &&
                       PA == other.PA &&
                       PB == other.PB &&
                       P2 == other.P2 &&
                       A == other.A &&
                       B == other.B;
            }

            public override int GetHashCode()
            {
                var hashCode = 1147610325;
                hashCode = hashCode * -1521134295 + LoadSchema.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<CoordinateSystemModel>.Default.GetHashCode(CoordinateSystem);
                hashCode = hashCode * -1521134295 + LoadDirection.GetHashCode();
                hashCode = hashCode * -1521134295 + P1.GetHashCode();
                hashCode = hashCode * -1521134295 + PA.GetHashCode();
                hashCode = hashCode * -1521134295 + PB.GetHashCode();
                hashCode = hashCode * -1521134295 + P2.GetHashCode();
                hashCode = hashCode * -1521134295 + A.GetHashCode();
                hashCode = hashCode * -1521134295 + B.GetHashCode();
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

        public override string Description => "Beam Distributed Load";

        public LoadType DistributedLoadType
        {
            get => _loadType;
            set => _loadType = value;
        }

        public BeamDistributedLoadModel()
        {
        }

        public BeamDistributedLoadModel(LoadCaseModel loadCase, LoadData value, LoadType type)
            : base(loadCase, value)
        {
            _loadType = type;
        }

        public BeamDistributedLoadModel(BeamDistributedLoadModel load)
        {
            _loadCase = load._loadCase;
            _loadType = load._loadType;
            _value = new LoadData()
            {
                LoadSchema = load._value.LoadSchema,
                CoordinateSystem = load.Value.CoordinateSystem != null ? new CoordinateSystemModel(load.Value.CoordinateSystem) : null,
                LoadDirection = load._value.LoadDirection,
                P1 = load._value.P1,
                PA = load._value.PA,
                PB = load._value.PB,
                P2 = load._value.P2,
                A = load._value.A,
                B = load._value.B
            };
        }

        #region Serialization 

        private string SERIALIZATION_RELEASE_NAME = "BeamDistributedLoadModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;
		protected BeamDistributedLoadModel(SerializationInfo info, StreamingContext context)
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
            _value = new LoadData
            {
                LoadSchema = (LoadSchema)info.GetValue("LoadSchema", typeof(LoadSchema)),
                CoordinateSystem = (CoordinateSystemModel)info.GetValue("CoordinateSystem", typeof(CoordinateSystemModel)),
                LoadDirection = (LoadDirection)info.GetValue("LoadDirection", typeof(LoadDirection)),
                P1 = info.GetDouble("P1"),
                PA = info.GetDouble("PA"),
                PB = info.GetDouble("PB"),
                P2 = info.GetDouble("P2"),
                A = info.GetDouble("A"),
                B = info.GetDouble("B")
            };
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("LoadType", _loadType, typeof(LoadType));
            info.AddValue("LoadSchema", _value.LoadSchema, typeof(LoadSchema));
            info.AddValue("CoordinateSystem", _value.CoordinateSystem, typeof(CoordinateSystemModel));
            info.AddValue("LoadDirection", _value.LoadDirection, typeof(LoadDirection));
            info.AddValue("P1", _value.P1);
            info.AddValue("PA", _value.PA);
            info.AddValue("PB", _value.PB);
            info.AddValue("P2", _value.P2);
            info.AddValue("A", _value.A);
            info.AddValue("B", _value.B);
        }

        #endregion

        public override void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
            if (!IsVisible)
                return;

            BeamModel beam = (BeamModel)element;
            if (beam != null)
            {
                Vector3d vec = beam.PointTo - beam.PointFrom;
                Vector3d uvec_h = new Vector3d(vec);
                uvec_h.Unitize();
                Vector3d uvec_v = Vector3d.Unset;

                if (_value.CoordinateSystem == null) // Local system
                {
                    if (Vector3d.CrossProduct(Vector3d.ZAxis, vec).Length >= 1.0E-12) // Not vertical
                    {
                        Plane pl = new Plane(beam.PointFrom, Vector3d.ZAxis, uvec_h);
                        switch (_value.LoadDirection)
                        {
                            case LoadDirection.X: // Axis 1
                                uvec_v = -pl.Normal;
                                break;
                            case LoadDirection.Y: // Axis 2
                                uvec_v = uvec_h;
                                uvec_v.Rotate(Math.PI / 2, pl.Normal);
                                break;
                            case LoadDirection.Z: // Principal axis
                                uvec_v = uvec_h;
                                break;
                        }
                        uvec_v.Rotate(beam.AngleDeg / 180 * Math.PI, uvec_h);
                    }
                    else // Vertical
                    {
                        switch (_value.LoadDirection)
                        {
                            case LoadDirection.X: // Axis 1
                                uvec_v = Vector3d.YAxis;
                                break;
                            case LoadDirection.Y: // Axis 2
                                uvec_v = Vector3d.XAxis;
                                break;
                            case LoadDirection.Z: // Principal axis
                                uvec_v = -Vector3d.ZAxis;
                                break;
                        }

                    }
                }
                else if (_value.CoordinateSystem.Name == CoordinateSystemModel.Global.Name)
                {
                    switch (_value.LoadDirection)
                    {
                        case LoadDirection.X:
                        case LoadDirection.X_Projected:
                            uvec_v = -Vector3d.XAxis;
                            break;
                        case LoadDirection.Y:
                        case LoadDirection.Y_Projected:
                            uvec_v = -Vector3d.YAxis;
                            break;
                        case LoadDirection.Z:
                        case LoadDirection.Z_Projected:
                            uvec_v = -Vector3d.ZAxis;
                            break;
                        case LoadDirection.Gravity:
                        case LoadDirection.Gravity_Projected:
                            uvec_v = Vector3d.ZAxis;
                            break;
                    }
                }
                else //  Custom system
                {
                }
                if (uvec_v == Vector3d.Unset)
                    return;

                Point3d target = display.Viewport.CameraTarget;
                double scale = DrawingHelper.GetScaleFactor(display.Viewport, /*beam.PointFrom + uvec_h * vec.Length*/target) * Scale;

                if (_value.CoordinateSystem == null && _value.LoadDirection == LoadDirection.Z)
                {
                    double p = Math.Max(_value.P1, Math.Max(_value.P2, Math.Max(_value.PA, _value.PB)));
                    display.DrawArrow(new Line(beam.PointFrom + uvec_v * -p * scale, beam.PointFrom), Color,
                        0, DrawingHelper.ArrowSize);
                }
                else
                {
                    List<Point3d> pts = new List<Point3d>();
                    switch (_value.LoadSchema)
                    {
                        case LoadSchema.Uniform:
                            pts.Add(beam.PointFrom + uvec_h * (vec.Length * _value.A));
                            pts.Add(pts[0] + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B) + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B));
                            display.DrawArrow(new Line(pts[1], pts[0]), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[2], pts[3]), Color, 0, DrawingHelper.ArrowSize);
                            break;
                        case LoadSchema.Keystone:
                            pts.Add(beam.PointFrom + uvec_h * (vec.Length * _value.A));
                            pts.Add(pts[0] + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B) + uvec_v * _value.PB * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B));
                            display.DrawArrow(new Line(pts[1], pts[0]), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[2], pts[3]), Color, 0, DrawingHelper.ArrowSize);
                            break;
                        case LoadSchema.Triangular:
                            pts.Add(beam.PointFrom);
                            pts.Add(beam.PointFrom + uvec_v * _value.P1 * scale);
                            pts.Add(beam.PointFrom + uvec_h * (vec.Length * _value.A) + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo + uvec_v * _value.P2 * scale);
                            pts.Add(beam.PointTo);
                            display.DrawArrow(new Line(pts[1], pts[0]), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[2], pts[0] + uvec_h * (vec.Length * _value.A)), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[3], pts[4]), Color, 0, DrawingHelper.ArrowSize);
                            break;
                        case LoadSchema.TriangularEnd1_Keystone:
                            pts.Add(beam.PointFrom);
                            pts.Add(beam.PointFrom + uvec_v * _value.P1 * scale);
                            pts.Add(beam.PointFrom + uvec_h * (vec.Length * _value.A) + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B) + uvec_v * _value.PB * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B));
                            display.DrawArrow(new Line(pts[1], pts[0]), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[2], pts[0] + uvec_h * (vec.Length * _value.A)), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[3], pts[4]), Color, 0, DrawingHelper.ArrowSize);
                            break;
                        case LoadSchema.Keystone_TriangularEnd2:
                            pts.Add(beam.PointFrom + uvec_h * (vec.Length * _value.A));
                            pts.Add(pts[0] + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B) + uvec_v * _value.PB * scale);
                            pts.Add(beam.PointTo + uvec_v * _value.P2 * scale);
                            pts.Add(beam.PointTo);
                            display.DrawArrow(new Line(pts[1], pts[0]), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[2], pts[4] - uvec_h * (vec.Length * _value.B)), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[3], pts[4]), Color, 0, DrawingHelper.ArrowSize);
                            break;
                        case LoadSchema.TriangularEnds_Keystone:
                            pts.Add(beam.PointFrom);
                            pts.Add(beam.PointFrom + uvec_v * _value.P1 * scale);
                            pts.Add(beam.PointFrom + uvec_h * (vec.Length * _value.A) + uvec_v * _value.PA * scale);
                            pts.Add(beam.PointTo - uvec_h * (vec.Length * _value.B) + uvec_v * _value.PB * scale);
                            pts.Add(beam.PointTo + uvec_v * _value.P2 * scale);
                            pts.Add(beam.PointTo);
                            display.DrawArrow(new Line(pts[1], pts[0]), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[2], pts[0] + uvec_h * (vec.Length * _value.A)), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[3], pts[4] - uvec_h * (vec.Length * _value.B)), Color, 0, DrawingHelper.ArrowSize);
                            display.DrawArrow(new Line(pts[3], pts[4]), Color, 0, DrawingHelper.ArrowSize);
                            break;
                    }
                    display.DrawPolygon(pts, Color, false);
                }
            }
        }

        public override LoadModel Union(LoadModel other)
        {
            BeamDistributedLoadModel union = (BeamDistributedLoadModel)base.Union(other);
            if (_loadType != ((BeamDistributedLoadModel)other)._loadType)

            if (_value.LoadSchema != ((BeamDistributedLoadModel)other)._value.LoadSchema)
                union._value.LoadSchema = LoadSchema.Undefined;
            if (_value.LoadDirection != ((BeamDistributedLoadModel)other)._value.LoadDirection)
                union._value.LoadDirection = LoadDirection.Undefined;
            if (_value.CoordinateSystem != ((BeamDistributedLoadModel)other)._value.CoordinateSystem)
                union._value.CoordinateSystem = null;
            if (_value.P1 != ((BeamDistributedLoadModel)other)._value.P1)
                union._value.P1 = double.NaN;
            if (_value.PA != ((BeamDistributedLoadModel)other)._value.PA)
                union._value.PA = double.NaN;
            if (_value.PB != ((BeamDistributedLoadModel)other)._value.PB)
                union._value.PB = double.NaN;
            if (_value.P2 != ((BeamDistributedLoadModel)other)._value.P2)
                union._value.P2 = double.NaN;
            if (_value.A != ((BeamDistributedLoadModel)other)._value.A)
                union._value.A = double.NaN;
            if (_value.B != ((BeamDistributedLoadModel)other)._value.B)
                union._value.B = double.NaN;

            return union;
        }

        public static bool operator ==(BeamDistributedLoadModel dl1, BeamDistributedLoadModel dl2)
        {
            if (ReferenceEquals(dl1, dl2))
                return true;
            if (dl1 is null)
                return false;
            return dl1.Equals(dl2);
        }

        public static bool operator !=(BeamDistributedLoadModel dl1, BeamDistributedLoadModel dl2)
        {
            return !(dl1 == dl2);
        }

        public bool Equals(BeamDistributedLoadModel other)
        {
            return !(other is null) && LoadCase == other.LoadCase && DistributedLoadType == other.DistributedLoadType &&
                Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is BeamDistributedLoadModel dl)
                return Equals(dl);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 71987660;
            hashCode = hashCode * 190471339 + LoadCase.GetHashCode();
            hashCode = hashCode * 190471339 + DistributedLoadType.GetHashCode();
            hashCode = hashCode * 190471339 + Value.GetHashCode();

            return hashCode;
        }

        public override string ToString()
        {
            string text = "FeMM Distributed " + _value.LoadSchema.GetDescription() + " load: ";
            text += $"Load Case: {_loadCase}, ";
            switch (_value.LoadSchema)
            {
                case LoadSchema.Uniform:
                    text += $"PA={_value.PA}, a={_value.A}, b={_value.B}";
                    break;
                case LoadSchema.Keystone:
                    text += $"PA={_value.PA}, PB={_value.PB}, a={_value.A}, b={_value.B}";
                    break;
                case LoadSchema.Triangular:
                    text += $"P1={_value.P1}, PA={_value.PA}, P2={_value.P2}, a={_value.A}";
                    break;
                case LoadSchema.TriangularEnd1_Keystone:
                    text += $"P1={_value.P1}, PA={_value.PA}, PB={_value.PB}, a={_value.A}, b={_value.B}";
                    break;
                case LoadSchema.Keystone_TriangularEnd2:
                    text += $"PA={_value.PA}, PB={_value.PB}, P2={_value.P2}, a={_value.A}, b={_value.B}";
                    break;
                case LoadSchema.TriangularEnds_Keystone:
                    text += $"P1={_value.P1}, PA={_value.PA}, PB={_value.PB}, P2={_value.P2}, a={_value.A}, b={_value.B}";
                    break;
            }
            if (_value.CoordinateSystem == null)
                text += " Local";
            else
                text += " " + _value.CoordinateSystem.Name;
            return text;
        }
    }
}
