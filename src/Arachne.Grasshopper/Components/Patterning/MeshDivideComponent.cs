using Arachne.Grasshopper.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class MeshDivideComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MeshDivideComponent class.
        /// </summary>
        public MeshDivideComponent()
          : base("Mesh Divide", "MD", "Divide a mesh generating geodesics curve", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to divide", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Number", "N", "The number of parts to divide", GH_ParamAccess.item);
            pManager.AddNumberParameter("Rotation", "D", "The rotation angle in degrees", GH_ParamAccess.item, 0);
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
            int numb = 0;
            double ang = 0;
            double res = 0;
            int steps = 0;

            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetData(1, ref numb))
                return;
            DA.GetData(2, ref ang);
            DA.GetData(3, ref res);
            if (!DA.GetData(4, ref steps))
                return;

            ang = RhinoMath.ToRadians(ang);

            // Find the rotated reference vectors
            BoundingBox bb = mesh.Value.GetBoundingBox(true);
            Polyline rect = new Polyline
            {
                bb.Min,
                { bb.Max.X, bb.Min.Y, 0 },
                { bb.Max.X, bb.Max.Y, 0 },
                { bb.Min.X, bb.Max.Y, 0 },
                bb.Min
            };
            Transform xform = Transform.Rotation(ang, bb.Min);
            rect.Transform(xform);
            Point3d o = rect.BoundingBox.Min;
            Vector3d v_x = new Vector3d(rect.BoundingBox.Max.X - rect.BoundingBox.Min.X, 0, 0);
            Vector3d v_y = new Vector3d(0, rect.BoundingBox.Max.Y - rect.BoundingBox.Min.Y, 0);

            xform = Transform.Rotation(-ang, bb.Min);
            o.Transform(xform);
            v_x.Transform(xform);
            v_y.Transform(xform);

            if (res == 0)
                res = v_x.Length / 10.0;
            double stepsize = 0.5;

            List<Curve> curves = new List<Curve>();
            Vector3d v = v_x;
            v.Unitize();
            double step = v_x.Length / numb;
            Brep b = Brep.CreateFromMesh(mesh.Value, true);

            for (int i = 1; i < numb; i++)
            {
                Point3d p1 = o + v * (step * i);
                Point3d p2 = p1 + v_y;

                Plane plane = new Plane(p1, v_y, Vector3d.ZAxis);
                Polyline[] pls = Intersection.MeshPlane(mesh.Value, plane);
                foreach (Polyline pl in pls)
                {
                    Line line = new Line(pl.First(), pl.Last);
                    Arachne.Grasshopper.Geometry.MeshGeodesic mg =
                        new Arachne.Grasshopper.Geometry.MeshGeodesic(mesh.Value, pl.First(), pl.Last, res, stepsize, steps);
                    curves.Add(MeshGeodesicComponent.ProjectPolylineToMesh(mg.Polyline, mesh.Value));
                }
            }

            DA.SetDataList(0, curves);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1af1264d-127a-5fc0-a8fc-93713840902e");
    }
}
