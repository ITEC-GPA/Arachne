using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GPC.Utilities.Units;

namespace Arachne.Arap
{
    /// <summary>Snapshot of GPC vertices and consecutive triangle indices (a,b,c,a,b,c,...).
    /// Connectivity uses vertex indices, not welded coordinates. Input collections are copied.</summary>
    public sealed class ArapMesh
    {
        internal readonly D3[] Points;
        internal readonly int[] Faces;
        public int VertexCount => Points.Length;
        public int TriangleCount => Faces.Length / 3;
        public ArapMesh(IEnumerable<Point3d> vertices, IEnumerable<int> triangles)
        {
            if (vertices == null || triangles == null) throw new ArgumentNullException("Mesh collections");
            Points = vertices.Select(p => p == null ? new D3(double.NaN, 0, 0) : new D3(p.X, p.Y, p.Z)).ToArray();
            Faces = triangles.ToArray();
        }
        public Point3d[] GetVertices() => Points.Select(p => p.Point()).ToArray();
        public int[] GetTriangleIndices() => (int[])Faces.Clone();
        /// <summary>Explicit unit conversion; the solver itself uses the mesh's length units.</summary>
        public ArapMesh ConvertUnits(UnitsConvert.LengthUnits from, UnitsConvert.LengthUnits to)
        {
            if (!Enum.IsDefined(typeof(UnitsConvert.LengthUnits), from) || !Enum.IsDefined(typeof(UnitsConvert.LengthUnits), to))
                throw new ArgumentOutOfRangeException("units");
            double factor = UnitsConvert.Convert(1, from, to, 1);
            return new ArapMesh(Points.Select(p => (p * factor).Point()), Faces);
        }
    }

    public sealed class ArapPin
    {
        public int Vertex { get; }
        internal D3 Target { get; }
        public Point3d Position => Target.Point();
        public ArapPin(int vertex, Point3d position)
        {
            Vertex = vertex;
            if (position == null) throw new ArgumentNullException(nameof(position));
            Target = new D3(position.X, position.Y, position.Z);
        }
    }

    public sealed class ArapOptions
    {
        public int MaxIterations { get; set; } = 1000;
        public int MaxPerimeterIterations { get; set; } = 1000;
        public int PerimeterSolveIterations { get; set; } = 100;
        /// <summary>Absolute tolerance in input mesh length units.</summary>
        public double PerimeterTolerance { get; set; } = 0.5;
        public double CornerAngle { get; set; } = 7 * Math.PI / 8;
        public double PerimeterStep { get; set; } = 0.1;
        /// <summary>Maximum vertex movement divided by the original bounding-box span.</summary>
        public double PositionTolerance { get; set; } = 1e-7;
        public double RelativeEnergyTolerance { get; set; } = 1e-9;
        public double LinearTolerance { get; set; } = 1e-11;
        /// <summary>Zero chooses max(200, 2 * number of free vertices), capped at 20000.</summary>
        public int MaxLinearIterations { get; set; }
        internal ArapOptions Snapshot()
        {
            var copy = (ArapOptions)MemberwiseClone();
            if (MaxIterations < 1 || MaxPerimeterIterations < 0 || PerimeterSolveIterations < 1 || MaxLinearIterations < 0)
                throw new ArgumentException("Invalid iteration limit.");
            if (!D3.Finite(PerimeterTolerance) || PerimeterTolerance < 0 || !D3.Finite(CornerAngle) || CornerAngle <= 0 || CornerAngle > Math.PI ||
                !D3.Finite(PerimeterStep) || PerimeterStep <= 0 || PerimeterStep > 0.5 ||
                !Positive(PositionTolerance) || !Positive(RelativeEnergyTolerance) || !Positive(LinearTolerance) || LinearTolerance >= 1)
                throw new ArgumentException("Invalid ARAP tolerance, corner angle or perimeter step (0 < step <= 0.5).");
            return copy;
        }
        private static bool Positive(double x) => D3.Finite(x) && x > 0;
    }

    public enum ArapStatus { Converged, IterationLimit, PerimeterLimit, InvalidInput, InvalidParameterization, NumericalFailure, Cancelled }

    public sealed class ArapIteration
    {
        public int Iteration { get; internal set; }
        /// <summary>Zero for the first solve; positive for perimeter adjustment passes.</summary>
        public int PerimeterPass { get; internal set; }
        /// <summary>Energy in normalized coordinates. Comparable within a pass with fixed pins.</summary>
        public double Energy { get; internal set; }
        public double MaxDisplacement { get; internal set; }
        public double LinearResidual { get; internal set; }
    }

    public sealed class ArapResult
    {
        private readonly D3[] positions;
        public ArapStatus Status { get; }
        public bool Success => Status == ArapStatus.Converged;
        public string Message { get; }
        public IReadOnlyList<ArapIteration> History { get; }
        public int PerimeterIterations { get; }
        public double MaxPerimeterError { get; }
        public double MaxRelativeEdgeError { get; }
        /// <summary>Returns a copy. Failed/cancelled solves expose diagnostics, but no usable mesh.</summary>
        public Point3d[] GetVertices() => positions.Select(p => p.Point()).ToArray();
        internal ArapResult(ArapStatus status, string message, D3[] vertices, List<ArapIteration> history,
            int perimeterPass = 0, double perimeterError = 0, double edgeError = 0)
        {
            Status = status; Message = message;
            positions = status == ArapStatus.Converged ? (D3[])vertices.Clone() : Array.Empty<D3>();
            History = Array.AsReadOnly(history.ToArray());
            PerimeterIterations = perimeterPass; MaxPerimeterError = perimeterError; MaxRelativeEdgeError = edgeError;
        }
    }

    // Small numerical work vector. Public geometry always uses the local GPC types.
    internal struct D3
    {
        internal double X, Y, Z;
        internal D3(double x, double y, double z = 0) { X = x; Y = y; Z = z; }
        internal double this[int i] { get => i == 0 ? X : i == 1 ? Y : Z; set { if (i == 0) X = value; else if (i == 1) Y = value; else Z = value; } }
        internal double Norm2 => Dot(this, this);
        internal double Norm => Math.Sqrt(Norm2);
        internal bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        internal Point3d Point() => new Point3d(X, Y, Z);
        internal static bool Finite(double x) => !double.IsNaN(x) && !double.IsInfinity(x);
        public static D3 operator +(D3 a, D3 b) => new D3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static D3 operator -(D3 a, D3 b) => new D3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static D3 operator *(D3 a, double s) => new D3(a.X * s, a.Y * s, a.Z * s);
        public static D3 operator /(D3 a, double s) => a * (1 / s);
        internal static double Dot(D3 a, D3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        internal static D3 Cross(D3 a, D3 b) => new D3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }

    internal sealed class ArapFailure : Exception
    {
        internal ArapStatus Status { get; }
        internal ArapFailure(ArapStatus status, string message) : base(message) { Status = status; }
    }
}
