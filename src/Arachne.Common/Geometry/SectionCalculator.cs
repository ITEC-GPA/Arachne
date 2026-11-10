using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GSection = GPC.Model.Sections.Section;

namespace Arachne.Common.Geometry
{
    /// <summary>
    /// Section components boundary: the dimensions typed in a section component become a GPC <see cref="GSection"/> with declared
    /// properties and an outline. The calculation is the one of FeMM (SectionModel.CalculateSection/GetShapes at Arachne 7b1108d),
    /// kept operation by operation so that areas and moments are bit-identical; it is not stored, the GPC section is.
    /// Dimensions in the document units (the section is scaled to mm by Build Model).
    /// </summary>
    public sealed class SectionCalculator
    {
        public enum Kind { Undefined = -1, SolidCircle, HollowCircle, SolidRectangle, HollowRectangle, I, T, C, Omega, Angle, Z, Generic, GenericShapes, Composite }
        public enum Mirroring { None = -1, Top, Bottom, Left, Right }

        public Kind Type { get; set; } = Kind.Undefined;
        public string Name { get; set; } = "";
        public double D, B, B1, B2, L, T, T1, T2, T3, R, MirrorGapA;
        public Mirroring Mirror { get; set; } = Mirroring.None;
        public Shapes GenericShapes { get; set; }
        // Results, as FeMM: the circles set only area, principal moments and moduli.
        public double Area, Ixx, Iyy, Ixy, I11, I22, J, Wxx, Wyy, W11, W22, Rho11, Rho22, AngleX1Rad;
        public Point2d Centroid = new Point2d(0, 0);

        private Shapes GetShapes()
        {
            if (Type == Kind.GenericShapes)
            {
                // As FeMM: the generic shapes themselves are moved (idempotent after the first call)
                var moved = new Shapes(GenericShapes);
                moved.MoveToMin();
                return moved;
            }
            var border = new Polygon2d(); Polygon2d[] holes = null;
            double d1 = -(B1 - Math.Max(B1, B2)) / 2, d2 = -(B2 - Math.Max(B1, B2)) / 2;
            switch (Type)
            {
                case Kind.SolidRectangle: border.Add(0, 0); border.Add(0, D); border.Add(B, D); border.Add(B, 0); break;
                case Kind.HollowRectangle:
                    border.Add(0, 0); border.Add(0, D); border.Add(B, D); border.Add(B, 0);
                    holes = new[] { new Polygon2d { { T2, T1 }, { T2, D - T1 }, { B - T2, D - T1 }, { B - T2, T1 } } };
                    break;
                case Kind.I:
                case Kind.Composite:
                    border.Add(d1, 0); border.Add(d1, T1); border.Add((Math.Max(B1, B2) - T3) / 2, T1); border.Add((Math.Max(B1, B2) - T3) / 2, D - T2);
                    border.Add(d2, D - T2); border.Add(d2, D); border.Add(d2 + B2, D); border.Add(d2 + B2, D - T2);
                    border.Add((Math.Max(B1, B2) - T3) / 2 + T3, D - T2); border.Add((Math.Max(B1, B2) - T3) / 2 + T3, T1); border.Add(d1 + B1, T1); border.Add(d1 + B1, 0);
                    break;
                case Kind.T:
                    border.Add((B - T2) / 2, 0); border.Add((B - T2) / 2, D - T1);
                    if (L > 0 && T3 > 0) { border.Add(T3, D - T1); border.Add(T3, D - T1 - L); border.Add(0, D - T1 - L); } else border.Add(0, D - T1);
                    border.Add(0, D); border.Add(B, D);
                    if (L > 0 && T3 > 0) { border.Add(B, D - T1 - L); border.Add(B - T3, D - T1 - L); border.Add(B - T3, D - T1); } else border.Add(B, D - T1);
                    border.Add((B - T2) / 2 + T2, D - T1); border.Add((B - T2) / 2 + T2, 0);
                    break;
                case Kind.C:
                    border.Add(0, 0); border.Add(0, D); border.Add(B, D);
                    if (L > 0 && T3 > 0) { border.Add(B, D - T1 - L); border.Add(B - T3, D - T1 - L); border.Add(B - T3, D - T1); } else border.Add(B, D - T1);
                    border.Add(T2, D - T1); border.Add(T2, T1);
                    if (L > 0 && T3 > 0) { border.Add(B - T3, T1); border.Add(B - T3, T1 + L); border.Add(B, T1 + L); } else border.Add(B, T1);
                    border.Add(B, 0);
                    break;
                case Kind.Omega:
                    border.Add(0, L); border.Add(0, D + L);
                    if (L > 0 && T3 > 0) { border.Add(B - T3, D + L); border.Add(B - T3, D + L * 2); border.Add(B, D + L * 2); } else border.Add(B, D + L);
                    border.Add(B, D + L - T1); border.Add(T2, D + L - T1); border.Add(T2, L + T1); border.Add(B, L + T1);
                    if (L > 0 && T3 > 0) { border.Add(B, 0); border.Add(B - T3, 0); border.Add(B - T3, L); } else border.Add(B, L);
                    border.Add(B, 0);
                    break;
                case Kind.Angle: border.Add(0, 0); border.Add(0, D); border.Add(T2, D); border.Add(T2, T1); border.Add(B, T1); border.Add(B, 0); break;
                case Kind.Z:
                    border.Add(B2 - T3, 0); border.Add(B2 - T3, D - T2); border.Add(0, D - T2); border.Add(0, D);
                    border.Add(B2, D); border.Add(B2, T1); border.Add(B2 - T3 + B1, T1); border.Add(B2 - T3 + B1, 0);
                    break;
            }
            var shape = new Shape2d(border, holes);
            shape.MoveToMin();
            return new Shapes(new[] { shape });
        }

        private Shapes MirroredShapes()
        {
            if (Mirror == Mirroring.None) return GetShapes();
            double a = -1, b = 0, c = -MirrorGapA / 2.0;
            var list = new List<Shape2d>();
            foreach (Shape2d main in GetShapes()) { list.Add(main); list.Add(main.Mirror2d(a, b, c)); }
            return new Shapes(list);
        }

        /// <summary>FeMM SectionModel.CalculateSection, unchanged.</summary>
        public void Calculate()
        {
            switch (Type)
            {
                case Kind.SolidCircle:
                    Area = Math.Pow(D / 2, 2) * Math.PI; I11 = Math.PI * Math.Pow(D, 4) / 64; I22 = I11; W11 = I11 / (D * 0.5); W22 = W11;
                    break;
                case Kind.HollowCircle:
                    Area = Math.Pow(D / 2, 2) * Math.PI - Math.Pow(D / 2 - T, 2) * Math.PI; I11 = Math.PI * (Math.Pow(D, 4) - Math.Pow(D - T * 2, 4)) / 64; I22 = I11;
                    W11 = I11 / (D * 0.5); W22 = W11;
                    break;
                case Kind.Composite: break;
                default:
                    var shapes = MirroredShapes();
                    shapes.MoveToMin();
                    shapes.GetGeometricalDatas(Type == Kind.GenericShapes, 0.000000001, out double xG, out double yG, out Area, out Ixx, out Iyy,
                        out Ixy, out I11, out I22, out AngleX1Rad, out Wxx, out Wyy, out W11, out W22, out Rho11, out Rho22);
                    if (Type == Kind.GenericShapes && AngleX1Rad < -0.01) AngleX1Rad += Math.PI;
                    Centroid = new Point2d(xG, yG);
                    break;
            }
        }

        /// <summary>The outline drawn by the previews: the shapes (mirrored as computed) or, for circles and generic sections,
        /// a polygon of 64 sides of the same area. Presentation only.</summary>
        private Shape2d Outline()
        {
            int sides = 64;
            Shape2d Circle(double diameter, double hole)
            {
                var outer = new Polygon2d(diameter, sides, new Point2d(diameter / 2, diameter / 2));
                Polygon2d[] holes = hole > 0 ? new[] { new Polygon2d(hole, sides, new Point2d(diameter / 2, diameter / 2)) } : null;
                return new Shape2d(outer, holes);
            }
            switch (Type)
            {
                case Kind.SolidCircle: return D > 0 ? Circle(D, 0) : null;
                case Kind.HollowCircle: return D > 0 ? Circle(D, Math.Max(0, D - 2 * T)) : null;
                case Kind.Generic: case Kind.Composite: case Kind.Undefined: return null;
            }
            var shapes = MirroredShapes().ToShapes2d();
            if (shapes.Length == 0) return null;
            var first = new Shape2d(shapes[0].Fill2d, shapes[0].Holes2d, shapes.Length > 1 ? shapes.Skip(1).ToArray() : null);
            return first;
        }

        /// <summary>The GPC section of the current values (after <see cref="Calculate"/> where the component calls it).</summary>
        public GSection ToSection()
        {
            Shape2d outline = null;
            try { outline = Outline(); } catch (Exception) { outline = null; }
            return new GSection(outline, Area, Ixx, Iyy, Ixy, I11, I22, J, new Point2d(Centroid.X, Centroid.Y), AngleX1Rad, W11, W22, Wxx, Wyy, Name ?? "");
        }

        /// <summary>The label of FeMM (SectionModel.Code) for the section kinds that had one.</summary>
        public string Code()
        {
            switch (Type)
            {
                case Kind.SolidCircle: return string.Format("CSS {0}", D);
                case Kind.HollowCircle: return string.Format("CHS {0}x{1}", D, T);
                case Kind.SolidRectangle: return string.Format("RSS {0}x{1}", D, B);
                case Kind.HollowRectangle: return (B == D ? "SHS" : "RHS") + string.Format(" {0}x{1}", D, B) + (T1 == T2 ? string.Format("x{0}", T1) : string.Format("x{0}x{1}", T1, T2));
                case Kind.C: return (L == 0 ? "U" : "C") + string.Format(" {0}x{1}x{2}", D, B, T1);
                default: return Type.ToString();
            }
        }
    }
}
