using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Analysis
{
    /// <summary>
    /// The options of the Form Find, which in FeMM were inputs of the Form Find itself. Same
    /// names and defaults, except the tolerances of the algebraic solver (1e-6, the ALGLIB
    /// default); the Load Cases select the loads to apply.
    /// </summary>
    public class SolverOptionsComponent : GH_Component
    {
        public SolverOptionsComponent()
          : base("Solver Options", "SO", "The options of the Form Find. Without this component the Form Find uses the defaults", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ANALYSIS)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            var defaults = new SolverOptionsModel();
            pManager.AddNumberParameter("Gravity", "G", "The gravity acceleration for self-weight calculation purpose, in model units (9806.65 in N/mm). It applies to the beams and to the plates with a Plate Property", GH_ParamAccess.item, defaults.Gravity);
            pManager.AddNumberParameter("Pressure", "P", "The normal pressure applied to the model", GH_ParamAccess.item, defaults.Pressure);
            int i = pManager.AddGenericParameter("Pressure Load Case", "LC", "The load case used for the resulting pressure forces", GH_ParamAccess.item);
            pManager[i].Optional = true;
            i = pManager.AddGenericParameter("Load Cases", "LCs", "The load cases of the node and beam loads to apply. If empty, all the loads", GH_ParamAccess.list);
            pManager[i].Optional = true;
            pManager.AddIntegerParameter("Max Iter", "MI", "Max number of iterations in solution", GH_ParamAccess.item, defaults.MaxIterations);
            pManager.AddNumberParameter("Tolerance", "T", "Tolerance on relative error during solution", GH_ParamAccess.item, defaults.Tolerance);
            pManager.AddIntegerParameter("Max Iter,s", "MI,s", "Max number of iterations for the algebric solver (0: no limit)", GH_ParamAccess.item, defaults.SolverMaxIterations);
            pManager.AddNumberParameter("ToleranceA,s", "TA,s", "Tolerance A for the algebric solver: relative residual of the conjugate gradient (Linear); least squares residual |A^T r|/(|A| |r|) of LSQR (Non-Linear)", GH_ParamAccess.item, defaults.SolverToleranceA);
            pManager.AddNumberParameter("ToleranceB,s", "TB,s", "Tolerance B for the algebric solver: LSQR (Non-Linear) stops when |r| <= B |b|", GH_ParamAccess.item, defaults.SolverToleranceB);
            pManager.AddBooleanParameter("Create mesh", "CM", "Automatically create the plates and the mesh", GH_ParamAccess.item, defaults.CreateMesh);
            i = pManager.AddGenericParameter("Plate Property", "PP", "The property of the plates generated from the beams: their weight (with Gravity) and the plates of the result. Create mesh must be set to true", GH_ParamAccess.item);
            pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Options", "O", "The options for the Form Find", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var options = new SolverOptionsModel();
            double gravity = 0, pressure = 0, tolerance = 0, solverToleranceA = 0, solverToleranceB = 0;
            int maxIterations = 0, solverMaxIterations = 0;
            bool createMesh = true;
            GH_LoadCase loadCase = null;
            var loadCases = new List<GH_LoadCase>();
            GH_PlateProperty plateProperty = null;

            if (!DA.GetData(0, ref gravity) || !DA.GetData(1, ref pressure) || !DA.GetData(4, ref maxIterations) || !DA.GetData(5, ref tolerance) ||
                !DA.GetData(6, ref solverMaxIterations) || !DA.GetData(7, ref solverToleranceA) || !DA.GetData(8, ref solverToleranceB) || !DA.GetData(9, ref createMesh))
                return;
            DA.GetData(2, ref loadCase);
            DA.GetDataList(3, loadCases);
            DA.GetData(10, ref plateProperty);

            if (maxIterations < 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Max Iter must be at least 1");
                return;
            }
            if (tolerance < 0 || solverMaxIterations < 0 || solverToleranceA < 0 || solverToleranceB < 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Tolerances and Max Iter,s must not be negative");
                return;
            }
            // If the user gives the pressures it must give also the load case
            if (pressure != 0 && loadCase == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Give the Load Case to assign at the pressure forces");
                return;
            }
            if (solverToleranceA == 0 && solverToleranceB == 0 && solverMaxIterations != 0)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, "With the tolerances at 0 the algebraic solver runs until the rounding errors or Max Iter,s stop it");

            options.Gravity = gravity;
            options.Pressure = pressure;
            options.PressureLoadCase = loadCase?.Value;
            options.LoadCases = loadCases.Where(lc => lc?.Value != null).Select(lc => lc.Value).ToList();
            options.MaxIterations = maxIterations;
            options.Tolerance = tolerance;
            options.SolverMaxIterations = solverMaxIterations;
            options.SolverToleranceA = solverToleranceA;
            options.SolverToleranceB = solverToleranceB;
            options.CreateMesh = createMesh;
            options.PlateProperty = plateProperty?.Value;

            DA.SetData(0, new GH_SolverOptions(options));
        }

        protected override System.Drawing.Bitmap Icon => ComponentIcons.Get(GetType().Name);

        public override Guid ComponentGuid => new Guid("4b18d9c1-d734-40c5-99d8-9d53333a76d5");

        public override GH_Exposure Exposure => GH_Exposure.primary;
    }
}
