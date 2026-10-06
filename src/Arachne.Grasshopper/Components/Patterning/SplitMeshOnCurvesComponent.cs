using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class SplitMeshOnCurvesComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SplitMeshOnCurvesComponent class.
        /// </summary>
        public SplitMeshOnCurvesComponent()
          : base("Split Mesh On Curves", "SM", "Splits a mesh on the given curves", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PATTERNING)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh", GH_ParamAccess.item);
            pManager.AddCurveParameter("Geodesics", "G", "The geodesics lines for cutting the mesh", GH_ParamAccess.list);
            pManager.AddNumberParameter("Extend", "E", "Extend the lines endings of the given amount", GH_ParamAccess.item, 10.0);
            pManager.AddNumberParameter("Height", "H", "Height of the cutters used to make the cut", GH_ParamAccess.item, 100.0);
            pManager.AddVectorParameter("Direction", "D", "Direction in wich to raise the cutter", GH_ParamAccess.item, Vector3d.ZAxis);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Panels", "P", "The resulting panels", GH_ParamAccess.list);
            pManager.AddMeshParameter("Cutters", "C", "The cutters used to make the cut", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;
            List<GH_Curve> geodesics = new List<GH_Curve>();

            double extend = 10.0;
            double height = 100.0;
            Vector3d direction = Vector3d.ZAxis;

            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetDataList(1, geodesics))
                return;

            DA.GetData(2, ref extend);
            DA.GetData(3, ref height);
            DA.GetData(4, ref direction);

            Curve[] geodesicCurves = new Curve[geodesics.Count];
            Mesh[] panels;
            Mesh[] cutters;

            for (int i = 0; i < geodesics.Count; i++)
            {
                geodesicCurves[i] = geodesics[i].Value;
            }

            try
            {
                (panels, cutters) = CutMesh2(mesh.Value, geodesicCurves.ToArray(), direction, extend, height);
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                panels = null;
                cutters = null;
            }
            DA.SetDataList(0, panels);
            DA.SetDataList(1, cutters);
        }


        public static (Mesh[], Mesh[]) CutMesh2(Mesh mesh, Curve[] geodesics, Vector3d dir, double extend = 10, double height = 100)
        {
            Vector3d up = Vector3d.Multiply(dir, height);
            Vector3d down = Vector3d.Multiply(dir, -height);

            Mesh bufferMesh = mesh.DuplicateMesh();
            List<Mesh> cutters = new List<Mesh>();

            var toMesh = new Vector3d(bufferMesh.GetBoundingBox(false).Center);
            var toOrigin = toMesh;
            toOrigin.Reverse();

            bufferMesh.Translate(toOrigin);

            for (int i = 0; i < geodesics.Length; ++i)
            {
                var curveA = geodesics[i].DuplicateCurve();
                if (extend > 0)
                {
                    curveA = curveA.Extend(CurveEnd.Both, extend, CurveExtensionStyle.Line);
                }

                curveA.Translate(toOrigin);
                var curveB = curveA.DuplicateCurve();

                curveA.Translate(up);
                curveB.Translate(down);

                var lofts = Brep.CreateFromLoft(new List<Curve>() { curveA, curveB }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false);
                for (int j = 0; j < lofts.Length; ++j)
                {
                    cutters.AddRange(Mesh.CreateFromBrep(lofts[j], MeshingParameters.Minimal));
                }
            }

            var meshes = bufferMesh.Split(cutters);

            for (int i = 0; i < meshes.Length; ++i)
            {
                meshes[i].Translate(toMesh);
            }


            for (int i = 0; i < cutters.Count; ++i)
            {
                cutters[i].Translate(toMesh);
            }

            return (meshes, cutters.ToArray());
        }


        public static Mesh[] CutMesh(Mesh mesh, Curve[] geodesics)
        {
            Mesh bufferMesh = mesh.DuplicateMesh();

            //Triangulation of faces
            bufferMesh.Faces.ConvertQuadsToTriangles();

            bufferMesh.Weld(Math.PI);

            bufferMesh.UnifyNormals();
            bufferMesh.Normals.ComputeNormals();
            bufferMesh.FaceNormals.UnitizeFaceNormals();
            bufferMesh.Normals.UnitizeNormals();

            //Converting to brep
            Brep brep = Brep.CreateFromMesh(bufferMesh, true);
            List<Brep> cutters = new List<Brep>();

            //Creating cutters
            for (int j = 0; j < geodesics.Length; j++)
            {
                Curve c = geodesics[j];

                if (!c.TryGetPolyline(out Polyline pl))
                {
                    throw new NotSupportedException("Section is not a polyline");
                }

                double heigth = 1d / 100d * c.GetLength();

                Polyline plus = new Polyline();
                Polyline minus = new Polyline();

                bool add = true;
                double minDistance = c.GetLength() / (pl.Count - 1) / 10;
                Point3d prevAdded = Point3d.Unset;

                for (int i = 0; i < pl.Count; i++)
                {
                    MeshPoint mpt;
                    Vector3d normal;
                    //Avoiding first node and last node, singularities points
                    if (i == 0)
                    {
                        mpt = bufferMesh.ClosestMeshPoint(pl[1], 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    }
                    else if (i == pl.Count - 1)
                    {
                        mpt = bufferMesh.ClosestMeshPoint(pl[pl.Count - 2], 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    }
                    else
                    {
                        mpt = bufferMesh.ClosestMeshPoint(pl[i], 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                    }
                    if (mpt == null)
                    {
                        add = false;
                        break;
                    }
                    if (prevAdded == Point3d.Unset || i == pl.Count - 1 || prevAdded.DistanceTo(pl[i]) > minDistance)
                    {
                        normal = bufferMesh.FaceNormals[mpt.FaceIndex];
                        plus.Add(pl[i] + normal * heigth);
                        minus.Add(pl[i] - normal * heigth);
                        prevAdded = pl[i];
                        //RhinoDoc.ActiveDoc.Objects.AddPoint(pl[i]);
                    }
                }

                //Extending the cutting lines
                Curve plusCurve = plus.ToPolylineCurve();
                Curve minusCurve = minus.ToPolylineCurve();
                plusCurve = plusCurve.Extend(CurveEnd.Both, 10 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, CurveExtensionStyle.Line);
                minusCurve = minusCurve.Extend(CurveEnd.Both, 10 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, CurveExtensionStyle.Line);

                //RhinoDoc.ActiveDoc.Objects.AddCurve(plusCurve);
                //RhinoDoc.ActiveDoc.Objects.AddCurve(minusCurve);
                if (add)
                {
                    cutters.AddRange(Brep.CreateFromLoft(new List<Curve>() { plusCurve, minusCurve }, Point3d.Unset, Point3d.Unset, LoftType.Straight, false));
                    //SweepTwoRail stw = new SweepTwoRail();
                    //cutters.AddRange(stw.PerformSweep(plusCurve, minusCurve, new LineCurve(plusCurve.PointAtStart, minusCurve.PointAtStart)));
                }
            }

            //Moving points away from cutters to avoid problems
            for (int j = 0; j < cutters.Count; j++)
            {
                Brep b = cutters[j];
                for (int i = 0; i < bufferMesh.Vertices.Count; i++)
                {
                    if (b.ClosestPoint(bufferMesh.Vertices[i], out Point3d closestPoint, out ComponentIndex ci, out double s, out double t, 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance,
                        out Vector3d normal))
                    {
                        bufferMesh.Vertices.SetVertex(i, closestPoint + 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance * normal);
                    }
                }
            }


            Brep[] split_breps = brep.Split(cutters, 10 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            //RhinoDoc.ActiveDoc.Objects.AddBrep(brep);
            //foreach (Brep cut in cutters)
            //{
            //    RhinoDoc.ActiveDoc.Objects.AddBrep(cut);
            //}

            //foreach (Brep split in split_breps)
            //{
            //    RhinoDoc.ActiveDoc.Objects.AddBrep(split);
            //}

            List<Mesh> result = new List<Mesh>();

            for (int k = 0; k < split_breps.Length; k++)
            {
                Brep b = split_breps[k];
                //
                // https://developer.rhino3d.com/api/RhinoCommon/html/M_Rhino_Geometry_BrepFace_Split.htm
                // https://developer.rhino3d.com/api/RhinoCommon/html/M_Rhino_Geometry_Mesh_CreateFromBrep_1.htm
                //
                // https://github.com/gradientspace/geometry3Sharp
                //
                //Testing if have an area
                if (b.GetArea() < 1000 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance) continue;

                Mesh brep_mesh = new Mesh();
                int i = 0;

                foreach (BrepFace bf in b.Faces)
                {
                    Curve ol = bf.OuterLoop.To3dCurve();
                    double t0 = ol.Domain.T0;
                    double t1 = ol.Domain.T1;
                    double t = ol.Domain.T0;
                    List<double> ts = new List<double> { t };

                    while (ol.GetNextDiscontinuity(Continuity.C1_continuous, t0, t1, out t))
                    {
                        ts.Add(t);
                        t0 = t;
                    }
                    Curve[] segs = ol.Split(ts);
                    List<Point3d> points = segs.Select(s => s.PointAtStart).Distinct().ToList();

                    //Removing double points
                    for (int index = points.Count - 1; index >= 1; index--)
                    {
                        bool remove;
                        Point3d p;
                        remove = false;
                        p = points[index];
                        for (int oIndex = index - 1; oIndex >= 0; oIndex--)
                        {
                            Point3d op;
                            op = points[oIndex];
                            if (p.DistanceTo(op) < 10 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                            {
                                remove = true;
                                break;
                            }
                        }
                        if (remove)
                        {
                            points.RemoveAt(index);
                        }
                    }

                    if (points.Count > 2)
                    {
                        // Check point colinearity
                        for (int j = points.Count - 1; j >= 0; j--)
                        {
                            Point3d p1, p2;
                            if (j == 0)
                                p1 = points[points.Count() - 1];
                            else
                                p1 = points[j - 1];
                            if (j == points.Count() - 1)
                                p2 = points[0];
                            else
                                p2 = points[j + 1];

                            Vector3d v1 = points[j] - p1;
                            Vector3d v2 = p2 - points[j];

                            double ang = Vector3d.VectorAngle(v1, v2);
                            if (ang < 0.01)
                            {
                                points.RemoveAt(j);
                                break;
                            }
                        }

                        if (points.Count > 2)
                        {
                            int n = brep_mesh.Vertices.Count;
                            brep_mesh.Vertices.AddVertices(points);

                            if (points.Count == 3)
                            {
                                brep_mesh.Faces.AddFace(n, n + 1, n + 2);
                            }
                            else if (points.Count == 4)
                            {
                                brep_mesh.Faces.AddFace(n, n + 1, n + 2, n + 3);
                            }
                            else
                            {
                                Polyline pl;
                                MeshFace[] mfs;
                                pl = new Polyline(points)
                                {
                                    points[0]
                                };


                                mfs = pl.TriangulateClosedPolyline();
                                if (mfs != null)
                                {
                                    for (int f = 0; f < mfs.Length; f++)
                                    {
                                        MeshFace fs = mfs[f];
                                        if (fs.IsTriangle)
                                        {
                                            brep_mesh.Faces.AddFace(n + fs.A, n + fs.B, n + fs.C);
                                        }
                                        else
                                        {
                                            throw new NotSupportedException();
                                        }
                                    }
                                }
                            }
                        }
                    }

                    i++;
                }
                brep_mesh.Vertices.CombineIdentical(true, true);
                brep_mesh.Vertices.Align(10 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                brep_mesh.Normals.ComputeNormals();
                brep_mesh.Compact();
                brep_mesh.Weld(Math.PI);
                brep_mesh.Smooth(1, true, true, false, true, SmoothingCoordinateSystem.Object);
                brep_mesh.Faces.ConvertQuadsToTriangles();
                brep_mesh.CollapseFacesByArea(1000 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance, 0);

                for (i = 0; i < 3; i++)
                {
                    brep_mesh.CollapseFacesByEdgeLength(false, 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                }

                brep_mesh.Weld(Math.PI);
                brep_mesh.RebuildNormals();
                brep_mesh.UnifyNormals();
                brep_mesh.Normals.ComputeNormals();

                if (brep_mesh.IsValid)
                {
                    result.Add(brep_mesh);
                }
            }

            Vector3d direction = geodesics[0].PointAtEnd - geodesics[0].PointAtStart;
            direction.Unitize();

            if (Math.Abs(direction * Vector3d.ZAxis) > 0.999)
            {
                direction = Vector3d.CrossProduct(direction, Vector3d.YAxis);
            }
            else
            {
                direction = Vector3d.CrossProduct(direction, Vector3d.ZAxis);
            }
            direction.Unitize();

            if (direction * Vector3d.XAxis < 0)
            {
                direction *= -1;
            }

            result.Sort(new MeshComparer(direction));
            return result.ToArray();
        }

        private class MeshComparer : IComparer<Mesh>
        {
            private Vector3d _direction;

            public MeshComparer(Vector3d direction)
            {
                _direction = direction;
            }

            public int Compare(Mesh x, Mesh y)
            {
                if (x == null && y == null)
                {
                    return 0;
                }
                else if (x == null)
                {
                    return 1;
                }
                else if (y == null)
                {
                    return -1;
                }

                Point3d centerX = x.GetBoundingBox(false).Center;
                Point3d centerY = y.GetBoundingBox(false).Center;

                double posX = new Vector3d(centerX) * _direction;
                double posY = new Vector3d(centerY) * _direction;
                if (Math.Abs(posX - posY) < RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                {
                    return 0;
                }
                else if (posX < posY)
                {
                    return -1;
                }
                else
                {
                    return 1;
                }
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("f1efd4c3-1fac-5f78-b272-a0b71b9af9f6");
    }
}