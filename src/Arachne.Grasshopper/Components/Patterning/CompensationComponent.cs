using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class CompensationComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public CompensationComponent()
          : base("Compensation", "C", "Compensate the flatten patches", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Panel", "P", "The flatten panel to compensate", GH_ParamAccess.item);
            pManager.AddVectorParameter("Warp direction", "W", "The vector of the warp firection of the panel", GH_ParamAccess.item);
            pManager.AddNumberParameter("Wa Scale (%)", "Wa%", "The deformation in the warp direction in %(greater than 0 = reduction)", GH_ParamAccess.item);
            pManager.AddNumberParameter("We Scale (%)", "We%", "The deformation in the weft direction in %(greater than 0 = reduction)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Fixed lengths", "FLs", "The fixed lengths of edges, for example borders, defined with the Fixed compensation component", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddIntegerParameter("Max Iter NL", "MI,NL", "Max number of iterations in non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 20);//reduced at default
            pManager.AddNumberParameter("Tolerance NL", "T NL", "Tolerance on relative error durign non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 0.0001);
            pManager.AddIntegerParameter("Max Iter,s", "MI,s", "Max number of iterations for the solver", GH_ParamAccess.item, 10000);
            pManager.AddNumberParameter("Tolerance,s", "T,s", "Tolerance for the solver", GH_ParamAccess.item, 0.0001);
            //pManager.AddNumberParameter("Top Scale (%)", "T", "The scale factor in the relative X axis of the top side", GH_ParamAccess.item);
            //pManager.AddNumberParameter("Bottom Scale (%)", "B", "The scale factor in the relative X axis of the top side", GH_ParamAccess.item);
            //pManager.AddNumberParameter("Transaction Length", "L", "The transaction length beetweel the vertical and horizontal sides", GH_ParamAccess.item);
            //pManager.AddIntegerParameter("Junction type", "J", "Set the junction type between the top, bottom, left and right side", GH_ParamAccess.item, 0);
            //Param_Integer jParam = pManager[7] as Param_Integer;
            //jParam.AddNamedValue("Arc", 0);
            //jParam.AddNamedValue("Line", 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Panel", "P", "The compensated panel", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh panel = new GH_Mesh();
            Vector3d warp_dir = Vector3d.Unset;
            double waPerc = 0;
            double wePerc = 0;
            int maxIterNL = 0;
            double tol = 0;
            int maxIterSolver = 0;
            double epsS = 0;
            List<GH_ObjectWrapper> fixedBeams = new List<GH_ObjectWrapper>();

            if (!DA.GetData(0, ref panel))
                return;
            if (!DA.GetData(1, ref warp_dir))
                return;
            if (!DA.GetData(2, ref waPerc))
                return;
            if (!DA.GetData(3, ref wePerc))
                return;
            DA.GetDataList(4, fixedBeams);
            if (!DA.GetData(5, ref maxIterNL))
                return;
            if (!DA.GetData(6, ref tol))
                return;
            if (!DA.GetData(7, ref maxIterSolver))
                return;
            if (!DA.GetData(8, ref epsS))
                return;

            try
            {
                var cables = fixedBeams.Select(w => w.Value as Arachne.Core.Cable ?? throw new ArgumentException("Expected Arachne cable constraints.")).ToArray();
                double geometryTolerance = RhinoDoc.ActiveDoc?.ModelAbsoluteTolerance ?? 1e-6;
                DA.SetData(0, Services.CompensationService.Apply(panel.Value, warp_dir, waPerc, wePerc, cables, maxIterNL, tol, maxIterSolver, epsS, geometryTolerance));
            }
            catch (Exception error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, error.Message);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("51dce643-1c37-56ac-9eb1-9641e7bcdf1f");
    }
}
