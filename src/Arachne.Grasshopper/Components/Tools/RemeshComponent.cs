using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Tools
{
    public class RemeshComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the NodeComponent class.
        /// </summary>
        public RemeshComponent()
          : base("Remesh", "R", "Remesh an existing mesh", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PATTERNING)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to remesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Target length", "TL", "The target length of the mesh", GH_ParamAccess.item);
            pManager.AddNumberParameter("Max distance", "MD", "Max distance to consider two points collinear", GH_ParamAccess.item, 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            pManager.AddPlaneParameter("Plane", "P", "The plane where the geometry will be remeshed", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Mesh", "M", "The remeshed mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh mesh = null;
            double targetLength = 0;
            double maxDistance = 0;
            Plane plane = Plane.Unset;
            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetData(1, ref targetLength))
                return;
            if (!DA.GetData(2, ref maxDistance))
                return;
            DA.GetData(3, ref plane);

            mesh.RebuildNormals();
            mesh.UnifyNormals();
            mesh.Normals.ComputeNormals();

            if (plane == Plane.Unset)
            {
                Vector3d normal = new Vector3d();
                foreach (Vector3d n in mesh.Normals)
                {
                    normal += n;
                }
                normal /= mesh.Normals.Count;
                plane = new Plane(mesh.Vertices[0], normal);
            }

            if (targetLength < 1000 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Target length too small");
                return;
            }

            Polyline[] nakeds = mesh.GetNakedEdges();
            List<PolylineCurve> plCurves = new List<PolylineCurve>();

            foreach (Polyline p in nakeds)
                plCurves.Add(new PolylineCurve(p));

            Curve[] joineds = Curve.JoinCurves(plCurves);
            joineds = Curve.JoinCurves(joineds);//2 is meglio che 1

            if (joineds.Length != 1 || !joineds[0].TryGetPolyline(out Polyline pl))
                throw new NotSupportedException("Mesh edge is not a close polyline");

            //Simplify polyline
            Vector3d prevDir;
            Vector3d nextDir = Vector3d.Unset;
            int startingIndex = -1;

            //finding a point on a kink
            for (int i = 0; i < pl.Count - 2; i++)
            {
                if (nextDir == Vector3d.Unset)
                {
                    prevDir = pl[i + 1] - pl[i];
                    prevDir.Unitize();
                }
                else
                {
                    prevDir = nextDir;
                }
                nextDir = pl[i + 2] - pl[i + 1];
                nextDir.Unitize();

                if (nextDir * prevDir < 0.95)
                {
                    //kink at i+1
                    startingIndex = i + 1;
                    break;
                }
            }
            if (startingIndex == -1)
                throw new NotSupportedException();

            int i0 = startingIndex;
            int iNext = i0;
            Polyline cleanedPolyline = new Polyline { pl[startingIndex] };

            do
            {
                iNext++;

                if (iNext >= pl.Count)
                {
                    iNext = 0;
                }
                if (iNext == startingIndex)
                {
                    //closing 
                    cleanedPolyline.Add(pl[startingIndex]);
                    break;
                }
                bool add = false;
                Vector3d dir = pl[iNext] - pl[i0];
                dir.Unitize();
                if (iNext < i0)
                {
                    for (int i = i0 + 1; i < pl.Count; i++)
                    {
                        Vector3d diff = pl[i] - pl[i0];
                        Vector3d projection = diff * dir * dir;
                        Vector3d ortoProjection = diff - projection;
                        if (ortoProjection.SquareLength > maxDistance * maxDistance)
                        {
                            add = true;
                            break;
                        }
                    }

                    for (int i = 0; i < iNext; i++)
                    {
                        Vector3d diff = pl[i] - pl[i0];
                        Vector3d projection = diff * dir * dir;
                        Vector3d ortoProjection = diff - projection;
                        if (ortoProjection.SquareLength > maxDistance * maxDistance)
                        {
                            add = true;
                            break;
                        }
                    }
                }
                else
                {
                    for (int i = i0 + 1; i < iNext; i++)
                    {
                        Vector3d diff = pl[i] - pl[i0];
                        Vector3d projection = diff * dir * dir;
                        Vector3d ortoProjection = diff - projection;
                        if (ortoProjection.SquareLength > maxDistance * maxDistance)
                        {
                            add = true;
                            break;
                        }
                    }
                }
                if (add)
                {
                    if (iNext == 0)
                        iNext = pl.Count - 1;
                    else
                        iNext -= 1;

                    i0 = iNext;
                    cleanedPolyline.Add(pl[iNext]);
                }
            } while (true == true);

            //RhinoDoc.ActiveDoc.Objects.AddPolyline(cleanedPolyline);

            //adding points between cleaned points
            for (int i = cleanedPolyline.Count - 2; i > -1; i--)
            {
                Point3d origin = cleanedPolyline[i + 1];
                Vector3d diff = cleanedPolyline[i] - origin;
                if (diff.SquareLength > 1.3 * targetLength * 1.3 * targetLength)
                {
                    //adding points
                    double length = diff.Length;
                    int intervals = Convert.ToInt32(length / (1.3 * targetLength));
                    intervals++;
                    double delta = length / intervals;
                    diff.Unitize();

                    for (int j = 1; j < intervals; j++)
                        cleanedPolyline.Insert(i + 1, origin + diff * delta * j);
                }
            }

            //Flattening on plane
            Polyline onPlane = new Polyline();
            for (int i = 0; i < cleanedPolyline.Count; i++)
                onPlane.Add(plane.ClosestPoint(cleanedPolyline[i]));

            //Creating new mesh
            MeshingParameters param = MeshingParameters.Default;
            param.MinimumEdgeLength = 0.8 * targetLength;
            param.MaximumEdgeLength = 0.9 * targetLength;

            Curve perimeter = onPlane.ToPolylineCurve();
            Mesh flattened = Mesh.CreateFromPlanarBoundary(perimeter, param, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);

            //moving to original mesh
            List<Point3d> projecteds = new List<Point3d>();
            foreach (Point3d v in flattened.Vertices)
            {
                Point3d[] buffer = Intersection.ProjectPointsToMeshes(new Mesh[] { mesh }, new Point3d[] { v }, plane.Normal, 0);
                if (buffer.Length == 0)
                {
                    LineCurve lCurve = new LineCurve(v - plane.Normal * 1e10, v + plane.Normal * 1e10);
                    if (!lCurve.ClosestPoints(joineds[0], out Point3d pointOnThis, out Point3d pointOnOther))
                        throw new NotSupportedException();
                    projecteds.Add(pointOnOther);
                }
                else
                {
                    projecteds.Add(buffer[0]);
                }

            }

            for (int i = 0; i < flattened.Vertices.Count; i++)
                flattened.Vertices.SetVertex(i, projecteds[i]);

            flattened.Vertices.CombineIdentical(true, true);
            flattened.Faces.ConvertQuadsToTriangles();
            flattened.Weld(Math.PI);
            flattened.Normals.ComputeNormals();
            flattened.Compact();
            flattened.Smooth(1, true, true, false, true, SmoothingCoordinateSystem.Object);
            flattened.Weld(Math.PI);
            flattened.RebuildNormals();
            flattened.UnifyNormals();
            flattened.Normals.ComputeNormals();

            DA.SetData(0, flattened);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7fd3c82c-a8ea-4d66-9147-c8b722b9b1bb");
    }
}