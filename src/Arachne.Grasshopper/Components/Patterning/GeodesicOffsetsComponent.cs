using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class GeodesicOffsetsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GeodesicOffsetsComponent class.
        /// </summary>
        public GeodesicOffsetsComponent()
          : base("Geodesic Offsets", "GO", "Generate the offsets of a geodesic line", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh", GH_ParamAccess.item);
            pManager.AddCurveParameter("Geodesic", "G", "The geodesic to copy", GH_ParamAccess.item);
            int i = pManager.AddIntegerParameter("Side", "S", "The side of the geodesic line where to create the geodesic offset", GH_ParamAccess.item, 1);
            Param_Integer par = (Param_Integer)pManager[i];
            par.AddNamedValue("Left", -1);
            par.AddNamedValue("Right", 1);
            pManager.AddNumberParameter("Distance", "D", "The offset distance", GH_ParamAccess.item);
            pManager.AddNumberParameter("Tolerance", "T", "The distance tolerance", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Resolution", "R", "The resolution for generating the geodesics lines", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Iterations", "I", "The number of iteration for the geodesic calculation", GH_ParamAccess.item, 1000);
            pManager.AddBooleanParameter("Vertical", "V", "Determine if the cutting plane is vertical or contained on starting geodesic", GH_ParamAccess.item, true);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Geodesics", "Gs", "The resulting geodesic curves", GH_ParamAccess.list);
            pManager.AddMeshParameter("Mesh", "Ms", "The mesh", GH_ParamAccess.item);
            pManager.AddMeshParameter("Flatten Mesh", "Fl", "The resulting flatten mesh", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;
            GH_Curve geodesic = null;
            int side = 0;
            double dist = 0;
            double tol = 0;
            double res = 0;
            int steps = 0;
            bool isVertical = false;

            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetData(1, ref geodesic))
                return;
            if (!DA.GetData(2, ref side))
                return;
            if (!DA.GetData(3, ref dist))
                return;
            if (!DA.GetData(4, ref tol))
                return;
            if (!DA.GetData(5, ref res))
                return;
            if (!DA.GetData(6, ref steps))
                return;
            if (!DA.GetData(7, ref isVertical))
                return;


            double stepsize = 0.5;

            //getting refPlane passing on geodesic
            Curve gd = geodesic.Value.PullToMesh(mesh.Value, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            Point3d ini = gd.PointAtStart;
            Point3d end = gd.PointAtEnd;

            Plane refPlane;
            if (isVertical)
            {
                Vector3d dir = end - ini;
                dir.Unitize();
                if (Math.Abs(dir * Vector3d.ZAxis) > 0.999)
                    throw new NotSupportedException("Unable to use vertical with vertical geodesic");

                Vector3d normal = Vector3d.CrossProduct(dir, Vector3d.ZAxis);
                refPlane = new Plane(ini, normal);
            }
            else
            {
                Point3d mid = gd.PointAtLength(0.5 * gd.GetLength());
                MeshPoint mp = mesh.Value.ClosestMeshPoint(mid, 0);

                if (mp == null)
                {
                    throw new NotSupportedException("Unable to find the normal at mid point of geodesic");
                }

                mesh.Value.RebuildNormals();
                Vector3d normal = mesh.Value.FaceNormals[mp.FaceIndex];
                refPlane = new Plane(ini, end, ini + 100 * normal);
            }

            bool reverse = false;
            if (side == 1)
            {
                //Right, positive x
                if (refPlane.Normal * Vector3d.XAxis < 0)
                {
                    reverse = true;
                }
            }
            else if (side == -1)
            {
                //Left, negative x
                if (refPlane.Normal * Vector3d.XAxis > 0)
                {
                    reverse = true;
                }
            }
            else
            {
                throw new NotSupportedException();
            }

            if (reverse)
            {
                refPlane = new Plane(refPlane.Origin, -refPlane.Normal);
            }

            //finding max width
            Point3d maxPoint = Point3d.Unset;
            double maxDistance = double.MinValue;
            Transform trans = Transform.ChangeBasis(Plane.WorldXY, refPlane);

            for (int i = 0; i < mesh.Value.Vertices.Count; i++)
            {
                Point3d p = (Point3d)mesh.Value.Vertices[i];
                Point3d localP = trans * p;
                if (localP.Z > maxDistance)
                {
                    maxDistance = localP.Z;
                    maxPoint = p;
                }
            }

            if (maxDistance < 0.05 * dist)
            {
                //Too small residual length
                return;
            }

            try
            {
                const int MAX_ITERATIONS = 20;

                int iter = 1;
                double planeShift = dist;
                double lowerBound = -1;
                double upperBound = -1;
                do
                {
                    //finding cutting curves
                    List<Curve> cuttingCurves = new List<Curve> { gd };
                    bool exceedMaxDistance = planeShift >= maxDistance + RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;

                    List<Curve> result = new List<Curve>();
                    if (exceedMaxDistance)
                    {
                        result = null;
                    }
                    else
                    {
                        Plane shifted = new Plane(refPlane.Origin + refPlane.Normal * planeShift, refPlane.Normal);
                        Polyline[] pls = Intersection.MeshPlane(mesh.Value, shifted);

                        for (int i = 0; i < pls.Length; i++)
                        {
                            Polyline pl = pls[i];
                            if (res == 0)
                                res = pl.First.DistanceTo(pl.Last) / 20.0;

                            Arachne.Grasshopper.Geometry.MeshGeodesic mg = new Arachne.Grasshopper.Geometry.MeshGeodesic(mesh.Value, pl.First, pl.Last, res, stepsize, steps);
                            result.Add(MeshGeodesicComponent.ProjectPolylineToMesh(mg.Polyline, mesh.Value));
                        }
                        cuttingCurves.AddRange(result);
                    }
                    //cutting
                    //foreach (Curve c in cuttingCurves)
                    //{
                    //    RhinoDoc.ActiveDoc.Objects.AddCurve(c);
                    //}

                    Mesh[] cuts = SplitMeshOnCurvesComponent.CutMesh(mesh.Value, cuttingCurves.ToArray());


                    //Finding the internal piece
                    Mesh internalCut = null;
                    for (int i = 0; i < cuts.Length; i++)
                    {
                        Mesh cut = cuts[i];
                        double maximumDistance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance * 100;
                        bool touchAll = true;
                        bool touched;

                        for (int j = 0; j < cuttingCurves.Count; j++)
                        {
                            Curve c = cuttingCurves[j];
                            touched = false;
                            for (int k = 0; k < cut.Vertices.Count; k++)
                            {
                                Point3d v = (Point3d)cut.Vertices[k];

                                if (c.ClosestPoint(v, out double t, maximumDistance))
                                {
                                    touched = true;
                                    break;
                                }
                            }
                            if (!touched)
                            {
                                touchAll = false;
                                break;
                            }
                        }
                        if (touchAll)
                        {
                            if (exceedMaxDistance)
                            {
                                touched = false;
                                for (int p = 0; p < cut.Vertices.Count; p++)
                                {
                                    Point3d v = (Point3d)cut.Vertices[p];
                                    if (v.DistanceTo(maxPoint) < maximumDistance)
                                    {
                                        touched = true;
                                        break;
                                    }
                                }
                                //RhinoDoc.ActiveDoc.Objects.AddPoint(maxPoint);
                                if (!touched)
                                {
                                    touchAll = false;
                                }
                            }
                            if (touchAll)
                            {
                                internalCut = cut;
                                break;
                            }
                        }
                    }
                    if (internalCut == null)
                    {
                        throw new NotSupportedException("Unable to find an internal mesh");
                    }

                    //internalCut.Reduce(Convert.ToInt32(internalCut.Faces.Count * 0.9), true, 5, false);
                    //RhinoDoc.ActiveDoc.Objects.AddMesh(internalCut);

                    //Flattening
                    FlatteningComponent.GetFlattenedMesh(internalCut, 1, 10, 0.01, 0, 0, out TensileLib.FormFinding.Solver.ResultCodes ret, out Mesh flat);

                    if (ret != TensileLib.FormFinding.Solver.ResultCodes.Converged)
                    {
                        Mesh flatBuffer = new Mesh();
                        if (!ARAPHelper.Flattening(internalCut, flatBuffer))
                        {
                            if (flat == null)
                            {
                                throw new NotSupportedException("Unable to flat the mesh");
                            }
                        }
                        else
                        {
                            flat = flatBuffer;
                        }
                    }

                    if (flat == null)
                    {
                        throw new NotSupportedException("Unable to flat the mesh");
                    }

                    //Finding the width of the mesh
                    //Finding the nodes on the first cut, which impose the direction
                    int firstNode = -1;
                    int lastNode = -1;

                    for (int i = 0; i < internalCut.Vertices.Count; i++)
                    {
                        Point3d p3d = new Point3d(internalCut.Vertices[i]);

                        if (firstNode < 0 && p3d.DistanceTo(ini) < tol)
                            firstNode = i;

                        if (lastNode < 0 && p3d.DistanceTo(end) < tol)
                            lastNode = i;

                        if (firstNode > -1 && lastNode > -1)
                            break;
                    }


                    Vector3d flatDir = flat.Vertices[lastNode] - flat.Vertices[firstNode];
                    flatDir.Unitize();
                    Vector3d orto = Vector3d.CrossProduct(flatDir, Vector3d.ZAxis);
                    double posMin = double.MaxValue;
                    double posMax = double.MinValue;

                    for (int i = 0; i < flat.Vertices.Count; i++)
                    {
                        Point3d v = (Point3d)flat.Vertices[i];
                        double pos = new Vector3d(v) * orto;

                        posMin = Math.Min(posMin, pos);
                        posMax = Math.Max(posMax, pos);
                    }

                    double width = Math.Abs(posMax - posMin);
                    if (width < 1000 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                    {
                        throw new NotSupportedException("Dimensione del cut troppo piccola");
                    }

                    //RhinoDoc.ActiveDoc.Objects.AddMesh(flat);

                    if (exceedMaxDistance && width < dist)
                    {
                        return;
                    }
                    else if (Math.Abs(width - dist) < tol)
                    {
                        //RhinoDoc.ActiveDoc.Objects.AddMesh(flat);
                        //RhinoDoc.ActiveDoc.Objects.AddLine(flat.Vertices[firstNode], flat.Vertices[lastNode]);
                        DA.SetDataList(0, result);
                        DA.SetData(1, internalCut);
                        DA.SetData(2, flat);
                        return;
                    }
                    else if (iter > MAX_ITERATIONS)
                    {
                        throw new NotSupportedException($"Not converged after {MAX_ITERATIONS} iterations");
                    }
                    else
                    {
                        if (width < dist)
                        {
                            lowerBound = planeShift;
                        }
                        else
                        {
                            upperBound = planeShift;
                        }

                        if (lowerBound > 0 && upperBound > 0)
                        {
                            planeShift = (lowerBound + upperBound) / 2;
                        }
                        else
                        {
                            planeShift = planeShift * dist / width;
                        }

                        iter++;
                    }
                } while (true == true);
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("GeodesicOffsetsIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("c1e96689-970d-5d37-9fac-5f2fd2e5ebe3");
    }
}
