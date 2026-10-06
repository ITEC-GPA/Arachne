using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class FlatteningComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FlatteningComponent class.
        /// </summary>
        public FlatteningComponent()
          : base("Flattening", "F", "Cut the mesh on the geodesics lines and creates the flatten the panels", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PATTERNING)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //pManager.AddMeshParameter("Mesh", "M", "The mesh", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Geodesics", "G", "The geodesics lines for cutting the mesh", GH_ParamAccess.list);
            pManager.AddMeshParameter("Mesh", "M", "The mesh to flatten", GH_ParamAccess.item);
            pManager.AddNumberParameter("Border Weight", "BW", "The weight to assign to the border during the flattening operation", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("Max Iter NL", "MI,NL", "Max number of iterations in non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 20);//reduced at default
            pManager.AddNumberParameter("Tolerance NL", "T NL", "Tolerance on relative error durign non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 0.01);
            pManager.AddIntegerParameter("Max Iter,s", "MI,s", "Max number of iterations for the solver", GH_ParamAccess.item, 10000);
            pManager.AddNumberParameter("Tolerance,s", "T,s", "Tolerance for the solver (0 = automatic, 1e-6)", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            //pManager.AddMeshParameter("Panels 3D", "3D", "The 3D panels", GH_ParamAccess.list);
            //pManager.AddMeshParameter("Panels 2D", "2D", "The flatten panels", GH_ParamAccess.list);
            //pManager.AddVectorParameter("Warp directions", "W", "The warp direction of each panel", GH_ParamAccess.list);
            pManager.AddMeshParameter("Flatten", "F", "The flatten mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;
            double weigth = 0;
            int max_iter = 0;
            double tol = 0;
            int maxIterSolver = 0;
            double epsS = 0;
            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetData(1, ref weigth))
                return;
            if (!DA.GetData(2, ref max_iter))
                return;
            if (!DA.GetData(3, ref tol))
                return;
            if (!DA.GetData(4, ref maxIterSolver))
                return;
            if (!DA.GetData(5, ref epsS))
                return;

            Mesh flat;

            try
            {
                GetFlattenedMesh(mesh.Value, weigth, max_iter, tol, epsS, maxIterSolver, out Solver.ResultCodes ret, out flat);
                switch (ret)
                {
                    case Solver.ResultCodes.Converged:
                    case Solver.ResultCodes.ConvergedUsingSlowMethodWithoutWeigths:
                        {
                            break;
                        }
                    case Solver.ResultCodes.NotconvergedAtMaxIterationNumber:
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged at max iteration number. Test internal lengths and area.");
                            break;
                        }
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            DA.SetData(0, flat);
        }

        internal static void GetFlattenedMesh(Mesh mesh, double borderWeigth, int max_iter, double tol, double epsS, int maxIterSolver, out Solver.ResultCodes ret, out Mesh flat)
            => Services.FlatteningService.GetFlattenedMesh(mesh, borderWeigth, max_iter, tol, epsS, maxIterSolver, out ret, out flat);
        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b8fa603b-1674-573c-9380-450724df4b24");
    }
}
