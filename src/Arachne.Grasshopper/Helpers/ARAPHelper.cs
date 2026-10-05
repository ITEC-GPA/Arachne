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
						Z[i * 2 + 0] = pinsPositions[i].X;
						Z[i * 2 + 1] = pinsPositions[i].Y;
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
						Z[i * 3 + 0] = pinsPositions[i].X;
						Z[i * 3 + 1] = pinsPositions[i].Y;
						Z[i * 3 + 2] = pinsPositions[i].Z;
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
