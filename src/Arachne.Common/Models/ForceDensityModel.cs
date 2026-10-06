using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using Arachne.Common.Helpers;

namespace Arachne.Common.Models
{
    [Serializable]
    public class ForceDensityModel : LoadModel<double>, IEquatable<ForceDensityModel>
    {
        public enum ForceDensityTypes
        {
            ForceDensity = 0,
            Cable = 1,
			Membrane = 2,
			Curvature = 3,
		}

		public enum MembraneTypes
		{
			Undefined = -1,
			Warp = 0,
			Weft = 1,
		}

		protected static bool _isVisible = false;
        protected static double _scale = 1;
        protected static Color _color = Color.IndianRed;
        protected double _weight;
        protected double _curvatureRatioValue;
		protected ForceDensityTypes _type;
		private MembraneTypes _membraneType;
        protected bool _project;


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

        public override string Description => "Force Density";

        public double Weight
        {
            get => _weight;
            set => _weight = value;
        }

		public double CurvatureLengthValue
		{
			get => _curvatureRatioValue;
			set => _curvatureRatioValue = value;
		}

		public bool Project
		{
			get => _project;
			set => _project = value;
		}

		public MembraneTypes MembraneType { get => _membraneType; set => _membraneType = value; }

		public ForceDensityTypes ForceDensityTipe
		{
			get => _type;
			set => _type = value;
		}

		public ForceDensityModel()
        {
            _weight = 1.0;
        }

		public ForceDensityModel(LoadCaseModel loadCase, double value, double weight = 0)
            : base(loadCase, value)
		{
			_weight = weight;
			_type = ForceDensityTypes.Cable;
		}

		public ForceDensityModel(LoadCaseModel loadCase, double value, ForceDensityTypes forceDensityType, double weight = 0)
            : base (loadCase, value)
        {
            _weight = weight;
            _type = forceDensityType;
        }

		public ForceDensityModel(LoadCaseModel loadCase, double value, ForceDensityTypes forceDensityType, bool project, double weight = 0, double border = 0, MembraneTypes membraneType = MembraneTypes.Undefined)
            : base(loadCase, value)
		{
			_weight = weight;
			_type = forceDensityType;
            _project = project;
            _curvatureRatioValue = border;
            _membraneType = membraneType;
		}

		public ForceDensityModel(ForceDensityModel fd)
        {
            _loadCase = fd._loadCase;
            _value = fd._value;
            _weight = fd._weight;
            _type = fd._type;
            _project = fd._project;
            _curvatureRatioValue = fd._curvatureRatioValue;
        }

        #region Serialization
        private readonly string SERIALIZATION_RELEASE_NAME = "ForceDensityModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private readonly int SERIALIZATION_RELEASE = 2;

		protected ForceDensityModel(SerializationInfo info, StreamingContext context)
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

            _value = info.GetDouble("Value");
            _weight = info.GetDouble("Weight");
			_project = info.GetBoolean("Project");
            _curvatureRatioValue = info.GetDouble("CurvatureLengthValue");
            _type = (ForceDensityTypes)info.GetValue("Type", typeof(ForceDensityTypes));
			_membraneType = (MembraneTypes)info.GetValue("MembraneType", typeof(MembraneTypes));
		}

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Value", _value);
            info.AddValue("Weight", _weight);
            info.AddValue("Type", _type);
            info.AddValue("CurvatureLengthValue", _curvatureRatioValue);
            info.AddValue("Project", _project);
            info.AddValue("MembraneTypes", _membraneType);
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

                    Plane pl = new Plane(beam.PointFrom, Vector3d.ZAxis, uvec_h);
                    Vector3d uvec_v = uvec_h;
                    uvec_v.Rotate(Math.PI / 2, pl.Normal);

                    //double scale = DrawingHelper.GetScaleFactor(display.Viewport, beam.PointFrom + uvec_h * vec.Length);

                    int n = 1000;
                    double step = vec.Length / n;
                    List<Point3d> pts = new List<Point3d> { beam.PointFrom };
                    for (int i = 0; i < n; i++)
                    {
                        Point3d pt = beam.PointFrom + uvec_h * (step * i);
                        pt += uvec_v * scale * Scale;
                        pts.Add(pt);
                        uvec_v.Rotate(0.1, vec);
                    }
                    pts.Add(beam.PointTo);

                    display.DrawPolyline(pts, Color);
                }
            }
        }

        public static bool operator ==(ForceDensityModel fd1, ForceDensityModel fd2)
        {
            if (ReferenceEquals(fd1, fd2))
                return true;
            return !(fd1 is null) && fd1.Equals(fd2);
        }

        public static bool operator !=(ForceDensityModel fd1, ForceDensityModel fd2)
        {
            return !(fd1 == fd2);
        }

        public bool Equals(ForceDensityModel other)
        {
            return !(other is null) && 
                LoadCase == other.LoadCase && 
                Weight == other.Weight &&
                Value == other.Value &&
                Project == other.Project &&
                CurvatureLengthValue == other.CurvatureLengthValue &&
				ForceDensityTipe == other.ForceDensityTipe;
        }

        public override bool Equals(object obj)
        {
            if (obj is ForceDensityModel fd)
                return Equals(fd);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 23;
                hashCode = hashCode * -23 + LoadCase.GetHashCode();
                hashCode = hashCode * -23 + Weight.GetHashCode();
                hashCode = hashCode * -23 + Value.GetHashCode();
                hashCode = hashCode * -23 + CurvatureLengthValue.GetHashCode();
                hashCode = hashCode * -23 + Project.GetHashCode();
				hashCode = hashCode * -23 + ForceDensityTipe.GetHashCode();

                return hashCode;
            }
        }

        public override string ToString()
        {
            string text = $"FeMM Force Density: ";
            text += $"Load Case: {_loadCase}; ";
            text += $"Value={_value}; ";
            text += $"Type={_type}; ";
            text += $"Weight={_weight}; ";
            text += $"Project={_project}";
            if(_type == ForceDensityTypes.Membrane)
				text += $"Membrane type={_membraneType}";

			return text;
        }
    }
}
