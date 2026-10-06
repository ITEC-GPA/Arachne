using System;
using System.Collections.Generic;
using System.Linq;
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
        private Point3d position;
        private Vector3d force = new Vector3d(0, 0, 0), weight = new Vector3d(0, 0, 0);
        public int Id { get; internal set; }
        public Point3d Position { get => new Point3d(position); internal set => position = new Point3d(value); }
        public bool FixX { get; internal set; }
        public bool FixY { get; internal set; }
        public bool FixZ { get; internal set; }
        public Vector3d Force { get => new Vector3d(force); internal set => force = new Vector3d(value); }
        public Vector3d PositionWeight { get => new Vector3d(weight); internal set => weight = new Vector3d(value); }
    }

    public sealed class FormEdge
    {
        private readonly Cable cable;
        public int Id { get; internal set; }
        public int Start { get; }
        public int End { get; }
        public Cable Cable => cable.Copy();
        internal FormEdge(int start, int end, Cable cable) { Start = start; End = end; this.cable = cable.Copy(); }
    }

    public sealed class FormModel
    {
        private readonly int[][] faces;
        public Guid Id { get; internal set; } = Guid.NewGuid();
        public IReadOnlyList<FormNode> Nodes { get; }
        public IReadOnlyList<FormEdge> Edges { get; }
        public IReadOnlyList<int[]> Faces => Array.AsReadOnly(faces.Select(f => (int[])f.Clone()).ToArray());
        public double Tolerance { get; }
        internal FormModel(List<FormNode> nodes, List<FormEdge> edges, List<int[]> faces, double tolerance)
        {
            for (int i=0;i<nodes.Count;i++) nodes[i].Id=i+1;
            for (int i=0;i<edges.Count;i++) edges[i].Id=i+1;
            Nodes = nodes.AsReadOnly(); Edges = edges.AsReadOnly(); this.faces = faces.Select(f => (int[])f.Clone()).ToArray(); Tolerance = tolerance;
        }
        public FormModel Snapshot() => new FormModel(Nodes.Select(n => new FormNode { Position=n.Position, FixX=n.FixX, FixY=n.FixY, FixZ=n.FixZ, Force=n.Force, PositionWeight=n.PositionWeight }).ToList(),
            Edges.Select(e => new FormEdge(e.Start,e.End,e.Cable)).ToList(), Faces.ToList(), Tolerance) { Id=Id };
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
        public SolverSettings Copy() { var copy=(SolverSettings)MemberwiseClone(); copy.PlaneOrigin=new Point3d(PlaneOrigin);copy.PlaneNormal=new Vector3d(PlaneNormal);return copy; }
        public override string ToString() => $"{Kind}; max {MaxIterations}; tolerance {Tolerance:G3}";
    }

    public sealed class FormResult
    {
        public Guid Id { get; internal set; } = Guid.NewGuid();
        public FormModel Model { get; internal set; }
        public SolverSettings Settings { get; internal set; }
        public SolvedModel Solved { get; internal set; }
        public List<SolverIteration> History { get; } = new List<SolverIteration>();
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
