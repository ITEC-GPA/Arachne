using Arachne.Common.Helpers;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
	[Serializable]
	public class PlateModel : ElementModel
	{
		public enum PlateType
		{
			Invalid,
			Tri3,
			Quad4,
			Tri6,
			Quad8,
			Quad9,
			Generic,
		}

		protected static bool _isAxisVisible = false;
		protected static double _axisScale = 1;
		protected string _plateId;
		protected ObservableCollection<Point3d> _points;
		protected double _angleDeg;
		protected double _offset;
		protected PlatePropertyModel _property;
		protected Brep _shape;

		public static bool IsAxisVisible
		{
			get => _isAxisVisible;
			set => _isAxisVisible = value;
		}

		public static double AxisScale
		{
			get => _axisScale;
			set => _axisScale = value;
		}

		public string PlateId
		{
			get => _plateId;
			set => _plateId = value;
		}

		public ObservableCollection<Point3d> Points { get => _points; set => _points = value; }

		public double AngleDeg
		{
			get => _angleDeg;
			set
			{
				_angleDeg = value;
				CreateShapes();
			}
		}

		public double Offset
		{
			get => _offset;
			set
			{
				_offset = value;
				CreateShapes();
			}
		}

		public PlatePropertyModel PlateProperty
		{
			get => _property;
			set
			{
				_property = value;
				CreateShapes();
			}
		}

		public PlateType Type
		{
			get
			{
				switch (_points.Count)
				{
					case 3:
						return PlateType.Tri3;
					case 4:
						return PlateType.Quad4;
					case 6:
						return PlateType.Tri6;
					case 8:
						return PlateType.Quad8;
					case 9:
						return PlateType.Quad9;
				}
				return PlateType.Generic;
			}
		}

		public Brep Shape => _shape;

		public bool IsValid => _points.Count != 0 && _shape != null && Type != PlateType.Invalid;

		public PlateModel(string plateId = "", Point3d[] points = null, PlatePropertyModel pp = null, GroupModel[] groups = null)
			: base()
		{
			_plateId = plateId;
			if (points != null)
				_points = new ObservableCollection<Point3d>(points);
			else
				_points = new ObservableCollection<Point3d>();
			_points.CollectionChanged += (s, e) => CreateShapes();
			_angleDeg = 0;
			_offset = 0;
			_property = pp;
			if (groups != null)
			{
				foreach (GroupModel group in groups)
					_groups.Add(new GroupModel(group));
			}
			CreateShapes();
		}

		public PlateModel(Point3d[] points, PlatePropertyModel pp, GroupModel grp = null)
			: base()
		{
			_plateId = "";
			_points = new ObservableCollection<Point3d>(points);
			_points.CollectionChanged += (s, e) => CreateShapes();
			_angleDeg = 0;
			_offset = 0;
			_property = pp;
			if (grp != null)
				_groups.Add(grp);
			CreateShapes();
		}

		public PlateModel(PlateModel plate)
			: base(plate)
		{
			_plateId = plate._plateId;			
            _points = new ObservableCollection<Point3d>(plate._points);
            _points.CollectionChanged += (s, e) => CreateShapes();
            _angleDeg = plate._angleDeg;
			_offset = plate._offset;
			_property = plate._property;
			_angleDeg = plate._angleDeg;
            CreateShapes();
		}

		#region Serialization

		private string SERIALIZATION_RELEASE_NAME = "PlateModel_SERIALIZATION_RELEASE"; //do not modify the name!
		private int SERIALIZATION_RELEASE = 1;

		protected PlateModel(SerializationInfo info, StreamingContext context)
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
			_plateId = info.GetString("PlateId");
			_points = new ObservableCollection<Point3d>();
			int n_points = info.GetInt32("NPoints");
			for (int i = 0; i < n_points; i++)
				_points.Add((Point3d)info.GetValue($"Point{i}", typeof(Point3d)));
			_points.CollectionChanged += (s, e) => CreateShapes();
			_angleDeg = info.GetDouble("Angle");
			_offset = info.GetDouble("Offset");
			_property = (PlatePropertyModel)info.GetValue("PlateProperty", typeof(PlatePropertyModel));
			CreateShapes();
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("PlateId", _plateId);
			info.AddValue("NPoints", _points.Count);
			for (int i = 0; i < _points.Count; i++)
				info.AddValue($"Point{i}", _points[i], typeof(Point3d));
			info.AddValue("Angle", _angleDeg);
			info.AddValue("Offset", _offset);
			info.AddValue("PlateProperty", _property, typeof(PlatePropertyModel));
		}

		#endregion

		public override void DrawWireframe(DisplayPipeline display, RhinoViewport viewport, Color color)
		{
			display.DrawPolygon(_points, color, false);
		}

		public override void DrawSolid(DisplayPipeline display, RhinoViewport viewport, Color color)
		{
			if (_shape != null)
				display.DrawBrepWires(_shape, color);
		}

		public override void DrawAttributes(DisplayPipeline display, RhinoViewport viewport)
		{
			base.DrawAttributes(display, viewport);
			if (IsAxisVisible)
			{
				double dimension;
				double x = 0;
				double y = 0;
				double z = 0;
				int count = 0;
				BoundingBox bbox = new BoundingBox(_points);
				foreach (Point3d p in _points)
				{
					x += p.X;
					y += p.Y;
					z += p.Z;
					count++;
				}
				Point3d g = new Point3d(x / count, y / count, z / count);
				dimension = bbox.Diagonal.Length * 0.1 * AxisScale;
				GetLocalAxes(out Vector3d xAxes, out Vector3d yAxes, out Vector3d zAxes);
				display.DrawArrow(new Line(g, g + xAxes * dimension), Color.Red, 0, DrawingHelper.ArrowSize);
				display.DrawArrow(new Line(g, g + yAxes * dimension), Color.Green, 0, DrawingHelper.ArrowSize);
				display.DrawArrow(new Line(g, g + zAxes * dimension), Color.Blue, 0, DrawingHelper.ArrowSize);
			}
		}

		public override void CreateShapes()
		{
			if (_property == null || Type == PlateType.Invalid)
				return;

			Brep brep;
			Vector3d v;

			if (Type == PlateType.Tri3)
			{
				NurbsSurface surface = NurbsSurface.CreateFromCorners(_points[0], _points[1], _points[2]);
				v = surface.NormalAt(0.5, 0.5);
				brep = surface.ToBrep();
			}
			else if (Type == PlateType.Quad4)
			{
				NurbsSurface surface = NurbsSurface.CreateFromCorners(_points[0], _points[1], _points[2], _points[3]);
				v = surface.NormalAt(0.5, 0.5);
				brep = surface.ToBrep();
			}
			else
			{
				Polyline poly = new Polyline();
				poly.AddRange(_points);
				if(!poly.IsClosed) 
					poly.Add(_points[0]);
				var pp = poly.ToPolylineCurve();
				brep = Brep.CreatePlanarBreps(pp, 0.001).FirstOrDefault();
				v = brep.Surfaces.FirstOrDefault().NormalAt(0.5, 0.5);
			}

			v.Unitize();

			// Without a thickness the view is the surface (FeMM: the Load Patch type)
			if (!(_property.Thickness > 0))
			{
				_shape = brep;
			}
			else
			{
				brep.Translate(v * (-_property.Thickness / 2 + _offset));
				LineCurve line = new LineCurve(_points[0], _points[0] + v * _property.Thickness);
				_shape = brep.Faces[0].CreateExtrusion(line, true);
			}
		}

		public void GetLocalAxes(out Vector3d axes1, out Vector3d axes2, out Vector3d axes3)
		{
			//orientation like Straus7
			if (Type == PlateType.Tri3)
			{
				//   3
				//
				//1     2
				//xAxes from 1 to mid23
				Vector3d from2To3;
				Point3d mid23;
				Vector3d dir0, dir1;
				from2To3 = _points[2] - _points[1];
				mid23 = _points[1] + 0.5 * from2To3;
				axes1 = mid23 - _points[0];
				axes1.Unitize();
				dir0 = _points[1] - _points[0];
				dir1 = _points[2] - _points[1];
				axes3 = Vector3d.CrossProduct(dir0, dir1);
				axes3.Unitize();
				axes2 = Vector3d.CrossProduct(axes3, axes1);
				axes2.Unitize();
			}
			else if (Type == PlateType.Quad4)
			{
				//4      3
				//
				//
				//1      2
				//xAxes from mid 14 to mid23 
				Vector3d from1To4, from2To3, dir0, dir1;
				Point3d mid14, mid23;
				from1To4 = _points[3] - _points[0];
				from2To3 = _points[2] - _points[1];
				mid14 = _points[0] + 0.5 * from1To4;
				mid23 = _points[1] + 0.5 * from2To3;
				axes1 = mid23 - mid14;
				axes1.Unitize();
				dir0 = _points[1] - _points[0];
				dir0.Unitize();
				dir1 = _points[2] - _points[1];
				dir1.Unitize();
				if (Vector3d.Multiply(dir0, dir1) > 0.99999)
				{
					from1To4.Unitize();
					dir1 = from1To4;
				}
				axes3 = Vector3d.CrossProduct(dir0, dir1);
				axes2 = Vector3d.CrossProduct(axes3, axes1);
				axes2.Unitize();
				//need a recalculation of zdir, points could not be on a plane
				axes3 = Vector3d.CrossProduct(axes1, axes2);
				axes3.Unitize();
			}
			else
			{
				//4      3
				//
				//
				//1      2
				//xAxes from mid 14 to mid23 
				Vector3d from1To4, from2To3, dir0, dir1;
				Point3d mid14, mid23;
				from1To4 = _points[3] - _points[0];
				from2To3 = _points[2] - _points[1];
				mid14 = _points[0] + 0.5 * from1To4;
				mid23 = _points[1] + 0.5 * from2To3;
				axes1 = mid23 - mid14;
				axes1.Unitize();
				dir0 = _points[1] - _points[0];
				dir0.Unitize();
				dir1 = _points[2] - _points[1];
				dir1.Unitize();
				if (Vector3d.Multiply(dir0, dir1) > 0.99999)
				{
					from1To4.Unitize();
					dir1 = from1To4;
				}
				axes3 = Vector3d.CrossProduct(dir0, dir1);
				axes2 = Vector3d.CrossProduct(axes3, axes1);
				axes2.Unitize();
				//need a recalculation of zdir, points could not be on a plane
				axes3 = Vector3d.CrossProduct(axes1, axes2);
				axes3.Unitize();
			}

			//Rotating of angle
			if (_angleDeg != 0)
			{
				double radAngle = _angleDeg * System.Math.PI / 180d;
				GPC.Geometry.Point3d pOnX = new GPC.Geometry.Point3d(axes1.X, axes1.Y, axes1.Z);
				GPC.Geometry.Point3d pOnY = new GPC.Geometry.Point3d(axes2.X, axes2.Y, axes2.Z);
				GPC.Geometry.CoordinateSystem trans = new GPC.Geometry.CoordinateSystem(GPC.Geometry.Point3d.Origin, pOnX, pOnY);
				GPC.Geometry.Point3d newDirX = trans.ToGlobal(new GPC.Geometry.Point3d(Math.Cos(radAngle), Math.Sin(radAngle), 0));
				axes1 = new Vector3d(newDirX.X, newDirX.Y, newDirX.Z);
				axes2 = Vector3d.CrossProduct(axes3, axes1);
			}
		}

		public override string ToString()
		{
			string text = "FeMM Plate";
			if (_plateId != string.Empty)
				text += $" #{_plateId}";
			text += $"\nProperty: {_property}";
			text += "\nGroups: " + string.Join(", ", _groups);
			text += "\nPoints: " + string.Join("; ", _points);
			text += $"\nAngle: {_angleDeg}";
			text += $"\nOffset: {_offset}";
			text += "\n" + base.ToString();
			return text;
		}
	}
}
