using Arachne.Grasshopper.Helpers;
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class MeshRemapComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MeshRemap class.
        /// </summary>
        public MeshRemapComponent()
          : base("Mesh Remap", "MR", "Remap points from a mesh to another", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh A", "MA", "Mesh A", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh B", "MB", "Mesh B", GH_ParamAccess.item);
            pManager.AddGeometryParameter("Geometry A", "GA", "Geometry on mesh A that will be remapped on mesh B", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddGeometryParameter("Geometry B", "GB", "Geometry on mesh B that will be remapped on mesh A", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddIntegerParameter("Curve Samples", "CS", "Number of samples per curve", GH_ParamAccess.item, 100);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance of the mesh closest point operation", GH_ParamAccess.item, 100);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("A to B", "A", "Geometry A mapped to Mesh B", GH_ParamAccess.list);
            pManager.AddGeometryParameter("B to A", "B", "Geometry B mapped to Mesh A", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh meshA = new GH_Mesh();
            GH_Mesh meshB = new GH_Mesh();
            List<IGH_GeometricGoo> geomA = new List<IGH_GeometricGoo>();
            List<IGH_GeometricGoo> geomB = new List<IGH_GeometricGoo>();
            int samples = 100;
            double tol = 100;

            if (!DA.GetData(0, ref meshA))
            {
                return;
            }
            if (!DA.GetData(1, ref meshB))
            {
                return;
            }
            DA.GetDataList(2, geomA);
            DA.GetDataList(3, geomB);

            DA.GetData(4, ref samples);
            DA.GetData(5, ref tol);

            var AtoB = new List<IGH_GeometricGoo>();
            var BtoA = new List<IGH_GeometricGoo>();

            if (meshA.Value.Vertices.Count != meshB.Value.Vertices.Count || meshA.Value.Faces.Count != meshB.Value.Faces.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Mesh A and mesh B have a different number of vertices or faces.");
            }

            for (int i = 0; i < geomA.Count; ++i)
            {
                Remap(meshA, meshB, geomA[i], ref AtoB, samples, tol);
            }

            for (int i = 0; i < geomB.Count; ++i)
            {
                Remap(meshB, meshA, geomB[i], ref BtoA, samples, tol);
            }

            DA.SetDataList(0, AtoB);
            DA.SetDataList(1, BtoA);
        }

        private void Remap(GH_Mesh meshA, GH_Mesh meshB, IGH_GeometricGoo geometry, ref List<IGH_GeometricGoo> outList, int samples, double tol)
        {
            // Remap one point
            GH_Point pt = geometry as GH_Point;
            if (pt != null)
            {
                if (FromAtoB(meshA.Value, meshB.Value, pt.Value, tol, out Point3d newPoint))
                {
                    outList.Add(new GH_Point(newPoint));
                    return;
                }
            }

            // Remap a curve
            GH_Curve crv = geometry as GH_Curve;
            if (crv != null)
            {
                // If samples is set, resample the curve
                if (samples > 0)
                {
                    Polyline poly = new Polyline();
                    double sampleInterval = 1.0 / samples;
                    for (int j = 0; j <= samples; ++j)
                    {
                        if (FromAtoB(meshA.Value, meshB.Value, crv.Value.PointAtNormalizedLength(j * sampleInterval), tol, out Point3d newPoint))
                        {
                            poly.Add(newPoint);
                            continue;
                        }
                    }
                    if (poly.Count > 1)
                    {
                        outList.Add(new GH_Curve(poly.ToPolylineCurve()));
                    }
                }
                // If samples is not set, transpose the curve
                else
                {
                    // Transpose a polyline curve
                    // @TODO: It sometimes exit with an invalid curve
                    PolylineCurve polyC = crv.Value as PolylineCurve;
                    if (polyC != null)
                    {
                        Polyline poly = new Polyline();

                        for (int n = 0; n < polyC.PointCount; ++n)
                        {
                            if (FromAtoB(meshA.Value, meshB.Value, polyC.Point(n), tol, out Point3d newpt))
                            {
                                poly.Add(newpt);
                            }
                        }
                        outList.Add(new GH_Curve(poly.ToPolylineCurve()));
                        return;
                    }

                    // Default case: map start and end point
                    var outLine = new LineCurve();

                    if (FromAtoB(meshA.Value, meshB.Value, crv.Value.PointAtStart, tol, out Point3d newStartPoint) &&
                        FromAtoB(meshA.Value, meshB.Value, crv.Value.PointAtEnd, tol, out Point3d newEndPoint))
                    {
                        outLine.SetStartPoint(newStartPoint);
                        outLine.SetEndPoint(newEndPoint);
                        outList.Add(new GH_Curve(outLine));
                    }
                    return;

                }
            }
        }

        /// <summary>
        /// Map a point from meshA to mesh B.
        /// </summary>
        private bool FromAtoB(Mesh meshA, Mesh meshB, Point3d pt, double tol, out Point3d newPoint)
        {
            newPoint = Point3d.Unset;

            var closest = meshA.ClosestMeshPoint(pt, tol);
            if (closest == null)
                return false;

            var face = meshB.Faces[closest.FaceIndex];
            Point3d pt3d = new Point3d(0, 0, 0);

            pt3d += new Vector3d(meshB.Vertices[face.A]) * closest.T[0];
            pt3d += new Vector3d(meshB.Vertices[face.B]) * closest.T[1];
            pt3d += new Vector3d(meshB.Vertices[face.C]) * closest.T[2];
            pt3d += new Vector3d(meshB.Vertices[face.D]) * closest.T[3];

            newPoint = pt3d;
            return true;
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("MeshRemapIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("fd929959-b16b-5f27-8026-cb7374c3922a");
    }
}