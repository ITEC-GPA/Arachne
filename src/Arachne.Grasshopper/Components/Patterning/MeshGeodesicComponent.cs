using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class MeshGeodesicComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MeshGeodesicComponent class.
        /// </summary>
        public MeshGeodesicComponent()
          : base("Mesh Geodesic", "MG", "Create a geodesic line in a mesh", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to divide", GH_ParamAccess.item);
            pManager.AddPointParameter("From", "F", "From point", GH_ParamAccess.list);
            pManager.AddPointParameter("To", "To", "To point", GH_ParamAccess.list);
            pManager.AddNumberParameter("Resolution", "R", "The resolution for generating the geodesics lines", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("Iterations", "I", "The number of iteration for the geodesic calculation", GH_ParamAccess.item, 1000);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Geodesics", "G", "The resulting geodesics curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;

            List<Point3d> from = new List<Point3d>();
            List<Point3d> to = new List<Point3d>();

            double res = 0;
            int steps = 0;

            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetDataList(1, from))
                return;
            if (!DA.GetDataList(2, to))
                return;
            if (!DA.GetData(3, ref res))
                return;
            if (!DA.GetData(4, ref steps))
                return;

            if (from.Count != to.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "From and To point lists have not the same size");
            }


            List<Curve> curves = new List<Curve>();

            for (int c = 0; c < Math.Min(from.Count, to.Count); ++c)
            {
                Point3d pt1 = from[c];
                Point3d pt2 = to[c];
                //pt1.Z = 0;
                //pt2.Z = 0;
                if (res == 0)
                    res = pt1.DistanceTo(pt2) / 10.0;

                double stepsize = 0.5;
                Vector3d normal = mesh.Value.NormalAt(mesh.Value.ClosestMeshPoint(0.5 * (pt1 + pt2), 0));
                Plane plane = new Plane(pt1, pt2, pt1 + 100 * normal);
                Polyline[] pls = Intersection.MeshPlane(mesh.Value, plane);

                if (pls == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The line doesn't intersect the mesh.");
                    return;
                }

                List<PolylineCurve> plCurves = new List<PolylineCurve>(pls.Length);
                for (int i = 0; i < pls.Length; i++)
                {
                    Polyline pl = pls[i];
                    plCurves.Add(pl.ToPolylineCurve());
                }

                Curve[] buffer = plCurves.ToArray();
                for (int i = 0; i < 3; i++)
                {
                    buffer = Curve.JoinCurves(buffer, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
                }

                List<Polyline> plsList;
                plsList = new List<Polyline>();
                for (int i = 0; i < buffer.Length; i++)
                {
                    PolylineCurve plc = (PolylineCurve)buffer[i];

                    if (plc == null)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Joined line is not a polyline.");
                        return;
                    }

                    Polyline pl = plc.ToPolyline();
                    if (pl.ClosestPoint(pt1).DistanceTo(pt1) < 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance &&
                        pl.ClosestPoint(pt2).DistanceTo(pt2) < 100 * RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
                    {
                        plsList.Add(pl);
                    }

                }

                //Brep b = Brep.CreateFromMesh(mesh.Value, true);
                for (int i = 0; i < plsList.Count; i++)
                {
                    Polyline pl = plsList[i];
                    if (pl.First.DistanceTo(pt1) > pl.Last.DistanceTo(pt1))
                        pl.Reverse();

                    Arachne.Grasshopper.Geometry.MeshGeodesic mg = new Arachne.Grasshopper.Geometry.MeshGeodesic(mesh.Value, pl.First, pl.Last, res, stepsize, steps);
                    curves.Add(ProjectPolylineToMesh(mg.Polyline, mesh.Value));
                }
            }
            DA.SetDataList(0, curves);
        }

        internal static PolylineCurve ProjectPolylineToMesh(Polyline polyline, Mesh m)
        {
            PolylineCurve buffer = polyline.ToPolylineCurve().PullToMesh(m, RhinoDoc.ActiveDoc.ModelAbsoluteTolerance);
            Polyline result = new Polyline { buffer.PointAtStart };

            for (int i = 1; i < buffer.PointCount - 1; i++)
            {
                MeshPoint p = m.ClosestMeshPoint(buffer.Point(i), 0);
                if (p.ComponentIndex.ComponentIndexType == ComponentIndexType.MeshTopologyEdge)
                {
                    result.Add(p.Point);
                }
            }
            result.Add(buffer.PointAtEnd);
            return result.ToPolylineCurve();
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("MeshGeodesicIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("fe0db245-06c9-5b1c-92bd-6b7c8bff360d");
    }
}
