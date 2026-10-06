using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
	public class SectionModel : ISerializable
	{
		public enum SectionTypes
		{
			Undefined = -1,
			SolidCircle,
			HollowCircle,
			SolidRectangle,
			HollowRectangle,
			I,
			T,
			C,
			Omega,
			Angle,
			Z,
			Generic,
			GenericShapes,
			Composite,
		}

		public enum MirrorTypes
		{
			None = -1,
			Top,
			Bottom,
			Left,
			Right,
			LeftAndTop,
			LeftAndBottom,
			RightAndTop,
			RightAndBottom,
			LeftTopOnly,
			LeftBottomOnly,
			RightTopOnly,
			RightBottomOnly
		}

		#region Viariables

		protected string _name;
		protected string _id;
		protected double _sectionArea;
		protected double _sectionArea_n0;
		protected double _sectionArea_n1;
		protected double _ixx;
		protected double _ixx_n0;
		protected double _ixx_n1;
		protected double _iyy;
		protected double _iyy_n0;
		protected double _iyy_n1;
		protected double _ixy;
		protected double _i11;
		protected double _i22;
		protected double _j;
		protected double _j_n0;
		protected double _j_n1;
		protected double _wxx;
		protected double _wyy;
		protected double _w11;
		protected double _w22;
		protected double _rho11;
		protected double _rho22;
		protected double _angleX1Rad;
		protected double _shearL1;
		protected double _shearL2;
		protected double _shearA1;
		protected double _shearA2;
		protected int _integrationPoints;
		protected SectionTypes _sectionType;
		protected double _d;
		protected double _b;
		protected double _b1;
		protected double _b2;
		protected double _l;
		protected double _t;
		protected double _t1;
		protected double _t2;
		protected double _t3;
		protected double _r;
		protected double _n0;
		protected double _n1;
		protected double _rebarDistance;
		protected double _rebarArea;
		protected MirrorTypes _mirrorType;
		protected double _mirrorGapA;
		protected double _mirrorGapB;
		protected Point2d _centroid;
		protected bool _includeTorsionalStiffness;
		protected bool _isDBSection;
		protected Geometry.Shapes _genericShapes;

		#endregion

		#region Properties

		public string Id { get => _id; set => _id = value; }
		public string Name { get => _name; set => _name = value; }
		public double SectionArea { get => _sectionArea; set => _sectionArea = value; }
		public double SectionArea_n0 { get => _sectionArea_n0; set => _sectionArea_n0 = value; }
		public double SectionArea_n1 { get => _sectionArea_n1; set => _sectionArea_n1 = value; }
		public double Ixx { get => _ixx; set => _ixx = value; }
		public double Ixx_n0 { get => _ixx_n0; set => _ixx_n0 = value; }
		public double Ixx_n1 { get => _ixx_n1; set => _ixx_n1 = value; }
		public double Iyy { get => _iyy; set => _iyy = value; }
		public double Iyy_n0 { get => _iyy_n0; set => _iyy_n0 = value; }
		public double Iyy_n1 { get => _iyy_n1; set => _iyy_n1 = value; }
		public double Ixy { get => _ixy; set => _ixy = value; }
		public double I11 { get => _i11; set => _i11 = value; }
		public double I22 { get => _i22; set => _i22 = value; }
		public double J { get => _j; set => _j = value; }
		public double J_n0 { get => _j_n0; set => _j_n0 = value; }
		public double J_n1 { get => _j_n1; set => _j_n1 = value; }
		public double W11 { get => _w11; set => _w11 = value; }
		public double W22 { get => _w22; set => _w22 = value; }
		public double Rho11 { get => _rho11; set => _rho11 = value; }
		public double Rho22 { get => _rho22; set => _rho22 = value; }
		public double Wxx { get => _wxx; set => _wxx = value; }
		public double Wyy { get => _wyy; set => _wyy = value; }
		public double AngleX1Rad { get => _angleX1Rad; set => _angleX1Rad = value; }
		public double ShearL1 { get => _shearL1; set => _shearL1 = value; }
		public double ShearL2 { get => _shearL2; set => _shearL2 = value; }
		public double ShearA1 { get => _shearA1; set => _shearA1 = value; }
		public double ShearA2 { get => _shearA2; set => _shearA2 = value; }
		public int IntegrationPoints { get => _integrationPoints; set => _integrationPoints = value; }
		public SectionTypes SectionType { get => _sectionType; set => _sectionType = value; }
		public double D { get => _d; set => _d = value; }
		public double B { get => _b; set => _b = value; }
		public double B1 { get => _b1; set => _b1 = value; }
		public double B2 { get => _b2; set => _b2 = value; }
		public double L { get => _l; set => _l = value; }
		public double T { get => _t; set => _t = value; }
		public double T1 { get => _t1; set => _t1 = value; }
		public double T2 { get => _t2; set => _t2 = value; }
		public double T3 { get => _t3; set => _t3 = value; }
		public double R { get => _r; set => _r = value; }
		public double RebarArea { get => _rebarArea; set => _rebarArea = value; }
		public double RebarDistanceFromTop { get => _rebarDistance; set => _rebarDistance = value; }
		public double N0 { get => _n0; set => _n0 = value; }
		public double N1 { get => _n1; set => _n1 = value; }
		public MirrorTypes Mirror { get => _mirrorType; set => _mirrorType = value; }
		public double MirrorGapA { get => _mirrorGapA; set => _mirrorGapA = value; }
		public double MirrorGapB { get => _mirrorGapB; set => _mirrorGapB = value; }
		public Point2d Centroid { get => _centroid; set => _centroid = value; }
		public bool IncludeTorsionalStiffness { get => _includeTorsionalStiffness; set => _includeTorsionalStiffness = value; }
		public bool IsDBSection { get => _isDBSection; set => _isDBSection = value; }
		public Geometry.Shapes GenericShapes { get => _genericShapes; set => _genericShapes = value; }
		public string Code
		{
			get
			{
				string code = "";
				double f = 1;
				switch (_sectionType)
				{
					case SectionTypes.SolidCircle:
						code = string.Format("CSS {0}", _d * f);
						break;
					case SectionTypes.HollowCircle:
						code = string.Format("CHS {0}x{1}", _d * f, _t * f);
						break;
					case SectionTypes.SolidRectangle:
						code = string.Format("RSS {0}x{1}", _d * f, _b * f);
						break;
					case SectionTypes.HollowRectangle:
						if (_b == _d)
							code = "SHS";
						else
							code = "RHS";
						code += string.Format(" {0}x{1}", _d * f, _b * f);
						if (_t1 == _t2)
							code += string.Format("x{0}", _t1 * f);
						else
							code += string.Format("x{0}x{1}", _t1 * f, _t2 * f);
						break;
					case SectionTypes.C:
						if (_l == 0)
							code = string.Format("U {0}x{1}x{2}", _d * f, _b * f, _t1 * f);
						else
							code = string.Format("C {0}x{1}x{2}", _d * f, _b * f, _t1 * f);
						break;
						// TODO: case ecc.
				}
				return code;
			}
		}
		public bool IsValid => _sectionType != SectionTypes.Undefined;

		#endregion

		public SectionModel()
		{
			_sectionType = SectionTypes.Undefined;
			_mirrorType = MirrorTypes.None;
			_isDBSection = false;
			_genericShapes = null;
		}

		public SectionModel(string id)
			: this()
		{
			_id = id;
		}

		public SectionModel(SectionModel bp)
		{
			CopyFrom(bp);
		}

		#region Serialization

		private string SERIALIZATION_RELEASE_NAME = "SERIALIZATION_RELEASE_SectionModel"; //do not modify the name!
		private int SERIALIZATION_RELEASE = 4;

		protected SectionModel(SerializationInfo info, StreamingContext context)
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
			_id = info.GetString("Id");
			_name = info.GetString("Name");
			_sectionArea = info.GetDouble("Area");
			_i11 = info.GetDouble("I11");
			_i22 = info.GetDouble("I22");
			_j = info.GetDouble("J");
			if (savedRelease > 0)
			{
				_w11 = info.GetDouble("W11");
				_w22 = info.GetDouble("W22");
				_angleX1Rad = info.GetDouble("AngleX1");
			}
			_shearL1 = info.GetDouble("ShearL1");
			_shearL2 = info.GetDouble("ShearL2");
			_shearA1 = info.GetDouble("ShearA1");
			_shearA2 = info.GetDouble("ShearA2");
			_integrationPoints = info.GetInt32("IntegrationPoints");
			_sectionType = (SectionTypes)info.GetValue("SectionType", typeof(SectionTypes));
			_d = info.GetDouble("D");
			_b = info.GetDouble("B");
			_b1 = info.GetDouble("B1");
			_b2 = info.GetDouble("B2");
			_l = info.GetDouble("L");
			_t = info.GetDouble("T");
			_t1 = info.GetDouble("T1");
			_t2 = info.GetDouble("T2");
			_t3 = info.GetDouble("T3");
			_mirrorType = (MirrorTypes)info.GetValue("MirrorType", typeof(MirrorTypes));
			_mirrorGapA = info.GetDouble("MirrorGapA");
			_mirrorGapB = info.GetDouble("MirrorGapB");
			_centroid = (Point2d)info.GetValue("Centroid", typeof(Point2d));
			_includeTorsionalStiffness = info.GetBoolean("IncludeTorsionalStiffness");
			_isDBSection = info.GetBoolean("IsDBSection");

			if (savedRelease >= 2)
				_genericShapes = (Geometry.Shapes)info.GetValue("GenericShapes", typeof(Geometry.Shapes));
			else
				_genericShapes = null;

			if (savedRelease >= 3)
				_r = info.GetDouble("R");
			else
				_r = 0;

			if (savedRelease >= 4)
			{
				_n0 = info.GetDouble("N0");
				_n1 = info.GetDouble("N1");
				_j_n0 = info.GetDouble("_j_n0");
				_j_n1 = info.GetDouble("_j_n1");
				_ixx_n0 = info.GetDouble("_ixx_n0");
				_ixx_n1 = info.GetDouble("_ixx_n1");
				_iyy_n0 = info.GetDouble("_iyy_n0");
				_iyy_n1 = info.GetDouble("_iyy_n1");
				_sectionArea_n0 = info.GetDouble("_sectionArea_n0");
				_sectionArea_n1 = info.GetDouble("_sectionArea_n1");
				_rebarArea = info.GetDouble("RebarArea");
				_rebarDistance = info.GetDouble("RebarDistance");
			}
			else
			{
				_n0 = 0;
				_n1 = 0;
				_sectionArea_n0 = 0;
				_sectionArea_n1 = 0;
				_j_n0 = 0;
				_j_n1 = 0;
				_ixx_n0 = 0;
				_ixx_n1 = 0;
				_iyy_n0 = 0;
				_iyy_n1 = 0;
				_rebarArea = 0;
				_rebarDistance = 0;
			}
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("Id", _id);
			info.AddValue("Name", _name);
			info.AddValue("Area", _sectionArea);
			info.AddValue("I11", _i11);
			info.AddValue("I22", _i22);
			info.AddValue("J", _j);
			info.AddValue("W11", _w11);
			info.AddValue("W22", _w22);
			info.AddValue("AngleX1", _angleX1Rad);
			info.AddValue("ShearL1", _shearL1);
			info.AddValue("ShearL2", _shearL2);
			info.AddValue("ShearA1", _shearA1);
			info.AddValue("ShearA2", _shearA2);
			info.AddValue("IntegrationPoints", _integrationPoints);
			info.AddValue("SectionType", _sectionType, typeof(SectionTypes));
			info.AddValue("D", _d);
			info.AddValue("B", _b);
			info.AddValue("B1", _b1);
			info.AddValue("B2", _b2);
			info.AddValue("L", _l);
			info.AddValue("T", _t);
			info.AddValue("T1", _t1);
			info.AddValue("T2", _t2);
			info.AddValue("T3", _t3);
			info.AddValue("MirrorType", _mirrorType, typeof(MirrorTypes));
			info.AddValue("MirrorGapA", _mirrorGapA);
			info.AddValue("MirrorGapB", _mirrorGapB);
			info.AddValue("Centroid", _centroid, typeof(Point2d));
			info.AddValue("IncludeTorsionalStiffness", _includeTorsionalStiffness);
			info.AddValue("IsDBSection", _isDBSection);
			//from release 2
			info.AddValue("GenericShapes", _genericShapes);
			//from release 3
			info.AddValue("R", _r);
			//from release 4
			info.AddValue("N0", _n0);
			info.AddValue("N1", _n1);
			info.AddValue("_j_n0", _j_n0);
			info.AddValue("_j_n1", _j_n1);
			info.AddValue("_ixx_n0", _ixx_n0);
			info.AddValue("_ixx_n1", _ixx_n1);
			info.AddValue("_iyy_n0", _iyy_n0);
			info.AddValue("_iyy_n1", _iyy_n1);
			info.AddValue("_sectionArea_n0", _sectionArea_n0);
			info.AddValue("_sectionArea_n1", _sectionArea_n1);
			info.AddValue("RebarArea", _rebarArea);
			info.AddValue("RebarDistance", _rebarDistance);
		}

		#endregion

		public void CopyFrom(SectionModel bp)
		{
			if (bp._id != null)
				_id = bp._id;
			_name = bp._name;
			_sectionArea = bp._sectionArea;
			_i11 = bp._i11;
			_i22 = bp._i22;
			_j = bp._j;
			_w11 = bp._w11;
			_w22 = bp._w22;
			_angleX1Rad = bp._angleX1Rad;
			_shearL1 = bp._shearL1;
			_shearL2 = bp._shearL2;
			_shearA1 = bp._shearA1;
			_shearA2 = bp._shearA2;
			_integrationPoints = bp._integrationPoints;
			_sectionType = bp._sectionType;
			_d = bp._d;
			_b = bp._b;
			_b1 = bp._b1;
			_b2 = bp._b2;
			_l = bp._l;
			_t = bp._t;
			_t1 = bp._t1;
			_t2 = bp._t2;
			_t3 = bp._t3;
			_r = bp._r;
			_mirrorType = bp._mirrorType;
			_mirrorGapA = bp._mirrorGapA;
			_mirrorGapB = bp._mirrorGapB;
			_centroid = bp._centroid;
			_includeTorsionalStiffness = bp._includeTorsionalStiffness;
			_isDBSection = bp.IsDBSection;
			_genericShapes = bp.GenericShapes;
			_n0 = bp.N0;
			_n1 = bp.N1;
			_rebarArea = bp.RebarArea;
			_rebarDistance = bp.RebarDistanceFromTop;
			_sectionArea_n0 = bp.SectionArea_n0;
			_sectionArea_n1 = bp.SectionArea_n1;
			_iyy_n0 = bp.Iyy_n0;
			_iyy_n1 = bp.Iyy_n1;
			_ixx_n0 = bp.Ixx_n0;
			_ixx_n1 = bp.Ixx_n1;
			_j_n0 = bp.J_n0;
			_j_n1 = bp.J_n1;
		}

		private Geometry.Shapes GetShapes()
		{
			switch (_sectionType)
			{
				case SectionTypes.GenericShapes:
					Geometry.Shapes movedShapes = new Geometry.Shapes(_genericShapes);
					movedShapes.MoveToMin();
					return movedShapes;
				default:
					GPC.Geometry.Polygon2d border = new GPC.Geometry.Polygon2d();
					GPC.Geometry.Polygon2d[] holes = null;

					double d1 = -(_b1 - Math.Max(_b1, _b2)) / 2;
					double d2 = -(_b2 - Math.Max(_b1, _b2)) / 2;
					switch (_sectionType)
					{
						case SectionTypes.SolidCircle:
							break;
						case SectionTypes.HollowCircle:
							break;
						case SectionTypes.SolidRectangle:
							border.Add(0, 0);
							border.Add(0, _d);
							border.Add(_b, _d);
							border.Add(_b, 0);
							break;
						case SectionTypes.HollowRectangle:
							border.Add(0, 0);
							border.Add(0, _d);
							border.Add(_b, _d);
							border.Add(_b, 0);
							holes = new[] { new GPC.Geometry.Polygon2d {
								{ _t2, _t1 },
								{ _t2, _d - _t1 },
								{ _b - _t2, _d - _t1 },
								{ _b - _t2, _t1 }}};
							break;
						case SectionTypes.I:
							border.Add(d1, 0);
							border.Add(d1, _t1);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2, _t1);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2, _d - _t2);
							border.Add(d2, _d - _t2);
							border.Add(d2, _d);
							border.Add(d2 + _b2, _d);
							border.Add(d2 + _b2, _d - _t2);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2 + _t3, _d - _t2);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2 + _t3, _t1);
							border.Add(d1 + _b1, _t1);
							border.Add(d1 + _b1, 0);
							break;
						case SectionTypes.T:
							border.Add((_b - _t2) / 2, 0);
							border.Add((_b - _t2) / 2, _d - _t1);
							if (_l > 0 && _t3 > 0)
							{
								border.Add(_t3, _d - _t1);
								border.Add(_t3, _d - _t1 - _l);
								border.Add(0, _d - _t1 - _l);
							}
							else
							{
								border.Add(0, _d - _t1);
							}
							border.Add(0, _d);
							border.Add(_b, _d);
							if (_l > 0 && _t3 > 0)
							{
								border.Add(_b, _d - _t1 - _l);
								border.Add(_b - _t3, _d - _t1 - _l);
								border.Add(_b - _t3, _d - _t1);
							}
							else
							{
								border.Add(_b, _d - _t1);
							}
							border.Add((_b - _t2) / 2 + _t2, _d - _t1);
							border.Add((_b - _t2) / 2 + _t2, 0);
							break;
						case SectionTypes.C:
							border.Add(0, 0);
							border.Add(0, _d);
							border.Add(_b, _d);
							if (_l > 0 && _t3 > 0)
							{
								border.Add(_b, _d - _t1 - _l);
								border.Add(_b - _t3, _d - _t1 - _l);
								border.Add(_b - _t3, _d - _t1);
							}
							else
							{
								border.Add(_b, _d - _t1);
							}
							border.Add(_t2, _d - _t1);
							border.Add(_t2, _t1);
							if (_l > 0 && _t3 > 0)
							{
								border.Add(_b - _t3, _t1);
								border.Add(_b - _t3, _t1 + _l);
								border.Add(_b, _t1 + _l);
							}
							else
							{
								border.Add(_b, _t1);
							}
							border.Add(_b, 0);
							break;
						case SectionTypes.Omega:
							border.Add(0, _l);
							border.Add(0, _d + _l);
							if (_l > 0 && _t3 > 0)
							{
								border.Add(_b - _t3, _d + _l);
								border.Add(_b - _t3, _d + _l * 2);
								border.Add(_b, _d + _l * 2);
							}
							else
							{
								border.Add(_b, _d + _l);
							}
							border.Add(_b, _d + _l - _t1);
							border.Add(_t2, _d + _l - _t1);
							border.Add(_t2, _l + _t1);
							border.Add(_b, _l + _t1);
							if (_l > 0 && _t3 > 0)
							{
								border.Add(_b, 0);
								border.Add(_b - _t3, 0);
								border.Add(_b - _t3, _l);
							}
							else
							{
								border.Add(_b, _l);
							}
							border.Add(_b, 0);
							break;
						case SectionTypes.Angle:
							border.Add(0, 0);
							border.Add(0, _d);
							border.Add(_t2, _d);
							border.Add(_t2, _t1);
							border.Add(_b, _t1);
							border.Add(_b, 0);
							break;
						case SectionTypes.Z:
							border.Add(_b2 - _t3, 0);
							border.Add(_b2 - _t3, _d - _t2);
							border.Add(0, _d - _t2);
							border.Add(0, _d);
							border.Add(_b2, _d);
							border.Add(_b2, _t1);
							border.Add(_b2 - _t3 + _b1, _t1);
							border.Add(_b2 - _t3 + _b1, 0);
							break;
						case SectionTypes.Composite:
							border.Add(d1, 0);
							border.Add(d1, _t1);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2, _t1);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2, _d - _t2);
							border.Add(d2, _d - _t2);
							border.Add(d2, _d);
							border.Add(d2 + _b2, _d);
							border.Add(d2 + _b2, _d - _t2);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2 + _t3, _d - _t2);
							border.Add((Math.Max(_b1, _b2) - _t3) / 2 + _t3, _t1);
							border.Add(d1 + _b1, _t1);
							border.Add(d1 + _b1, 0);
							break;
					}

					GPC.Geometry.Shape2d shape = new GPC.Geometry.Shape2d(border, holes);

					shape.MoveToMin();
					return new Geometry.Shapes(new[] { shape });
			}
		}

		public void CalculateSection()
		{
			switch (_sectionType)
			{
				case SectionTypes.SolidCircle:
					_sectionArea = Math.Pow(_d / 2, 2) * Math.PI;
					_i11 = Math.PI * Math.Pow(_d, 4) / 64;
					_i22 = _i11;
					_w11 = _i11 / (_d * 0.5);
					_w22 = _w11;
					break;
				case SectionTypes.HollowCircle:
					_sectionArea = Math.Pow(_d / 2, 2) * Math.PI - Math.Pow(_d / 2 - _t, 2) * Math.PI;
					_i11 = Math.PI * (Math.Pow(_d, 4) - Math.Pow(_d - _t * 2, 4)) / 64;
					_i22 = _i11;
					_w11 = _i11 / (_d * 0.5);
					_w22 = _w11;
					break;
				case SectionTypes.Composite:
					//TODO: implementare
					break;
				default:
					Geometry.Shapes shapes;
					if (_mirrorType == MirrorTypes.None)
					{
						shapes = GetShapes();
					}
					else
					{
						double a = -1;
						double b = 0;
						double c = -_mirrorGapA / 2.0;
						List<GPC.Geometry.Shape2d> shapeList;
						shapeList = new List<GPC.Geometry.Shape2d>();
						foreach (GPC.Geometry.Shape2d main in GetShapes())
						{
							shapeList.Add(main);
							shapeList.Add(main.Mirror2d(a, b, c));
						}
						shapes = new Geometry.Shapes(shapeList);
					}
					shapes.MoveToMin();
					double xG, yG;
					if (_sectionType == SectionTypes.GenericShapes)
					{
						shapes.GetGeometricalDatas(true, 0.000000001, out xG, out yG, out _sectionArea, out _ixx, out _iyy,
							out _ixy, out _i11, out _i22, out _angleX1Rad, out _wxx, out _wyy, out _w11, out _w22, out _rho11, out _rho22);
						if (_angleX1Rad < -0.01)
						{
							_angleX1Rad += Math.PI;
						}
					}
					else
					{
						shapes.GetGeometricalDatas(false, 0.000000001, out xG, out yG, out _sectionArea, out _ixx, out _iyy,
							out _ixy, out _i11, out _i22, out _angleX1Rad, out _wxx, out _wyy, out _w11, out _w22, out _rho11, out _rho22);
					}
					_centroid = new Point2d(xG, yG);
					break;
			}
		}

		public override string ToString()
		{
			string text = "FeMM Section";
			if (_id != null && _id != string.Empty && _id != "")
				text += $" #{_id}";
			if (_name != null && _name != string.Empty && _name != "")
				text += $" #{_name}";
			text += $"\nSection: {_sectionType}";
			text += string.Format("\nArea: {0:F2}", _sectionArea);
			text += string.Format($"\nCentroid: {Math.Round(_centroid.X, 2)}, {Math.Round(_centroid.Y, 2)}");
			text += string.Format("\nPrincipal angle: {0:F4} deg", -Rhino.RhinoMath.ToDegrees(_angleX1Rad));
			return text;
		}
	}
}
