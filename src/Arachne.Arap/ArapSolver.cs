using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Arachne.Arap
{
    /// <summary>Independent local/global ARAP: per-triangle rotations in 2D, spokes-and-rims
    /// rotations in 3D. Each call owns its state and snapshots its options and constraints.</summary>
    public static class ArapSolver
    {
        public static ArapResult Flatten(ArapMesh mesh, ArapOptions options = null,
            CancellationToken cancellationToken = default, Action<ArapIteration> progress = null)
            => Run(mesh, null, options, 2, true, cancellationToken, progress);
        public static ArapResult Unwrap(ArapMesh mesh, IEnumerable<ArapPin> pins = null, ArapOptions options = null,
            CancellationToken cancellationToken = default, Action<ArapIteration> progress = null)
            => Run(mesh, pins, options, 2, false, cancellationToken, progress);
        public static ArapResult Deform(ArapMesh mesh, IEnumerable<ArapPin> pins = null, ArapOptions options = null,
            CancellationToken cancellationToken = default, Action<ArapIteration> progress = null)
            => Run(mesh, pins, options, 3, false, cancellationToken, progress);

        private static ArapResult Run(ArapMesh mesh, IEnumerable<ArapPin> inputPins, ArapOptions inputOptions,
            int dimension, bool flatten, CancellationToken token, Action<ArapIteration> progress)
        {
            var history = new List<ArapIteration>(); int perimeterPass = 0; double perimeterError = 0;
            try
            {
                token.ThrowIfCancellationRequested();
                var options = (inputOptions ?? new ArapOptions()).Snapshot();
                var topology = new MeshTopology(mesh, token);
                var u = dimension == 2 ? topology.Initial2D(options, token) : (D3[])topology.V.Clone();
                var requestedPins = inputPins?.ToArray();
                var pins = ReadPins(requestedPins, topology, u, dimension);
                AlignToPins(u, pins, topology, dimension);
                var engine = new Engine(topology, dimension, options, history, token, progress);
                if (!engine.Optimize(u, pins, options.MaxIterations, 0))
                    return Failed(ArapStatus.IterationLimit, "ARAP reached its iteration limit; no converged mesh is available.", history);
                if (dimension == 2 && !topology.PositiveFaces(u))
                    return Failed(ArapStatus.InvalidParameterization, "The constraints produce inverted or degenerate planar triangles.", history);
                if (flatten)
                {
                    var sides = MakeSides(topology, options.CornerAngle);
                    perimeterError = PerimeterError(sides, u) * topology.Scale;
                    // A zero budget explicitly means that perimeter matching was not verified.
                    if (options.MaxPerimeterIterations == 0)
                        return new ArapResult(ArapStatus.PerimeterLimit, "Perimeter iteration budget is zero; no developed mesh was accepted.", u, history, 0, perimeterError);
                    var cornerPins = new Dictionary<int, D3>();
                    foreach (var side in sides) { cornerPins[side.Ids[0]] = u[side.Ids[0]]; cornerPins[side.Ids[side.Ids.Length - 1]] = u[side.Ids[side.Ids.Length - 1]]; }
                    bool innerConverged = true;
                    while (perimeterError > options.PerimeterTolerance && perimeterPass < options.MaxPerimeterIterations)
                    {
                        token.ThrowIfCancellationRequested(); perimeterPass++;
                        var movement = cornerPins.Keys.ToDictionary(i => i, i => new D3());
                        foreach (var side in sides)
                        {
                            int a = side.Ids[0], b = side.Ids[side.Ids.Length - 1];
                            D3 chord = u[b] - u[a]; double length = chord.Norm;
                            if (length < 1e-14) throw new ArapFailure(ArapStatus.InvalidParameterization, "A perimeter side has coincident endpoints.");
                            double difference = SideLength(side.Ids, u) - side.Target;
                            D3 step = chord * (difference * options.PerimeterStep / length);
                            movement[a] += step; movement[b] -= step;
                        }
                        // Simultaneous corner updates avoid dependence on dictionary/side traversal order.
                        foreach (int i in cornerPins.Keys.ToArray()) cornerPins[i] = u[i] + movement[i];
                        innerConverged = engine.Optimize(u, cornerPins, options.PerimeterSolveIterations, perimeterPass);
                        if (!topology.PositiveFaces(u)) throw new ArapFailure(ArapStatus.InvalidParameterization, "Perimeter adjustment inverted or collapsed planar triangles.");
                        perimeterError = PerimeterError(sides, u) * topology.Scale;
                    }
                    // Recheck the actual final geometry, including the last permitted iteration.
                    if (perimeterError > options.PerimeterTolerance)
                        return new ArapResult(ArapStatus.PerimeterLimit, "Side-length tolerance was not reached; inspect MaxPerimeterError and History.", u, history, perimeterPass, perimeterError);
                    if (!innerConverged) return new ArapResult(ArapStatus.IterationLimit, "Perimeter lengths pass, but the last inner ARAP solve has not converged.", u, history, perimeterPass, perimeterError);
                }
                if (u.Any(p => !p.IsFinite)) throw new ArapFailure(ArapStatus.NumericalFailure, "Non-finite solver output.");
                if (dimension == 3)
                    foreach (var t in topology.Triangles)
                        if (D3.Cross(u[t.Id[1]] - u[t.Id[0]], u[t.Id[2]] - u[t.Id[0]]).Norm <= 1e-12 * t.Local[1].X * t.Local[2].Y)
                            throw new ArapFailure(ArapStatus.InvalidParameterization, "Deformation collapsed a triangle.");
                double edgeError = 0;
                foreach (var t in topology.Triangles) for (int e = 0; e < 3; e++)
                {
                    int a = t.Id[e], b = t.Id[(e + 1) % 3];
                    edgeError = Math.Max(edgeError, Math.Abs((u[a] - u[b]).Norm / (topology.V[a] - topology.V[b]).Norm - 1));
                }
                var output = u.Select(p => p * topology.Scale + topology.Origin).ToArray();
                if (dimension == 2) for (int i = 0; i < output.Length; i++) output[i].Z = 0;
                // Avoid round-trip rounding of user-specified hard constraints.
                if (!flatten && requestedPins != null) foreach (var pin in requestedPins) output[pin.Vertex] = pin.Target;
                if (dimension == 2) for (int i = 0; i < output.Length; i++) output[i].Z = 0;
                if (output.Any(p => !p.IsFinite)) throw new ArapFailure(ArapStatus.NumericalFailure, "Output coordinates exceed the numerical range.");
                return new ArapResult(ArapStatus.Converged, "ARAP converged. Check edge distortion before using the developed geometry for fabrication.", output, history, perimeterPass, perimeterError, edgeError);
            }
            catch (OperationCanceledException) { return Failed(ArapStatus.Cancelled, "ARAP calculation cancelled.", history); }
            catch (ArapFailure e) { return Failed(e.Status, e.Message, history); }
            catch (ArgumentException e) { return Failed(ArapStatus.InvalidInput, e.Message, history); }
        }
        private static ArapResult Failed(ArapStatus status, string message, List<ArapIteration> history)
            => new ArapResult(status, message, Array.Empty<D3>(), history);

        private static Dictionary<int, D3> ReadPins(IEnumerable<ArapPin> input, MeshTopology mesh, D3[] initial, int dim)
        {
            var pins = new Dictionary<int, D3>();
            if (input != null) foreach (var pin in input)
            {
                if (pin == null || pin.Vertex < 0 || pin.Vertex >= mesh.V.Length || !pin.Target.IsFinite)
                    throw new ArapFailure(ArapStatus.InvalidInput, "Pin indices and positions must be valid and finite.");
                if (pins.ContainsKey(pin.Vertex)) throw new ArapFailure(ArapStatus.InvalidInput, "Duplicate pin at vertex " + pin.Vertex + ".");
                if (dim == 2 && pin.Target.Z != 0) throw new ArapFailure(ArapStatus.InvalidInput, "Unwrap pin positions must lie in the XY plane (Z=0).");
                D3 target = (pin.Target - mesh.Origin) / mesh.Scale; if (dim == 2) target.Z = 0;
                if (!target.IsFinite) throw new ArapFailure(ArapStatus.InvalidInput, "Pin positions exceed the numerical range relative to the mesh.");
                pins.Add(pin.Vertex, target);
            }
            if (pins.Count == 0) foreach (var component in mesh.Components) pins.Add(component[0], initial[component[0]]);
            foreach (var component in mesh.Components)
                if (!component.Any(pins.ContainsKey)) throw new ArapFailure(ArapStatus.InvalidInput, "Every disconnected mesh part requires at least one pin.");
            return pins;
        }

        private static void AlignToPins(D3[] u, Dictionary<int, D3> pins, MeshTopology mesh, int dim)
        {
            foreach (var component in mesh.Components)
            {
                var ids = component.Where(pins.ContainsKey).ToArray(); D3 a = new D3(), b = new D3();
                foreach (int i in ids) { a += u[i]; b += pins[i]; } a /= ids.Length; b /= ids.Length;
                var h = new double[3, 3]; foreach (int i in ids) ManagedAlgebra.Outer(h, u[i] - a, pins[i] - b, 1);
                var rotation = ManagedAlgebra.Rotation(h, dim);
                foreach (int i in component) u[i] = ManagedAlgebra.Apply(rotation, u[i] - a) + b;
            }
            foreach (var pin in pins) u[pin.Key] = pin.Value;
        }

        private sealed class Side { internal int[] Ids; internal double Target; }
        private static List<Side> MakeSides(MeshTopology mesh, double cornerAngle)
        {
            var sides = new List<Side>();
            foreach (var loop in mesh.Boundaries)
            {
                var corners = new List<int>();
                for (int i = 0; i < loop.Length; i++)
                {
                    D3 a = mesh.V[loop[(i + loop.Length - 1) % loop.Length]] - mesh.V[loop[i]], b = mesh.V[loop[(i + 1) % loop.Length]] - mesh.V[loop[i]];
                    double angle = Math.Acos(Math.Max(-1, Math.Min(1, D3.Dot(a, b) / (a.Norm * b.Norm))));
                    if (angle < cornerAngle) corners.Add(i);
                }
                // Smooth boundaries need anchors too. Three deterministic arc-length anchors
                // replace the legacy failure when no two corners meet the angle threshold.
                if (corners.Count < 2)
                {
                    corners.Clear(); corners.Add(0); double total = SideLength(loop.Concat(new[] { loop[0] }).ToArray(), mesh.V), distance = 0;
                    for (int i = 1; i < loop.Length && corners.Count < 3; i++)
                    { distance += (mesh.V[loop[i]] - mesh.V[loop[i - 1]]).Norm; if (distance >= total * corners.Count / 3) corners.Add(i); }
                    for (int i = 1; corners.Count < 3 && i < loop.Length; i++) if (!corners.Contains(i)) corners.Add(i);
                    corners.Sort();
                }
                for (int c = 0; c < corners.Count; c++)
                {
                    int start = corners[c], end = corners[(c + 1) % corners.Count]; var ids = new List<int> { loop[start] };
                    for (int j = (start + 1) % loop.Length; j != end; j = (j + 1) % loop.Length) ids.Add(loop[j]); ids.Add(loop[end]);
                    sides.Add(new Side { Ids = ids.ToArray(), Target = SideLength(ids, mesh.V) });
                }
            }
            return sides;
        }
        private static double SideLength(IReadOnlyList<int> ids, D3[] v)
        { double length = 0; for (int i = 1; i < ids.Count; i++) length += (v[ids[i]] - v[ids[i - 1]]).Norm; return length; }
        private static double PerimeterError(List<Side> sides, D3[] uv) => sides.Max(s => Math.Abs(SideLength(s.Ids, uv) - s.Target));

        private sealed class Engine
        {
            private struct Term { internal int A, B, Cluster; internal D3 Rest; internal double Weight; }
            private readonly MeshTopology mesh;
            private readonly int dimension, clusterCount;
            private readonly List<Term> terms = new List<Term>();
            private readonly ArapOptions options;
            private readonly List<ArapIteration> history;
            private readonly CancellationToken token;
            private readonly Action<ArapIteration> progress;
            internal Engine(MeshTopology mesh, int dimension, ArapOptions options, List<ArapIteration> history, CancellationToken token, Action<ArapIteration> progress)
            {
                this.mesh = mesh; this.dimension = dimension; this.options = options; this.history = history; this.token = token; this.progress = progress;
                clusterCount = dimension == 2 ? mesh.Triangles.Count : mesh.V.Length;
                for (int f = 0; f < mesh.Triangles.Count; f++)
                {
                    var t = mesh.Triangles[f];
                    for (int e = 0; e < 3; e++)
                    {
                        int a = t.Id[e], b = t.Id[(e + 1) % 3];
                        if (dimension == 2) terms.Add(new Term { A = a, B = b, Cluster = f, Rest = t.Local[e] - t.Local[(e + 1) % 3], Weight = t.Weight[e] });
                        else foreach (int cluster in t.Id) terms.Add(new Term { A = a, B = b, Cluster = cluster, Rest = mesh.V[a] - mesh.V[b], Weight = t.Weight[e] / 3 });
                    }
                }
            }
            private double[][,] Rotations(D3[] u)
            {
                var h = new double[clusterCount][,];
                for (int i = 0; i < clusterCount; i++) h[i] = new double[3, 3];
                foreach (var term in terms) ManagedAlgebra.Outer(h[term.Cluster], term.Rest, u[term.A] - u[term.B], term.Weight);
                for (int i = 0; i < clusterCount; i++) { if ((i & 255) == 0) token.ThrowIfCancellationRequested(); h[i] = ManagedAlgebra.Rotation(h[i], dimension); }
                return h;
            }
            private double Energy(D3[] u, double[][,] rotation)
            {
                double sum = 0;
                foreach (var term in terms) { D3 difference = u[term.A] - u[term.B] - ManagedAlgebra.Apply(rotation[term.Cluster], term.Rest); sum += term.Weight * difference.Norm2; }
                if (!D3.Finite(sum) || sum < -1e-10) throw new ArapFailure(ArapStatus.NumericalFailure, "ARAP energy is not finite or nonnegative.");
                return Math.Max(0, sum);
            }
            internal bool Optimize(D3[] u, Dictionary<int, D3> pins, int maxIterations, int perimeterPass)
            {
                int n = u.Length; var fixedVertex = new bool[n];
                foreach (var pin in pins) { fixedVertex[pin.Key] = true; u[pin.Key] = pin.Value; }
                var system = new SparseSystem(mesh.Stiffness, fixedVertex); var rotation = Rotations(u); double previous = Energy(u, rotation);
                for (int iteration = 0; iteration < maxIterations; iteration++)
                {
                    token.ThrowIfCancellationRequested(); var rhs = new D3[n]; var before = (D3[])u.Clone();
                    foreach (var term in terms)
                    {
                        D3 edge = ManagedAlgebra.Apply(rotation[term.Cluster], term.Rest) * term.Weight;
                        rhs[term.A] += edge; rhs[term.B] -= edge;
                    }
                    double residual = 0;
                    for (int d = 0; d < dimension; d++)
                    {
                        var x = u.Select(p => p[d]).ToArray(); var b = rhs.Select(p => p[d]).ToArray();
                        residual = Math.Max(residual, system.Solve(b, x, options, token));
                        for (int i = 0; i < n; i++) u[i][d] = x[i];
                    }
                    rotation = Rotations(u); double energy = Energy(u, rotation), movement = 0;
                    for (int i = 0; i < n; i++) movement = Math.Max(movement, (u[i] - before[i]).Norm);
                    if (!D3.Finite(movement) || energy > previous + 1e-9 * Math.Max(1, previous))
                        throw new ArapFailure(ArapStatus.NumericalFailure, "ARAP energy increased unexpectedly or coordinates are non-finite.");
                    var sample = new ArapIteration { Iteration = history.Count + 1, PerimeterPass = perimeterPass, Energy = energy, MaxDisplacement = movement * mesh.Scale, LinearResidual = residual };
                    history.Add(sample); progress?.Invoke(sample);
                    token.ThrowIfCancellationRequested();
                    bool converged = movement <= options.PositionTolerance && Math.Abs(previous - energy) <= options.RelativeEnergyTolerance * Math.Max(previous, 1e-16) + 1e-14;
                    if (converged) return true;
                    previous = energy;
                }
                return false;
            }
        }
    }
}
