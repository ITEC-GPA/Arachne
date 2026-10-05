using System;
using GPC.Geometry;
using GPC.Utilities.Units;

namespace Arachne.Core
{
    public static class Validation
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static void Finite(double value, string name)
        { if (!IsFinite(value)) throw new ArgumentException(name + " must be finite."); }
        public static void Positive(double value, string name)
        { Finite(value, name); if (value <= 0) throw new ArgumentException(name + " must be > 0."); }
        public static void NonNegative(double value, string name)
        { Finite(value, name); if (value < 0) throw new ArgumentException(name + " must be >= 0."); }
        public static void Point(Point3d point)
        { if (point == null) throw new ArgumentException("Missing point."); Finite(point.X, "X"); Finite(point.Y, "Y"); Finite(point.Z, "Z"); }
        public static void Vector(Vector3d vector)
        { if (vector == null) throw new ArgumentException("Missing vector."); Finite(vector.X, "X"); Finite(vector.Y, "Y"); Finite(vector.Z, "Z"); }
        public static double Distance(Point3d a, Point3d b)
        { double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z; return Math.Sqrt(x*x + y*y + z*z); }
        public static void Settings(SolverSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!Enum.IsDefined(typeof(SolverKind), settings.Kind)) throw new ArgumentException("Unknown solver.");
            if (settings.MaxIterations < 1) throw new ArgumentException("Max iterations must be >= 1.");
            Positive(settings.Tolerance, "Tolerance");
            NonNegative(settings.AlgebraicMaxIterations, "Algebraic iterations");
            NonNegative(settings.AlgebraicToleranceA, "Algebraic tolerance A");
            NonNegative(settings.AlgebraicToleranceB, "Algebraic tolerance B");
            if (settings.AlgebraicToleranceB != 0) throw new ArgumentException("TensileLib 2.0.5.3 supports one algebraic tolerance: use A and keep B at 0.");
            NonNegative(settings.Gravity, "Gravity"); Finite(settings.Pressure, "Pressure");
            Point(settings.PlaneOrigin); Vector(settings.PlaneNormal);
            Positive(settings.PlaneNormal.X * settings.PlaneNormal.X + settings.PlaneNormal.Y * settings.PlaneNormal.Y + settings.PlaneNormal.Z * settings.PlaneNormal.Z, "Plane normal squared length");
            if (settings.Pressure != 0 && !settings.CreateMesh) throw new ArgumentException("Pressure requires Create mesh.");
        }
    }

    public static class UnitConversion
    {
        public static double ToMetres(double value, UnitsConvert.LengthUnits units)
        { Validation.Finite(value, "Length"); return UnitsConvert.Convert(value, units, UnitsConvert.LengthUnits.m, 1); }
    }
}
