using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using GPC.Utilities.Units;
using Rhino.Collections;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_BeamSection : GH_Goo<SectionModel>
    {
        public SectionModel.SectionTypes SectionType
        {
            get => Value.SectionType;
            set => Value.SectionType = value;
        }

        public double D
        {
            get => Value.D;
            set => Value.D = value;
        }

        public double B
        {
            get => Value.B;
            set => Value.B = value;
        }

        public double B1
        {
            get => Value.B1;
            set => Value.B1 = value;
        }

        public double B2
        {
            get => Value.B2;
            set => Value.B2 = value;
        }

        public double L
        {
            get => Value.L;
            set => Value.L = value;
        }

        public double T
        {
            get => Value.T;
            set => Value.T = value;
        }

        public double T1
        {
            get => Value.T1;
            set => Value.T1 = value;
        }

        public double T2
        {
            get => Value.T2;
            set => Value.T2 = value;
        }

        public double T3
        {
            get => Value.T3;
            set => Value.T3 = value;
        }

        public double R
        {
            get => Value.R;
            set => Value.R = value;
        }

        public SectionModel.MirrorTypes MirrorType
        {
            get => Value.Mirror;
            set => Value.Mirror = value;
        }

        public double MirrorGapA
        {
            get => Value.MirrorGapA;
            set => Value.MirrorGapA = value;
        }

        public double MirrorGapB
        {
            get => Value.MirrorGapB;
            set => Value.MirrorGapB = value;
        }

        public double SectionArea
        {
            get => Value.SectionArea;
            set => Value.SectionArea = value;
        }

        public double J
        {
            get => Value.J;
            set => Value.J = value;
        }

        public double I11
        {
            get => Value.I11;
            set => Value.I11 = value;
        }

        public double I22
        {
            get => Value.I22;
            set => Value.I22 = value;
        }

        public double SL1
        {
            get => Value.ShearL1;
            set => Value.ShearL1 = value;
        }

        public double SL2
        {
            get => Value.ShearL2;
            set => Value.ShearL2 = value;
        }

        public double SA1
        {
            get => Value.ShearA1;
            set => Value.ShearA1 = value;
        }

        public double SA2
        {
            get => Value.ShearA2;
            set => Value.ShearA2 = value;
        }

        public double W11
        {
            get => Value.W11;
            set => Value.W11 = value;
        }

        public double W22
        {
            get => Value.W22;
            set => Value.W22 = value;
        }

        public Point2d Centroid
        {
            get => Value.Centroid;
            set => Value.Centroid = value;
        }

        public double AngleX1
        {
            get => Value.AngleX1Rad;
            set => Value.AngleX1Rad = value;
        }

        public bool IsDBSection
        {
            get => Value.IsDBSection;
            set => Value.IsDBSection = value;
        }

        public Common.Geometry.Shapes GenericShapes
        {
            get => Value.GenericShapes;
            set => Value.GenericShapes = value;
        }

        public override bool IsValid => SectionType != SectionModel.SectionTypes.Undefined;

        public override string IsValidWhyNot => "The section type is not defined";

        public override string TypeName => "BeamSection";

        public override string TypeDescription => "Defines a beam section";

        public GH_BeamSection()
        {
            Value = new SectionModel();
            SectionType = SectionModel.SectionTypes.Undefined;
            MirrorType = SectionModel.MirrorTypes.None;
            GenericShapes = null;
        }

        public GH_BeamSection(SectionModel section)
        {
            Value = section;
        }

        public GH_BeamSection(GH_BeamSection section)
        {
            Value = section.Value;
        }

        public override bool CastFrom(object source)
        {
            if (source is GH_String str)
            {
                // CHS circle
                // RHS rect 
                // SHS square

                IsDBSection = false;
                if (str.Value.StartsWith("CHS"))
                {
                    try
                    {
                        double[] vals = str.Value.Substring(3).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.HollowCircle;
                        D = vals[0];
                        T = vals[1];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("SHS"))
                {
                    try
                    {
                        double[] vals = str.Value.Substring(3).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.HollowRectangle;
                        D = vals[0];
                        B = vals[0];
                        T1 = vals[1];
                        T2 = vals[1];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("RHS"))
                {
                    try
                    {

                        double[] vals = str.Value.Substring(3).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();

                        if (vals.Length == 3 || vals.Length == 4)
                        {
                            SectionType = SectionModel.SectionTypes.HollowRectangle;
                            D = vals[0];
                            B = vals[1];
                            T1 = vals[2];
                            T2 = vals.Length == 4 ? vals[3] : vals[2];
                            return true;
                        }
                        else
                        {
                            throw new ArgumentException();
                        }
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("I"))
                {
                    try
                    {
                        double[] vals = str.Value.Substring(1).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.I;
                        D = vals[0];
                        B1 = vals[1];
                        B2 = vals[2];
                        T1 = vals[3];
                        T2 = vals[4];
                        T3 = vals[5];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("C"))
                {
                    try
                    {
                        double[] vals = str.Value.Substring(1).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.C;
                        D = vals[0];
                        B = vals[1];
                        T1 = vals[2];
                        T2 = vals[3];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("L"))
                {
                    try
                    {
                        double[] vals = str.Value.Substring(1).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.Angle;
                        D = vals[0];
                        B = vals[1];
                        T1 = vals[2];
                        T2 = vals[3];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("R"))
                {
                    try
                    {
                        double[] vals = str.Value.ToLower().Substring(1).Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.SolidRectangle;
                        D = vals[0];
                        B = vals[1];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("T"))
                {
                    try
                    {
                        double[] vals = str.Value.Substring(1).Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.T;
                        D = vals[0];
                        B = vals[1];
                        T1 = vals[2];
                        T2 = vals[3];
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("SC"))
                {
                    try
                    {
                        //double[] vals = str.Value.Substring(2).Trim().Select(s => Convert.ToDouble(s)).ToArray();
                        SectionType = SectionModel.SectionTypes.SolidCircle;
                        D = Convert.ToInt32(str.Value.Substring(2).Trim());
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                }
                else if (str.Value.StartsWith("2x"))
                {
                    string sec = str.Value.Substring(2);
                    MirrorType = SectionModel.MirrorTypes.Left;
                    if (sec.StartsWith("C"))
                    {
                        try
                        {
                            double[] vals = sec.Substring(1).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                            SectionType = SectionModel.SectionTypes.C;
                            D = vals[0];
                            B = vals[1];
                            T1 = vals[2];
                            T2 = vals[3];
                            MirrorGapA = vals[4];
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                    else if (sec.StartsWith("L"))
                    {
                        try
                        {
                            double[] vals = sec.Substring(1).ToLower().Split(new char[] { 'x' }).Select(s => Convert.ToDouble(s)).ToArray();
                            SectionType = SectionModel.SectionTypes.Angle;
                            D = vals[0];
                            B = vals[1];
                            T1 = vals[2];
                            T2 = vals[3];
                            MirrorGapA = vals[4];
                            return true;
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                    }
                }
                return false;
            }
            else if (source.GetType() == typeof(GH_Curve))
            {
                GH_Curve curve = (GH_Curve)source;
                if (curve.Value is ArcCurve)
                {
                    ArcCurve circle = curve.Value as ArcCurve;
                    if (circle.IsCircle())
                    {
                        SectionType = SectionModel.SectionTypes.SolidCircle;
                        D = circle.Radius * 2;
                        return true;
                    }
                }
                else
                {
                    if (curve.Value.IsPolyline() && curve.Value.IsClosed && curve.Value.IsPlanar())
                    {
                        if (curve.Value.TryGetPolyline(out Polyline pline))
                        {
                            if (pline.Count > 2)
                            {
                                Plane pl = new Plane(pline[0], pline[1], pline[2]);
                                pline.Transform(Transform.PlaneToPlane(pl, Plane.WorldXY));
                                GPC.Geometry.Polygon2d fill;
                                fill = new GPC.Geometry.Polygon2d();
                                foreach (Point3d p in pline)
                                {
                                    fill.Add(p.X, p.Y);
                                }
                                GenericShapes = new Common.Geometry.Shapes(new[] { new GPC.Geometry.Shape2d(fill, null) });
                                SectionType = SectionModel.SectionTypes.GenericShapes;
                                return true;
                            }
                        }
                    }

                    return true;
                }
            }

            return false;
        }

        public bool CastTo<T>(out T target)
        {
            try
            {
                double t = 0.01;
                switch (SectionType)
                {
                    case SectionModel.SectionTypes.SolidCircle:
                        {
                            ArcCurve border = new ArcCurve(new Circle(D / 2));

                            if (typeof(T) == typeof(GH_Curve))
                            {
                                target = (T)Convert.ChangeType(new GH_Curve(border), typeof(T));
                                return true;
                            }
                            else if (typeof(T) == typeof(GH_Surface))
                            {
                                Brep[] brep = Brep.CreatePlanarBreps(border, t);
                                target = (T)Convert.ChangeType(new GH_Surface(brep[0]), typeof(T));
                                return true;
                            }
                        }
                        break;
                    case SectionModel.SectionTypes.HollowCircle:
                        {
                            ArcCurve border = new ArcCurve(new Circle(D / 2));
                            ArcCurve inner = new ArcCurve(new Circle(D / 2 - this.T));
                            if (typeof(T) == typeof(GH_Curve))
                            {
                                target = (T)Convert.ChangeType(new GH_Curve(border), typeof(T));
                                return true;
                            }
                            else if (typeof(T) == typeof(GH_Surface))
                            {
                                CurveList curves = new CurveList
                                {
                                    border,
                                    inner
                                };
                                Brep[] brep = Brep.CreatePlanarBreps(curves, t);
                                target = (T)Convert.ChangeType(new GH_Surface(brep[0]), typeof(T));
                                return true;
                            }
                        }
                        break;
                    case SectionModel.SectionTypes.SolidRectangle:
                        {
                            List<Point3d> pts = new List<Point3d>
                            {
                                new Point3d(-B / 2, -D / 2, 0),
                                new Point3d(B / 2, -D / 2, 0),
                                new Point3d(B / 2, D / 2, 0),
                                new Point3d(-B / 2, D / 2, 0),
                                new Point3d(-B / 2, -D / 2, 0)
                            };
                            PolylineCurve border = new PolylineCurve(pts);
                            if (typeof(T) == typeof(GH_Curve))
                            {
                                target = (T)Convert.ChangeType(new GH_Curve(border), typeof(T));
                                return true;
                            }
                            else if (typeof(T) == typeof(GH_Surface))
                            {
                                Brep[] brep = Brep.CreatePlanarBreps(border, t);
                                target = (T)Convert.ChangeType(new GH_Surface(brep[0]), typeof(T));
                                return true;
                            }
                        }
                        break; ;
                    case SectionModel.SectionTypes.HollowRectangle:
                        {
                            List<Point3d> ptsOut = new List<Point3d>
                            {
                                new Point3d(-B / 2, -D / 2, 0),
                                new Point3d(B / 2, -D / 2, 0),
                                new Point3d(B / 2, D / 2, 0),
                                new Point3d(-B / 2, D / 2, 0),
                                new Point3d(-B / 2, -D / 2, 0)
                            };
                            PolylineCurve border = new PolylineCurve(ptsOut);
                            List<Point3d> ptsIn = new List<Point3d>
                            {
                                new Point3d(-B / 2 + T2, -D / 2 + T1, 0),
                                new Point3d(B / 2 - T2, -D / 2 + T1, 0),
                                new Point3d(B / 2 - T2, D / 2 - T1, 0),
                                new Point3d(-B / 2 + T2, D / 2 - T1, 0),
                                new Point3d(-B / 2 + T2, -D / 2 + T1, 0)
                            };
                            PolylineCurve inner = new PolylineCurve(ptsIn);
                            if (typeof(T) == typeof(GH_Curve))
                            {
                                target = (T)Convert.ChangeType(new GH_Curve(border), typeof(T));
                                return true;
                            }
                            else if (typeof(T) == typeof(GH_Surface))
                            {
                                CurveList curves = new CurveList
                                {
                                    border,
                                    inner
                                };
                                Brep[] brep = Brep.CreatePlanarBreps(curves, t);
                                target = (T)Convert.ChangeType(new GH_Surface(brep[0]), typeof(T));
                                return true;
                            }
                        }
                        break;
                    case SectionModel.SectionTypes.I:
                        {
                            List<Point3d> pts = new List<Point3d>
                            {
                                new Point3d(-B1 / 2, -D / 2, 0),
                                new Point3d(B1 / 2, -D / 2, 0),
                                new Point3d(B1 / 2, -D / 2 + T1, 0),
                                new Point3d(T3 / 2, -D / 2 + T1, 0),
                                new Point3d(T3 / 2, D / 2 - T2, 0),
                                new Point3d(B2 / 2, D / 2 - T2, 0),
                                new Point3d(B2 / 2, D / 2, 0),
                                new Point3d(-B2 / 2, D / 2, 0),
                                new Point3d(-B2 / 2, D / 2 - T2, 0),
                                new Point3d(-T3 / 2, D / 2 - T2, 0),
                                new Point3d(-T3 / 2, -D / 2 + T1, 0),
                                new Point3d(-B1 / 2, -D / 2 + T1, 0),
                                new Point3d(-B1 / 2, -D / 2, 0)
                            };
                            PolylineCurve border = new PolylineCurve(pts);
                            if (typeof(T) == typeof(GH_Curve))
                            {
                                target = (T)Convert.ChangeType(new GH_Curve(border), typeof(T));
                                return true;
                            }
                            else if (typeof(T) == typeof(GH_Surface))
                            {
                                Brep[] brep = Brep.CreatePlanarBreps(border, t);
                                target = (T)Convert.ChangeType(new GH_Surface(brep[0]), typeof(T));
                                return true;
                            }
                        }
                        break;
                    case SectionModel.SectionTypes.T:
                        {
                            List<Point3d> pts = new List<Point3d>
                            {
                                new Point3d(-T2 / 2, -D / 2, 0),
                                new Point3d(T2 / 2, -D / 2, 0),
                                new Point3d(T2 / 2, D / 2 - T1, 0),
                                new Point3d(B / 2 - T3, D / 2 - T1, 0)
                            };
                            if (T3 > 0 && L > 0)
                            {
                                pts.Add(new Point3d(B / 2 - T3, D / 2 - T1 - L, 0));
                                pts.Add(new Point3d(B / 2, D / 2 - T1 - L, 0));
                            }
                            pts.Add(new Point3d(B / 2, D / 2, 0));
                            pts.Add(new Point3d(-B / 2, D / 2, 0));
                            pts.Add(new Point3d(-B / 2, D / 2 - T1 - L, 0));
                            if (T3 > 0 && L > 0)
                            {
                                pts.Add(new Point3d(-B / 2 + T3, D / 2 - T1 - L, 0));
                                pts.Add(new Point3d(-B / 2 + T3, D / 2 - T1, 0));
                            }
                            pts.Add(new Point3d(-T2 / 2, D / 2 - T1, 0));
                            pts.Add(new Point3d(-T2 / 2, -D / 2, 0));
                            PolylineCurve border = new PolylineCurve(pts);
                            if (typeof(T) == typeof(GH_Curve))
                            {
                                target = (T)Convert.ChangeType(new GH_Curve(border), typeof(T));
                                return true;
                            }
                            else if (typeof(T) == typeof(GH_Surface))
                            {
                                Brep[] brep = Brep.CreatePlanarBreps(border, t);
                                target = (T)Convert.ChangeType(new GH_Surface(brep[0]), typeof(T));
                                return true;
                            }
                        }
                        break;
                    //case SectionType.GenericShapes:
                    //    {
                    //        //covert only section with one shape
                    //        if (GenericShapes.Count == 1)
                    //        {
                    //            CurveList loops;
                    //            List<Point3d> pts;
                    //            Utilities.Geometry.Polygon fill;
                    //            loops = new CurveList();
                    //            pts = new List<Point3d>();
                    //            fill = GenericShapes[0].Fill;
                    //            foreach (Utilities.Geometry.Point2d p in fill)
                    //            {
                    //                pts.Add(new Point3d(p.X, p.Y, 0));
                    //            }
                    //            pts.Add(new Point3d(fill[0].X, fill[0].Y, 0));
                    //            loops.Add(new PolylineCurve(pts));
                    //            if (GenericShapes[0].HolesCanBeNull != null)
                    //            {
                    //                foreach (Utilities.Geometry.Polygon hole in GenericShapes[0].HolesCanBeNull)
                    //                {
                    //                    pts = new List<Point3d>();
                    //                    foreach (Utilities.Geometry.Point2d p in hole)
                    //                    {
                    //                        pts.Add(new Point3d(p.X, p.Y, 0));
                    //                    }
                    //                    pts.Add(new Point3d(hole[hole.Count - 1].X, hole[hole.Count - 1].Y, 0));
                    //                    loops.Add(new PolylineCurve(pts));
                    //                }
                    //            }
                    //            Brep[] brep = Brep.CreatePlanarBreps(loops, t);
                    //            target = (T)Convert.ChangeType(new GHBrep(brep[0]), typeof(T));
                    //            return true;
                    //        }
                    //    }
                    //    break;
                    default:
                        {
                            List<Point3d> pts = new List<Point3d>
                            {
                                new Point3d(0, 0, 0),
                                new Point3d(0, 20, 0)
                            };
                            target = (T)Convert.ChangeType(new GH_Curve(new PolylineCurve(pts)), typeof(T));
                        }
                        return true;
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

            target = default;
            return false;
        }

        public override IGH_Goo Duplicate()
        {
            return new GH_BeamSection(this);
        }

        public override IGH_GooProxy EmitProxy()
        {
            return null;
        }

        public void ConvertUnits(ModelModel.ModelUnits startingUnits, ModelModel.ModelUnits destinationUnits)
        {
            Common.Helpers.ModelHelper.GetUtilitiesConversionParams(
                startingUnits,
                out UnitsConvert.LengthUnits startingLengthUnits,
                out UnitsConvert.ForceUnits bufferF,
                out UnitsConvert.MassUnits bufferM,
                out UnitsConvert.PressureUnits bufferP,
                out UnitsConvert.TemperatureUnits bufferT);
            Common.Helpers.ModelHelper.GetUtilitiesConversionParams(
                destinationUnits,
                out UnitsConvert.LengthUnits destinationLengthUnits,
                out bufferF,
                out bufferM,
                out bufferP,
                out bufferT);

            D = UnitsConvert.Convert(D, startingLengthUnits, destinationLengthUnits, 1);
            B = UnitsConvert.Convert(B, startingLengthUnits, destinationLengthUnits, 1);
            B1 = UnitsConvert.Convert(B1, startingLengthUnits, destinationLengthUnits, 1);
            B2 = UnitsConvert.Convert(B2, startingLengthUnits, destinationLengthUnits, 1);
            L = UnitsConvert.Convert(L, startingLengthUnits, destinationLengthUnits, 1);
            T = UnitsConvert.Convert(T, startingLengthUnits, destinationLengthUnits, 1);
            T1 = UnitsConvert.Convert(T1, startingLengthUnits, destinationLengthUnits, 1);
            T2 = UnitsConvert.Convert(T2, startingLengthUnits, destinationLengthUnits, 1);
            T3 = UnitsConvert.Convert(T3, startingLengthUnits, destinationLengthUnits, 1);
            MirrorGapA = UnitsConvert.Convert(MirrorGapA, startingLengthUnits, destinationLengthUnits, 1);
            MirrorGapB = UnitsConvert.Convert(MirrorGapB, startingLengthUnits, destinationLengthUnits, 1);
            SectionArea = UnitsConvert.Convert(SectionArea, startingLengthUnits, destinationLengthUnits, 2);
            J = UnitsConvert.Convert(J, startingLengthUnits, destinationLengthUnits, 4);
            I11 = UnitsConvert.Convert(I11, startingLengthUnits, destinationLengthUnits, 4);
            I22 = UnitsConvert.Convert(I22, startingLengthUnits, destinationLengthUnits, 4);
            SL1 = UnitsConvert.Convert(SL1, startingLengthUnits, destinationLengthUnits, 1);
            SL2 = UnitsConvert.Convert(SL2, startingLengthUnits, destinationLengthUnits, 1);
            SA1 = UnitsConvert.Convert(SA1, startingLengthUnits, destinationLengthUnits, 2);
            SA2 = UnitsConvert.Convert(SA2, startingLengthUnits, destinationLengthUnits, 2);
            W11 = UnitsConvert.Convert(W11, startingLengthUnits, destinationLengthUnits, 3);
            W22 = UnitsConvert.Convert(W22, startingLengthUnits, destinationLengthUnits, 3);
            var x = UnitsConvert.Convert(Centroid.X, startingLengthUnits, destinationLengthUnits, 1);
            var y = UnitsConvert.Convert(Centroid.Y, startingLengthUnits, destinationLengthUnits, 1);
            Centroid = new Point2d(x, y);
            if (GenericShapes != null)
            {
                foreach (GPC.Geometry.Shape2d s in GenericShapes)
                {
                    foreach (GPC.Geometry.Point2d p in s.Fill)
                    {
                        p.Move(UnitsConvert.Convert(p.X, startingLengthUnits, destinationLengthUnits, 1),
                               UnitsConvert.Convert(p.Y, startingLengthUnits, destinationLengthUnits, 1));
                    }
                    if (s.Holes != null)
                    {
                        foreach (GPC.Geometry.Polygon2d hole in s.Holes2d)
                        {
                            foreach (GPC.Geometry.Point2d p in hole)
                            {
                                p.Move(UnitsConvert.Convert(p.X, startingLengthUnits, destinationLengthUnits, 1),
                                       UnitsConvert.Convert(p.Y, startingLengthUnits, destinationLengthUnits, 1));
                            }
                        }
                    }
                }
            }
        }

        public override bool Read(GH_IReader reader)
        {
            try
            {
                SectionType = (SectionModel.SectionTypes)reader.GetInt32("section");
                D = reader.GetDouble("d");
                B = reader.GetDouble("b");
                B1 = reader.GetDouble("b1");
                B2 = reader.GetDouble("b2");
                L = reader.GetDouble("l");
                T = reader.GetDouble("t");
                T1 = reader.GetDouble("t1");
                T2 = reader.GetDouble("t2");
                T3 = reader.GetDouble("t3");
                MirrorType = (SectionModel.MirrorTypes)reader.GetInt32("mirror");
                MirrorGapA = reader.GetDouble("gap_a");
                double MirrorGapBBuffer = 0;
                if (reader.TryGetDouble("gapB", ref MirrorGapBBuffer))
                    MirrorGapB = MirrorGapBBuffer;
                SectionArea = reader.GetDouble("Area");
                J = reader.GetDouble("J");
                I11 = reader.GetDouble("I11");
                I22 = reader.GetDouble("I22");
                SL1 = reader.GetDouble("SL1");
                SL2 = reader.GetDouble("SL2");
                SA1 = reader.GetDouble("SA1");
                SA2 = reader.GetDouble("SA2");
                W11 = reader.GetDouble("W11");
                W22 = reader.GetDouble("W22");
                double ox = reader.GetDouble("OX");
                double oy = reader.GetDouble("OY");
                Centroid = new Point2d(ox, oy);
                AngleX1 = reader.GetDouble("Angle");
                IsDBSection = reader.GetBoolean("IsDBSection");
                bool hasGenericShape = false;
                GenericShapes = null;
                if (reader.TryGetBoolean("HasGenericShapes", ref hasGenericShape))
                {
                    if (hasGenericShape)
                    {
                        int shapesCount = reader.GetInt32("GenericShapes_Count");
                        List<GPC.Geometry.Shape2d> shapes;
                        shapes = new List<GPC.Geometry.Shape2d>();
                        for (int i = 0; i < shapesCount; i++)
                        {
                            GH_IReader chunk = reader.FindChunk("GenericShapes", i);
                            int fillCount = chunk.GetInt32("FillCount");
                            GPC.Geometry.Polygon2d fill = new GPC.Geometry.Polygon2d();
                            for (int j = 0; j < fillCount; j++)
                            {
                                double x = chunk.GetDouble("FillX", j);
                                double y = chunk.GetDouble("FillY", j);
                                fill.Add(x, y);
                            }
                            int holeCount = chunk.GetInt32("HoleCount");
                            GPC.Geometry.Polygon2d[] holes;
                            if (holeCount == 0)
                            {
                                holes = null;
                            }
                            else
                            {
                                List<GPC.Geometry.Polygon2d> holesList;
                                holesList = new List<GPC.Geometry.Polygon2d>();
                                for (int j = 0; j < holeCount; j++)
                                {
                                    int count;
                                    GPC.Geometry.Polygon2d hole = new GPC.Geometry.Polygon2d();
                                    chunk = reader.FindChunk("GenericShapes_" + i.ToString() + "_hole", j);
                                    count = chunk.GetInt32("HoleCount");
                                    for (int k = 0; k < count; k++)
                                    {
                                        double x = chunk.GetDouble("HoleX", k);
                                        double y = chunk.GetDouble("HoleY", k);
                                        hole.Add(x, y);
                                    }
                                    holesList.Add(hole);
                                }
                                holes = holesList.ToArray();
                            }
                            shapes.Add(new GPC.Geometry.Shape2d(fill, holes));
                        }
                        GenericShapes = new Common.Geometry.Shapes(shapes);
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override object ScriptVariable()
        {
            return null;
        }

        public override bool Write(GH_IWriter writer)
        {
            try
            {
                writer.SetInt32("section", (int)SectionType);
                writer.SetDouble("d", D);
                writer.SetDouble("b", B);
                writer.SetDouble("b1", B1);
                writer.SetDouble("b2", B2);
                writer.SetDouble("l", L);
                writer.SetDouble("t", T);
                writer.SetDouble("t1", T1);
                writer.SetDouble("t2", T2);
                writer.SetDouble("t3", T3);
                writer.SetInt32("mirror", (int)MirrorType);
                writer.SetDouble("gap_a", MirrorGapA);
                writer.SetDouble("gapB", MirrorGapB);
                writer.SetDouble("Area", SectionArea);
                writer.SetDouble("J", J);
                writer.SetDouble("I11", I11);
                writer.SetDouble("I22", I22);
                writer.SetDouble("SL1", SL1);
                writer.SetDouble("SL2", SL2);
                writer.SetDouble("SA1", SA1);
                writer.SetDouble("SA2", SA2);
                writer.SetDouble("W11", W11);
                writer.SetDouble("W22", W22);
                writer.SetDouble("OX", Centroid.X);
                writer.SetDouble("OY", Centroid.Y);
                writer.SetDouble("Angle", AngleX1);
                writer.SetBoolean("IsDBSection", IsDBSection);
                writer.SetBoolean("HasGenericShapes", GenericShapes != null);
                if (GenericShapes != null)
                {
                    writer.SetInt32("GenericShapes_Count", GenericShapes.Count);
                    for (int i = 0; i < GenericShapes.Count; i++)
                    {
                        GH_IWriter chunk;
                        chunk = writer.CreateChunk("GenericShapes", i);
                        chunk.SetInt32("FillCount", GenericShapes[i].Fill.Count);
                        for (int j = 0; j < GenericShapes[i].Fill.Count; j++)
                        {
                            chunk.SetDouble("FillX", j, GenericShapes[i].Fill[j].X);
                            chunk.SetDouble("FillY", j, GenericShapes[i].Fill[j].Y);
                        }
                        int holeCount;
                        if (GenericShapes[i].Holes == null)
                        {
                            holeCount = 0;
                        }
                        else
                        {
                            holeCount = GenericShapes[i].Holes.Length;
                        }
                        chunk.SetInt32("HoleCount", holeCount);
                        for (int j = 0; j < holeCount; j++)
                        {
                            GPC.Geometry.Polygon2d hole = GenericShapes[i].Holes[j].GetPolygon2d();
                            chunk = writer.CreateChunk("GenericShapes_" + i.ToString() + "_hole", j);
                            chunk.SetInt32("HoleCount", hole.Count);
                            for (int k = 0; k < hole.Count; k++)
                            {
                                chunk.SetDouble("HoleX", k, hole[k].X);
                                chunk.SetDouble("HoleY", k, hole[k].Y);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            string text;
            if (SectionType == SectionModel.SectionTypes.GenericShapes)
            {
                text = string.Format("{0} {1} shapes", SectionType, GenericShapes.Count);
            }
            else
            {
                switch (SectionType)
                {
                    case SectionModel.SectionTypes.HollowCircle:
                        // Diameter x Thickness
                        text = $"HollowCircle: {D}x{T}";
                        break;
                    case SectionModel.SectionTypes.HollowRectangle:
                        // Height x Width x Horizontal Thickness x Vertical Thickness
                        text = $"HollowRectangle: {D}x{B}x{T1}x{T2}";
                        break;
                    case SectionModel.SectionTypes.C:
                        // Height x Flange width x Flange thickness x Web thickness
                        text = $"CSection: {D}x{B}x{T1}x{T2}";
                        break;
                    case SectionModel.SectionTypes.Angle:
                        if (MirrorType == SectionModel.MirrorTypes.Left)
                            // Height x Flange width x Flange thickness x Web thickness x Gap
                            text = $"DoubleAngle: {D}x{B}x{T1}x{T2}x{MirrorGapA}";
                        else
                            // Height x Flange width x Flange thickness x Web thickness
                            text = $"Angle: {D}x{B}x{T1}x{T2}";
                        break;
                    case SectionModel.SectionTypes.T:
                        // Height x Flange width x Flange thickness x Web thickness
                        text = $"TSection: {D}x{B}x{T1}x{T2}";
                        break;
                    case SectionModel.SectionTypes.I:
                        // Height x Web Thickness x Bottom flange width x Bottom flange thickness x Top flange width x Top flange thickness x Radius
                        text = $"ISection: {D}x{T3}x{B1}x{T1}x{B2}x{T2}x{R}";
                        break;
                    case SectionModel.SectionTypes.SolidCircle:
                        // Diameter
                        text = $"SolidCircle: {D}";
                        break;
                    case SectionModel.SectionTypes.SolidRectangle:
                        // Height x Width
                        text = $"SolidRectangle: {D}x{B}";
                        break;
                    case SectionModel.SectionTypes.Composite:
                        text = $"ISection: {D}x{T3}x{B1}x{T1}x{B2}x{T2}\n";
                        text += $"SolidRectangle: {T}x{B}";
                        break;
                    default:
                        text = $"{SectionType}: {Math.Max(B1, Math.Max(B2, B))}x{D}";
                        break;
                }
            }
            if (MirrorType != SectionModel.MirrorTypes.None)
            {
                text += $"\nMirror: {MirrorType}, Gap: {MirrorGapA}";
            }
            return text;
        }
    }
}
