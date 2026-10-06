using Arachne.Grasshopper.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class LineUpComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the LineUp class.
        /// </summary>
        public LineUpComponent()
          : base("Line-Up", "LU", "Line-Up given geometry", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "The geometry to line-up. (Mesh, PolylineCurve)", GH_ParamAccess.list);
            pManager.AddLineParameter("Line", "L", "Base line", GH_ParamAccess.item, new Line(Point3d.Origin, new Point3d(1, 0, 0)));
            pManager.AddNumberParameter("Spacing", "S", "Space between geometry in line", GH_ParamAccess.item, 100);
            pManager.AddBooleanParameter("MinimalBounding", "M", "Use minimal bounding algorithm", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Geometry", "G", "The lined-up geometry", GH_ParamAccess.list);
            pManager.AddCurveParameter("Box", "B", "The line aligned bounding box of the geometry", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<IGH_GeometricGoo> geom = new List<IGH_GeometricGoo>();
            GH_Line line = null;
            double spacing = 100;
            bool minimalBounding = false;

            double discontinuityAngle = 15 * Math.PI / 180;

            if (!DA.GetDataList(0, geom))
            {
                return;
            }
            if (!DA.GetData(1, ref line))
            {
                return;
            }
            if (!DA.GetData(2, ref spacing))
            {
                return;
            }
            if (!DA.GetData(3, ref minimalBounding))
            {
                return;
            }

            var ret = new List<IGH_GeometricGoo>();
            var boxes = new List<GH_Curve>();

            // Transform each geometry
            foreach (var g in geom)
            {
                // Transform curves
                GH_Curve curve = g as GH_Curve;
                if (curve != null)
                {
                    PolylineCurve polyc = curve.Value as PolylineCurve;
                    if (polyc != null)
                    {
                        var polycopy = new PolylineCurve(polyc);
                        Transform trafo;
                        if (minimalBounding)
                        {
                            List<Point3d> points = new List<Point3d>();
                            for (int i = 0; i < polyc.PointCount; i++) { points.Add(polyc.Point(i)); }
                            trafo = MinRectangleAxisAligned(points);
                        }
                        else
                        {
                            var polylines = new Polyline[] { polyc.ToPolyline() };
                            List<Polyline> borders = BordersComponent.BreakAtPolylines(polylines, discontinuityAngle);
                            trafo = SpecularAligned(borders);
                        }
                        polycopy.Transform(trafo);
                        ret.Add(new GH_Curve(polycopy));
                        continue;
                    }
                }

                // Transform meshes
                GH_Mesh mesh = g as GH_Mesh;
                if (mesh != null)
                {
                    var meshcopy = mesh.DuplicateMesh();
                    Transform trafo;
                    if (minimalBounding)
                    {
                        List<Point3d> points = new List<Point3d>();
                        for (int i = 0; i < mesh.Value.Vertices.Count; i++) { points.Add(mesh.Value.Vertices[i]); }
                        trafo = MinRectangleAxisAligned(points);
                    }
                    else
                    {
                        var polylines = meshcopy.Value.GetNakedEdges();
                        List<Polyline> borders = BordersComponent.BreakAtPolylines(polylines, discontinuityAngle);
                        trafo = SpecularAligned(borders);
                    }
                    ret.Add(meshcopy.Transform(trafo));
                    continue;
                }
            }

            // Compute the direction and the angle of the line
            var offset = 0.0;
            var vec = new Vector3d(line.Value.To - line.Value.From);
            vec.Unitize();
            var angle = Math.Atan2(vec.Y, vec.X);

            // Transform each geometry to the final position and make a polylineCurve bbox
            foreach (var g in ret)
            {
                if (g == null)
                {
                    boxes.Add(null);
                    continue;
                }
                // Original bbox. Make it before and transform it later (cause it is aligned to XY axes)
                var bbox = g.GetBoundingBox(Transform.Identity);
                var p = new Polyline
                {
                    bbox.Min,
                    new Point3d(bbox.Max.X, bbox.Min.Y, bbox.Min.Z),
                    new Point3d(bbox.Max.X, bbox.Max.Y, bbox.Min.Z),
                    new Point3d(bbox.Min.X, bbox.Max.Y, bbox.Min.Z),
                    bbox.Min
                };

                // Rotate as the line
                g.Transform(Transform.Rotation(angle, bbox.Min));
                p.Transform(Transform.Rotation(angle, bbox.Min));

                // Translate to position on the line
                var position = line.Value.From + vec * offset;
                g.Transform(Transform.Translation(position - bbox.Min));
                p.Transform(Transform.Translation(position - bbox.Min));

                offset += bbox.Diagonal.X + spacing;
                boxes.Add(new GH_Curve(p.ToPolylineCurve()));
            }

            DA.SetDataList(0, ret);
            DA.SetDataList(1, boxes);
        }


        public Transform SpecularAligned(List<Polyline> polylines)
        {
            if (polylines.Count != 4)
            {
                return Transform.Identity;
            }

            Vector3d axis = new Vector3d(0, 0, 0);

            for (int i = 0; i < polylines.Count - 1; i++)
            {
                for (int j = i + 1; j < polylines.Count; j++)
                {
                    if (polylines[i][0] != polylines[j][0] &&
                        polylines[i][0] != polylines[j][polylines[j].Count - 1] &&
                        polylines[i][polylines[i].Count - 1] != polylines[j][0] &&
                        polylines[i][polylines[i].Count - 1] != polylines[j][polylines[j].Count - 1])
                    {

                        var pta = polylines[i].ToPolylineCurve().PointAtNormalizedLength(0.5);
                        var ptb = polylines[j].ToPolylineCurve().PointAtNormalizedLength(0.5);
                        var vec = new Vector3d(pta - ptb);

                        if (vec.Length > axis.Length)
                        {
                            axis = vec;
                        }
                    }
                }
            }

            axis.Unitize();
            var angle = Math.Atan2(axis.Y, axis.X);
            angle += Math.PI * 0.5;
            if (axis.Y > 0)
            {
                return Transform.Rotation(-angle + Math.PI, polylines[0][0]);
            }
            else
            {
                return Transform.Rotation(-angle, polylines[0][0]);
            }

        }

        // Find the transformation that gives the mininal Boundind rectangle aligned to XY axis. The points are considered to be on XY plane.
        public Transform MinRectangleAxisAligned(List<Point3d> points)
        {
            // Find the point with minimal Y coordinate. If two points have the same Y, choose the one with min X
            Vector3d P = new Vector3d(points[0]);
            foreach (Point3d pt in points)
            {
                if (pt.Y < P.Y || pt.Y == P.Y && pt.X < P.X)
                {
                    P = new Vector3d(pt);
                }
            }

            // Sort the points:
            //      consider the vector made from point P and the point to sort and compute the angle between the vector and X axis
            //      if two points have the same angle, sort using the Y coord
            //      if two points have the same Y coord, sort using the X coord
            List<Point3d> sorted = new List<Point3d>(points);
            sorted.Sort((ptA, ptB) =>
            {
                var comp = Math.Atan2(ptA.Y - P.Y, ptA.X - P.X).CompareTo(Math.Atan2(ptB.Y - P.Y, ptB.X - P.X));
                if (comp != 0) { return comp; }
                comp = ptA.Y.CompareTo(ptB.Y);
                if (comp != 0) { return comp; }
                return ptA.X.CompareTo(ptB.X);
            });

            // Indices of the points forming the convex hull of the shape. The first point is P, and it is part of the convex hull
            List<int> hull = new List<int>();
            hull.Add(0);

            // Loop on sorted points
            for (int i = 1; i < sorted.Count; ++i)
            {
                // If only on point in the hull, add and continue
                if (hull.Count < 2)
                {
                    hull.Add(i);
                    continue;
                }

                // Take the last two points of the hull and the current point
                var ptA = new Vector3d(sorted[hull[hull.Count - 2]]);
                var ptB = new Vector3d(sorted[hull[hull.Count - 1]]);
                var ptC = new Vector3d(sorted[i]);

                // If the three points forms a turn to the right, this means that the last added point is not part of the convex hull.
                // Remove it and repeat the iteration
                if (Vector3d.CrossProduct(ptB - ptA, ptB - ptC).Z >= 0)
                {
                    hull.RemoveAt(hull.Count - 1);
                    --i;
                    continue;
                }

                // Otherwise this is a left turn, add the point
                hull.Add(i);
            }

            // Build a closed polyline with points in the hull
            Polyline poly = new Polyline();
            foreach (var idx in hull)
            {
                poly.Add(sorted[idx]);
            }
            poly.Add(sorted[0]);

            var resTrafo = new Transform(Transform.Identity);
            var polyCopy = new Polyline(poly);
            var min = polyCopy.BoundingBox.Diagonal.X;

            // Test all segments in the hull aligned with both X and Y axes, and choose the one with min X diagonal component
            for (int i = 0; i < poly.SegmentCount; ++i)
            {
                var seg = poly.SegmentAt(i);
                var vec = new Vector3d(seg.To - seg.From);

                // Test against X
                polyCopy = new Polyline(poly);
                var angle = Math.Atan2(vec.Y, vec.X);
                var trafo = Transform.Rotation(-angle, seg.From);
                polyCopy.Transform(trafo);
                if (polyCopy.BoundingBox.Diagonal.X < min)
                {
                    min = polyCopy.BoundingBox.Diagonal.X;
                    resTrafo = trafo;
                }

                // Test against Y
                polyCopy = new Polyline(poly);
                angle = angle > 0 ? angle - Math.PI * 0.5 : angle + Math.PI * 0.5;
                trafo = Transform.Rotation(-angle, seg.From);
                polyCopy.Transform(trafo);
                if (polyCopy.BoundingBox.Diagonal.X < min)
                {
                    min = polyCopy.BoundingBox.Diagonal.X;
                    resTrafo = trafo;
                }

            }

            // Return the transformation
            return resTrafo;
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("dce9b365-6f46-5d47-a342-bc5a769d640d");
    }
}