using System;
using System.Collections.Generic;
using GPC.Geometry;

namespace Arachne.Core
{
    public enum TargetKind { ForceDensity, CableForce, Membrane, Curvature }
    public enum MembraneDirection { Undefined = -1, Warp = 0, Weft = 1 }
    public enum SolverKind { Linear, LinearIterative, NonLinear }
    public enum SolveStatus { Converged, NotConverged, Failed, DependencyMissing, Cancelled }

    // All model values use metres, newtons, kilograms and seconds.
    public sealed class Cable
    {
        public Point3d Start { get; set; }
        public Point3d End { get; set; }
        public TargetKind TargetKind { get; set; }
        public double Target { get; set; } = 1;
        public MembraneDirection Direction { get; set; } = MembraneDirection.Undefined;
        public bool Project { get; set; }
        public double TargetWeight { get; set; }
        public double Chord { get; set; }
        public double? FixedLength { get; set; }
        public double? FixedForce { get; set; }
        public double ConstraintWeight { get; set; } = 1;
        public double Area { get; set; }
        public double Density { get; set; }
        public List<CableLoad> Loads { get; } = new List<CableLoad>();

        public Cable Copy()
        {
            var copy = new Cable {
                Start = new Point3d(Start), End = new Point3d(End), TargetKind = TargetKind,
                Target = Target, Direction = Direction, Project = Project, TargetWeight = TargetWeight,
                Chord = Chord, FixedLength = FixedLength, FixedForce = FixedForce,
                ConstraintWeight = ConstraintWeight, Area = Area, Density = Density
            };
            foreach (var load in Loads) copy.Loads.Add(new CableLoad(load.Fraction, new Vector3d(load.Force)));
            return copy;
        }
        public override string ToString() => $"Cable: {TargetKind} = {Target:G5}";
    }

    public sealed class CableLoad
    {
        public double Fraction { get; }
        public Vector3d Force { get; }
        public CableLoad(double fraction, Vector3d force) { Fraction = fraction; Force = force; }
    }

    public sealed class NodeCondition
    {
        public Point3d Position { get; set; }
        public bool FixX { get; set; }
        public bool FixY { get; set; }
        public bool FixZ { get; set; }
        public Vector3d Force { get; set; } = new Vector3d(0, 0, 0);
        public Vector3d PositionWeight { get; set; } = new Vector3d(0, 0, 0);
        public override string ToString() => $"Node condition: X={FixX}, Y={FixY}, Z={FixZ}";
    }

    public sealed class FormNode
    {
        public Point3d Position { get; internal set; }
        public bool FixX { get; internal set; }
        public bool FixY { get; internal set; }
        public bool FixZ { get; internal set; }
        public Vector3d Force { get; internal set; } = new Vector3d(0, 0, 0);
        public Vector3d PositionWeight { get; internal set; } = new Vector3d(0, 0, 0);
    }

    public sealed class FormEdge
    {
        public int Start { get; }
        public int End { get; }
        public Cable Cable { get; }
        internal FormEdge(int start, int end, Cable cable) { Start = start; End = end; Cable = cable; }
    }

    public sealed class FormModel
    {
        public IReadOnlyList<FormNode> Nodes { get; }
        public IReadOnlyList<FormEdge> Edges { get; }
        public IReadOnlyList<int[]> Faces { get; }
        public double Tolerance { get; }
        internal FormModel(List<FormNode> nodes, List<FormEdge> edges, List<int[]> faces, double tolerance)
        { Nodes = nodes.AsReadOnly(); Edges = edges.AsReadOnly(); Faces = faces.AsReadOnly(); Tolerance = tolerance; }
        public override string ToString() => $"Arachne: {Nodes.Count} nodes, {Edges.Count} cables, {Faces.Count} faces";
    }

    public sealed class SolverSettings
    {
        public SolverKind Kind { get; set; }
        public int MaxIterations { get; set; } = 100;
        public double Tolerance { get; set; } = 1e-4;
        public int AlgebraicMaxIterations { get; set; }
        public double AlgebraicToleranceA { get; set; }
        public double AlgebraicToleranceB { get; set; }
        public double Gravity { get; set; }
        public double Pressure { get; set; }
        public bool CreateMesh { get; set; } = true;
        public Point3d PlaneOrigin { get; set; } = Point3d.Origin;
        public Vector3d PlaneNormal { get; set; } = Vector3d.ZAxis;
        public override string ToString() => $"{Kind}; max {MaxIterations}; tolerance {Tolerance:G3}";
    }

    public sealed class FormResult
    {
        public SolveStatus Status { get; internal set; }
        public int OuterIterations { get; internal set; }
        public List<string> Messages { get; } = new List<string>();
        public List<Point3d> Positions { get; } = new List<Point3d>();
        public List<double> AxialForces { get; } = new List<double>();
        public List<double> ForceDensities { get; } = new List<double>();
        public List<Vector3d> PressureForces { get; } = new List<Vector3d>();
        public List<int[]> Faces { get; } = new List<int[]>();
    }
}
