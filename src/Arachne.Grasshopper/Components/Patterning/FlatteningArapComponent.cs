using Arachne.Grasshopper.Helpers;
using Arachne.Arap;
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
            pManager.AddTextParameter("Status", "S", "ARAP outcome; only Converged publishes a mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "R", "Solver message and convergence diagnostics", GH_ParamAccess.list);
            pManager.AddNumberParameter("Energy", "E", "Normalized energy per iteration; perimeter updates change the constraints between passes", GH_ParamAccess.list);
            pManager.AddNumberParameter("Edge distortion", "ED", "Maximum absolute fractional edge-length change; 0.02 means 2%", GH_ParamAccess.item);
            pManager.AddNumberParameter("Perimeter error", "PE", "Maximum side-length mismatch in mesh units after the last perimeter pass", GH_ParamAccess.item);
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

            if (mesh?.Value == null || !mesh.Value.IsValid || mesh.Value.Faces.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A valid mesh is required.");
                return;
            }
            Mesh mesh3D = mesh.Value.DuplicateMesh();
            mesh3D.Faces.ConvertQuadsToTriangles();

            Mesh flat = mesh3D.DuplicateMesh();

            try
            {
                var result = ARAPHelper.Flattening(mesh3D, flat, new ArapOptions {
                    MaxIterations = max_iter, MaxPerimeterIterations = max_perimeter_iter,
                    PerimeterTolerance = perimeter_tol, CornerAngle = angle_tol, PerimeterStep = convergence_speed });
                ARAPHelper.SetDiagnostics(DA, result, true);
                if (!result.Success)
                {
                    AddRuntimeMessage(ARAPHelper.FailureLevel(result), result.Message);
                    return;
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
