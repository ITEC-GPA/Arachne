using Arachne.Common.Helpers;
using Rhino.Collections;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
    [Serializable]
    public class BeamModel : ElementModel
    {
        protected static bool _isAxisVisible = false;
        protected static double _axisScale = 1;
        protected string _beamId;
        protected Point3d _pointFrom;
        protected Point3d _pointTo;
        protected double _angleDeg;
        protected Vector2d _offset;
        protected BeamPropertyModel _beamProperty;
        protected List<Brep> _shapes;

        public double Length => (_pointTo - _pointFrom).Length;

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

        public string BeamId
        {
            get => _beamId;
            set => _beamId = value;
        }

        public Point3d PointFrom
        {
            get => _pointFrom;
            set
            {
                _pointFrom = value;
                CreateShapes();
            }
        }

        public Point3d PointTo
        {
            get => _pointTo;
            set
            {
                _pointTo = value;
                CreateShapes();
            }
        }

        public double AngleDeg
        {
            get => _angleDeg;
            set
            {
                _angleDeg = value;
                CreateShapes();
            }
        }

        public Vector2d Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                CreateShapes();
            }
        }

        public BeamPropertyModel BeamProperty
        {
            get => _beamProperty;
            set
            {
                _beamProperty = value;
                CreateShapes();
            }
        }

        public Brep[] Shapes => _shapes.ToArray();

        public bool IsValid
        {
            get
            {
                return _pointFrom != Point3d.Unset && _pointTo != Point3d.Unset &&
                    _pointFrom.DistanceTo(_pointTo) > 0.0 && _shapes != null && _shapes.Count > 0;
            }
        }

        public bool IsVertical
        {
            get
            {
                Vector3d v = ToVector();
                v.Unitize();
                return Vector3d.CrossProduct(Vector3d.ZAxis, v).Length < 1.0E-12;
            }
        }

        public BeamModel(string beamId = "", Point3d? pointFrom = null, Point3d? pointTo = null, BeamPropertyModel bp = null, GroupModel[] groups = null)
            : base()
        {
            _attributes.Add(new ElementIdAttributeModel(beamId));
            _beamId = "";
            _pointFrom = pointFrom ?? Point3d.Unset;
            _pointTo = pointTo ?? Point3d.Unset;
            _angleDeg = 0;
            _offset = Vector2d.Zero;
            _beamProperty = bp;
            if (groups != null)
            {
                foreach (GroupModel group in groups)
                    _groups.Add(new GroupModel(group));
            }
            CreateShapes();
        }

        public BeamModel(Point3d pointFrom, Point3d pointTo, BeamPropertyModel bp, GroupModel grp = null, double angleDeg = 0)
            : base()
        {
            _beamId = "";
            _pointFrom = pointFrom;
            _pointTo = pointTo;
            _angleDeg = angleDeg;
            _offset = Vector2d.Zero;
            _beamProperty = bp;
            if (grp != null)
                _groups.Add(grp);
            CreateShapes();
        }

        public BeamModel(BeamModel beam)
            : base(beam)
        {
            _beamId = beam._beamId;
            _pointFrom = beam._pointFrom;
            _pointTo = beam._pointTo;
            _angleDeg = beam._angleDeg;
            _offset = beam._offset;
            _beamProperty = beam._beamProperty;
            if (beam._shapes != null)
                _shapes = new List<Brep>(beam._shapes);
        }

        #region Serialization

        private readonly string SERIALIZATION_RELEASE_NAME = "BeamModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private readonly int SERIALIZATION_RELEASE = 1;

        protected BeamModel(SerializationInfo info, StreamingContext context)
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
            _beamId = info.GetString("BeamId");
            _pointFrom = (Point3d)info.GetValue("PointFrom", typeof(Point3d));
            _pointTo = (Point3d)info.GetValue("PointTo", typeof(Point3d));
            _angleDeg = info.GetDouble("Angle");
            _offset = (Vector2d)info.GetValue("Offset", typeof(Vector2d));
            _beamProperty = (BeamPropertyModel)info.GetValue("BeamProperty", typeof(BeamPropertyModel));
            CreateShapes();
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("BeamId", _beamId);
            info.AddValue("PointFrom", _pointFrom, typeof(Point3d));
            info.AddValue("PointTo", _pointTo, typeof(Point3d));
            info.AddValue("Angle", _angleDeg);
            info.AddValue("Offset", _offset, typeof(Vector2d));
            info.AddValue("BeamProperty", _beamProperty, typeof(BeamPropertyModel));
        }

        #endregion

        public Vector3d ToVector()
        {
            return _pointTo - _pointFrom;
        }

        /*public override void Merge(ElementModel other)
		{
			base.Merge(other);
			BeamModel beam = (BeamModel)other;
			if (beam.Angle != 0 && _angle == 0)
				_angle = beam.Angle;
		}*/

        public override void DrawWireframe(DisplayPipeline display, RhinoViewport viewport, Color color)
        {
            display.DrawLine(_pointFrom, _pointTo, color);
        }

        public override void DrawSolid(DisplayPipeline display, RhinoViewport viewport, Color color)
        {
            if (_shapes != null)
            {
                foreach (Brep shape in _shapes)
                    display.DrawBrepWires(shape, color);
            }
        }

        public override void DrawAttributes(DisplayPipeline display, RhinoViewport viewport)
        {
            base.DrawAttributes(display, viewport);
            if (IsAxisVisible)
            {
                double length = (_pointTo - _pointFrom).Length;
                GetLocalAxes(true, out Vector3d axes1, out Vector3d axes2, out Vector3d axes3, out Transform toGlobal);
                Point3d origin = new Point3d(_offset.X, _offset.Y, 0.5 * length);
                origin.Transform(toGlobal);

                double dimension = length * 0.1 * AxisScale;
                display.DrawArrow(new Line(origin, origin + axes1 * dimension), Color.Red, 0, DrawingHelper.ArrowSize);
                display.DrawArrow(new Line(origin, origin + axes2 * dimension), Color.Green, 0, DrawingHelper.ArrowSize);
                display.DrawArrow(new Line(origin, origin + axes3 * dimension), Color.Blue, 0, DrawingHelper.ArrowSize);
            }
        }

        //public override void CreateShapes()
        //{
        //    if (_property == null)
        //        return;
        //    Brep main_surface = null;
        //    double tol = 0.001;
        //    Point2d c = _property.Centroid;
        //    switch (_property.Section)
        //    {
        //        case SectionType.SolidCircle:
        //            {
        //                ArcCurve border = new ArcCurve(new Circle(_property.D / 2));
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.HollowCircle:
        //            {
        //                ArcCurve border = new ArcCurve(new Circle(_property.D / 2));
        //                ArcCurve inner = new ArcCurve(new Circle(_property.D / 2 - _property.T));
        //                CurveList curves = new CurveList();
        //                curves.Add(border);
        //                curves.Add(inner);
        //                Brep[] breps = Brep.CreatePlanarBreps(curves, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.SolidRectangle:
        //            {
        //                List<Point3d> pts = new List<Point3d>();
        //                pts.Add(new Point3d(-_property.B / 2, -_property.D / 2, 0));
        //                pts.Add(new Point3d(_property.B / 2, -_property.D / 2, 0));
        //                pts.Add(new Point3d(_property.B / 2, _property.D / 2, 0));
        //                pts.Add(new Point3d(-_property.B / 2, _property.D / 2, 0));
        //                pts.Add(new Point3d(-_property.B / 2, -_property.D / 2, 0));
        //                PolylineCurve border = new PolylineCurve(pts);
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.HollowRectangle:
        //            {
        //                List<Point3d> ptsOut = new List<Point3d>();
        //                ptsOut.Add(new Point3d(-_property.B / 2, -_property.D / 2, 0));
        //                ptsOut.Add(new Point3d(_property.B / 2, -_property.D / 2, 0));
        //                ptsOut.Add(new Point3d(_property.B / 2, _property.D / 2, 0));
        //                ptsOut.Add(new Point3d(-_property.B / 2, _property.D / 2, 0));
        //                ptsOut.Add(new Point3d(-_property.B / 2, -_property.D / 2, 0));
        //                PolylineCurve border = new PolylineCurve(ptsOut);
        //                List<Point3d> ptsIn = new List<Point3d>();
        //                ptsIn.Add(new Point3d(-_property.B / 2 + _property.T2, -_property.D / 2 + _property.T1, 0));
        //                ptsIn.Add(new Point3d(_property.B / 2 - _property.T2, -_property.D / 2 + _property.T1, 0));
        //                ptsIn.Add(new Point3d(_property.B / 2 - _property.T2, _property.D / 2 - _property.T1, 0));
        //                ptsIn.Add(new Point3d(-_property.B / 2 + _property.T2, _property.D / 2 - _property.T1, 0));
        //                ptsIn.Add(new Point3d(-_property.B / 2 + _property.T2, -_property.D / 2 + _property.T1, 0));
        //                PolylineCurve inner = new PolylineCurve(ptsIn);

        //                CurveList curves = new CurveList();
        //                curves.Add(border);
        //                curves.Add(inner);
        //                Brep[] breps = Brep.CreatePlanarBreps(curves, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.I:
        //            {
        //                List<Point3d> pts = new List<Point3d>();
        //                pts.Add(new Point3d(-_property.B1 / 2, -c.Y, 0));
        //                pts.Add(new Point3d(_property.B1 / 2, -c.Y, 0));
        //                pts.Add(new Point3d(_property.B1 / 2, -c.Y + _property.T1, 0));
        //                pts.Add(new Point3d(_property.T3 / 2, -c.Y + _property.T1, 0));
        //                pts.Add(new Point3d(_property.T3 / 2, -c.Y + _property.D - _property.T2, 0));
        //                pts.Add(new Point3d(_property.B2 / 2, -c.Y + _property.D - _property.T2, 0));
        //                pts.Add(new Point3d(_property.B2 / 2, -c.Y + _property.D, 0));
        //                pts.Add(new Point3d(-_property.B2 / 2, -c.Y + _property.D, 0));
        //                pts.Add(new Point3d(-_property.B2 / 2, -c.Y + _property.D - _property.T2, 0));
        //                pts.Add(new Point3d(-_property.T3 / 2, -c.Y + _property.D - _property.T2, 0));
        //                pts.Add(new Point3d(-_property.T3 / 2, -c.Y + _property.T1, 0));
        //                pts.Add(new Point3d(-_property.B1 / 2, -c.Y + _property.T1, 0));
        //                pts.Add(new Point3d(-_property.B1 / 2, -c.Y, 0));
        //                PolylineCurve border = new PolylineCurve(pts);
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.T:
        //            {
        //                List<Point3d> pts = new List<Point3d>();
        //                pts.Add(new Point3d(-_property.T2 / 2, -c.Y, 0));
        //                pts.Add(new Point3d(_property.T2 / 2, -c.Y, 0));
        //                pts.Add(new Point3d(_property.T2 / 2, -c.Y + _property.D - _property.T1, 0));
        //                pts.Add(new Point3d(_property.B / 2 - _property.T3, -c.Y + _property.D - _property.T1, 0));
        //                if (_property.T3 > 0 && _property.L > 0)
        //                {
        //                    pts.Add(new Point3d(_property.B / 2 - _property.T3, -c.Y + _property.D - _property.T1 - _property.L, 0));
        //                    pts.Add(new Point3d(_property.B / 2, -c.Y + _property.D - _property.T1 - _property.L, 0));
        //                }
        //                pts.Add(new Point3d(_property.B / 2, -c.Y + _property.D, 0));
        //                pts.Add(new Point3d(-_property.B / 2, -c.Y + _property.D, 0));
        //                pts.Add(new Point3d(-_property.B / 2, -c.Y + _property.D - _property.T1 - _property.L, 0));
        //                if (_property.T3 > 0 && _property.L > 0)
        //                {
        //                    pts.Add(new Point3d(-_property.B / 2 + _property.T3, -c.Y + _property.D - _property.T1 - _property.L, 0));
        //                    pts.Add(new Point3d(-_property.B / 2 + _property.T3, -c.Y + _property.D - _property.T1, 0));
        //                }
        //                pts.Add(new Point3d(-_property.T2 / 2, -c.Y + _property.D - _property.T1, 0));
        //                pts.Add(new Point3d(-_property.T2 / 2, -c.Y, 0));
        //                PolylineCurve border = new PolylineCurve(pts);
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.C:
        //        case SectionType.Omega:
        //            {
        //                List<Point3d> pts = new List<Point3d>();
        //                pts.Add(new Point3d(-c.X, -c.Y, 0));
        //                pts.Add(Point3d.Add(pts[0], new Vector3d(0, _property.D, 0)));
        //                pts.Add(Point3d.Add(pts[1], new Vector3d(_property.B, 0, 0)));
        //                pts.Add(Point3d.Add(pts[2], new Vector3d(0, -_property.T1, 0)));
        //                pts.Add(Point3d.Add(pts[3], new Vector3d(-_property.B + _property.T2, 0, 0)));
        //                pts.Add(Point3d.Add(pts[4], new Vector3d(0, -_property.D + _property.T1 * 2, 0)));
        //                pts.Add(Point3d.Add(pts[5], new Vector3d(_property.B - _property.T2, 0, 0)));
        //                pts.Add(Point3d.Add(pts[6], new Vector3d(0, -_property.T1, 0)));
        //                pts.Add(pts[0]);
        //                PolylineCurve border = new PolylineCurve(pts);
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.Angle:
        //            {
        //                List<Point3d> pts = new List<Point3d>();
        //                pts.Add(new Point3d(-c.X, -c.Y, 0));
        //                pts.Add(Point3d.Add(pts[0], new Vector3d(0, _property.D, 0)));
        //                pts.Add(Point3d.Add(pts[1], new Vector3d(_property.T2, 0, 0)));
        //                pts.Add(Point3d.Add(pts[2], new Vector3d(0, -_property.D + _property.T1, 0)));
        //                pts.Add(Point3d.Add(pts[3], new Vector3d(_property.B - _property.T2, 0, 0)));
        //                pts.Add(Point3d.Add(pts[4], new Vector3d(0, -_property.T1, 0)));
        //                pts.Add(Point3d.Add(pts[5], new Vector3d(-_property.B, 0, 0)));
        //                pts.Add(pts[0]);
        //                PolylineCurve border = new PolylineCurve(pts);
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //        case SectionType.Z:
        //            {

        //            }
        //            break;
        //        case SectionType.Generic:
        //            {
        //                //equivalent circular section
        //                ArcCurve border = new ArcCurve(new Circle(0.001 * System.Math.Sqrt(_property.SectionArea / System.Math.PI)));
        //                Brep[] breps = Brep.CreatePlanarBreps(border, tol);
        //                main_surface = breps[0];
        //            }
        //            break;
        //    }

        //    if (main_surface != null)
        //    {
        //        BeamTaperAttributeModel.AttributeData taper = BeamTaperAttributeModel.GetTaper(this);

        //        if (taper == null)
        //        {
        //            main_surface.Translate(_offset.X, _offset.Y, 0);
        //            _shapes = new List<Brep>();

        //            List<Brep> surfaces = new List<Brep>();
        //            surfaces.Add(main_surface);

        //            if (BeamProperty.Mirror != MirrorType.None)
        //            {
        //                main_surface.Translate(BeamProperty.Centroid.X + BeamProperty.MirrorGapA / 2, 0, 0);
        //                Brep mirrored = main_surface.DuplicateBrep();
        //                mirrored.Transform(Transform.Mirror(Point3d.Origin, Vector3d.XAxis));
        //                surfaces.Add(mirrored);
        //            }

        //            GetLocalAxes(false, out Vector3d x, out Vector3d y, out Vector3d z, out Transform t);
        //            foreach (Brep surface in surfaces)
        //            {
        //                //Transform t = Transform.PlaneToPlane(Plane.WorldXY, pl);
        //                surface.Transform(t);

        //                LineCurve line = new LineCurve(_pointFrom, _pointTo);
        //                Brep shape = surface.Faces[0].CreateExtrusion(line, true);
        //                //shape.Compact();
        //                _shapes.Add(shape);
        //            }
        //        }
        //        else
        //        {
        //            Brep main_from_surface, main_to_surface;
        //            Transform fromScale, toScale;

        //            fromScale = Transform.Scale(Plane.WorldXY, taper.NFromX, taper.NFromY, 1);
        //            toScale = Transform.Scale(Plane.WorldXY, taper.NToX, taper.NToY, 1);
        //            main_from_surface = main_surface.DuplicateBrep();
        //            main_from_surface.MakeDeformable();
        //            main_from_surface.Transform(fromScale);
        //            main_to_surface = main_surface.DuplicateBrep();
        //            main_to_surface.MakeDeformable();
        //            main_to_surface.Transform(toScale);

        //            main_from_surface.Translate(_offset.X, _offset.Y, 0);
        //            main_to_surface.Translate(_offset.X, _offset.Y, 0);

        //            _shapes = new List<Brep>();

        //            GetLocalAxes(false, out Vector3d x, out Vector3d y, out Vector3d z, out Transform t);

        //            main_from_surface.Transform(t);
        //            main_to_surface.Transform(t);

        //            LineCurve line = new LineCurve(_pointFrom, _pointTo);

        //            t = Transform.Translation(_pointTo - _pointFrom);

        //            main_to_surface.Transform(t);

        //            for (int i = 0; i < main_from_surface.Loops.Count; i++)
        //            {
        //                Curve fromC, toC;
        //                fromC = main_from_surface.Loops[i].To3dCurve();
        //                toC = main_to_surface.Loops[i].To3dCurve();

        //                SweepOneRail s1r;
        //                s1r = new SweepOneRail();
        //                s1r.GlobalShapeBlending = true;
        //                _shapes.AddRange(s1r.PerformSweep(line, new Curve[] { fromC, toC }));
        //            }

        //            _shapes.Add(main_from_surface);
        //            _shapes.Add(main_to_surface);
        //        }
        //        Brep[] join = Brep.JoinBreps(_shapes, 0.001);
        //        _shapes.Clear();
        //        _shapes.AddRange(join);
        //    }
        //}

        public override void CreateShapes()
        {
            if (_beamProperty == null)
                return;
            List<Brep> main_surfaces = new List<Brep>();
            double tol = 0.001;
            Point2d c = _beamProperty.Centroid;

            if (_beamProperty.GetType() != typeof(BeamNonPrismaticPropertyModel))
            {
                switch (_beamProperty.SectionType)
                {
                    case SectionModel.SectionTypes.SolidCircle:
                        {
                            ArcCurve border = new ArcCurve(new Circle(_beamProperty.D / 2));
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.HollowCircle:
                        {
                            ArcCurve border = new ArcCurve(new Circle(_beamProperty.D / 2));
                            ArcCurve inner = new ArcCurve(new Circle(_beamProperty.D / 2 - _beamProperty.T));
                            CurveList curves = new CurveList
                        {
                            border,
                            inner
                        };
                            Brep[] breps = Brep.CreatePlanarBreps(curves, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.SolidRectangle:
                        {
                            List<Point3d> pts = new List<Point3d>
                        {
                            new Point3d(-_beamProperty.B / 2, -_beamProperty.D / 2, 0),
                            new Point3d(_beamProperty.B / 2, -_beamProperty.D / 2, 0),
                            new Point3d(_beamProperty.B / 2, _beamProperty.D / 2, 0),
                            new Point3d(-_beamProperty.B / 2, _beamProperty.D / 2, 0),
                            new Point3d(-_beamProperty.B / 2, -_beamProperty.D / 2, 0)
                        };
                            PolylineCurve border = new PolylineCurve(pts);
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.HollowRectangle:
                        {
                            List<Point3d> ptsOut = new List<Point3d>
                        {
                            new Point3d(-_beamProperty.B / 2, -_beamProperty.D / 2, 0),
                            new Point3d(_beamProperty.B / 2, -_beamProperty.D / 2, 0),
                            new Point3d(_beamProperty.B / 2, _beamProperty.D / 2, 0),
                            new Point3d(-_beamProperty.B / 2, _beamProperty.D / 2, 0),
                            new Point3d(-_beamProperty.B / 2, -_beamProperty.D / 2, 0)
                        };
                            PolylineCurve border = new PolylineCurve(ptsOut);
                            List<Point3d> ptsIn = new List<Point3d>
                        {
                            new Point3d(-_beamProperty.B / 2 + _beamProperty.T2, -_beamProperty.D / 2 + _beamProperty.T1, 0),
                            new Point3d(_beamProperty.B / 2 - _beamProperty.T2, -_beamProperty.D / 2 + _beamProperty.T1, 0),
                            new Point3d(_beamProperty.B / 2 - _beamProperty.T2, _beamProperty.D / 2 - _beamProperty.T1, 0),
                            new Point3d(-_beamProperty.B / 2 + _beamProperty.T2, _beamProperty.D / 2 - _beamProperty.T1, 0),
                            new Point3d(-_beamProperty.B / 2 + _beamProperty.T2, -_beamProperty.D / 2 + _beamProperty.T1, 0)
                        };
                            PolylineCurve inner = new PolylineCurve(ptsIn);

                            CurveList curves = new CurveList
                        {
                            border,
                            inner
                        };
                            Brep[] breps = Brep.CreatePlanarBreps(curves, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.I:
                        {
                            List<Point3d> pts = new List<Point3d>
                        {
                            new Point3d(-_beamProperty.B1 / 2, -c.Y, 0),
                            new Point3d(_beamProperty.B1 / 2, -c.Y, 0),
                            new Point3d(_beamProperty.B1 / 2, -c.Y + _beamProperty.T1, 0),
                            new Point3d(_beamProperty.T3 / 2, -c.Y + _beamProperty.T1, 0),
                            new Point3d(_beamProperty.T3 / 2, -c.Y + _beamProperty.D - _beamProperty.T2, 0),
                            new Point3d(_beamProperty.B2 / 2, -c.Y + _beamProperty.D - _beamProperty.T2, 0),
                            new Point3d(_beamProperty.B2 / 2, -c.Y + _beamProperty.D, 0),
                            new Point3d(-_beamProperty.B2 / 2, -c.Y + _beamProperty.D, 0),
                            new Point3d(-_beamProperty.B2 / 2, -c.Y + _beamProperty.D - _beamProperty.T2, 0),
                            new Point3d(-_beamProperty.T3 / 2, -c.Y + _beamProperty.D - _beamProperty.T2, 0),
                            new Point3d(-_beamProperty.T3 / 2, -c.Y + _beamProperty.T1, 0),
                            new Point3d(-_beamProperty.B1 / 2, -c.Y + _beamProperty.T1, 0),
                            new Point3d(-_beamProperty.B1 / 2, -c.Y, 0)
                        };
                            PolylineCurve border = new PolylineCurve(pts);
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.T:
                        {
                            List<Point3d> pts = new List<Point3d>
                        {
                            new Point3d(-_beamProperty.T2 / 2, -c.Y, 0),
                            new Point3d(_beamProperty.T2 / 2, -c.Y, 0),
                            new Point3d(_beamProperty.T2 / 2, -c.Y + _beamProperty.D - _beamProperty.T1, 0),
                            new Point3d(_beamProperty.B / 2 - _beamProperty.T3, -c.Y + _beamProperty.D - _beamProperty.T1, 0)
                        };
                            if (_beamProperty.T3 > 0 && _beamProperty.L > 0)
                            {
                                pts.Add(new Point3d(_beamProperty.B / 2 - _beamProperty.T3, -c.Y + _beamProperty.D - _beamProperty.T1 - _beamProperty.L, 0));
                                pts.Add(new Point3d(_beamProperty.B / 2, -c.Y + _beamProperty.D - _beamProperty.T1 - _beamProperty.L, 0));
                            }
                            pts.Add(new Point3d(_beamProperty.B / 2, -c.Y + _beamProperty.D, 0));
                            pts.Add(new Point3d(-_beamProperty.B / 2, -c.Y + _beamProperty.D, 0));
                            pts.Add(new Point3d(-_beamProperty.B / 2, -c.Y + _beamProperty.D - _beamProperty.T1 - _beamProperty.L, 0));
                            if (_beamProperty.T3 > 0 && _beamProperty.L > 0)
                            {
                                pts.Add(new Point3d(-_beamProperty.B / 2 + _beamProperty.T3, -c.Y + _beamProperty.D - _beamProperty.T1 - _beamProperty.L, 0));
                                pts.Add(new Point3d(-_beamProperty.B / 2 + _beamProperty.T3, -c.Y + _beamProperty.D - _beamProperty.T1, 0));
                            }
                            pts.Add(new Point3d(-_beamProperty.T2 / 2, -c.Y + _beamProperty.D - _beamProperty.T1, 0));
                            pts.Add(new Point3d(-_beamProperty.T2 / 2, -c.Y, 0));
                            PolylineCurve border = new PolylineCurve(pts);
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.C:
                    case SectionModel.SectionTypes.Omega:
                        {
                            List<Point3d> pts = new List<Point3d> { new Point3d(-c.X, -c.Y, 0) };
                            pts.Add(Point3d.Add(pts[0], new Vector3d(0, _beamProperty.D, 0)));
                            pts.Add(Point3d.Add(pts[1], new Vector3d(_beamProperty.B, 0, 0)));
                            pts.Add(Point3d.Add(pts[2], new Vector3d(0, -_beamProperty.T1, 0)));
                            pts.Add(Point3d.Add(pts[3], new Vector3d(-_beamProperty.B + _beamProperty.T2, 0, 0)));
                            pts.Add(Point3d.Add(pts[4], new Vector3d(0, -_beamProperty.D + _beamProperty.T1 * 2, 0)));
                            pts.Add(Point3d.Add(pts[5], new Vector3d(_beamProperty.B - _beamProperty.T2, 0, 0)));
                            pts.Add(Point3d.Add(pts[6], new Vector3d(0, -_beamProperty.T1, 0)));
                            pts.Add(pts[0]);
                            PolylineCurve border = new PolylineCurve(pts);
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.Angle:
                        {
                            List<Point3d> pts = new List<Point3d> { new Point3d(-c.X, -c.Y, 0) };
                            pts.Add(Point3d.Add(pts[0], new Vector3d(0, _beamProperty.D, 0)));
                            pts.Add(Point3d.Add(pts[1], new Vector3d(_beamProperty.T2, 0, 0)));
                            pts.Add(Point3d.Add(pts[2], new Vector3d(0, -_beamProperty.D + _beamProperty.T1, 0)));
                            pts.Add(Point3d.Add(pts[3], new Vector3d(_beamProperty.B - _beamProperty.T2, 0, 0)));
                            pts.Add(Point3d.Add(pts[4], new Vector3d(0, -_beamProperty.T1, 0)));
                            pts.Add(Point3d.Add(pts[5], new Vector3d(-_beamProperty.B, 0, 0)));
                            pts.Add(pts[0]);
                            PolylineCurve border = new PolylineCurve(pts);
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.Z:
                        {

                        }
                        break;
                    case SectionModel.SectionTypes.Generic:
                        {
                            //equivalent circular section
                            ArcCurve border = new ArcCurve(new Circle(0.001 * System.Math.Sqrt(_beamProperty.SectionArea / System.Math.PI)));
                            Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                            if (breps != null)
                                main_surfaces.Add(breps[0]);
                        }
                        break;
                    case SectionModel.SectionTypes.GenericShapes:
                        {
                            foreach (GPC.Geometry.Shape2d s in _beamProperty.GenericShapes)
                            {
                                CurveList curves = new CurveList();
                                //fill
                                List<Point3d> pts;
                                pts = new List<Point3d>();
                                foreach (GPC.Geometry.Point2d p in s.Fill)
                                {
                                    pts.Add(new Point3d(p.X - c.X, p.Y - c.Y, 0));
                                }
                                pts.Add(new Point3d(s.Fill[0].X - c.X, s.Fill[0].Y - c.Y, 0));
                                curves.Add(new PolylineCurve(pts));
                                if (s.Holes != null)
                                {
                                    for (int i = 0; i < s.Holes2d.Length; i++)
                                    {
                                        GPC.Geometry.Polygon2d hole = s.Holes2d[i];
                                        pts = new List<Point3d>();
                                        foreach (GPC.Geometry.Point2d p in hole)
                                        {
                                            pts.Add(new Point3d(p.X - c.X, p.Y - c.Y, 0));
                                        }
                                        pts.Add(new Point3d(hole[0].X - c.X, hole[0].Y - c.Y, 0));
                                        curves.Add(new PolylineCurve(pts));
                                    }
                                }
                                main_surfaces.Add(Brep.CreatePlanarBreps(curves, tol)[0]);
                            }
                            break;
                        }
                }
            }
            else
            {
                BeamNonPrismaticPropertyModel nonPrism = (BeamNonPrismaticPropertyModel)_beamProperty;

                Point2d c2 = nonPrism.EndSectionCentroid;

                SetSurface(nonPrism.Section, c, ref main_surfaces, tol);
                SetSurface(nonPrism.EndSection, new Point2d(c2.X, c2.Y), ref main_surfaces, tol);
            }

            if (main_surfaces.Count > 0)
            {
                BeamTaperAttributeModel.AttributeData taper = BeamTaperAttributeModel.GetTaper(this);
                _shapes = new List<Brep>();

                if (taper == null && _beamProperty.GetType() != typeof(BeamNonPrismaticPropertyModel))
                {
                    for (int i = 0; i < main_surfaces.Count; i++)
                    {
                        Brep main_surface = main_surfaces[i];
                        main_surface.Translate(_offset.X, _offset.Y, 0);

                        List<Brep> surfaces = new List<Brep> { main_surface };

                        if (BeamProperty.Mirror != SectionModel.MirrorTypes.None)
                        {
                            main_surface.Translate(BeamProperty.Centroid.X + BeamProperty.MirrorGapA / 2, 0, 0);
                            Brep mirrored = main_surface.DuplicateBrep();
                            mirrored.Transform(Transform.Mirror(Point3d.Origin, Vector3d.XAxis));
                            surfaces.Add(mirrored);
                        }

                        GetLocalAxes(false, out Vector3d x, out Vector3d y, out Vector3d z, out Transform t);
                        for (int j = 0; j < surfaces.Count; j++)
                        {
                            Brep surface = surfaces[j];
                            surface.Transform(t);

                            LineCurve line = new LineCurve(_pointFrom, _pointTo);
                            Brep shape = surface.Faces[0].CreateExtrusion(line, true);
                            _shapes.Add(shape);
                        }
                    }
                }
                else if (_beamProperty.GetType() == typeof(BeamNonPrismaticPropertyModel))
                {
                    Brep main_from_surface = main_surfaces[0].DuplicateBrep();
                    main_from_surface.MakeDeformable();

                    Brep main_to_surface = main_surfaces[1].DuplicateBrep();
                    main_to_surface.MakeDeformable();

                    main_from_surface.Translate(_offset.X, _offset.Y, 0);
                    main_to_surface.Translate(_offset.X, _offset.Y, 0);

                    GetLocalAxes(false, out Vector3d x, out Vector3d y, out Vector3d z, out Transform t);

                    main_from_surface.Transform(t);
                    main_to_surface.Transform(t);

                    LineCurve line = new LineCurve(_pointFrom, _pointTo);

                    t = Transform.Translation(_pointTo - _pointFrom);

                    main_to_surface.Transform(t);

                    List<Brep> surfaces = new List<Brep>();
                    for (int i = 0; i < main_from_surface.Loops.Count; i++)
                    {
                        Curve fromC = main_from_surface.Loops[i].To3dCurve();
                        Curve toC = main_to_surface.Loops[i].To3dCurve();

                        SweepOneRail s1r = new SweepOneRail { GlobalShapeBlending = true };
                        surfaces.AddRange(s1r.PerformSweep(line, new Curve[] { fromC, toC }));
                    }

                    surfaces.Add(main_from_surface);
                    surfaces.Add(main_to_surface);

                    Brep[] join = Brep.JoinBreps(surfaces, 0.001);
                    _shapes.Add(join[0]);
                }
                else
                {
                    for (int j = 0; j < main_surfaces.Count; j++)
                    {
                        Brep main_surface = main_surfaces[j];
                        Transform fromScale = Transform.Scale(Plane.WorldXY, taper.NFromX, taper.NFromY, 1);
                        Transform toScale = Transform.Scale(Plane.WorldXY, taper.NToX, taper.NToY, 1);

                        Brep main_from_surface = main_surface.DuplicateBrep();
                        main_from_surface.MakeDeformable();
                        main_from_surface.Transform(fromScale);

                        Brep main_to_surface = main_surface.DuplicateBrep();
                        main_to_surface.MakeDeformable();
                        main_to_surface.Transform(toScale);

                        main_from_surface.Translate(_offset.X, _offset.Y, 0);
                        main_to_surface.Translate(_offset.X, _offset.Y, 0);

                        GetLocalAxes(false, out Vector3d x, out Vector3d y, out Vector3d z, out Transform t);

                        main_from_surface.Transform(t);
                        main_to_surface.Transform(t);

                        LineCurve line = new LineCurve(_pointFrom, _pointTo);

                        t = Transform.Translation(_pointTo - _pointFrom);

                        main_to_surface.Transform(t);

                        List<Brep> surfaces = new List<Brep>();
                        for (int i = 0; i < main_from_surface.Loops.Count; i++)
                        {
                            Curve fromC, toC;
                            fromC = main_from_surface.Loops[i].To3dCurve();
                            toC = main_to_surface.Loops[i].To3dCurve();

                            SweepOneRail s1r;
                            s1r = new SweepOneRail
                            {
                                GlobalShapeBlending = true
                            };
                            surfaces.AddRange(s1r.PerformSweep(line, new Curve[] { fromC, toC }));
                        }

                        surfaces.Add(main_from_surface);
                        surfaces.Add(main_to_surface);

                        Brep[] join = Brep.JoinBreps(surfaces, 0.001);
                        _shapes.Add(join[0]);
                    }
                }
            }
        }

        public void GetLocalAxes(bool getPrincipal, out Vector3d axes1, out Vector3d axes2, out Vector3d axes3, out Transform toGlobal)
        {
            double r = _angleDeg / 180 * Math.PI;
            if (!getPrincipal)
            {
                r -= _beamProperty.AngleX1Rad;
            }
            axes3 = _pointTo - _pointFrom;
            axes3.Unitize();
            if (!IsVertical) // Not vertical
            {
                Vector3d v_xy = new Vector3d(axes3.X, axes3.Y, 0);
                v_xy.Unitize();

                axes1 = Vector3d.CrossProduct(Vector3d.ZAxis, axes3);
                axes1.Unitize();
                axes2 = Vector3d.CrossProduct(axes3, axes1);
                axes2.Unitize();
            }
            else // Vertical
            {
                axes2 = Vector3d.CrossProduct(axes3, Vector3d.YAxis);
                axes2.Unitize();
                axes1 = Vector3d.CrossProduct(axes2, axes3);
                axes1.Unitize();
            }
            Plane pl = new Plane(_pointFrom, axes1, axes2);
            pl.Rotate(r, pl.Normal);
            axes1 = pl.XAxis;
            axes2 = pl.YAxis;
            toGlobal = Transform.PlaneToPlane(Plane.WorldXY, pl);
        }

        public override string ToString()
        {
            string text = "FeMM Beam";
            if (_beamId != string.Empty)
                text += $" #{_beamId}";
            text += $"\nProperty: {_beamProperty.Name}";
            text += $"\nAngle: {_angleDeg}";
            text += "\nGroups: " + string.Join(", ", _groups);
            text += $"\nFrom point: {_pointFrom}";
            text += $"\nTo point: {_pointTo}";
            text += "\nLenght: " + _pointFrom.DistanceTo(_pointTo).ToString("F2");
            text += "\n" + base.ToString();
            return text;
        }

        private void SetSurface(SectionModel sectionModel, Point2d centroid, ref List<Brep> main_surfaces, double tol)
        {
            switch (_beamProperty.SectionType)
            {
                case SectionModel.SectionTypes.SolidCircle:
                    {
                        ArcCurve border = new ArcCurve(new Circle(sectionModel.D / 2));
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.HollowCircle:
                    {
                        ArcCurve border = new ArcCurve(new Circle(sectionModel.D / 2));
                        ArcCurve inner = new ArcCurve(new Circle(sectionModel.D / 2 - sectionModel.T));
                        CurveList curves = new CurveList
                        {
                            border,
                            inner
                        };
                        Brep[] breps = Brep.CreatePlanarBreps(curves, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.SolidRectangle:
                    {
                        List<Point3d> pts = new List<Point3d>
                        {
                            new Point3d(-sectionModel.B / 2, -sectionModel.D / 2, 0),
                            new Point3d(sectionModel.B / 2, -sectionModel.D / 2, 0),
                            new Point3d(sectionModel.B / 2, sectionModel.D / 2, 0),
                            new Point3d(-sectionModel.B / 2, sectionModel.D / 2, 0),
                            new Point3d(-sectionModel.B / 2, -sectionModel.D / 2, 0)
                        };
                        PolylineCurve border = new PolylineCurve(pts);
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.HollowRectangle:
                    {
                        List<Point3d> ptsOut = new List<Point3d>
                        {
                            new Point3d(-sectionModel.B / 2, -sectionModel.D / 2, 0),
                            new Point3d(sectionModel.B / 2, -sectionModel.D / 2, 0),
                            new Point3d(sectionModel.B / 2, sectionModel.D / 2, 0),
                            new Point3d(-sectionModel.B / 2, sectionModel.D / 2, 0),
                            new Point3d(-sectionModel.B / 2, -sectionModel.D / 2, 0)
                        };
                        PolylineCurve border = new PolylineCurve(ptsOut);
                        List<Point3d> ptsIn = new List<Point3d>
                        {
                            new Point3d(-sectionModel.B / 2 + sectionModel.T2, -sectionModel.D / 2 + sectionModel.T1, 0),
                            new Point3d(sectionModel.B / 2 - sectionModel.T2, -sectionModel.D / 2 + sectionModel.T1, 0),
                            new Point3d(sectionModel.B / 2 - sectionModel.T2, sectionModel.D / 2 - sectionModel.T1, 0),
                            new Point3d(-sectionModel.B / 2 + sectionModel.T2, sectionModel.D / 2 - sectionModel.T1, 0),
                            new Point3d(-sectionModel.B / 2 + sectionModel.T2, -sectionModel.D / 2 + sectionModel.T1, 0)
                        };
                        PolylineCurve inner = new PolylineCurve(ptsIn);

                        CurveList curves = new CurveList
                        {
                            border,
                            inner
                        };
                        Brep[] breps = Brep.CreatePlanarBreps(curves, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.I:
                    {
                        List<Point3d> pts = new List<Point3d>
                        {
                            new Point3d(-sectionModel.B1 / 2, -centroid.Y, 0),
                            new Point3d(sectionModel.B1 / 2, -centroid.Y, 0),
                            new Point3d(sectionModel.B1 / 2, -centroid.Y + sectionModel.T1, 0),
                            new Point3d(sectionModel.T3 / 2, -centroid.Y + sectionModel.T1, 0),
                            new Point3d(sectionModel.T3 / 2, -centroid.Y + sectionModel.D - sectionModel.T2, 0),
                            new Point3d(sectionModel.B2 / 2, -centroid.Y + sectionModel.D - sectionModel.T2, 0),
                            new Point3d(sectionModel.B2 / 2, -centroid.Y + sectionModel.D, 0),
                            new Point3d(-sectionModel.B2 / 2, -centroid.Y + sectionModel.D, 0),
                            new Point3d(-sectionModel.B2 / 2, -centroid.Y + sectionModel.D - sectionModel.T2, 0),
                            new Point3d(-sectionModel.T3 / 2, -centroid.Y + sectionModel.D - sectionModel.T2, 0),
                            new Point3d(-sectionModel.T3 / 2, -centroid.Y + sectionModel.T1, 0),
                            new Point3d(-sectionModel.B1 / 2, -centroid.Y + sectionModel.T1, 0),
                            new Point3d(-sectionModel.B1 / 2, -centroid.Y, 0)
                        };
                        PolylineCurve border = new PolylineCurve(pts);
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.T:
                    {
                        List<Point3d> pts = new List<Point3d>
                        {
                            new Point3d(-sectionModel.T2 / 2, -centroid.Y, 0),
                            new Point3d(sectionModel.T2 / 2, -centroid.Y, 0),
                            new Point3d(sectionModel.T2 / 2, -centroid.Y + sectionModel.D - sectionModel.T1, 0),
                            new Point3d(sectionModel.B / 2 - sectionModel.T3, -centroid.Y + sectionModel.D - sectionModel.T1, 0)
                        };
                        if (sectionModel.T3 > 0 && sectionModel.L > 0)
                        {
                            pts.Add(new Point3d(sectionModel.B / 2 - sectionModel.T3, -centroid.Y + sectionModel.D - sectionModel.T1 - sectionModel.L, 0));
                            pts.Add(new Point3d(sectionModel.B / 2, -centroid.Y + sectionModel.D - sectionModel.T1 - sectionModel.L, 0));
                        }
                        pts.Add(new Point3d(sectionModel.B / 2, -centroid.Y + sectionModel.D, 0));
                        pts.Add(new Point3d(-sectionModel.B / 2, -centroid.Y + sectionModel.D, 0));
                        pts.Add(new Point3d(-sectionModel.B / 2, -centroid.Y + sectionModel.D - sectionModel.T1 - sectionModel.L, 0));
                        if (sectionModel.T3 > 0 && sectionModel.L > 0)
                        {
                            pts.Add(new Point3d(-sectionModel.B / 2 + sectionModel.T3, -centroid.Y + sectionModel.D - sectionModel.T1 - sectionModel.L, 0));
                            pts.Add(new Point3d(-sectionModel.B / 2 + sectionModel.T3, -centroid.Y + sectionModel.D - sectionModel.T1, 0));
                        }
                        pts.Add(new Point3d(-sectionModel.T2 / 2, -centroid.Y + sectionModel.D - sectionModel.T1, 0));
                        pts.Add(new Point3d(-sectionModel.T2 / 2, -centroid.Y, 0));
                        PolylineCurve border = new PolylineCurve(pts);
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.C:
                case SectionModel.SectionTypes.Omega:
                    {
                        List<Point3d> pts = new List<Point3d> { new Point3d(-centroid.X, -centroid.Y, 0) };
                        pts.Add(Point3d.Add(pts[0], new Vector3d(0, sectionModel.D, 0)));
                        pts.Add(Point3d.Add(pts[1], new Vector3d(sectionModel.B, 0, 0)));
                        pts.Add(Point3d.Add(pts[2], new Vector3d(0, -sectionModel.T1, 0)));
                        pts.Add(Point3d.Add(pts[3], new Vector3d(-sectionModel.B + sectionModel.T2, 0, 0)));
                        pts.Add(Point3d.Add(pts[4], new Vector3d(0, -sectionModel.D + sectionModel.T1 * 2, 0)));
                        pts.Add(Point3d.Add(pts[5], new Vector3d(sectionModel.B - sectionModel.T2, 0, 0)));
                        pts.Add(Point3d.Add(pts[6], new Vector3d(0, -sectionModel.T1, 0)));
                        pts.Add(pts[0]);
                        PolylineCurve border = new PolylineCurve(pts);
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.Angle:
                    {
                        List<Point3d> pts = new List<Point3d> { new Point3d(-centroid.X, -centroid.Y, 0) };
                        pts.Add(Point3d.Add(pts[0], new Vector3d(0, sectionModel.D, 0)));
                        pts.Add(Point3d.Add(pts[1], new Vector3d(sectionModel.T2, 0, 0)));
                        pts.Add(Point3d.Add(pts[2], new Vector3d(0, -sectionModel.D + sectionModel.T1, 0)));
                        pts.Add(Point3d.Add(pts[3], new Vector3d(sectionModel.B - sectionModel.T2, 0, 0)));
                        pts.Add(Point3d.Add(pts[4], new Vector3d(0, -sectionModel.T1, 0)));
                        pts.Add(Point3d.Add(pts[5], new Vector3d(-sectionModel.B, 0, 0)));
                        pts.Add(pts[0]);
                        PolylineCurve border = new PolylineCurve(pts);
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.Z:
                    {

                    }
                    break;
                case SectionModel.SectionTypes.Generic:
                    {
                        //equivalent circular section
                        ArcCurve border = new ArcCurve(new Circle(0.001 * System.Math.Sqrt(sectionModel.SectionArea / System.Math.PI)));
                        Brep[] breps = Brep.CreatePlanarBreps(border, tol);
                        if (breps != null)
                            main_surfaces.Add(breps[0]);
                    }
                    break;
                case SectionModel.SectionTypes.GenericShapes:
                    {
                        foreach (GPC.Geometry.Shape2d s in sectionModel.GenericShapes)
                        {
                            CurveList curves = new CurveList();
                            //fill
                            List<Point3d> pts;
                            pts = new List<Point3d>();
                            foreach (GPC.Geometry.Point2d p in s.Fill)
                            {
                                pts.Add(new Point3d(p.X - centroid.X, p.Y - centroid.Y, 0));
                            }
                            pts.Add(new Point3d(s.Fill[0].X - centroid.X, s.Fill[0].Y - centroid.Y, 0));
                            curves.Add(new PolylineCurve(pts));
                            if (s.Holes != null)
                            {
                                for (int i = 0; i < s.Holes2d.Length; i++)
                                {
                                    GPC.Geometry.Polygon2d hole = s.Holes2d[i];
                                    pts = new List<Point3d>();
                                    foreach (GPC.Geometry.Point2d p in hole)
                                    {
                                        pts.Add(new Point3d(p.X - centroid.X, p.Y - centroid.Y, 0));
                                    }
                                    pts.Add(new Point3d(hole[0].X - centroid.X, hole[0].Y - centroid.Y, 0));
                                    curves.Add(new PolylineCurve(pts));
                                }
                            }
                            main_surfaces.Add(Brep.CreatePlanarBreps(curves, tol)[0]);
                        }
                        break;
                    }
            }

        }
    }
}
