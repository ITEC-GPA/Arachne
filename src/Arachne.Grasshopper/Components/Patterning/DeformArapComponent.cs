using Arachne.Grasshopper.Helpers;
using Arachne.Arap;
using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class DeformArapComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the UnwrapComponent class.
        /// </summary>
        public DeformArapComponent()
          : base("Deform ARAP", "DA", "Modify a mesh minimizing deformation", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var idx = 0;
            pManager.AddMeshParameter("Mesh", "M", "The mesh to deform", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Iterations", "I", "The number of ARAP iterations", GH_ParamAccess.item, 1000);
            idx = pManager.AddIntegerParameter("Pinned Vertices", "PV", "The indices of the pinned vertices", GH_ParamAccess.list);
            pManager[idx].Optional = true;
            idx = pManager.AddPointParameter("Pin Positions", "PP", "The coordinates of the pinned vertices", GH_ParamAccess.list);
            pManager[idx].Optional = true;
            pManager.AddBooleanParameter("Force 2D", "2D", "Force the deformed mesh in 2D as in flattening", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Deformed", "D", "The deformed mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Status", "S", "ARAP outcome; only Converged publishes a mesh", GH_ParamAccess.item);
            pManager.AddTextParameter("Report", "R", "Solver message and convergence diagnostics", GH_ParamAccess.list);
            pManager.AddNumberParameter("Energy", "E", "Normalized energy per iteration; perimeter updates change the constraints between passes", GH_ParamAccess.list);
            pManager.AddNumberParameter("Edge distortion", "ED", "Maximum absolute fractional edge-length change; 0.02 means 2%", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;
            int max_iter = 0;
            List<int> pins = new List<int>();
            List<Point3d> points = new List<Point3d>();
            bool isUnwrap = false;

            if (!DA.GetData(0, ref mesh))
                return;
            DA.GetData(1, ref max_iter);
            DA.GetDataList(2, pins);
            DA.GetDataList(3, points);
            DA.GetData(4, ref isUnwrap);

            if (mesh?.Value == null || !mesh.Value.IsValid || mesh.Value.Faces.Count == 0 || max_iter <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "A valid mesh and positive iteration count are required.");
                return;
            }

            Mesh mesh3D = mesh.Value.DuplicateMesh();
            mesh3D.Faces.ConvertQuadsToTriangles();

            for (int i = 0; i < pins.Count; i++)
            {
                if (pins[i] < 0 || pins[i] >= mesh.Value.Vertices.Count)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Pinned vertex index is outside the mesh.");
                    return;
                }
            }

            // Preserve the old position-only convention; otherwise let the engine
            // choose one automatic anchor for every disconnected part.
            if (pins.Count == 0 && points.Count > 0) pins.Add(0);

            if (points.Count > pins.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "More pin positions than pinned vertices.");
                return;
            }

            while (points.Count < pins.Count)
            {
                points.Add(mesh.Value.Vertices.Point3dAt(pins[points.Count]));
            }

            // Check duplicate pins
            HashSet<int> checkSet = new HashSet<int>();
            for (int i = 0; i < pins.Count; ++i)
            {
                if (checkSet.Contains(pins[i]))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Found duplicated pins, only the first occurence is used.");
                    pins.RemoveAt(i);
                    points.RemoveAt(i);
                    i--;
                    continue;
                }
                checkSet.Add(pins[i]);
            }

            Mesh deformed = mesh3D.DuplicateMesh();

            try
            {
                var result = ARAPHelper.Deform(mesh3D, deformed, pins, points, isUnwrap, new ArapOptions { MaxIterations = max_iter });
                ARAPHelper.SetDiagnostics(DA, result, false);
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

            deformed.Faces.Clear();
            deformed.Faces.AddFaces(mesh.Value.Faces);

            deformed.RebuildNormals();
            var ret = new GH_Mesh(deformed);
            DA.SetData(0, ret);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("DeformIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("e56e8131-f146-50bd-b8a8-e083e2c93558");
    }
}
