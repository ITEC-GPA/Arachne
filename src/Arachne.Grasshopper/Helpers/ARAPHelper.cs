using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;


namespace Arachne.Grasshopper.Helpers
{
	internal class ARAPHelper
	{

        // Native 0.9.0 normalizes vertices to 100 but reads pin positions without scaling.
        // Reproduce its scale here; do not change the caller's coordinates or the native binary.
        private static double NativePinScale(Mesh mesh)
        {
            if (mesh == null || !mesh.IsValid || mesh.Faces.Count == 0 || mesh.Faces.QuadCount != 0)
                throw new ArgumentException("ARAP requires a valid triangulated mesh.");
            double min = float.MaxValue, max = 1.1754943508222875e-38; // FLT_MIN in the native implementation
            foreach (var vertex in mesh.Vertices)
            {
                min = Math.Min(min, Math.Min(vertex.X, Math.Min(vertex.Y, vertex.Z)));
                max = Math.Max(max, Math.Max(vertex.X, Math.Max(vertex.Y, vertex.Z)));
            }
            double scale = (max - min) / 100.0;
            if (scale <= 0 || double.IsInfinity(scale) || double.IsNaN(scale))
                throw new ArgumentException("ARAP mesh has zero or invalid coordinate range.");
            return scale;
        }

        private static void ValidatePins(Mesh mesh, List<int> pins, int positionCount)
        {
            if (pins.Count == 0 || pins.Count != positionCount || pins.Distinct().Count() != pins.Count ||
                pins.Any(i => i < 0 || i >= mesh.Vertices.Count))
                throw new ArgumentException("ARAP pins must be distinct valid vertex indices with matching positions.");
        }
		public static void LibVersion()
		{
			IGLWrapper.Version();
		}
		public static void Configure(int arapIter, int perimeterIter, double perimeterTol, double angleTol, double stretchSpeed)
		{
            IGLWrapper.SetConfig(arapIter, perimeterIter, perimeterTol, angleTol, stretchSpeed);
		}

		public static bool Flattening(Mesh input, Mesh output)
		{
            NativePinScale(input); // validates the triangulated mesh before entering native code
			if (input.IsValid)
			{
				if (input.Vertices.Count > 0 && input.Faces.Count > 0)
				{
					int nV = input.Vertices.Count();
					int nT = input.Faces.Count();

					double[] V = new double[nV * 3];
					double[] U = new double[nV * 2];
					int[] F = new int[nT * 3];

					for (int i = 0; i < nV; i++)
					{
						V[i * 3 + 0] = input.Vertices[i].X;
						V[i * 3 + 1] = input.Vertices[i].Y;
						V[i * 3 + 2] = input.Vertices[i].Z;
					}

					for (int i = 0; i < nT; i++)
					{
						F[i * 3 + 0] = input.Faces[i].A;
						F[i * 3 + 1] = input.Faces[i].B;
						F[i * 3 + 2] = input.Faces[i].C;
					}

					bool result = false;

					try
					{
						result = IGLWrapper.MeshFlattening(V, F, nV, nT, U);
                        if (!result) return false;
                        if (U.Any(v => double.IsNaN(v) || double.IsInfinity(v)))
                            throw new InvalidOperationException("Native ARAP returned non-finite coordinates.");
					}
					catch (Exception ex) { throw new InvalidOperationException("Native ARAP calculation failed.", ex); }

					for (int i = 0; i < nV; i++)
					{
						output.Vertices.SetVertex(i, U[i * 2 + 0], U[i * 2 + 1], 0);
					}

					return result;
				}
			}

			return false;
		}

		public static void Unwrap(Mesh input, Mesh output, List<int> pins, List<Point2d> pinsPositions)
		{
            double scale = NativePinScale(input);
            ValidatePins(input, pins, pinsPositions.Count);
            if (pinsPositions.Any(p => !p.IsValid)) throw new ArgumentException("Pin coordinates must be finite.");
			if (input.IsValid)
			{
				if (input.Vertices.Count > 0 && input.Faces.Count > 0)
				{
					int nV = input.Vertices.Count();
					int nT = input.Faces.Count();

					double[] V = new double[nV * 3];
					double[] U = new double[nV * 2];
					int[] F = new int[nT * 3];

					for (int i = 0; i < nV; i++)
					{
						V[i * 3 + 0] = input.Vertices[i].X;
						V[i * 3 + 1] = input.Vertices[i].Y;
						V[i * 3 + 2] = input.Vertices[i].Z;
                    }

                    for (int i = 0; i < nT; i++)
					{
						F[i * 3 + 0] = input.Faces[i].A;
						F[i * 3 + 1] = input.Faces[i].B;
						F[i * 3 + 2] = input.Faces[i].C;
					}

					int nP = pins.Count;
					int[] P = new int[nP];
					double[] Z = new double[nP * 2];

					for (int i = 0; i < nP; ++i)
					{
						P[i] = pins[i];
						Z[i * 2 + 0] = pinsPositions[i].X / scale;
						Z[i * 2 + 1] = pinsPositions[i].Y / scale;
					}

					try
					{
                        IGLWrapper.MeshUnwrap(V, F, nV, nT, nP, P, Z, U);
					}
					catch (Exception ex) { throw new InvalidOperationException("Native ARAP calculation failed.", ex); }

					for (int i = 0; i < nV; i++)
					{
						output.Vertices.SetVertex(i, U[i * 2 + 0], U[i * 2 + 1], 0);
					}
				}
			}
		}

		public static void Deform(Mesh input, Mesh output, List<int> pins, List<Point3d> pinsPositions)
		{
            double scale = NativePinScale(input);
            ValidatePins(input, pins, pinsPositions.Count);
            if (pinsPositions.Any(p => !p.IsValid)) throw new ArgumentException("Pin coordinates must be finite.");
			if (input.IsValid)
			{
				if (input.Vertices.Count > 0 && input.Faces.Count > 0)
				{
					int nV = input.Vertices.Count();
					int nT = input.Faces.Count();

					double[] V = new double[nV * 3];
					double[] U = new double[nV * 3];
					int[] F = new int[nT * 3];

					for (int i = 0; i < nV; i++)
					{
						V[i * 3 + 0] = input.Vertices[i].X;
						V[i * 3 + 1] = input.Vertices[i].Y;
						V[i * 3 + 2] = input.Vertices[i].Z;
					}

					for (int i = 0; i < nT; i++)
					{
						F[i * 3 + 0] = input.Faces[i].A;
						F[i * 3 + 1] = input.Faces[i].B;
						F[i * 3 + 2] = input.Faces[i].C;
					}

					int nP = pins.Count;
					int[] P = new int[nP];
					double[] Z = new double[nP * 3];

					for (int i = 0; i < pins.Count; ++i)
					{
						P[i] = pins[i];
						Z[i * 3 + 0] = pinsPositions[i].X / scale;
						Z[i * 3 + 1] = pinsPositions[i].Y / scale;
						Z[i * 3 + 2] = pinsPositions[i].Z / scale;
					}

					try
					{
                        IGLWrapper.MeshDeform(V, F, nV, nT, nP, P, Z, U);
					}
					catch (Exception ex) { throw new InvalidOperationException("Native ARAP calculation failed.", ex); }

					for (int i = 0; i < nV; i++)
					{
						output.Vertices.SetVertex(i, U[i * 3 + 0], U[i * 3 + 1], U[i * 3 + 2]);
					}
				}
			}
		}
	}
}
