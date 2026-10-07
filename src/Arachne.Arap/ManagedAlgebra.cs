using System;
using System.Collections.Generic;
using System.Threading;

namespace Arachne.Arap
{
    internal static class ManagedAlgebra
    {
        // Jacobi eigensolver for the 3x3 PCA and 4x4 quaternion matrices only.
        // No global numerical provider and no native linear-algebra dependency.
        internal static double[,] Eigenvectors(double[,] source, out double[] values)
        {
            int n = source.GetLength(0);
            var a = (double[,])source.Clone(); var v = new double[n, n];
            double scale = 0;
            for (int i = 0; i < n; i++) { v[i, i] = 1; for (int j = 0; j < n; j++) scale = Math.Max(scale, Math.Abs(a[i, j])); }
            if (scale > 0) for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) a[i, j] /= scale;
            for (int sweep = 0; sweep < 80; sweep++)
            {
                int p = 0, q = 1; double largest = 0;
                for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)
                    if (Math.Abs(a[i, j]) > largest) { largest = Math.Abs(a[i, j]); p = i; q = j; }
                if (largest < 2e-15) break;
                double angle = 0.5 * Math.Atan2(2 * a[p, q], a[q, q] - a[p, p]);
                double c = Math.Cos(angle), s = Math.Sin(angle);
                double pp = a[p, p], qq = a[q, q], pq = a[p, q];
                a[p, p] = c * c * pp - 2 * s * c * pq + s * s * qq;
                a[q, q] = s * s * pp + 2 * s * c * pq + c * c * qq;
                a[p, q] = a[q, p] = 0;
                for (int k = 0; k < n; k++)
                {
                    if (k != p && k != q)
                    {
                        double kp = a[k, p], kq = a[k, q];
                        a[k, p] = a[p, k] = c * kp - s * kq;
                        a[k, q] = a[q, k] = s * kp + c * kq;
                    }
                    double vp = v[k, p], vq = v[k, q];
                    v[k, p] = c * vp - s * vq; v[k, q] = s * vp + c * vq;
                }
            }
            values = new double[n]; for (int i = 0; i < n; i++) values[i] = a[i, i] * scale;
            return v;
        }

        // H = sum(weight * restEdge * currentEdge^T). The dominant quaternion
        // maximizes sum(currentEdge dot R * restEdge), enforcing det(R)=+1.
        internal static double[,] Rotation(double[,] h, int dimension)
        {
            var r = new double[3, 3]; r[2, 2] = 1;
            if (dimension == 2)
            {
                double a = h[0, 0] + h[1, 1], b = h[0, 1] - h[1, 0];
                double length = Math.Sqrt(a * a + b * b);
                double c = length > 1e-30 ? a / length : 1, s = length > 1e-30 ? b / length : 0;
                r[0, 0] = r[1, 1] = c; r[0, 1] = -s; r[1, 0] = s; return r;
            }
            double tr = h[0, 0] + h[1, 1] + h[2, 2];
            var k = new double[4, 4]; k[0, 0] = tr;
            k[0, 1] = k[1, 0] = h[1, 2] - h[2, 1];
            k[0, 2] = k[2, 0] = h[2, 0] - h[0, 2];
            k[0, 3] = k[3, 0] = h[0, 1] - h[1, 0];
            for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) k[i + 1, j + 1] = h[i, j] + h[j, i] - (i == j ? tr : 0);
            var eigen = Eigenvectors(k, out var values); int best = 0;
            for (int i = 1; i < 4; i++) if (values[i] > values[best]) best = i;
            double w = eigen[0, best], x = eigen[1, best], y = eigen[2, best], z = eigen[3, best];
            r[0, 0] = 1 - 2 * (y * y + z * z); r[0, 1] = 2 * (x * y - z * w); r[0, 2] = 2 * (x * z + y * w);
            r[1, 0] = 2 * (x * y + z * w); r[1, 1] = 1 - 2 * (x * x + z * z); r[1, 2] = 2 * (y * z - x * w);
            r[2, 0] = 2 * (x * z - y * w); r[2, 1] = 2 * (y * z + x * w); r[2, 2] = 1 - 2 * (x * x + y * y);
            return r;
        }
        internal static D3 Apply(double[,] r, D3 p) => new D3(
            r[0, 0] * p.X + r[0, 1] * p.Y + r[0, 2] * p.Z,
            r[1, 0] * p.X + r[1, 1] * p.Y + r[1, 2] * p.Z,
            r[2, 0] * p.X + r[2, 1] * p.Y + r[2, 2] * p.Z);
        internal static void Outer(double[,] h, D3 p, D3 q, double weight)
        { for (int i = 0; i < 3; i++) for (int j = 0; j < 3; j++) h[i, j] += weight * p[i] * q[j]; }
    }

    /// <summary>Symmetric sparse stiffness matrix, Dirichlet elimination and Jacobi-preconditioned CG.
    /// Storage is proportional to mesh edges. The true residual is checked before accepting a solve.</summary>
    internal sealed class SparseSystem
    {
        private readonly Dictionary<int, double>[] rows;
        private readonly bool[] fixedVertex;
        private readonly double[] diagonal;
        internal SparseSystem(Dictionary<int, double>[] source, bool[] pins)
        {
            rows = source; fixedVertex = pins; diagonal = new double[rows.Length];
            for (int i = 0; i < rows.Length; i++)
            {
                diagonal[i] = rows[i].TryGetValue(i, out double d) ? d : 0;
                if (!pins[i] && (!D3.Finite(diagonal[i]) || diagonal[i] <= 0))
                    throw new ArapFailure(ArapStatus.NumericalFailure, "Non-positive stiffness diagonal at vertex " + i + ".");
            }
        }
        internal static void AddEdge(Dictionary<int, double>[] rows, int a, int b, double weight)
        { Add(rows[a], a, weight); Add(rows[b], b, weight); Add(rows[a], b, -weight); Add(rows[b], a, -weight); }
        private static void Add(Dictionary<int, double> row, int col, double value)
        { row.TryGetValue(col, out double old); row[col] = old + value; }
        internal static Dictionary<int, double>[] Rows(int n)
        { var rows = new Dictionary<int, double>[n]; for (int i = 0; i < n; i++) rows[i] = new Dictionary<int, double>(); return rows; }
        private void Multiply(double[] x, double[] output)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                double sum = 0;
                if (!fixedVertex[i]) foreach (var item in rows[i]) if (!fixedVertex[item.Key]) sum += item.Value * x[item.Key];
                output[i] = sum;
            }
        }
        internal double Solve(double[] load, double[] x, ArapOptions options, CancellationToken token)
        {
            int n = rows.Length, free = 0; var b = (double[])load.Clone();
            var r = new double[n]; var z = new double[n]; var p = new double[n]; var ap = new double[n];
            for (int i = 0; i < n; i++)
            {
                if (fixedVertex[i]) { b[i] = 0; continue; }
                free++;
                foreach (var item in rows[i]) if (fixedVertex[item.Key]) b[i] -= item.Value * x[item.Key];
            }
            double bnorm = Math.Sqrt(Dot(b, b)), threshold = options.LinearTolerance * Math.Max(1, bnorm);
            Multiply(x, ap);
            for (int i = 0; i < n; i++) if (!fixedVertex[i]) { r[i] = b[i] - ap[i]; z[i] = p[i] = r[i] / diagonal[i]; }
            double rz = Dot(r, z); int limit = options.MaxLinearIterations > 0 ? options.MaxLinearIterations : Math.Min(20000, Math.Max(200, 2 * free));
            for (int iteration = 0; iteration <= limit; iteration++)
            {
                token.ThrowIfCancellationRequested();
                if (Math.Sqrt(Dot(r, r)) <= threshold)
                {
                    Multiply(x, ap);
                    for (int i = 0; i < n; i++) r[i] = fixedVertex[i] ? 0 : b[i] - ap[i];
                    double actual = Math.Sqrt(Dot(r, r));
                    if (actual <= threshold * 2) return actual / Math.Max(1, bnorm);
                    for (int i = 0; i < n; i++) z[i] = p[i] = fixedVertex[i] ? 0 : r[i] / diagonal[i];
                    rz = Dot(r, z);
                }
                if (iteration == limit) break;
                Multiply(p, ap); double pap = Dot(p, ap);
                if (!D3.Finite(pap) || pap <= 0 || !D3.Finite(rz))
                    throw new ArapFailure(ArapStatus.NumericalFailure, "The constrained stiffness system is singular or ill-conditioned.");
                double alpha = rz / pap;
                for (int i = 0; i < n; i++) if (!fixedVertex[i]) { x[i] += alpha * p[i]; r[i] -= alpha * ap[i]; z[i] = r[i] / diagonal[i]; }
                double next = Dot(r, z), beta = next / rz; rz = next;
                for (int i = 0; i < n; i++) p[i] = z[i] + beta * p[i];
            }
            throw new ArapFailure(ArapStatus.NumericalFailure, "Sparse linear solver reached its iteration limit without satisfying the residual tolerance.");
        }
        private static double Dot(double[] a, double[] b) { double sum = 0; for (int i = 0; i < a.Length; i++) sum += a[i] * b[i]; return sum; }
    }
}
