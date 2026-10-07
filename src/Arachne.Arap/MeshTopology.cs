using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Arachne.Arap
{
    internal sealed class MeshTopology
    {
        internal readonly D3[] V;
        internal readonly int[] F;
        internal readonly D3 Origin;
        internal readonly double Scale;
        internal readonly List<int[]> Components = new List<int[]>();
        internal readonly List<int[]> Boundaries = new List<int[]>();
        internal readonly Dictionary<int, double>[] Stiffness;
        internal readonly List<Triangle> Triangles = new List<Triangle>();
        internal readonly List<int>[] Neighbours;
        internal readonly int[] Component;
        private sealed class Edge { internal int A, B, Face, Count = 1; }
        internal sealed class Triangle
        {
            internal int[] Id;
            internal D3[] Local;
            // Edges are 0->1, 1->2, 2->0. Each weight is half the opposite cotangent.
            internal double[] Weight;
        }
        internal MeshTopology(ArapMesh mesh, CancellationToken token)
        {
            if (mesh == null || mesh.VertexCount < 3 || mesh.Faces.Length < 3 || mesh.Faces.Length % 3 != 0) Invalid("A nonempty triangular mesh is required.");
            F = mesh.Faces; var input = mesh.Points;
            D3 min = input[0], max = input[0];
            foreach (D3 p in input)
            {
                if (!p.IsFinite) Invalid("Mesh coordinates must be finite.");
                for (int j = 0; j < 3; j++) { min[j] = Math.Min(min[j], p[j]); max[j] = Math.Max(max[j], p[j]); }
            }
            D3 span = max - min; Scale = Math.Max(span.X, Math.Max(span.Y, span.Z)); Origin = min + span * 0.5;
            if (!D3.Finite(Scale) || Scale <= 0 || !Origin.IsFinite) Invalid("Mesh extent is zero or outside numerical range.");
            V = input.Select(p => (p - Origin) / Scale).ToArray();
            Neighbours = new List<int>[V.Length]; var incident = new List<int>[V.Length];
            for (int i = 0; i < V.Length; i++) { Neighbours[i] = new List<int>(); incident[i] = new List<int>(); }
            Stiffness = SparseSystem.Rows(V.Length);
            var edges = new Dictionary<long, Edge>(); var uniqueFaces = new HashSet<string>();
            var faceNeighbours = new List<int>[F.Length / 3];
            for (int i = 0; i < faceNeighbours.Length; i++) faceNeighbours[i] = new List<int>();
            for (int t = 0; t < F.Length / 3; t++)
            {
                token.ThrowIfCancellationRequested();
                int[] ids = { F[3 * t], F[3 * t + 1], F[3 * t + 2] };
                if (ids.Any(i => i < 0 || i >= V.Length) || ids.Distinct().Count() != 3) Invalid("Invalid triangle indices at face " + t + ".");
                if (!uniqueFaces.Add(string.Join(",", ids.OrderBy(i => i)))) Invalid("Duplicate triangle at face " + t + ".");
                D3 e = V[ids[1]] - V[ids[0]], f = V[ids[2]] - V[ids[0]];
                double length = e.Norm, area2 = D3.Cross(e, f).Norm;
                double maxEdge2 = Math.Max(e.Norm2, Math.Max(f.Norm2, (f - e).Norm2));
                if (area2 <= 1e-12 * maxEdge2 || area2 < 1e-28) Invalid("Degenerate or excessively thin triangle at face " + t + ".");
                var triangle = new Triangle { Id = ids, Local = new[] { new D3(), new D3(length, 0), new D3(D3.Dot(e, f) / length, area2 / length) }, Weight = new double[3] };
                for (int j = 0; j < 3; j++)
                {
                    int a = ids[j], b = ids[(j + 1) % 3], c = ids[(j + 2) % 3];
                    triangle.Weight[j] = 0.5 * D3.Dot(V[a] - V[c], V[b] - V[c]) / area2;
                    SparseSystem.AddEdge(Stiffness, a, b, triangle.Weight[j]); incident[a].Add(t);
                    long key = Key(a, b);
                    if (edges.TryGetValue(key, out Edge edge))
                    {
                        if (++edge.Count > 2) Invalid("Non-manifold edge " + a + "-" + b + ".");
                        if (edge.A != b) Invalid("Inconsistent face orientation across edge " + a + "-" + b + ".");
                        faceNeighbours[t].Add(edge.Face); faceNeighbours[edge.Face].Add(t);
                    }
                    else { edges.Add(key, new Edge { A = a, B = b, Face = t }); Neighbours[a].Add(b); Neighbours[b].Add(a); }
                }
                Triangles.Add(triangle);
            }
            var next = new Dictionary<int, int>(); var incoming = new HashSet<int>();
            foreach (Edge edge in edges.Values.Where(e => e.Count == 1))
            {
                if (next.ContainsKey(edge.A) || !incoming.Add(edge.B)) Invalid("Non-manifold boundary vertex.");
                next.Add(edge.A, edge.B);
            }
            for (int i = 0; i < V.Length; i++)
            {
                if (incident[i].Count == 0) Invalid("Unused mesh vertex " + i + ".");
                // A vertex may not join separate triangle fans, even in closed meshes.
                var fan = new HashSet<int>(incident[i]); var seen = new HashSet<int>(); var pending = new Stack<int>(); pending.Push(incident[i][0]);
                while (pending.Count > 0) { int t = pending.Pop(); if (!seen.Add(t)) continue; foreach (int adjacent in faceNeighbours[t]) if (fan.Contains(adjacent)) pending.Push(adjacent); }
                if (seen.Count != fan.Count) Invalid("Non-manifold triangle fan at vertex " + i + ".");
            }
            var visitedBoundary = new HashSet<int>();
            foreach (int first in next.Keys.OrderBy(i => i))
            {
                if (visitedBoundary.Contains(first)) continue;
                var loop = new List<int>(); int current = first;
                do { if (!visitedBoundary.Add(current) || !next.ContainsKey(current)) Invalid("Broken boundary loop."); loop.Add(current); current = next[current]; } while (current != first);
                Boundaries.Add(loop.ToArray());
            }
            Component = Enumerable.Repeat(-1, V.Length).ToArray();
            for (int first = 0; first < V.Length; first++)
            {
                if (Component[first] >= 0) continue;
                var vertices = new List<int>(); var pending = new Stack<int>(); pending.Push(first); Component[first] = Components.Count;
                while (pending.Count > 0) { int v = pending.Pop(); vertices.Add(v); foreach (int j in Neighbours[v]) if (Component[j] < 0) { Component[j] = Components.Count; pending.Push(j); } }
                Components.Add(vertices.OrderBy(i => i).ToArray());
            }
        }
        private static long Key(int a, int b) => ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
        private static void Invalid(string message) => throw new ArapFailure(ArapStatus.InvalidInput, message);
        internal void RequirePlanarTopology()
        {
            for (int c = 0; c < Components.Count; c++)
            {
                int loops = Boundaries.Count(b => Component[b[0]] == c);
                int edges = Components[c].Sum(v => Neighbours[v].Count) / 2;
                int faces = Triangles.Count(t => Component[t.Id[0]] == c);
                if (loops == 0 || Components[c].Length - edges + faces != 2 - loops)
                    Invalid("Flatten/Unwrap requires an open genus-zero panel. Cut closed surfaces or handles before developing them.");
            }
        }
        internal D3[] Initial2D(ArapOptions options, CancellationToken token)
        {
            RequirePlanarTopology(); var uv = new D3[V.Length];
            for (int c = 0; c < Components.Count; c++)
            {
                D3 center = new D3(), areaNormal = new D3(); var cov = new double[3, 3];
                foreach (int i in Components[c]) center += V[i]; center /= Components[c].Length;
                foreach (int i in Components[c]) ManagedAlgebra.Outer(cov, V[i] - center, V[i] - center, 1);
                var eigen = ManagedAlgebra.Eigenvectors(cov, out var values); int smallest = 0;
                for (int j = 1; j < 3; j++) if (values[j] < values[smallest]) smallest = j;
                foreach (var t in Triangles.Where(t => Component[t.Id[0]] == c)) areaNormal += D3.Cross(V[t.Id[1]] - V[t.Id[0]], V[t.Id[2]] - V[t.Id[0]]);
                D3 normal = new D3(eigen[0, smallest], eigen[1, smallest], eigen[2, smallest]);
                if (D3.Dot(normal, areaNormal) < 0) normal *= -1;
                D3 axis = Math.Abs(normal.X) < 0.9 ? new D3(1, 0, 0) : new D3(0, 1, 0);
                D3 x = axis - normal * D3.Dot(axis, normal); x /= x.Norm; D3 y = D3.Cross(normal, x);
                foreach (int i in Components[c]) uv[i] = new D3(D3.Dot(V[i], x), D3.Dot(V[i], y));
                if (!PositiveFaces(uv, c)) HarmonicDisk(uv, c, options, token);
            }
            return uv;
        }
        private void HarmonicDisk(D3[] uv, int component, ArapOptions options, CancellationToken token)
        {
            var loops = Boundaries.Where(b => Component[b[0]] == component).ToArray();
            if (loops.Length != 1) throw new ArapFailure(ArapStatus.InvalidParameterization, "Projection folds a panel with holes. Split it into disk-like panels before unwrapping.");
            int[] boundary = loops[0]; double total = 0;
            for (int i = 0; i < boundary.Length; i++) total += (V[boundary[(i + 1) % boundary.Length]] - V[boundary[i]]).Norm;
            double distance = 0; var pins = new bool[V.Length]; var rows = SparseSystem.Rows(V.Length);
            for (int i = 0; i < V.Length; i++) pins[i] = Component[i] != component;
            for (int i = 0; i < boundary.Length; i++)
            {
                double theta = 2 * Math.PI * distance / total;
                uv[boundary[i]] = new D3(Math.Cos(theta), Math.Sin(theta)) * (total / (2 * Math.PI)); pins[boundary[i]] = true;
                distance += (V[boundary[(i + 1) % boundary.Length]] - V[boundary[i]]).Norm;
            }
            foreach (int i in Components[component]) foreach (int j in Neighbours[i]) if (j > i) SparseSystem.AddEdge(rows, i, j, 1);
            var system = new SparseSystem(rows, pins);
            for (int d = 0; d < 2; d++) { var x = uv.Select(p => p[d]).ToArray(); system.Solve(new double[V.Length], x, options, token); foreach (int i in Components[component]) uv[i][d] = x[i]; }
            if (!PositiveFaces(uv, component)) throw new ArapFailure(ArapStatus.InvalidParameterization, "Cannot initialize a nondegenerate planar parameterization.");
        }
        internal bool PositiveFaces(D3[] uv, int component = -1)
        {
            foreach (var t in Triangles)
            {
                if (component >= 0 && Component[t.Id[0]] != component) continue;
                D3 a = uv[t.Id[1]] - uv[t.Id[0]], b = uv[t.Id[2]] - uv[t.Id[0]];
                if (!a.IsFinite || !b.IsFinite || a.X * b.Y - a.Y * b.X <= 1e-12 * (t.Local[1].X * t.Local[2].Y)) return false;
            }
            return true;
        }
    }
}
