// Derived from FeMM 30-5-2024: FeMM.Common/Helpers/CableNetHelper.cs. See docs/PORTING.md.
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Geometry
{
	public static class CableNetHelper
	{
        private static void ValidateInputs(List<Curve> fixedEdges, List<Curve> freeEdges, List<Curve> perimeter, double tolerance, double matching)
        {
            Arachne.Core.Validation.Positive(tolerance, "Tolerance");
            Arachne.Core.Validation.NonNegative(matching, "Matching tolerance");
            if (fixedEdges == null || freeEdges == null || perimeter == null || perimeter.Count == 0)
                throw new ArgumentException("A closed perimeter is required.");
            if (fixedEdges.Count + freeEdges.Count == 0) throw new ArgumentException("Boundary curves are required.");
            foreach (var list in new[] { fixedEdges, freeEdges, perimeter })
                foreach (var curve in list)
                    if (curve == null || !curve.IsValid) throw new ArgumentException("Invalid boundary curve.");
        }
		struct NetPoint
		{
			public Point3d local;       // Local coords of the point
			public Point3d world;       // World coords of the point
			public bool isins;          // Is inside perimeter
			public double warpOrder;    // Ordering in warp space
			public double weftOrder;    // Ordering in weft space
			public bool isborder;       // Point comes from border
			public bool restrain;       // Is a restrained point
		}

		/// <summary>
		/// Match borders and cables. 
		/// Extremes of the curves are forced to the exact same point, and where the match is made inside the curve, that curve is splitted.
		/// </summary>
		/// <param name="fixBorders">Fixed borders of the net</param>
		/// <param name="freeBorders">Free borders of the net</param>
		/// <param name="internalCable">Internal cables of the net</param>
		/// <param name="tol">Tolerance used to match</param>
		private static void MatchCables(ref List<Curve> fixBorders, ref List<Curve> freeBorders, ref List<Curve> internalCable, double tol)
		{

			// Copy lists to allow use in local functions
			var fix = new List<Curve>(fixBorders);
			var free = new List<Curve>(freeBorders);
			var cab = new List<Curve>(internalCable);

			var nfix = fix.Count;
			var nfree = free.Count;
			var ncab = cab.Count;

			// Total cable count
			int cableCount = fixBorders.Count + freeBorders.Count + internalCable.Count;

			// Flags to check if start and end of each cable is matched or not
			bool[] matchCheck = new bool[cableCount * 2];
			for (int i = 0; i < matchCheck.Length; ++i)
			{
				matchCheck[i] = false;
			}

			#region Local Functions

			// Retrieve curve from the correct list using a single progressive index (fix -> free -> cables)
			Curve Getcurve(int i) => i < nfix ? fix[i] : i < nfix + nfree ? free[i - nfix] : cab[i - nfix - nfree];

			// Set curve in the correct list using a single progressive index (fix -> free -> cables)
			void Setcurve(int i, Curve c)
			{
				if (i < nfix) { fix[i] = c; }
				else if (i < nfix + nfree) { free[i - nfix] = c; }
				else { cab[i - nfix - nfree] = c; }
			}

			// Match two extremes from two curves
			void Match(int i, bool si, int j, bool sj)
			{
				var ci = Getcurve(i);
				var cj = Getcurve(j);

				var pti = si ? ci.PointAtStart : ci.PointAtEnd;
				var ptj = sj ? cj.PointAtStart : cj.PointAtEnd;

				Point3d pt;

				bool bothFix = i < nfix && j < nfix;
				bool bothFree = (i >= nfix && j >= nfix) && (i < nfree && j < nfree);
				bool bothCable = i >= nfree && j >= nfree;

				if (bothFix || bothFree || bothCable)
				{
					// If the two curves have the same role, chose the middle point
					pt = (pti + ptj) * 0.5;
				}
				else
				{
					// Otherwise, move the point of the less important curve to the other
					if (i < j) { pt = pti; }
					else { pt = ptj; }
				}

				// Set the point and update the curves
				if (si) { ci.SetStartPoint(pt); } else { ci.SetEndPoint(pt); }
				if (sj) { cj.SetStartPoint(pt); } else { cj.SetEndPoint(pt); }

				Setcurve(i, ci);
				Setcurve(j, cj);

				// Do not consider it welded if it is a cable
				if (j < nfix + nfree)
				{
					matchCheck[si ? i * 2 + 0 : i * 2 + 1] = true;
					matchCheck[sj ? j * 2 + 0 : j * 2 + 1] = true;
				}
			}
			#endregion

			// First of all, match the extremes of the curves
			for (int i = 0; i < cableCount; ++i)
			{
				if (matchCheck[i * 2] && matchCheck[i * 2 + 1])
				{
					continue;
				}

				var curveA = Getcurve(i);
				for (int j = 0; j < cableCount; ++j)
				{
					if (i == j) { continue; }
					var curveB = Getcurve(j);

					// If start not matched
					if (!matchCheck[i * 2])
					{
						if (curveA.PointAtStart.DistanceTo(curveB.PointAtStart) < tol)
						{
							Match(i, true, j, true); // Start-Start
						}
						if (curveA.PointAtStart.DistanceTo(curveB.PointAtEnd) < tol)
						{
							Match(i, true, j, false); // Start-End
						}
					}
					// If end not matched
					if (!matchCheck[i * 2 + 1])
					{
						if (curveA.PointAtEnd.DistanceTo(curveB.PointAtStart) < tol)
						{
							Match(i, false, j, true); // End-Start
						}
						if (curveA.PointAtEnd.DistanceTo(curveB.PointAtEnd) < tol)
						{
							Match(i, false, j, false); // End-End
						}
					}
				}
			}

			// Key: curves idex  Value: list of params where to split the curve
			var splits = new Dictionary<int, List<double>>();

			// Match remaining starts/ends and cables on curves, splitting where they match
			for (int i = 0; i < cableCount; ++i)
			{
				var curveA = Getcurve(i);
				if (matchCheck[i * 2] && matchCheck[i * 2 + 1])
				{
					continue;
				}

				for (int j = 0; j < cableCount; ++j)
				{
					if (i == j) { continue; }
					var curveB = Getcurve(j);
					double t;

					if (!matchCheck[i * 2])
					{
						// Match start with closest poit
						if (curveB.ClosestPoint(curveA.PointAtStart, out t) && curveA.PointAtStart.DistanceTo(curveB.PointAt(t)) < tol)
						{
							curveA.SetStartPoint(curveB.PointAt(t));
							Setcurve(i, curveA);
							matchCheck[i * 2] = true;
							if (!splits.ContainsKey(j)) { splits.Add(j, new List<double>()); }
							if (!splits[j].Contains(t)) { splits[j].Add(t); }
						}
					}
					if (!matchCheck[i * 2 + 1])
					{
						// Match end with closest poit
						if (curveB.ClosestPoint(curveA.PointAtEnd, out t) && curveA.PointAtEnd.DistanceTo(curveB.PointAt(t)) < tol)
						{
							curveA.SetEndPoint(curveB.PointAt(t));
							Setcurve(i, curveA);
							matchCheck[i * 2 + 1] = true;
							if (!splits.ContainsKey(j)) { splits.Add(j, new List<double>()); }
							if (!splits[j].Contains(t)) { splits[j].Add(t); }
						}
					}
				}
			}

			// Perform splits
			foreach (var entry in splits)
			{
				// Note: functions based on progressive index can't be used while updating list sizes
				// Split a curve in all the pieces, then change the origial curve with the first piece
				// and add all remaining pieces at the end of the list.

				// Fix borders
				if (entry.Key < fixBorders.Count)
				{
					var crv = fix[entry.Key];
					var crvs = crv.Split(entry.Value);

					fix[entry.Key] = crvs[0];
					for (int i = 1; i < crvs.Length; ++i)
					{
						fix.Add(crvs[i]);
					}
				}
				// Free borders
				else if (entry.Key < fixBorders.Count + freeBorders.Count)
				{
					var crv = free[entry.Key - fixBorders.Count];
					var crvs = crv.Split(entry.Value);

					free[entry.Key - fixBorders.Count] = crvs[0];
					for (int i = 1; i < crvs.Length; ++i)
					{
						free.Add(crvs[i]);
					}
				}
				// Cables
				else
				{
					var crv = cab[entry.Key - fixBorders.Count - freeBorders.Count];
					var crvs = crv.Split(entry.Value);

					cab[entry.Key - fixBorders.Count - freeBorders.Count] = crvs[0];
					for (int i = 1; i < crvs.Length; ++i)
					{
						cab.Add(crvs[i]);
					}
				}
			}

			// Overwrite original lists
			fixBorders = fix;
			freeBorders = free;
			internalCable = cab;
		}

		/// <summary>
		/// Match borders and cables. 
		/// Extremes of the curves are forced to the exact same point, and where the match is made inside the curve, that curve is splitted.
		/// </summary>
		/// <param name="fixCables">Fixed borders of the net</param>
		/// <param name="freeCables">Free borders of the net</param>
		/// <param name="tol">Tolerance used to match</param>
		private static void MatchCables2(ref List<Curve> fixCables, ref List<Curve> freeCables, double tol)
		{
            double SNAP_TOL = 0.00001;

            // Copy lists to allow use in local functions
            var fix = new List<Curve>(fixCables);
			var free = new List<Curve>(freeCables);

			var nfix = fix.Count;
			var nfree = free.Count;

			// Total cable count
			int cableCount = fixCables.Count + freeCables.Count;

			// Flags to check if start and end of each cable is matched or not
			bool[] matchCheck = new bool[cableCount * 2];
			for (int i = 0; i < matchCheck.Length; ++i)
			{
				matchCheck[i] = false;
			}

			#region Local Functions

			// Retrieve curve from the correct list using a single progressive index (fix -> free)
			Curve Getcurve(int i) => i < nfix ? fix[i] : free[i - nfix];

			// Set curve in the correct list using a single progressive index (fix -> free -> cables)
			void Setcurve(int i, Curve c)
			{
				if (i < nfix) { fix[i] = c; }
				else { free[i - nfix] = c; }
			}

			// Match two extremes from two curves
			void Match(int i, bool si, int j, bool sj)
			{
				var ci = Getcurve(i);
				var cj = Getcurve(j);

				var pti = si ? ci.PointAtStart : ci.PointAtEnd;
				var ptj = sj ? cj.PointAtStart : cj.PointAtEnd;

				Point3d pt;

				bool bothFix = i < nfix && j < nfix;
				bool bothFree = i >= nfix && j >= nfix;

				if (bothFix || bothFree)
				{
					// If the two curves have the same role, chose the middle point
					pt = (pti + ptj) * 0.5;
				}
				else
				{
					// Otherwise, move the point of the less important curve to the other
					if (i < j) { pt = pti; }
					else { pt = ptj; }
				}

				// Set the point and update the curves
				if (si) { ci.SetStartPoint(pt); } else { ci.SetEndPoint(pt); }
				if (sj) { cj.SetStartPoint(pt); } else { cj.SetEndPoint(pt); }

				Setcurve(i, ci);
				Setcurve(j, cj);

				// Do not consider match if both free
				if (!bothFree)
				{
					matchCheck[si ? i * 2 + 0 : i * 2 + 1] = true;
					matchCheck[sj ? j * 2 + 0 : j * 2 + 1] = true;
				}
			}
			#endregion

			// First of all, match the extremes of the curves
			for (int i = 0; i < cableCount; ++i)
			{
				if (matchCheck[i * 2] && matchCheck[i * 2 + 1])
				{
					continue;
				}

				var curveA = Getcurve(i);
				if (curveA.IsClosed) {
					continue;
				}

				for (int j = 0; j < cableCount; ++j)
				{
					if (i == j) { continue; }
					var curveB = Getcurve(j);
                    if (curveB.IsClosed) {
                        continue;
                    }

                    // If start not matched
                    if (!matchCheck[i * 2])
					{
						if (curveA.PointAtStart.DistanceTo(curveB.PointAtStart) < tol)
						{
							Match(i, true, j, true); // Start-Start
						}
						if (curveA.PointAtStart.DistanceTo(curveB.PointAtEnd) < tol)
						{
							Match(i, true, j, false); // Start-End
						}
					}
					// If end not matched
					if (!matchCheck[i * 2 + 1])
					{
						if (curveA.PointAtEnd.DistanceTo(curveB.PointAtStart) < tol)
						{
							Match(i, false, j, true); // End-Start
						}
						if (curveA.PointAtEnd.DistanceTo(curveB.PointAtEnd) < tol)
						{
							Match(i, false, j, false); // End-End
						}
					}
				}
			}

			// Key: curves idex  Value: list of params where to split the curve
			var splits = new Dictionary<int, List<double>>();

			// Match remaining starts/ends and cables on curves, splitting where they match
			for (int i = 0; i < cableCount; ++i)
			{
				var curveA = Getcurve(i);
				if (matchCheck[i * 2] && matchCheck[i * 2 + 1])
				{
					continue;
				}

				for (int j = 0; j < cableCount; ++j)
				{
					if (i == j) { continue; }
					var curveB = Getcurve(j);
					double t;

					if (!matchCheck[i * 2])
					{
						// Match start with closest poit
						if (curveB.ClosestPoint(curveA.PointAtStart, out t) && curveA.PointAtStart.DistanceTo(curveB.PointAt(t)) < tol)
						{
							if (curveB.IsClosed) { 
								// Note: Rhino cannot split a curve very close to start
								// TODO: and if it is close to end? and if it is a polycurve?
								if(t < SNAP_TOL) { t = 0; }
                            }

                            curveA.SetStartPoint(curveB.PointAt(t));
							Setcurve(i, curveA);
							matchCheck[i * 2] = true;
							if (!splits.ContainsKey(j)) { splits.Add(j, new List<double>()); }
							if (!splits[j].Contains(t)) { splits[j].Add(t); }
						}
					}
					if (!matchCheck[i * 2 + 1])
					{
						// Match end with closest poit
						if (curveB.ClosestPoint(curveA.PointAtEnd, out t) && curveA.PointAtEnd.DistanceTo(curveB.PointAt(t)) < tol)
						{
							if (curveB.IsClosed) { 
								// Note: Rhino cannot split a curve very close to start
								// TODO: and if it is close to end? and if it is a polycurve?
								if(t < SNAP_TOL) { t = 0; }
                            }

                            curveA.SetEndPoint(curveB.PointAt(t));
							Setcurve(i, curveA);
							matchCheck[i * 2 + 1] = true;
							if (!splits.ContainsKey(j)) { splits.Add(j, new List<double>()); }
							if (!splits[j].Contains(t)) { splits[j].Add(t); }
						}
					}
				}
			}

			// Perform splits
			foreach (var entry in splits)
			{
				// Note: functions based on progressive index can't be used while updating list sizes
				// Split a curve in all the pieces, then change the origial curve with the first piece
				// and add all remaining pieces at the end of the list.

				// Fix borders
				if (entry.Key < fixCables.Count)
				{
					var crv = fix[entry.Key];
					var crvs = crv.Split(entry.Value);

					fix[entry.Key] = crvs[0];
					for (int i = 1; i < crvs.Length; ++i)
					{
						fix.Add(crvs[i]);
					}
				}
				// Free borders
				else
				{
					var crv = free[entry.Key - fixCables.Count];
					var crvs = crv.Split(entry.Value);

					free[entry.Key - fixCables.Count] = crvs[0];
					for (int i = 1; i < crvs.Length; ++i)
					{
						free.Add(crvs[i]);
					}
				}
			}

			// Overwrite original lists
			fixCables = fix;
			freeCables = free;
		}

		public static Plane MakePlane(Plane refPlane, Point3d originOffset, double rotation)
		{
			Plane outPlane = refPlane;
			outPlane.Origin = originOffset;
			outPlane.Rotate(rotation, outPlane.Normal);
			return outPlane;
		}

		[Obsolete]
		/// <summary>
		/// Create a grid cable net
		/// </summary>
		/// <param name="fixBorders">Fixed borders of the net</param>
		/// <param name="freeBordes">Free borders of the net</param>
		/// <param name="internalCable">Internal cables of the net</param>
		/// <param name="warpStep">Distance between columns of the grid</param>
		/// <param name="weftStep">Distance between rows of the grid</param>
		/// <param name="originInput">Origin point of the grid</param>
		/// <param name="outWarp">Resulting list of columns curves splitted into line segments</param>
		/// <param name="outWeft">Resulting list of row curves splitted into line segments</param>
		/// <param name="outFreeBorder">Resulting list of free borders curves splitted into line segments</param>
		/// <param name="outFixBorder">Resulting list of fixed borders curves splitted into line segments</param>
		/// <param name="outInternalCables">Resulting list of internal cables curves splitted into line segments</param>
		/// <param name="restrain">Resulting list of points on fixed borders</param>
		/// <param name="notRestrain">Resulting list of points on free borders</param>
		/// <param name="internalPoint">Resulting list of points on grid joints and on interal cables</param>
		/// <param name="angleDegree">Rotation angle of the grid in degrees</param>
		/// <param name="tolerance">Tolerance parameter</param>
		/// <returns></returns>
		public static bool CreateCableNet(List<Curve> fixBorders, List<Curve> freeBordes, List<Curve> internalCable, double warpStep, double weftStep, Point2d originInput,
			out List<List<LineCurve>> outWarp, out List<List<LineCurve>> outWeft, out List<List<Curve>> outFreeBorder, out List<List<Curve>> outFixBorder, out List<List<LineCurve>> outInternalCables,
			out List<Point3d> restrain, out List<Point3d> notRestrain, out List<Point3d> internalPoint, double angleDegree = 0, double tolerance = 0.01, double matchingTol = 0)
		{
			if (matchingTol > 0)
			{
				MatchCables(ref fixBorders, ref freeBordes, ref internalCable, matchingTol);
			}

			outWarp = new List<List<LineCurve>>();
			outWeft = new List<List<LineCurve>>();
			outFreeBorder = new List<List<Curve>>();
			outFixBorder = new List<List<Curve>>();
			outInternalCables = new List<List<LineCurve>>();
			restrain = new List<Point3d>();
			notRestrain = new List<Point3d>();
			internalPoint = new List<Point3d>();

			double angle = RhinoMath.ToRadians(angleDegree);
			Point3d origin = new Point3d(originInput.X, originInput.Y, 0);

			Vector3d warpAxis = new Vector3d(Math.Cos(angle), Math.Sin(angle), 0);
			warpAxis.Unitize();
			Vector3d weftAxis = new Vector3d(Math.Cos(angle + Math.PI / 2), Math.Sin(angle + Math.PI / 2), 0);
			weftAxis.Unitize();

			Plane plane = new Plane(origin, warpAxis, weftAxis);
			Transform xform_to = Transform.ChangeBasis(Plane.WorldXY, plane);
			Transform xform_from = Transform.ChangeBasis(plane, Plane.WorldXY);
			origin.Transform(xform_to);

			// Project the polyline on the XY plane
			List<Curve> flattenedBorders = new List<Curve>();

			BoundingBox bbox = BoundingBox.Empty;
			for (int i = 0; i < fixBorders.Count; i++)
			{
				Curve c = fixBorders[i];
				c = Curve.ProjectToPlane(c, Plane.WorldXY);
				c.Transform(xform_to);
				flattenedBorders.Add(c);
				bbox.Union(c.GetBoundingBox(false));
			}

			for (int i = 0; i < freeBordes.Count; i++)
			{
				Curve c = freeBordes[i];
				c = Curve.ProjectToPlane(c, Plane.WorldXY);
				c.Transform(xform_to);
				flattenedBorders.Add(c);
				bbox.Union(c.GetBoundingBox(false));
			}

			Brep[] breps = Brep.CreatePlanarBreps(flattenedBorders, tolerance);
			if (breps == null)
				return false;

			double xStart = bbox.Min.X - bbox.Min.X % warpStep - warpStep + (origin.X % warpStep);
			int xNum = (int)Math.Round(bbox.Diagonal.X / warpStep) + 3;

			double yStart = bbox.Min.Y - bbox.Min.Y % weftStep - weftStep + (origin.Y % weftStep);
			int yNum = (int)Math.Round(bbox.Diagonal.Y / weftStep) + 3;

			Point3d[][] points = new Point3d[xNum][];
			bool[][] insideMap = new bool[xNum][];

			List<Point3d> warpBorderPts = new List<Point3d>();
			List<Point3d> weftBorderPts = new List<Point3d>();

			for (int i = 0; i < xNum; ++i)
			{
				points[i] = new Point3d[yNum];
				insideMap[i] = new bool[yNum];

				double x = xStart + warpStep * i;
				Plane warpPlane = new Plane(new Point3d(x, 0, 0), new Vector3d(1, 0, 0));
				warpPlane.Transform(xform_from);

				CutLinesWithPlane(fixBorders, warpPlane, ref weftBorderPts, xform_to, tolerance);
				CutLinesWithPlane(freeBordes, warpPlane, ref weftBorderPts, xform_to, tolerance);

				for (int j = 0; j < yNum; ++j)
				{
					double y = yStart + weftStep * j;

					if (i == 0)
					{
						Plane weftPlane = new Plane(new Point3d(0, y, 0), new Vector3d(0, 1, 0));
						weftPlane.Transform(xform_from);

						CutLinesWithPlane(fixBorders, weftPlane, ref warpBorderPts, xform_to, tolerance);
						CutLinesWithPlane(freeBordes, weftPlane, ref warpBorderPts, xform_to, tolerance);
					}

					Point3d pp = new Point3d(x, y, 0);
					pp.Transform(xform_from);
					pp.Transform(xform_to);
					points[i][j] = pp;
					insideMap[i][j] = IsPointInsideBreps(breps, points[i][j], tolerance);
				}
			}

			List<List<LineCurve>> warpLines = new List<List<LineCurve>>();
			List<List<LineCurve>> weftLines = new List<List<LineCurve>>();

			for (int j = 0; j < yNum; ++j)
			{
				List<LineCurve> lines = new List<LineCurve>();

				for (int i = 1; i < xNum; ++i)
				{
					if (insideMap[i - 1][j] && insideMap[i][j])
					{
						Line line = new Line(points[i - 1][j], points[i][j]);
						line.Transform(xform_from);
						lines.Add(new LineCurve(line));

						Point3d ptBuffer1 = new Point3d(points[i][j].X, points[i][j].Y, points[i][j].Z);
						Point3d ptBuffer2 = new Point3d(points[i - 1][j].X, points[i - 1][j].Y, points[i - 1][j].Z);
						ptBuffer1.Transform(xform_from);
						ptBuffer2.Transform(xform_from);
						if (!internalPoint.Contains(ptBuffer1))
							internalPoint.Add(ptBuffer1);
						if (!internalPoint.Contains(ptBuffer2))
							internalPoint.Add(ptBuffer2);

						continue;
					}
					if (insideMap[i - 1][j] || insideMap[i][j])
					{
						Point3d inside = insideMap[i][j] ? points[i][j] : points[i - 1][j];
						Point3d outside = insideMap[i][j] ? points[i - 1][j] : points[i][j];
						Point3d center = (inside + outside) / 2.0;

						Point3d pt = warpBorderPts[0];
						double minDist = Math.Sqrt(Math.Pow(center.X - pt.X, 2) + Math.Pow(center.Y - pt.Y, 2));

						for (int k = 1; k < warpBorderPts.Count; ++k)
						{
							Point3d curr = warpBorderPts[k];
							double dist = Math.Sqrt(Math.Pow(center.X - curr.X, 2) + Math.Pow(center.Y - curr.Y, 2));
							if (dist < minDist)
							{
								minDist = dist;
								pt = curr;
							}
						}
						Line line = new Line(inside, pt);
						line.Transform(xform_from);
						lines.Add(new LineCurve(line));
						pt.Transform(xform_from);

						AddToPointList(fixBorders, ref restrain, ref notRestrain, pt, tolerance);
					}
				}
				if (lines.Count > 0)
					warpLines.Add(lines);
			}

			for (int i = 0; i < xNum; ++i)
			{
				List<LineCurve> lines = new List<LineCurve>();

				for (int j = 1; j < yNum; ++j)
				{
					if (insideMap[i][j - 1] && insideMap[i][j])
					{
						Line line = new Line(points[i][j - 1], points[i][j]);
						line.Transform(xform_from);
						lines.Add(new LineCurve(line));

						Point3d ptBuffer1 = new Point3d(points[i][j].X, points[i][j].Y, points[i][j].Z);
						Point3d ptBuffer2 = new Point3d(points[i][j - 1].X, points[i][j - 1].Y, points[i][j - 1].Z);
						ptBuffer1.Transform(xform_from);
						ptBuffer2.Transform(xform_from);
						if (!internalPoint.Contains(ptBuffer1))
							internalPoint.Add(ptBuffer1);
						if (!internalPoint.Contains(ptBuffer2))
							internalPoint.Add(ptBuffer2);

						continue;
					}
					if (insideMap[i][j - 1] || insideMap[i][j])
					{
						Point3d inside = insideMap[i][j] ? points[i][j] : points[i][j - 1];
						Point3d outside = insideMap[i][j] ? points[i][j - 1] : points[i][j];
						Point3d center = (inside + outside) / 2.0;
						Point3d pt = weftBorderPts[0];
						double minDist = Math.Sqrt(Math.Pow(center.X - pt.X, 2) + Math.Pow(center.Y - pt.Y, 2));

						for (int k = 1; k < weftBorderPts.Count; ++k)
						{
							Point3d curr = weftBorderPts[k];
							double dist = Math.Sqrt(Math.Pow(center.X - curr.X, 2) + Math.Pow(center.Y - curr.Y, 2));
							if (dist < minDist)
							{
								minDist = dist;
								pt = curr;
							}
						}
						Line line = new Line(inside, pt);
						line.Transform(xform_from);
						lines.Add(new LineCurve(line));
						pt.Transform(xform_from);

						AddToPointList(fixBorders, ref restrain, ref notRestrain, pt, tolerance);
					}
				}

				if (lines.Count > 0)
					weftLines.Add(lines);
			}

			outInternalCables = ProcessInternalCable(internalCable, warpLines, weftLines, ref internalPoint, tolerance);
			outWarp = warpLines;
			outWeft = weftLines;

			List<Point3d> allPoints = new List<Point3d>(restrain);
			allPoints.AddRange(notRestrain);

			for (int i = 0; i < internalCable.Count; i++)
			{
				if (!allPoints.Contains(internalCable[i].PointAtStart))
					allPoints.Add(internalCable[i].PointAtStart);
				if (!allPoints.Contains(internalCable[i].PointAtEnd))
					allPoints.Add(internalCable[i].PointAtEnd);
			}

			PostProcess(fixBorders, freeBordes, ref outFixBorder, ref outFreeBorder, allPoints, restrain, notRestrain, tolerance);

			return true;
		}

		public static bool CreateCableNet2(List<Curve> fixBorders, List<Curve> freeCables, List<Curve> perimeter, double warpStep, double weftStep, Plane refPlane,
			out List<List<LineCurve>> outWarp, out List<List<LineCurve>> outWeft, out List<List<Curve>> outFreeCables, out List<List<Curve>> outFixBorders,
			out List<Point3d> restrain, out List<Point3d> notRestrain, out List<Point3d> internalPoint, double tolerance, double matchingTol)
		{
            ValidateInputs(fixBorders, freeCables, perimeter, tolerance, matchingTol);
            Arachne.Core.Validation.Positive(warpStep, "Warp step"); Arachne.Core.Validation.Positive(weftStep, "Weft step");
            if (!refPlane.IsValid) throw new ArgumentException("Invalid plane.");
            fixBorders = fixBorders.ConvertAll(c => c.DuplicateCurve()); freeCables = freeCables.ConvertAll(c => c.DuplicateCurve());
			if (matchingTol > 0)
			{
				MatchCables2(ref fixBorders, ref freeCables, matchingTol);
			}

			var toLocal = Transform.ChangeBasis(Plane.WorldXY, refPlane);
			var toWorld = Transform.ChangeBasis(refPlane, Plane.WorldXY);

			outWarp = new List<List<LineCurve>>();
			outWeft = new List<List<LineCurve>>();
			outFreeCables = new List<List<Curve>>();
			outFixBorders = new List<List<Curve>>();
			restrain = new List<Point3d>();
			notRestrain = new List<Point3d>();
			internalPoint = new List<Point3d>();

			// Make a bounding box of the perimeter in 2D space of the plane
			List<Curve> flattenedPerimeter = new List<Curve>();
			BoundingBox bbox = BoundingBox.Empty;
			for (int i = 0; i < perimeter.Count; i++)
			{
				Curve c = perimeter[i];
				c = Curve.ProjectToPlane(c, refPlane);
				flattenedPerimeter.Add(c);
				bbox.Union(c.GetBoundingBox(false));
			}
			Brep[] breps = Brep.CreatePlanarBreps(flattenedPerimeter, tolerance);
			if (breps == null)
				return false;

			Point2d localMin = new Point2d(Double.MaxValue, Double.MaxValue);
			Point2d localMax = new Point2d(-Double.MaxValue, -Double.MaxValue);
			var corners = bbox.GetCorners();
			for (int i = 0; i < 8; ++i)
			{
				Point3d localPt = corners[i];
				localPt.Transform(toLocal);
				if (localPt.X < localMin.X) { localMin.X = localPt.X; }
				if (localPt.X > localMax.X) { localMax.X = localPt.X; }
				if (localPt.Y < localMin.Y) { localMin.Y = localPt.Y; }
				if (localPt.Y > localMax.Y) { localMax.Y = localPt.Y; }
			}

			// Choose the starting point of the grid using the bounding box ad the origin of the plane
			Point2d localStart = new Point2d(
					localMin.X - localMin.X % warpStep,
					localMin.Y - localMin.Y % weftStep
				);

			// Compute the number of warp and weft lines
			int warpNum = (int)Math.Round((localMax.X - localMin.X) / warpStep) + 3;
			int weftNum = (int)Math.Round((localMax.Y - localMin.Y) / weftStep) + 3;

			// Compute the grid points
			var gridPts = new NetPoint[warpNum, weftNum];
			for (int i = 0; i < warpNum; ++i)
			{
				for (int j = 0; j < weftNum; ++j)
				{
					gridPts[i, j].local = new Point3d(localStart.X + warpStep * i, localStart.Y + weftStep * j, 0);
					gridPts[i, j].world = gridPts[i, j].local;
					gridPts[i, j].world.Transform(toWorld);
					gridPts[i, j].warpOrder = gridPts[i, j].local.Y;
					gridPts[i, j].weftOrder = gridPts[i, j].local.X;
					gridPts[i, j].isins = IsPointInsideBreps(breps, gridPts[i, j].world, tolerance);
					gridPts[i, j].isborder = false;
				}
			}

			var warpIntersections = new List<List<NetPoint>>();
			var weftIntersections = new List<List<NetPoint>>();

			// Compute warp intersections with the perimeter
			for (int i = 0; i < warpNum; ++i)
			{
				warpIntersections.Add(new List<NetPoint>());

				var planeOrigin = new Point3d(localStart.X + warpStep * i, localStart.X, 0);
				planeOrigin.Transform(toWorld);
				Plane warpPlane = new Plane(planeOrigin, refPlane.XAxis);

				var refInters = new List<Point3d>();
				CutLinesWithPlane(fixBorders, warpPlane, ref refInters, tolerance);
				var restrainN = refInters.Count;
				CutLinesWithPlane(freeCables, warpPlane, ref refInters, tolerance);

				for (int n = 0; n < refInters.Count; ++n)
				{
					var evPt = new NetPoint();
					evPt.world = refInters[n];
					evPt.local = refInters[n];
					evPt.local.Transform(toLocal);
					evPt.warpOrder = evPt.local.Y;
					evPt.weftOrder = evPt.local.X;
					evPt.isins = true;
					evPt.isborder = true;
					evPt.restrain = n < restrainN;
					warpIntersections[i].Add(evPt);
				}
			}

			// Compute weft intersections with the perimeter
			for (int j = 0; j < weftNum; ++j)
			{
				weftIntersections.Add(new List<NetPoint>());

				var planeOrigin = new Point3d(localStart.X, localStart.Y + weftStep * j, 0);
				planeOrigin.Transform(toWorld);
				Plane weftPlane = new Plane(planeOrigin, refPlane.YAxis);

				var refInters = new List<Point3d>();
				CutLinesWithPlane(fixBorders, weftPlane, ref refInters, tolerance);
				var restrainN = refInters.Count;
				CutLinesWithPlane(freeCables, weftPlane, ref refInters, tolerance);

				for (int n = 0; n < refInters.Count; ++n)
				{
					var evPt = new NetPoint();
					evPt.world = refInters[n];
					evPt.local = refInters[n];
					evPt.local.Transform(toLocal);
					evPt.warpOrder = evPt.local.Y;
					evPt.weftOrder = evPt.local.X;
					evPt.isins = true;
					evPt.isborder = true;
					evPt.restrain = n < restrainN;

					weftIntersections[j].Add(evPt);
				}
			}

			// Match border points
			if (matchingTol > 0)
			{
				// Save a list of corners of the perimeter
				var lineEndings = new List<NetPoint>();
				for (int k = 0; k < fixBorders.Count; ++k)
				{
					var netpt = new NetPoint();
					netpt.isborder = true;
					netpt.isins = true;
					netpt.restrain = true;

					var skipSta = false;
					var skipEnd = false;
					foreach (var pt in lineEndings)
					{
						if (pt.world.DistanceTo(fixBorders[k].PointAtStart) < tolerance) { skipSta = true; }
						if (pt.world.DistanceTo(fixBorders[k].PointAtEnd) < tolerance) { skipEnd = true; }
					}
					if (!skipSta)
					{
						netpt.world = fixBorders[k].PointAtStart;
						netpt.local = fixBorders[k].PointAtStart;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = netpt.local.Y;
						netpt.weftOrder = netpt.local.X;
						lineEndings.Add(netpt);
					}
					if (!skipEnd)
					{
						netpt.world = fixBorders[k].PointAtEnd;
						netpt.local = fixBorders[k].PointAtEnd;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = netpt.local.Y;
						netpt.weftOrder = netpt.local.X;
						lineEndings.Add(netpt);
					}
				}
				for (int k = 0; k < freeCables.Count; ++k)
				{
					var netpt = new NetPoint();
					netpt.isborder = true;
					netpt.isins = true;
					netpt.restrain = false;

					var skipSta = false;
					var skipEnd = false;
					foreach (var pt in lineEndings)
					{
						if (pt.world.DistanceTo(freeCables[k].PointAtStart) < tolerance) { skipSta = true; }
						if (pt.world.DistanceTo(freeCables[k].PointAtEnd) < tolerance) { skipEnd = true; }
					}
					if (!skipSta)
					{
						netpt.world = freeCables[k].PointAtStart;
						netpt.local = freeCables[k].PointAtStart;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = netpt.local.Y;
						netpt.weftOrder = netpt.local.X;
						lineEndings.Add(netpt);
					}
					if (!skipEnd)
					{
						netpt.world = freeCables[k].PointAtEnd;
						netpt.local = freeCables[k].PointAtEnd;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = netpt.local.Y;
						netpt.weftOrder = netpt.local.X;
						lineEndings.Add(netpt);
					}
				}

				// Compare each warp with each weft intersection
				for (int i = 0; i < warpIntersections.Count; ++i)
				{
					for (int ii = 0; ii < warpIntersections[i].Count; ++ii)
					{
						var Wa = warpIntersections[i][ii];
						// Check if it match with a corner
						for (int k = 0; k < lineEndings.Count; ++k)
						{
							if (lineEndings[k].local.DistanceTo(Wa.local) < matchingTol)
							{
								Wa = lineEndings[k];
								warpIntersections[i][ii] = Wa;
								break;
							}
						}

						for (int j = 0; j < weftIntersections.Count; ++j)
						{
							for (int jj = 0; jj < weftIntersections[j].Count; ++jj)
							{
								var We = weftIntersections[j][jj];
								// Check if it match with a corner
								for (int k = 0; k < lineEndings.Count; ++k)
								{
									if (lineEndings[k].local.DistanceTo(We.local) < matchingTol)
									{
										We = lineEndings[k];
										weftIntersections[j][jj] = We;
										break;
									}
								}

								// Check if there is a match between the two points
								if (Wa.local.DistanceTo(We.local) < matchingTol)
								{
									var newPt3D = (Wa.world + We.world) * 0.5;
									var newPt2D = (Wa.local + We.local) * 0.5;
									Wa.world = newPt3D;
									We.world = newPt3D;
									Wa.local = newPt2D;
									We.local = newPt2D;
									Wa.warpOrder = (Wa.warpOrder + We.warpOrder) * 0.5;
									We.warpOrder = Wa.warpOrder;
									Wa.weftOrder = (Wa.weftOrder + We.weftOrder) * 0.5;
									We.weftOrder = Wa.weftOrder;
									Wa.restrain = Wa.restrain || We.restrain;
									We.restrain = Wa.restrain || We.restrain;
									warpIntersections[i][ii] = Wa;
									weftIntersections[j][jj] = We;
								}
							}
						}
					}
				}
			}

			// Add restrained and not restrained points to the correct list
			for (int i = 0; i < warpIntersections.Count; ++i)
			{
				for (int ii = 0; ii < warpIntersections[i].Count; ++ii)
				{
					if (warpIntersections[i][ii].restrain)
					{
						restrain.Add(warpIntersections[i][ii].world);
					}
					else
					{
						notRestrain.Add(warpIntersections[i][ii].world);
					}
				}
			}
			for (int j = 0; j < weftIntersections.Count; ++j)
			{
				for (int jj = 0; jj < weftIntersections[j].Count; ++jj)
				{
					if (weftIntersections[j][jj].restrain)
					{
						restrain.Add(weftIntersections[j][jj].world);
					}
					else
					{
						notRestrain.Add(weftIntersections[j][jj].world);
					}
				}
			}

			// Coincident projected grid/border points must always use the real 3D border.
            // Otherwise equal sorting keys can leave an outside grid point between
            // a boundary intersection and its first interior neighbour, losing a cable.
            double gridMatchTolerance = Math.Max(tolerance, matchingTol);
			if (gridMatchTolerance > 0)
			{
				// Match warp grid points
				for (int i = 0; i < warpNum; ++i)
				{
					for (int j = 0; j < weftNum; ++j)
					{
						for (int t = 0; t < warpIntersections[i].Count; ++t)
						{
							if (Math.Abs(warpIntersections[i][t].local.Y - gridPts[i, j].local.Y) <= gridMatchTolerance)
							{
								gridPts[i, j] = warpIntersections[i][t];
								break;
							}
						}
					}
				}

				// Match weft grid points
				for (int j = 0; j < weftNum; ++j)
				{
					for (int i = 0; i < warpNum; ++i)
					{
						for (int t = 0; t < weftIntersections[j].Count; ++t)
						{
							if (Math.Abs(weftIntersections[j][t].local.X - gridPts[i, j].local.X) <= gridMatchTolerance)
							{
								gridPts[i, j] = weftIntersections[j][t];
								break;
							}
						}
					}
				}
			}

			// Add internal points
			for (int i = 0; i < warpNum; ++i)
			{
				for (int j = 0; j < weftNum; ++j)
				{
					if (gridPts[i, j].isins && !gridPts[i, j].isborder)
					{
						internalPoint.Add(gridPts[i, j].world);
					}
				}
			}

			// Build each warp line
			for (int i = 0; i < warpNum; ++i)
			{
				outWarp.Add(new List<LineCurve>());

				var linePoints = warpIntersections[i];

				for (int j = 0; j < weftNum; ++j)
				{
					linePoints.Add(gridPts[i, j]);
				}
				linePoints.Sort((a, b) => a.warpOrder.CompareTo(b.warpOrder));

				for (int n = 1; n < linePoints.Count; ++n)
				{
					if (linePoints[n - 1].world == linePoints[n].world) {
						continue;
					}

					if (linePoints[n - 1].isins && linePoints[n].isins)
					{
						if (linePoints[n - 1].isborder && linePoints[n].isborder)
						{
							bool discard = false;
							double outT = 0;
							var midPt = (linePoints[n - 1].world + linePoints[n].world) * 0.5;
							for (int k = 0; k < fixBorders.Count && !discard; ++k) {
								if (fixBorders[k].ClosestPoint(midPt, out outT, tolerance)) { 
									discard = true;
								}
							}
                            for (int k = 0; k < freeCables.Count && !discard; ++k){
                                if (freeCables[k].ClosestPoint(midPt, out outT, tolerance)){
                                    discard = true;
                                }
                            }
							if (!discard) { 
								outWarp[i].Add(new LineCurve(linePoints[n - 1].world, linePoints[n].world));
                            }
                        }
						else
						{
							outWarp[i].Add(new LineCurve(linePoints[n - 1].world, linePoints[n].world));
						}
					}
				}
			}


			// Build each weft line
			for (int j = 0; j < weftNum; ++j)
			{
				outWeft.Add(new List<LineCurve>());

				var linePoints = weftIntersections[j];

				for (int i = 0; i < warpNum; ++i)
				{
					linePoints.Add(gridPts[i, j]);
				}
				linePoints.Sort((a, b) => a.weftOrder.CompareTo(b.weftOrder));

				for (int n = 1; n < linePoints.Count; ++n)
				{
                    if (linePoints[n - 1].world == linePoints[n].world)
                    {
                        continue;
                    }

                    if (linePoints[n - 1].isins && linePoints[n].isins)
                    {
                        if (linePoints[n - 1].isborder && linePoints[n].isborder)
                        {
                            bool discard = false;
                            double outT = 0;
                            var midPt = (linePoints[n - 1].world + linePoints[n].world) * 0.5;
                            for (int k = 0; k < fixBorders.Count && !discard; ++k)
                            {
                                if (fixBorders[k].ClosestPoint(midPt, out outT, tolerance))
                                {
                                    discard = true;
                                }
                            }
                            for (int k = 0; k < freeCables.Count && !discard; ++k)
                            {
                                if (freeCables[k].ClosestPoint(midPt, out outT, tolerance))
                                {
                                    discard = true;
                                }
                            }
                            if (!discard)
                            {
                                outWeft[j].Add(new LineCurve(linePoints[n - 1].world, linePoints[n].world));
                            }
                        }
                        else
                        {
                            outWeft[j].Add(new LineCurve(linePoints[n - 1].world, linePoints[n].world));
                        }
                    }
                }
			}

			CleanUnusedPoints(outWeft, outWarp, ref restrain, ref notRestrain);

			foreach (Curve c in fixBorders)
			{
				outFixBorders.Add(new List<Curve> { c });
			}
			foreach (Curve c in freeCables)
			{
				outFreeCables.Add(new List<Curve> { c });
			}
            PostProcess(restrain, notRestrain, ref outFixBorders, ref outFreeCables, tolerance);
			PostProcess(outFixBorders, outFreeCables, ref restrain, ref notRestrain);

			return true;
		}

		[Obsolete]
		public static bool CreateRadialCableNet(List<Curve> fixBorders, List<Curve> freeBordes, List<Curve> internalCable, int radialSubd, double radialStep, Point2d originInput,
			out List<List<LineCurve>> outWarp, out List<List<LineCurve>> outWeft, out List<List<Curve>> outFreeBorder, out List<List<Curve>> outFixBorder, out List<List<LineCurve>> outInternalCables,
			out List<Point3d> restrain, out List<Point3d> notRestrain, out List<Point3d> internalPoint, double angleDegree = 0, double tolerance = 0.01, double matchingTol = 0)
		{
			if (matchingTol > 0)
			{
				MatchCables(ref fixBorders, ref freeBordes, ref internalCable, matchingTol);
			}

			outWarp = new List<List<LineCurve>>();
			outWeft = new List<List<LineCurve>>();
			outFreeBorder = new List<List<Curve>>();
			outFixBorder = new List<List<Curve>>();
			outInternalCables = new List<List<LineCurve>>();
			restrain = new List<Point3d>();
			notRestrain = new List<Point3d>();
			internalPoint = new List<Point3d>();

			double angleCS = RhinoMath.ToRadians(angleDegree);
			Point3d origin = new Point3d(originInput.X, originInput.Y, 0);

			Vector3d warpAxis = new Vector3d(Math.Cos(angleCS), Math.Sin(angleCS), 0);
			warpAxis.Unitize();
			Vector3d weftAxis = new Vector3d(Math.Cos(angleCS + Math.PI / 2), Math.Sin(angleCS + Math.PI / 2), 0);
			weftAxis.Unitize();

			Plane plane = new Plane(origin, warpAxis, weftAxis);
			Transform xform_to = Transform.ChangeBasis(Plane.WorldXY, plane);
			Transform xform_from = Transform.ChangeBasis(plane, Plane.WorldXY);
			origin.Transform(xform_from);

			// Project the polyline on the XY plane
			List<Curve> flattenedBorders = new List<Curve>();

			BoundingBox bbox = BoundingBox.Empty;
			for (int i = 0; i < fixBorders.Count; i++)
			{
				Curve c = fixBorders[i];
				c = Curve.ProjectToPlane(c, Plane.WorldXY);
				flattenedBorders.Add(c);
				bbox.Union(c.GetBoundingBox(false));
			}

			for (int i = 0; i < freeBordes.Count; i++)
			{
				Curve c = freeBordes[i];
				c = Curve.ProjectToPlane(c, Plane.WorldXY);
				flattenedBorders.Add(c);
				bbox.Union(c.GetBoundingBox(false));
			}

			Brep[] breps = Brep.CreatePlanarBreps(flattenedBorders, tolerance);
			if (breps == null)
				return false;

			double anglularRadialEnd = 2.0 * Math.PI;
			int radialNum = radialSubd + 1;
			double radialIncrement = anglularRadialEnd / (radialSubd);

			double ringEnd = bbox.Diagonal.Length;
			int ringNum = (int)Math.Round(ringEnd / radialStep) + 3;

			Point3d[][] points = new Point3d[radialNum][];
			bool[][] insideMap = new bool[radialNum][];

			List<Point3d> warpBorderPts = new List<Point3d>();
			List<Point3d> weftBorderPts = new List<Point3d>();
			List<Point3d> allBorderPts = new List<Point3d>();

			for (int i = 0; i < radialNum; ++i)
			{
				points[i] = new Point3d[ringNum];
				insideMap[i] = new bool[ringNum];
				double angle = radialIncrement * i;
				double cos = Math.Cos(angle);
				double sin = Math.Sin(angle);

				Plane warpPlane = new Plane(new Point3d(origin.X, origin.Y, 0), new Point3d(origin.X + radialStep * cos, origin.Y + radialStep * sin, 0), new Point3d(origin.X, origin.Y, 1));
				warpPlane.Transform(xform_to);

				if (i < radialNum - 1)
				{
					CutLinesWithPlane(fixBorders, warpPlane, ref weftBorderPts, tolerance);
					CutLinesWithPlane(freeBordes, warpPlane, ref weftBorderPts, tolerance);
				}

				for (int j = 0; j < ringNum; ++j)
				{
					if (i < radialNum - 1)
					{
						double r = radialStep * j;
						double x = origin.X + r * cos;
						double y = origin.Y + r * sin;

						Point3d pp = new Point3d(x, y, 0);
						pp.Transform(xform_to);
						points[i][j] = pp;
						insideMap[i][j] = IsPointInsideBreps(breps, points[i][j], tolerance);
					}
					else
					{
						points[i][j] = points[0][j];
						insideMap[i][j] = insideMap[0][j];
					}
				}
			}

			for (int j = 0; j < ringNum; ++j)
			{
				for (int i = 0; i < radialNum - 1; ++i)
				{
					Point3d PointStart = points[i][j];
					Point3d PointEnd = points[i + 1][j];

					LineCurve line2d = new LineCurve(PointStart, PointEnd);
					CutLinesWithLine(fixBorders, line2d, ref warpBorderPts, tolerance);
					CutLinesWithLine(freeBordes, line2d, ref warpBorderPts, tolerance);
				}
			}

			allBorderPts.AddRange(warpBorderPts);
			allBorderPts.AddRange(weftBorderPts);

			List<List<LineCurve>> warpLines = new List<List<LineCurve>>();
			List<List<LineCurve>> weftLines = new List<List<LineCurve>>();

			for (int i = 1; i < radialNum; ++i)
			{
				List<LineCurve> lineList = new List<LineCurve>();

				for (int j = 0; j < ringNum - 1; ++j)
				{
					if (insideMap[i - 1][j] && insideMap[i][j])
					{
						if (points[i - 1][j].DistanceTo(points[i][j]) > tolerance)
						{
							LineCurve line = new LineCurve(new Line(points[i - 1][j], points[i][j]));
							lineList.Add(line);

							if (!internalPoint.Contains(points[i][j]))
								internalPoint.Add(points[i][j]);
							if (!internalPoint.Contains(points[i - 1][j]))
								internalPoint.Add(points[i - 1][j]);

						}
						continue;
					}
					if (insideMap[i - 1][j] || insideMap[i][j])
					{
						Point3d inside = insideMap[i][j] ? points[i][j] : points[i - 1][j];
						Point3d outside = insideMap[i][j] ? points[i - 1][j] : points[i][j];

						Point3d pt = new Point3d();
						Vector3d direction = new Vector3d(new Point3d(outside.X, outside.Y, 0) - new Point3d(inside.X, inside.Y, 0));
						direction.Unitize();
						double angle = Vector3d.VectorAngle(direction, Vector3d.XAxis);
						double minAngle = double.MaxValue;

						for (int k = 0; k < warpBorderPts.Count; ++k)
						{
							Point3d curr = warpBorderPts[k];
							Vector3d directionBuffer = new Vector3d(new Point3d(curr.X, curr.Y, 0) - new Point3d(inside.X, inside.Y, 0));

							directionBuffer.Unitize();
							double angleBuffer = Vector3d.VectorAngle(directionBuffer, Vector3d.XAxis);

							if (Math.Abs(angleBuffer - angle) <= minAngle)
							{
								if (curr.X <= Math.Max(inside.X, outside.X) + tolerance && curr.X >= Math.Min(inside.X, outside.X) - tolerance &&
									curr.Y <= Math.Max(inside.Y, outside.Y) + tolerance && curr.Y >= Math.Min(inside.Y, outside.Y) - tolerance)
								{
									minAngle = Math.Abs(angleBuffer - angle);
									pt = curr;
								}
							}
						}

						LineCurve line = new LineCurve(new Line(inside, pt));
						lineList.Add(line);

						AddToPointList(fixBorders, ref restrain, ref notRestrain, pt, tolerance);
					}
				}

				warpLines.Add(lineList);
			}

			for (int j = 1; j < ringNum; ++j)
			{
				List<LineCurve> lineList = new List<LineCurve>();

				for (int i = 0; i < radialNum - 1; ++i)
				{
					if (insideMap[i][j - 1] && insideMap[i][j])
					{
						if (points[i][j - 1].DistanceTo(points[i][j]) > tolerance)
						{
							LineCurve line = new LineCurve(new Line(points[i][j - 1], points[i][j]));
							lineList.Add(line);

							if (!internalPoint.Contains(points[i][j]))
								internalPoint.Add(points[i][j]);
							if (!internalPoint.Contains(points[i][j - 1]))
								internalPoint.Add(points[i][j - 1]);

						}
						continue;
					}
					if (insideMap[i][j - 1] || insideMap[i][j])
					{
						Point3d inside = insideMap[i][j] ? points[i][j] : points[i][j - 1];
						Point3d outside = insideMap[i][j] ? points[i][j - 1] : points[i][j];

						Point3d pt = new Point3d();
						Vector3d direction = new Vector3d(new Point3d(outside.X, outside.Y, 0) - new Point3d(inside.X, inside.Y, 0));
						direction.Unitize();
						double angle = Vector3d.VectorAngle(direction, Vector3d.XAxis);
						double minAngle = double.MaxValue;

						for (int k = 0; k < weftBorderPts.Count; ++k)
						{
							Point3d curr = weftBorderPts[k];
							Vector3d directionBuffer = new Vector3d(new Point3d(curr.X, curr.Y, 0) - new Point3d(inside.X, inside.Y, 0));
							directionBuffer.Unitize();
							double angleBuffer = Vector3d.VectorAngle(directionBuffer, Vector3d.XAxis);

							if (Math.Abs(angleBuffer - angle) <= minAngle)
							{
								if (curr.X <= Math.Max(inside.X, outside.X) + tolerance && curr.X >= Math.Min(inside.X, outside.X) - tolerance &&
									curr.Y <= Math.Max(inside.Y, outside.Y) + tolerance && curr.Y >= Math.Min(inside.Y, outside.Y) - tolerance)
								{
									minAngle = Math.Abs(angleBuffer - angle);
									pt = curr;
								}
							}
						}

						LineCurve line = new LineCurve(new Line(inside, pt));
						lineList.Add(line);

						AddToPointList(fixBorders, ref restrain, ref notRestrain, pt, tolerance);
					}
				}

				weftLines.Add(lineList);
			}

			outInternalCables = ProcessInternalCable(internalCable, warpLines, weftLines, ref internalPoint, tolerance);
			outWarp = warpLines;
			outWeft = weftLines;

			List<Point3d> allPoints = new List<Point3d>(restrain);
			allPoints.AddRange(notRestrain);

			for (int i = 0; i < internalCable.Count; i++)
			{
				if (!allPoints.Contains(internalCable[i].PointAtStart))
					allPoints.Add(internalCable[i].PointAtStart);
				if (!allPoints.Contains(internalCable[i].PointAtEnd))
					allPoints.Add(internalCable[i].PointAtEnd);
			}

			PostProcess(fixBorders, freeBordes, ref outFixBorder, ref outFreeBorder, allPoints, restrain, notRestrain, tolerance);

			return true;
		}

		public static bool CreateRadialCableNet2(List<Curve> fixBorders, List<Curve> freeBorders, List<Curve> perimeter, int radialSubd, double radialStep, Plane refPlane,
			out List<List<LineCurve>> outWarp, out List<List<LineCurve>> outWeft, out List<List<Curve>> outFreeBorders, out List<List<Curve>> outFixBorders,
			out List<Point3d> restrain, out List<Point3d> notRestrain, out List<Point3d> internalPoint, double tolerance = 0.01, double matchingTol = 0)
		{
			ValidateInputs(fixBorders, freeBorders, perimeter, tolerance, matchingTol);
			if (radialSubd < 3) throw new ArgumentException("Radial divisions must be at least 3.");
			Arachne.Core.Validation.Positive(radialStep, "Radial step");
			if (!refPlane.IsValid) throw new ArgumentException("Invalid plane.");
			fixBorders = fixBorders.ConvertAll(c => c.DuplicateCurve());
			freeBorders = freeBorders.ConvertAll(c => c.DuplicateCurve());
			if (matchingTol > 0)
			{
				MatchCables2(ref fixBorders, ref freeBorders, matchingTol);
			}

			var toLocal = Transform.ChangeBasis(Plane.WorldXY, refPlane);
			var toWorld = Transform.ChangeBasis(refPlane, Plane.WorldXY);

			outWarp = new List<List<LineCurve>>();
			outWeft = new List<List<LineCurve>>();
			outFreeBorders = new List<List<Curve>>();
			outFixBorders = new List<List<Curve>>();
			restrain = new List<Point3d>();
			notRestrain = new List<Point3d>();
			internalPoint = new List<Point3d>();


			// Make a bounding box of the perimeter in 2D space of the plane
			List<Curve> flattenedPerimeter = new List<Curve>();
			BoundingBox bbox = BoundingBox.Empty;
			for (int i = 0; i < perimeter.Count; i++)
			{
				Curve c = perimeter[i];
				c = Curve.ProjectToPlane(c, refPlane);
				flattenedPerimeter.Add(c);
				bbox.Union(c.GetBoundingBox(false));
			}
			Brep[] breps = Brep.CreatePlanarBreps(flattenedPerimeter, tolerance);
			if (breps == null)
				return false;

			Point2d localMin = new Point2d(Double.MaxValue, Double.MaxValue);
			Point2d localMax = new Point2d(-Double.MaxValue, -Double.MaxValue);
			var corners = bbox.GetCorners();
			for (int i = 0; i < 8; ++i)
			{
				Point3d localPt = corners[i];
				localPt.Transform(toLocal);
				if (localPt.X < localMin.X) { localMin.X = localPt.X; }
				if (localPt.X > localMax.X) { localMax.X = localPt.X; }
				if (localPt.Y < localMin.Y) { localMin.Y = localPt.Y; }
				if (localPt.Y > localMax.Y) { localMax.Y = localPt.Y; }
			}

			double angle = 2.0 * Math.PI / (radialSubd);
			double length = Math.Max(localMin.DistanceTo(new Point2d(0, 0)), localMax.DistanceTo(new Point2d(0, 0)));
			length = length - length % radialStep + radialStep;

			int warpNum = (int)radialSubd;
			int weftNum = (int)Math.Round(length / radialStep) + 1;

			// Compute the grid points
			var gridPts = new NetPoint[warpNum, weftNum];
			for (int i = 0; i < warpNum; ++i)
			{
				for (int j = 0; j < weftNum; ++j)
				{
					gridPts[i, j].local = new Point3d(0, radialStep * j, 0);
					gridPts[i, j].local.Transform(Transform.Rotation(angle * i, new Vector3d(0, 0, 1), new Point3d(0, 0, 0)));
					gridPts[i, j].world = gridPts[i, j].local;
					gridPts[i, j].world.Transform(toWorld);
					gridPts[i, j].warpOrder = j * radialStep;
					gridPts[i, j].weftOrder = i * angle;

					gridPts[i, j].isins = IsPointInsideBreps(breps, gridPts[i, j].world, tolerance);
					gridPts[i, j].isborder = false;
				}
			}

			var warpIntersections = new List<List<NetPoint>>();
			var weftIntersections = new List<List<NetPoint>>();

			Polyline pline = new Polyline();
			// Compute warp intersections with the perimeter and build the line
			for (int i = 0; i < warpNum; ++i)
			{
				warpIntersections.Add(new List<NetPoint>());

				pline.Add(gridPts[i, 1].world);

				var planex = refPlane.YAxis;
				planex.Rotate(i * angle, refPlane.Normal);
				var warpPlane = new Plane(refPlane.Origin, planex, refPlane.Normal);

				var refInters = new List<Point3d>();
				CutLinesWithPlane(fixBorders, warpPlane, ref refInters, tolerance);
				int restrainN = refInters.Count;
				CutLinesWithPlane(freeBorders, warpPlane, ref refInters, tolerance);

				for (int n = 0; n < refInters.Count; ++n)
				{
					var dir = refInters[n] - warpPlane.Origin;
					dir.Unitize();
                    var cross = Vector3d.CrossProduct(dir, warpPlane.YAxis);
					if (warpPlane.Normal * cross > 0 || cross.Length < tolerance)
					{
						var evPt = new NetPoint();
						evPt.world = refInters[n];
						evPt.local = refInters[n];
						evPt.local.Transform(toLocal);
						evPt.warpOrder = new Point3d(evPt.local.X, evPt.local.Y, 0).DistanceTo(new Point3d(0, 0, 0));
						var vec = refPlane.ClosestPoint(refInters[n]) - refPlane.Origin;
						var currAngle = Vector3d.VectorAngle(refPlane.YAxis, vec, refPlane.Normal);
						if (currAngle < Math.PI && Vector3d.CrossProduct(refPlane.YAxis, vec) * refPlane.Normal < Math.PI) { currAngle = Math.PI * 2 - currAngle; }
						evPt.weftOrder = currAngle;
						evPt.isins = true;
						evPt.isborder = true;
						evPt.restrain = n < restrainN;
						warpIntersections[i].Add(evPt);
					}
				}
			}

			// close polyline
			pline.Add(gridPts[0, 1].world);
			var topLine = new Polyline(pline);
			var botLine = new Polyline(pline);

			topLine.Transform(Transform.Translation(refPlane.Normal * bbox.Diagonal.Length));
			botLine.Transform(Transform.Translation(refPlane.Normal * -bbox.Diagonal.Length));

			// Compute weft intersections with the perimeter
			// Add an empty list of lines in center node
			weftIntersections.Add(new List<NetPoint>());
			for (int j = 1; j < weftNum; ++j)
			{
				weftIntersections.Add(new List<NetPoint>());

				var currLines = new List<PolylineCurve>(2) { topLine.ToPolylineCurve(), botLine.ToPolylineCurve() };
				currLines[0].Transform(Transform.Scale(refPlane.Origin, j));
				currLines[1].Transform(Transform.Scale(refPlane.Origin, j));

				var loft = Brep.CreateFromLoft(currLines, Point3d.Unset, Point3d.Unset, LoftType.Straight, false)[0];

				var refInters = new List<Point3d>();
				CutLinesWithBrep(fixBorders, loft, ref refInters, tolerance);
				int restrainN = refInters.Count;
				CutLinesWithBrep(freeBorders, loft, ref refInters, tolerance);

				for (int n = 0; n < refInters.Count; ++n)
				{
					var evPt = new NetPoint();
					evPt.world = refInters[n];
					evPt.local = refInters[n];
					evPt.local.Transform(toLocal);
					evPt.warpOrder = new Point3d(evPt.local.X, evPt.local.Y, 0).DistanceTo(new Point3d(0, 0, 0));
					var vec = refPlane.ClosestPoint(refInters[n]) - refPlane.Origin;
					var currAngle = Vector3d.VectorAngle(refPlane.YAxis, vec, refPlane.Normal);
					if (currAngle < Math.PI && Vector3d.CrossProduct(refPlane.YAxis, vec) * refPlane.Normal < Math.PI) { currAngle = Math.PI * 2 - currAngle; }
					evPt.weftOrder = currAngle;
					evPt.isins = true;
					evPt.isborder = true;
					evPt.restrain = n < restrainN;
					weftIntersections[j].Add(evPt);
				}
			}

			// Match border points
			if (matchingTol > 0)
			{
				// Save a list of corners of the perimeter
				var lineEndings = new List<NetPoint>();
				for (int k = 0; k < fixBorders.Count; ++k)
				{
					var netpt = new NetPoint();
					netpt.isborder = true;
					netpt.isins = true;
					netpt.restrain = true;

					var skipSta = false;
					var skipEnd = false;
					foreach (var pt in lineEndings)
					{
						if (pt.world.DistanceTo(fixBorders[k].PointAtStart) < tolerance) { skipSta = true; }
						if (pt.world.DistanceTo(fixBorders[k].PointAtEnd) < tolerance) { skipEnd = true; }
					}
					if (!skipSta)
					{
						netpt.world = fixBorders[k].PointAtStart;
						netpt.local = fixBorders[k].PointAtStart;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = new Point3d(netpt.local.X, netpt.local.Y, 0).DistanceTo(new Point3d(0, 0, 0));
						var vec = refPlane.ClosestPoint(netpt.world) - refPlane.Origin;
						var currAngle = Vector3d.VectorAngle(refPlane.YAxis, vec, refPlane.Normal);
						if (currAngle < Math.PI && Vector3d.CrossProduct(refPlane.YAxis, vec) * refPlane.Normal < Math.PI) { currAngle = Math.PI * 2 - currAngle; }
						netpt.weftOrder = currAngle;
						lineEndings.Add(netpt);
					}
					if (!skipEnd)
					{
						netpt.world = fixBorders[k].PointAtEnd;
						netpt.local = fixBorders[k].PointAtEnd;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = new Point3d(netpt.local.X, netpt.local.Y, 0).DistanceTo(new Point3d(0, 0, 0));
						var vec = refPlane.ClosestPoint(netpt.world) - refPlane.Origin;
						var currAngle = Vector3d.VectorAngle(refPlane.YAxis, vec, refPlane.Normal);
						if (currAngle < Math.PI && Vector3d.CrossProduct(refPlane.YAxis, vec) * refPlane.Normal < Math.PI) { currAngle = Math.PI * 2 - currAngle; }
						netpt.weftOrder = currAngle;
						lineEndings.Add(netpt);
					}
				}
				for (int k = 0; k < freeBorders.Count; ++k)
				{
					var netpt = new NetPoint();
					netpt.isborder = true;
					netpt.isins = true;
					netpt.restrain = false;

					var skipSta = false;
					var skipEnd = false;
					foreach (var pt in lineEndings)
					{
						if (pt.world.DistanceTo(freeBorders[k].PointAtStart) < tolerance) { skipSta = true; }
						if (pt.world.DistanceTo(freeBorders[k].PointAtEnd) < tolerance) { skipEnd = true; }
					}
					if (!skipSta)
					{
						netpt.world = freeBorders[k].PointAtStart;
						netpt.local = freeBorders[k].PointAtStart;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = new Point3d(netpt.local.X, netpt.local.Y, 0).DistanceTo(new Point3d(0, 0, 0));
						var vec = refPlane.ClosestPoint(netpt.world) - refPlane.Origin;
						var currAngle = Vector3d.VectorAngle(refPlane.YAxis, vec, refPlane.Normal);
						if (currAngle < Math.PI && Vector3d.CrossProduct(refPlane.YAxis, vec) * refPlane.Normal < Math.PI) { currAngle = Math.PI * 2 - currAngle; }
						netpt.weftOrder = currAngle;
						lineEndings.Add(netpt);
					}
					if (!skipEnd)
					{
						netpt.world = freeBorders[k].PointAtEnd;
						netpt.local = freeBorders[k].PointAtEnd;
						netpt.local.Transform(toLocal);
						netpt.warpOrder = new Point3d(netpt.local.X, netpt.local.Y, 0).DistanceTo(new Point3d(0, 0, 0));
						var vec = refPlane.ClosestPoint(netpt.world) - refPlane.Origin;
						var currAngle = Vector3d.VectorAngle(refPlane.YAxis, vec, refPlane.Normal);
						if (currAngle < Math.PI && Vector3d.CrossProduct(refPlane.YAxis, vec) * refPlane.Normal < Math.PI) { currAngle = Math.PI * 2 - currAngle; }
						netpt.weftOrder = currAngle;
						lineEndings.Add(netpt);
					}
				}

				// Compare each warp with each weft intersection
				for (int i = 0; i < warpIntersections.Count; ++i)
				{
					for (int ii = 0; ii < warpIntersections[i].Count; ++ii)
					{
						var Wa = warpIntersections[i][ii];
						// Check if it match with a corner
						for (int k = 0; k < lineEndings.Count; ++k)
						{
							if (lineEndings[k].local.DistanceTo(Wa.local) < matchingTol)
							{
								Wa = lineEndings[k];
								warpIntersections[i][ii] = Wa;
								break;
							}
						}

						for (int j = 0; j < weftIntersections.Count; ++j)
						{
							for (int jj = 0; jj < weftIntersections[j].Count; ++jj)
							{
								var We = weftIntersections[j][jj];
								// Check if it match with a corner
								for (int k = 0; k < lineEndings.Count; ++k)
								{
									if (lineEndings[k].local.DistanceTo(We.local) < matchingTol)
									{
										We = lineEndings[k];
										weftIntersections[j][jj] = We;
										break;
									}
								}

								// Check if there is a match between the two points
								if (Wa.local.DistanceTo(We.local) < matchingTol)
								{
									var newPt3D = (Wa.world + We.world) * 0.5;
									var newPt2D = (Wa.local + We.local) * 0.5;
									Wa.world = newPt3D;
									We.world = newPt3D;
									Wa.local = newPt2D;
									We.local = newPt2D;
									Wa.warpOrder = (Wa.warpOrder + We.warpOrder) * 0.5;
									We.warpOrder = Wa.warpOrder;
									Wa.weftOrder = (Wa.weftOrder + We.weftOrder) * 0.5;
									We.weftOrder = Wa.weftOrder;
									Wa.restrain = Wa.restrain || We.restrain;
									We.restrain = Wa.restrain || We.restrain;
									warpIntersections[i][ii] = Wa;
									weftIntersections[j][jj] = We;
								}
							}
						}
					}
				}
			}

			// Add restrained and not restrained points to the correct list
			for (int i = 0; i < warpIntersections.Count; ++i)
			{
				for (int ii = 0; ii < warpIntersections[i].Count; ++ii)
				{
					if (warpIntersections[i][ii].restrain)
					{
						restrain.Add(warpIntersections[i][ii].world);
					}
					else
					{
						notRestrain.Add(warpIntersections[i][ii].world);
					}
				}
			}
			for (int j = 0; j < weftIntersections.Count; ++j)
			{
				for (int jj = 0; jj < weftIntersections[j].Count; ++jj)
				{
					if (weftIntersections[j][jj].restrain)
					{
						restrain.Add(weftIntersections[j][jj].world);
					}
					else
					{
						notRestrain.Add(weftIntersections[j][jj].world);
					}
				}
			}

			// Resolve coincident grid/border points even when optional snapping is off.
            double gridMatchTolerance = Math.Max(tolerance, matchingTol);
			if (gridMatchTolerance > 0)
			{
				// Match warp grid points
				for (int i = 0; i < warpNum; ++i)
				{
					for (int j = 0; j < weftNum; ++j)
					{
						for (int t = 0; t < warpIntersections[i].Count; ++t)
						{
                            var pta = new Point3d(warpIntersections[i][t].local.X, warpIntersections[i][t].local.Y, 0.0);
                            var ptb = new Point3d(gridPts[i, j].local.X, gridPts[i, j].local.Y, 0.0);
                            if (pta.DistanceTo(ptb) <= gridMatchTolerance)
                            {
								gridPts[i, j] = warpIntersections[i][t];
								break;
							}
						}
					}
				}

				// Match weft grid points
				for (int j = 0; j < weftNum; ++j)
				{
					for (int i = 0; i < warpNum; ++i)
					{
						for (int t = 0; t < weftIntersections[j].Count; ++t)
						{
							var pta = new Point3d(weftIntersections[j][t].local.X, weftIntersections[j][t].local.Y, 0.0);
							var ptb = new Point3d(gridPts[i, j].local.X, gridPts[i, j].local.Y, 0.0);
                            if (pta.DistanceTo(ptb) <= gridMatchTolerance)
							{
								gridPts[i, j] = weftIntersections[j][t];
								break;
							}
						}
					}
				}
			}

			// Add internal points
			for (int i = 0; i < warpNum; ++i)
			{
				for (int j = 0; j < weftNum; ++j)
				{
					if (gridPts[i, j].isins && !gridPts[i, j].isborder)
					{
						internalPoint.Add(gridPts[i, j].world);
					}
				}
			}

			// Build each warp line
			for (int i = 0; i < warpNum; ++i)
			{
				outWarp.Add(new List<LineCurve>());

				var linePoints = warpIntersections[i];

				for (int j = 0; j < weftNum; ++j)
				{
					linePoints.Add(gridPts[i, j]);
				}
				linePoints.Sort((a, b) => a.warpOrder.CompareTo(b.warpOrder));

				for (int n = 1; n < linePoints.Count; ++n)
				{
                    if (linePoints[n - 1].world == linePoints[n].world)
                    {
                        continue;
                    }

                    if (linePoints[n - 1].isins && linePoints[n].isins)
                    {
                        if (linePoints[n - 1].isborder && linePoints[n].isborder)
                        {
                            bool discard = false;
                            double outT = 0;
                            var midPt = (linePoints[n - 1].world + linePoints[n].world) * 0.5;
                            for (int k = 0; k < fixBorders.Count && !discard; ++k)
                            {
                                if (fixBorders[k].ClosestPoint(midPt, out outT, tolerance))
                                {
                                    discard = true;
                                }
                            }
                            for (int k = 0; k < freeBorders.Count && !discard; ++k)
                            {
                                if (freeBorders[k].ClosestPoint(midPt, out outT, tolerance))
                                {
                                    discard = true;
                                }
                            }
                            if (!discard)
                            {
                                outWarp[i].Add(new LineCurve(linePoints[n - 1].world, linePoints[n].world));
                            }
                        }
                        else
                        {
                            outWarp[i].Add(new LineCurve(linePoints[n - 1].world, linePoints[n].world));
                        }
                    }
                }
			}

			// Build each weft line
			for (int j = 0; j < weftNum; ++j)
			{
				outWeft.Add(new List<LineCurve>());

				var linePoints = weftIntersections[j];

				for (int i = 0; i < warpNum; ++i)
				{
					linePoints.Add(gridPts[i, j]);
				}
				linePoints.Sort((a, b) => a.weftOrder.CompareTo(b.weftOrder));

				for (int n = 0; n < linePoints.Count; ++n)
				{
					int p = n - 1 >= 0 ? n - 1 : linePoints.Count - 1;
                    if (linePoints[p].world == linePoints[n].world)
                    {
                        continue;
                    }

                    if (linePoints[p].isins && linePoints[n].isins)
					{
						if (linePoints[p].isborder && linePoints[n].isborder)
						{
                            bool discard = false;
                            double outT = 0;
                            var midPt = (linePoints[p].world + linePoints[n].world) * 0.5;
                            for (int k = 0; k < fixBorders.Count && !discard; ++k)
                            {
                                if (fixBorders[k].ClosestPoint(midPt, out outT, tolerance))
                                {
                                    discard = true;
                                }
                            }
                            for (int k = 0; k < freeBorders.Count && !discard; ++k)
                            {
                                if (freeBorders[k].ClosestPoint(midPt, out outT, tolerance))
                                {
                                    discard = true;
                                }
                            }
                            if (!discard)
                            {
                                outWeft[j].Add(new LineCurve(linePoints[p].world, linePoints[n].world));
                            }
                        }
						else
						{
							outWeft[j].Add(new LineCurve(linePoints[p].world, linePoints[n].world));
						}
					}
				}
			}

            CleanUnusedPoints(outWeft, outWarp, ref restrain, ref notRestrain);

            foreach (Curve c in fixBorders)
			{
				outFixBorders.Add(new List<Curve> { c });
			}
			foreach (Curve c in freeBorders)
			{
				outFreeBorders.Add(new List<Curve> { c });
			}
			PostProcess(restrain, notRestrain, ref outFixBorders, ref outFreeBorders, tolerance);
			PostProcess(outFixBorders, outFreeBorders, ref restrain, ref notRestrain);

			return true;
		}

		/// <summary>
		/// Check if a point is contaied inside a list of breps
		/// </summary>
		/// <param name="breps">The list of breps</param>
		/// <param name="testPt">The point to test</param>
		/// <param name="tolerance">Tolerance parameter</param>
		/// <returns></returns>
		private static bool IsPointInsideBreps(Brep[] breps, Point3d testPt, double tolerance)
		{
			for (int b = 0; b < breps.Length; b++)
			{
				Brep brep = breps[b];
				Point3d p = brep.ClosestPoint(testPt);
				for (int i = 0; i < brep.Edges.Count; i++)
				{
					if (brep.Edges[i].ClosestPoint(testPt, out double t, tolerance))
					{
						Point3d closestPoint = brep.Edges[i].PointAt(t);
						if (closestPoint.DistanceTo(testPt) < tolerance)
							return false;
					}
				}
				if (p.DistanceTo(testPt) < tolerance)
					return true;
			}

			return false;
		}

		/// <summary>
		/// Get the points resulting from cutting a list of curves using a plane
		/// </summary>
		/// <param name="curves">The list of curves</param>
		/// <param name="plane">The cutting plane</param>
		/// <param name="pts">List where to add resulting points</param>
		/// <param name="transform">Transform to apply after cut</param>
		/// <param name="tolerance">Tolerance parameter</param>
		private static void CutLinesWithPlane(List<Curve> curves, Plane plane, ref List<Point3d> pts, Transform transform, double tolerance)
		{
			for (int j = 0; j < curves.Count; j++)
			{
				Curve curve = curves[j];
				CurveIntersections inters = Intersection.CurvePlane(curve, plane, tolerance);
				if (inters == null)
				{
					continue;
				}
				IList<IntersectionEvent> list = inters;
				for (int i = 0; i < list.Count; i++)
				{
					IntersectionEvent inter = list[i];
					if (!inter.IsPoint)
						continue;

					Point3d pp = inter.PointA;
					pp.Transform(transform);
					if (!pts.Contains(pp))
						pts.Add(pp);
				}
			}
		}

		/// <summary>
		/// Get the points resulting from cutting a list of curves using a plane
		/// </summary>
		/// <param name="curves">The list of curves</param>
		/// <param name="plane">The cutting plane</param>
		/// <param name="pts">List where to add resulting points</param>
		/// <param name="tolerance">Tolerance parameter</param>
		private static void CutLinesWithPlane(List<Curve> curves, Plane plane, ref List<Point3d> pts, double tolerance)
		{
			for (int j = 0; j < curves.Count; j++)
			{
				Curve curve = curves[j];
				CurveIntersections inters = Intersection.CurvePlane(curve, plane, tolerance);
				if (inters == null)
				{
					continue;
				}
				IList<IntersectionEvent> list = inters;
				for (int i = 0; i < list.Count; i++)
				{
					IntersectionEvent inter = list[i];
					if (!inter.IsPoint)
						continue;
					Point3d pp = inter.PointA;
					if (!Contains(pts, pp, tolerance))
						pts.Add(pp);
				}
			}
		}

		/// <summary>
		/// Get the points resulting from cutting a list of curves using a line curve
		/// </summary>
		/// <param name="curves">The list of curves</param>
		/// <param name="line">The cutting line</param>
		/// <param name="pts">List where to add resulting points</param>
		/// <param name="tolerance">Tolerance parameter</param>
		private static void CutLinesWithLine(List<Curve> curves, LineCurve line, ref List<Point3d> pts, double tolerance)
		{
			for (int j = 0; j < curves.Count; j++)
			{
				Curve curve = curves[j];
				Curve curve2d = Curve.ProjectToPlane(curve, Plane.WorldXY);
				CurveIntersections inters = Intersection.CurveCurve(curve2d, line, tolerance, tolerance);

				if (inters == null || inters.Count == 0)
				{
					continue;
				}
				IList<IntersectionEvent> list = inters;
				for (int i = 0; i < list.Count; i++)
				{
					IntersectionEvent inter = list[i];
					if (!inter.IsPoint)
						continue;

					LineCurve lineCurve = new LineCurve(inter.PointA, inter.PointA + new Point3d(0, 0, 1));

					CurveIntersections interss = Intersection.CurveLine(curve, lineCurve.Line, tolerance, tolerance);
					if (interss != null && interss.Count > 0)
					{
						Point3d pp = interss[0].PointA;
						if (!pts.Contains(pp))
							pts.Add(pp);
					}
				}
			}
		}

		private static void CutLinesWithBrep(List<Curve> curves, Brep brep, ref List<Point3d> pts, double tolerance)
		{
			for (int j = 0; j < curves.Count; j++)
			{
				Curve curve = curves[j];
				Curve[] overlapCurves;
				Point3d[] intersPoints;

				if (!Intersection.CurveBrep(curve, brep, tolerance, out overlapCurves, out intersPoints))
				{
					continue;
				}
				for (int i = 0; i < intersPoints.Length; i++)
				{
					if (!Contains(pts, intersPoints[i], tolerance))
						pts.Add(intersPoints[i]);
				}
			}
		}

		/// <summary>
		/// Add a border point into the correct list
		/// </summary>
		/// <param name="fixBorders">Fix borders curves list</param>
		/// <param name="restrain">Restrain points list</param>
		/// <param name="freePoints">Free points list</param>
		/// <param name="pt">The point to add</param>
		/// <param name="tolerance">Tolerance parameter</param>
		private static void AddToPointList(List<Curve> fixBorders, ref List<Point3d> restrain, ref List<Point3d> freePoints, Point3d pt, double tolerance)
		{
			bool add = false;
			for (int k = 0; k < fixBorders.Count; k++)
			{
				if (fixBorders[k].ClosestPoint(pt, out double paramT))
				{
					Point3d closestP = fixBorders[k].PointAt(paramT);
					if (closestP.DistanceTo(pt) < tolerance)
					{
						restrain.Add(pt);
						add = true;
						break;
					}
				}
			}
			if (!add)
				freePoints.Add(pt);
		}

		/// <summary>
		///	Check if a list of points contains a point
		/// </summary>
		/// <param name="points">The list of points</param>
		/// <param name="pointToTest">The point to test</param>
		/// <param name="tolerance">Tolerance parameter</param>
		/// <returns></returns>
		private static bool Contains(List<Point3d> points, Point3d pointToTest, double tolerance)
		{
			for (int i = 0; i < points.Count; i++)
			{
				if (points[i].DistanceTo(pointToTest) < tolerance)
					return true;
			}
			return false;
		}

		/// <summary>
		/// Split the borders using points
		/// </summary>
		/// <param name="allPoints">All points obtained</param>
		/// <param name="outFixBorder">List of fix border curves splitted into line segments</param>
		/// <param name="outFreeBorder">List of free border curves splitted into line segments</param>
		/// <param name="tolerance">Tolerance parameter</param>
		private static void PostProcess(List<Point3d> allPoints, ref List<List<Curve>> outFixBorder, ref List<List<Curve>> outFreeBorder, double tolerance)
		{
			for (int j = 0; j < allPoints.Count; j++)
			{
				// splitto tutti i bordi fissi con tutti i punti sul bordo che ho e ricostruisco le liste
				for (int k = 0; k < outFixBorder.Count; k++)
				{
					for (int i = 0; i < outFixBorder[k].Count; i++)
					{
						if (outFixBorder[k][i].ClosestPoint(allPoints[j], out double t, tolerance))
						{
							Curve[] split = outFixBorder[k][i].Split(t);
							if (split != null && split.Length > 0)
							{
								for (int kk = 0; kk < split.Length; kk++)
								{
									if (split[kk].GetLength() > tolerance)
									{
										outFixBorder[k].Add(split[kk]);
									}
								}
								outFixBorder[k].RemoveAt(i);
							}
						}
					}
				}

				// splitto tutti i bordi mobili con tutti i punti sul bordo che ho e ricostruisco le liste
				for (int k = 0; k < outFreeBorder.Count; k++)
				{
					for (int i = 0; i < outFreeBorder[k].Count; i++)
					{
						if (outFreeBorder[k][i].ClosestPoint(allPoints[j], out double t, tolerance))
						{
							Curve[] split = outFreeBorder[k][i].Split(t);
							if (split != null && split.Length > 0)
							{
								for (int kk = 0; kk < split.Length; kk++)
								{
									if (split[kk].GetLength() > tolerance)
									{
										outFreeBorder[k].Add(split[kk]);
									}
								}
								outFreeBorder[k].RemoveAt(i);
							}
						}
					}
				}
			}

			// trasformo tutto in linea
			for (int i = 0; i < outFixBorder.Count; i++)
			{
				for (int j = 0; j < outFixBorder[i].Count; j++)
				{
					if (outFixBorder[i][j].Degree > 1)
					{
						outFixBorder[i][j] = new LineCurve(new Line(outFixBorder[i][j].PointAtStart, outFixBorder[i][j].PointAtEnd));
					}
				}
			}
			for (int i = 0; i < outFreeBorder.Count; i++)
			{
				for (int j = 0; j < outFreeBorder[i].Count; j++)
				{
					if (outFreeBorder[i][j].Degree > 1)
					{
						outFreeBorder[i][j] = new LineCurve(new Line(outFreeBorder[i][j].PointAtStart, outFreeBorder[i][j].PointAtEnd));
					}
				}
			}
		}

		private static void PostProcess(List<Point3d> borderPoints, List<Point3d> cablePoints, ref List<List<Curve>> outFixBorder, ref List<List<Curve>> outFreeBorder, double tolerance)
		{
			for (int j = 0; j < borderPoints.Count; j++)
			{
				// splitto tutti i bordi fissi con tutti i punti sul bordo che ho e ricostruisco le liste
				for (int k = 0; k < outFixBorder.Count; k++)
				{
					for (int i = 0; i < outFixBorder[k].Count; i++)
					{
						if (outFixBorder[k][i].ClosestPoint(borderPoints[j], out double t, tolerance))
						{
							Curve[] split = outFixBorder[k][i].Split(t);
							if (split != null && split.Length > 0)
							{
								for (int kk = 0; kk < split.Length; kk++)
								{
									if (split[kk].GetLength() > tolerance)
									{
										outFixBorder[k].Add(split[kk]);
									}
								}
								outFixBorder[k].RemoveAt(i);
							}
						}
					}
				}
			}
			for (int j = 0; j < cablePoints.Count; j++)
			{
				// splitto tutti i bordi mobili con tutti i punti sul bordo che ho e ricostruisco le liste
				for (int k = 0; k < outFreeBorder.Count; k++)
				{
					for (int i = 0; i < outFreeBorder[k].Count; i++)
					{
						if (outFreeBorder[k][i].ClosestPoint(cablePoints[j], out double t, tolerance))
						{
							Curve[] split = outFreeBorder[k][i].Split(t);
							if (split != null && split.Length > 0)
							{
								for (int kk = 0; kk < split.Length; kk++)
								{
									if (split[kk].GetLength() > tolerance)
									{
										outFreeBorder[k].Add(split[kk]);
									}
								}
								outFreeBorder[k].RemoveAt(i);
							}
						}
					}
				}
			}

			// trasformo tutto in linea
			for (int i = 0; i < outFixBorder.Count; i++)
			{
				for (int j = 0; j < outFixBorder[i].Count; j++)
				{
					if (outFixBorder[i][j].Degree > 1)
					{
						outFixBorder[i][j] = new LineCurve(new Line(outFixBorder[i][j].PointAtStart, outFixBorder[i][j].PointAtEnd));
					}
				}
			}
			for (int i = 0; i < outFreeBorder.Count; i++)
			{
				for (int j = 0; j < outFreeBorder[i].Count; j++)
				{
					if (outFreeBorder[i][j].Degree > 1)
					{
						outFreeBorder[i][j] = new LineCurve(new Line(outFreeBorder[i][j].PointAtStart, outFreeBorder[i][j].PointAtEnd));
					}
				}
			}
		}

		private static void PostProcess(List<List<Curve>> fixBorders, List<List<Curve>> freeBordes, ref List<Point3d> restraint, ref List<Point3d> notRestraint)
		{
			if (fixBorders != null && fixBorders.Count > 0)
			{
				for (int i = 0; i < fixBorders.Count; i++)
				{
					for (int j = 0; j < fixBorders[i].Count; j++)
					{
						if (!restraint.Contains(fixBorders[i][j].PointAtStart))
							restraint.Add(fixBorders[i][j].PointAtStart);
						if (!restraint.Contains(fixBorders[i][j].PointAtEnd))
							restraint.Add(fixBorders[i][j].PointAtEnd);
					}
				}
			}
			if (freeBordes != null && freeBordes.Count > 0)
			{
				for (int i = 0; i < freeBordes.Count; i++)
				{
					for (int j = 0; j < freeBordes[i].Count; j++)
					{
						if (!notRestraint.Contains(freeBordes[i][j].PointAtStart))
							notRestraint.Add(freeBordes[i][j].PointAtStart);
						if (!notRestraint.Contains(freeBordes[i][j].PointAtEnd))
							notRestraint.Add(freeBordes[i][j].PointAtEnd);
					}
				}
			}
		}

		private static void PostProcess(List<Curve> fixBorders, List<Curve> freeBordes, ref List<List<Curve>> outFixBorder, ref List<List<Curve>> outFreeBorder)
		{
			for (int i = 0; i < fixBorders.Count; i++)
			{
				outFixBorder.Add(new List<Curve>() { fixBorders[i] });
			}
			for (int i = 0; i < freeBordes.Count; i++)
			{
				outFreeBorder.Add(new List<Curve>() { freeBordes[i] });
			}
		}

		private static void PostProcess(List<Curve> fixBorders, List<Curve> freeBordes, ref List<List<Curve>> outFixBorder, ref List<List<Curve>> outFreeBorder,
			List<Point3d> allPoints, List<Point3d> restrain, List<Point3d> notRestrain, double tolerance)
		{
			PostProcess(fixBorders, freeBordes, ref outFixBorder, ref outFreeBorder);
			PostProcess(allPoints, ref outFixBorder, ref outFreeBorder, tolerance);
			PostProcess(outFixBorder, outFreeBorder, ref restrain, ref notRestrain);
		}

		private static void CleanUnusedPoints(List<List<LineCurve>> outWeft, List<List<LineCurve>> outWarp, ref List<Point3d> restrain, ref List<Point3d> notRestrain) {
            var checkSet = new HashSet<Point3d>();
            for (int i = 0; i < outWeft.Count; ++i)
            {
                for (int j = 0; j < outWeft[i].Count; ++j)
                {
                    if (checkSet.Contains(outWeft[i][j].PointAtStart) == false)
                    {
                        checkSet.Add(outWeft[i][j].PointAtStart);
                    }
                    if (checkSet.Contains(outWeft[i][j].PointAtEnd) == false)
                    {
                        checkSet.Add(outWeft[i][j].PointAtEnd);
                    }
                }
            }
            for (int i = 0; i < outWarp.Count; ++i)
            {
                for (int j = 0; j < outWarp[i].Count; ++j)
                {
                    if (checkSet.Contains(outWarp[i][j].PointAtStart) == false)
                    {
                        checkSet.Add(outWarp[i][j].PointAtStart);
                    }
                    if (checkSet.Contains(outWarp[i][j].PointAtEnd) == false)
                    {
                        checkSet.Add(outWarp[i][j].PointAtEnd);
                    }
                }
            }
            for (int i = 0; i < restrain.Count; ++i)
            {
                if (checkSet.Contains(restrain[i]) == false)
                {
                    restrain.RemoveAt(i);
                    i--;
                }
            }
            for (int i = 0; i < notRestrain.Count; ++i)
            {
                if (checkSet.Contains(notRestrain[i]) == false)
                {
                    notRestrain.RemoveAt(i);
                    i--;
                }
            }
        }

		private static List<List<LineCurve>> ProcessInternalCable(List<Curve> internalCable, List<List<LineCurve>> warpLines, List<List<LineCurve>> weftLines,
			ref List<Point3d> internalPoint, double tolerance)
		{
			List<List<LineCurve>> internalCableLines = new List<List<LineCurve>>();

			for (int c = 0; c < internalCable.Count; c++)
			{
				List<LineCurve> internalCableLinesBuffer = new List<LineCurve>();

				List<Point3d> splitPoints = new List<Point3d>();
				Curve cable = internalCable[c];
				Curve cable2d = internalCable[c];
				cable2d = Curve.ProjectToPlane(cable2d, Plane.WorldXY);

				for (int i = 0; i < warpLines.Count; i++)
				{
					for (int j = 0; j < warpLines[i].Count; j++)
					{
						Line line2d = new Line(warpLines[i][j].Line.FromX, warpLines[i][j].Line.FromY, 0, warpLines[i][j].Line.ToX, warpLines[i][j].Line.ToY, 0);
						LineCurve line = new LineCurve(line2d);
						CurveIntersections res = Intersection.CurveCurve(cable2d, line, tolerance, tolerance);

						if (res != null && res.Count > 0)
						{
							Point3d pp = res[0].PointA;
							Line line1 = new Line(warpLines[i][j].Line.From, pp);
							Line line2 = new Line(pp, warpLines[i][j].Line.To);

							warpLines[i].RemoveAt(j);
							warpLines[i].Insert(j, new LineCurve(line1));
							warpLines[i].Insert(j + 1, new LineCurve(line2));

							if (!splitPoints.Contains(pp))
								splitPoints.Add(pp);

							j++;
						}
					}
				}
				for (int i = 0; i < weftLines.Count; i++)
				{
					for (int j = 0; j < weftLines[i].Count; j++)
					{
						Line line2d = new Line(weftLines[i][j].Line.FromX, weftLines[i][j].Line.FromY, 0, weftLines[i][j].Line.ToX, weftLines[i][j].Line.ToY, 0);
						LineCurve line = new LineCurve(line2d);
						CurveIntersections res = Intersection.CurveCurve(cable2d, line, tolerance, tolerance);

						if (res != null && res.Count > 0)
						{
							Point3d pp = res[0].PointA;
							Line line1 = new Line(weftLines[i][j].Line.From, pp);
							Line line2 = new Line(pp, weftLines[i][j].Line.To);

							weftLines[i].RemoveAt(j);
							weftLines[i].Insert(j, new LineCurve(line1));
							weftLines[i].Insert(j + 1, new LineCurve(line2));

							if (!splitPoints.Contains(pp))
								splitPoints.Add(pp);

							j++;
						}
					}
				}

				List<double> splitT = new List<double>();

				for (int s = 0; s < splitPoints.Count; s++)
				{
					Point3d splitPoint = splitPoints[s];

					Line verticalLine = new Line(splitPoint, splitPoint + new Point3d(0, 0, 1));
					CurveIntersections inters = Intersection.CurveLine(cable2d, verticalLine, tolerance, tolerance);

					if (inters != null && inters.Count > 0)
					{
						Point3d intersPoint = inters[0].PointA;
						if (cable2d.ClosestPoint(intersPoint, out double t, 10 * tolerance))
						{
							splitT.Add(t);
						}
					}
				}

				splitT.Sort();

				Curve[] curves = cable2d.Split(splitT);
				for (int i = 0; i < curves.Length; i++)
				{
					if (i == 0)
					{
						internalCableLinesBuffer.Add(new LineCurve(new Line(cable.PointAtStart, curves[i].PointAtEnd)));
					}
					else if (i == curves.Length - 1)
					{
						if (!internalPoint.Contains(curves[i].PointAtStart))
							internalPoint.Add(curves[i].PointAtStart);
						internalCableLinesBuffer.Add(new LineCurve(new Line(curves[i].PointAtStart, cable.PointAtEnd)));
					}
					else
					{
						if (!internalPoint.Contains(curves[i].PointAtStart))
							internalPoint.Add(curves[i].PointAtStart);
						internalCableLinesBuffer.Add(new LineCurve(new Line(curves[i].PointAtStart, curves[i].PointAtEnd)));
					}
				}

				internalCableLines.Add(internalCableLinesBuffer);
			}

			return internalCableLines;
		}

		private static bool CleanListListLineCurve(ref List<List<LineCurve>> list, double tolerance)
		{
			if (list == null || list.Count == 0)
				return false;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Count > 0)
				{
					for (int j = 0; j < list[i].Count; j++)
					{
						if (list[i][j].GetLength() < tolerance)
						{
							list[i].RemoveAt(j);
							j--;
						}
					}
				}
			}
			return true;
		}
	}
}

