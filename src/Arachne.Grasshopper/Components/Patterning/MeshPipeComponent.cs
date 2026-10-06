using Arachne.Grasshopper.Helpers;
using System;
using System.Collections.Generic;
using System.Threading;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class MeshPipeComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the LineUp class.
        /// </summary>
        public MeshPipeComponent()
          : base("Mesh Pipe", "MP", "Creates a mesh pipe from a line", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddLineParameter("Line", "L", "Line axis", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "R", "Radius of the pipe", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("Edges", "E", "Number of edges of the base", GH_ParamAccess.item, 4);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The resulting mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Line ghline = null;
            double radius = 1;
            int edges = 4;

            if (!DA.GetData(0, ref ghline))
            {
                return;
            }
            DA.GetData(1, ref radius);
            DA.GetData(2, ref edges);

            if (edges < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Too few edges. Running with 3 edges.");

                edges = 3;
            }

            Line line = ghline.Value;
            Vector3d axis = line.To - line.From;

            var worldToStart = Transform.ChangeBasis(new Plane(line.From, axis), Plane.WorldXY);
            var startToEnd = Transform.Translation(axis);

            Point3d[] points = new Point3d[edges];

            double step = 2 * Math.PI / edges;
            double curentAngle = 0.0;
            if (edges % 2 == 0)
            {
                curentAngle = step * 0.5;
            }

            Vector3d refPt = new Vector3d(0, radius, 0);

            for (int i = 0; i < points.Length; i++)
            {
                var newVec = refPt;
                newVec.Rotate(curentAngle, Vector3d.ZAxis);
                points[i] = new Point3d(newVec);
                curentAngle += step;
            }
            Mesh mesh = new Mesh();

            for (int i = 0; i < points.Length; i++)
            {
                points[i].Transform(worldToStart);
                mesh.Vertices.Add(new Point3d(points[i]));
            }
            for (int i = 0; i < points.Length; i++)
            {
                points[i].Transform(startToEnd);
                mesh.Vertices.Add(new Point3d(points[i]));
            }
            for (int i = 0; i < points.Length; i++)
            {
                var a = i;
                var b = (i + 1) % edges;
                var c = b + edges;
                var d = i + edges;
                mesh.Faces.AddFace(a, b, c, d);
            }

            DA.SetData(0, mesh);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("dd2927ec-f681-56e7-ab75-f4916b2528d3");
    }
}