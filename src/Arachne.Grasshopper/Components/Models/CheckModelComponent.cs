using System;
using Arachne.Common.Helpers;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Models
{
    /// <summary>
    /// Finds the model errors that make the form finding fail, before running it.
    /// </summary>
    public class CheckModelComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CheckModelComponent class.
        /// </summary>
        public CheckModelComponent()
          : base("Check Model", "CkM", "Check a model before the form finding: supports, connectivity and force densities", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MODEL_BUILD)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "The FEM model", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddBooleanParameter("Valid", "V", "True if the model has no errors", GH_ParamAccess.item);
            pManager.AddTextParameter("Errors", "E", "The problems that make the form finding fail", GH_ParamAccess.list);
            pManager.AddTextParameter("Warnings", "W", "The probable mistakes", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Parts", "P", "The number of groups of nodes connected by beams that carry a force", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model model = null;
            if (!DA.GetData(0, ref model) || model?.Value == null)
                return;

            ModelChecks.Report report = ModelChecks.Check(model.Value, Helpers.ModelHelper.GetTolerance());
            foreach (string error in report.Errors)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, error);
            foreach (string warning in report.Warnings)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, warning);

            DA.SetData(0, report.IsValid);
            DA.SetDataList(1, report.Errors);
            DA.SetDataList(2, report.Warnings);
            DA.SetData(3, report.Parts);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("6b9c4718-1b7d-485e-9293-31637d077e35");
    }
}
