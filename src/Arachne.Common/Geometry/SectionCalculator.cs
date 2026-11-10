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

        // IEEE-754 cos/sin pairs at (2 * Math.PI / 64) * i, i=0..63, captured on .NET Framework 4.8 x64.
        // GPC Polygon2d used these values for the original circular outlines. Math.Sin/Cos on .NET 8 can differ
        // by one ULP: Shape2d participates in AnalysisFingerprint even when the section properties are declared.
        // Freeze the directions, not the resulting coordinates or input dimensions: no rounding or tolerance,
        // and keep the original multiply-then-add order for both the outer contour and any concentric hole.
        private static readonly double[] CircleDirections = new ulong[]
        {
            0x3FF0000000000000UL, 0x0000000000000000UL,
            0x3FEFD88DA3D12526UL, 0x3FB917A6BC29B42CUL,
            0x3FEF6297CFF75CB0UL, 0x3FC8F8B83C69A60AUL,
            0x3FEE9F4156C62DDAUL, 0x3FD294062ED59F05UL,
            0x3FED906BCF328D46UL, 0x3FD87DE2A6AEA963UL,
            0x3FEC38B2F180BDB1UL, 0x3FDE2B5D3806F63BUL,
            0x3FEA9B66290EA1A3UL, 0x3FE1C73B39AE68C8UL,
            0x3FE8BC806B151741UL, 0x3FE44CF325091DD6UL,
            0x3FE6A09E667F3BCDUL, 0x3FE6A09E667F3BCCUL,
            0x3FE44CF325091DD6UL, 0x3FE8BC806B151741UL,
            0x3FE1C73B39AE68C9UL, 0x3FEA9B66290EA1A3UL,
            0x3FDE2B5D3806F63EUL, 0x3FEC38B2F180BDB0UL,
            0x3FD87DE2A6AEA964UL, 0x3FED906BCF328D46UL,
            0x3FD294062ED59F05UL, 0x3FEE9F4156C62DDBUL,
            0x3FC8F8B83C69A60DUL, 0x3FEF6297CFF75CB0UL,
            0x3FB917A6BC29B438UL, 0x3FEFD88DA3D12525UL,
            0x3C91A60000000000UL, 0x3FF0000000000000UL,
            0xBFB917A6BC29B42FUL, 0x3FEFD88DA3D12526UL,
            0xBFC8F8B83C69A608UL, 0x3FEF6297CFF75CB0UL,
            0xBFD294062ED59F02UL, 0x3FEE9F4156C62DDBUL,
            0xBFD87DE2A6AEA962UL, 0x3FED906BCF328D46UL,
            0xBFDE2B5D3806F63CUL, 0x3FEC38B2F180BDB1UL,
            0xBFE1C73B39AE68C6UL, 0x3FEA9B66290EA1A5UL,
            0xBFE44CF325091DD5UL, 0x3FE8BC806B151742UL,
            0xBFE6A09E667F3BCCUL, 0x3FE6A09E667F3BCDUL,
            0xBFE8BC806B151741UL, 0x3FE44CF325091DD6UL,
            0xBFEA9B66290EA1A4UL, 0x3FE1C73B39AE68C8UL,
            0xBFEC38B2F180BDB0UL, 0x3FDE2B5D3806F63FUL,
            0xBFED906BCF328D46UL, 0x3FD87DE2A6AEA965UL,
            0xBFEE9F4156C62DDAUL, 0x3FD294062ED59F06UL,
            0xBFEF6297CFF75CB0UL, 0x3FC8F8B83C69A617UL,
            0xBFEFD88DA3D12525UL, 0x3FB917A6BC29B43CUL,
            0xBFF0000000000000UL, 0x3CA1A60000000000UL,
            0xBFEFD88DA3D12526UL, 0xBFB917A6BC29B42BUL,
            0xBFEF6297CFF75CB0UL, 0xBFC8F8B83C69A60EUL,
            0xBFEE9F4156C62DDBUL, 0xBFD294062ED59F01UL,
            0xBFED906BCF328D47UL, 0xBFD87DE2A6AEA961UL,
            0xBFEC38B2F180BDB1UL, 0xBFDE2B5D3806F63BUL,
            0xBFEA9B66290EA1A5UL, 0xBFE1C73B39AE68C6UL,
            0xBFE8BC806B151742UL, 0xBFE44CF325091DD4UL,
            0xBFE6A09E667F3BCEUL, 0xBFE6A09E667F3BCCUL,
            0xBFE44CF325091DDAUL, 0xBFE8BC806B15173EUL,
            0xBFE1C73B39AE68C8UL, 0xBFEA9B66290EA1A3UL,
            0xBFDE2B5D3806F63FUL, 0xBFEC38B2F180BDB0UL,
            0xBFD87DE2A6AEA96DUL, 0xBFED906BCF328D44UL,
            0xBFD294062ED59F07UL, 0xBFEE9F4156C62DDAUL,
            0xBFC8F8B83C69A619UL, 0xBFEF6297CFF75CAFUL,
            0xBFB917A6BC29B421UL, 0xBFEFD88DA3D12526UL,
            0xBCAA790000000000UL, 0xBFF0000000000000UL,
            0x3FB917A6BC29B407UL, 0xBFEFD88DA3D12526UL,
            0x3FC8F8B83C69A60CUL, 0xBFEF6297CFF75CB0UL,
            0x3FD294062ED59F00UL, 0xBFEE9F4156C62DDBUL,
            0x3FD87DE2A6AEA967UL, 0xBFED906BCF328D45UL,
            0x3FDE2B5D3806F63AUL, 0xBFEC38B2F180BDB1UL,
            0x3FE1C73B39AE68C5UL, 0xBFEA9B66290EA1A5UL,
            0x3FE44CF325091DD7UL, 0xBFE8BC806B151740UL,
            0x3FE6A09E667F3BCBUL, 0xBFE6A09E667F3BCEUL,
            0x3FE8BC806B15173EUL, 0xBFE44CF325091DDAUL,
            0x3FEA9B66290EA1A3UL, 0xBFE1C73B39AE68C8UL,
            0x3FEC38B2F180BDAFUL, 0xBFDE2B5D3806F640UL,
            0x3FED906BCF328D44UL, 0xBFD87DE2A6AEA96EUL,
            0x3FEE9F4156C62DDAUL, 0xBFD294062ED59F08UL,
            0x3FEF6297CFF75CAFUL, 0xBFC8F8B83C69A61BUL,
            0x3FEFD88DA3D12526UL, 0xBFB917A6BC29B425UL,
        }.Select(bits => BitConverter.Int64BitsToDouble(unchecked((long)bits))).ToArray();

        private static Polygon2d CirclePolygon(double diameter, double center)
        {
            double radius = diameter / 2;
            var points = new Point2d[64];
            for (int i = 0; i < points.Length; i++)
                points[i] = new Point2d(radius * CircleDirections[2 * i] + center, radius * CircleDirections[2 * i + 1] + center);
            return new Polygon2d(points);
        }

        /// <summary>The outline drawn by the previews: the shapes (mirrored as computed) or, for circles,
        /// a 64-sided inscribed polygon; mechanical properties remain analytical. Presentation only.</summary>
        private Shape2d Outline()
        {
            Shape2d Circle(double diameter, double hole)
            {
                var outer = CirclePolygon(diameter, diameter / 2);
                Polygon2d[] holes = hole > 0 ? new[] { CirclePolygon(hole, diameter / 2) } : null;
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
