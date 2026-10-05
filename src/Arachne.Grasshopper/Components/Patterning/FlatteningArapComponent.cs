using Arachne.Grasshopper.Helpers;
using System;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class FlatteningArapComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FlatteningComponent class.
        /// </summary>
        public FlatteningArapComponent()
          : base("Flattening ARAP", "FA", "Flatten a mesh in a 2D mesh", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to flatten", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Unwrap Iterations", "UI", "The number of ARAP unwrap iterations", GH_ParamAccess.item, 1000);
            pManager.AddIntegerParameter("Perimeter Iterations", "PI", "The number of Perimeter iterations", GH_ParamAccess.item, 1000);
            pManager.AddNumberParameter("Perimeter Tolerance", "PT", "The error tolerance in perimeter matching (document measure)", GH_ParamAccess.item, 0.5);
            pManager.AddNumberParameter("Angle Tolerance", "AT", "The tolerance to find edges of the mesh", GH_ParamAccess.item, Math.PI * 7 / 8);
            pManager.AddNumberParameter("Convergence Speed", "CS", "The speed of perimeter matching", GH_ParamAccess.item, 0.1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Flatten", "F", "The flatten mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;
            int max_iter = 0;
            int max_perimeter_iter = 0;
            double perimeter_tol = 0;
            double angle_tol = 0;
            double convergence_speed = 0;

            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetData(1, ref max_iter))
                return;
            if (!DA.GetData(2, ref max_perimeter_iter))
                return;
            if (!DA.GetData(3, ref perimeter_tol))
                return;
            if (!DA.GetData(4, ref angle_tol))
                return;
            if (!DA.GetData(5, ref convergence_speed))
                return;

            ARAPHelper.Configure(max_iter, max_perimeter_iter, perimeter_tol, angle_tol, convergence_speed);

            Mesh mesh3D = mesh.Value.DuplicateMesh();
            mesh3D.Faces.ConvertQuadsToTriangles();
            /* 
             mesh3D.Vertices.CombineIdentical(true, true);
             mesh3D.Faces.CullDegenerateFaces();
             mesh3D.UnifyNormals();
             mesh3D.RebuildNormals();
             mesh3D.Compact();
            */

            Mesh flat = mesh3D.DuplicateMesh();

            try
            {
                if (!ARAPHelper.Flattening(mesh3D, flat))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged at max iteration number");
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            flat.Faces.Clear();
            flat.Faces.AddFaces(mesh.Value.Faces);

            flat.RebuildNormals();
            var ret = new GH_Mesh(flat);
            DA.SetData(0, ret);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("FlatteningArapIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("d0b5cbbf-f1c9-5042-9b67-8eeedebb1a69");
    }
}