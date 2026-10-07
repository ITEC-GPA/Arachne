using System;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Definitions
{
    public class LoadCaseComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the LoadCaseComponent class.
        /// </summary>
        public LoadCaseComponent()
          : base("Load Case", "LC", "A load case definition", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_DEFINITIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The load case name", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("LoadCase", "LC", "The defined load case", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";

            if (!DA.GetData(0, ref name))
                return;

            LoadCaseModel lc = name == LoadCaseModel.Dead.Name ? new LoadCaseModel(LoadCaseModel.Dead) : new LoadCaseModel("", name);

            Message = name;

            DA.SetData(0, new GH_LoadCase(lc));
        }

        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        // New GUID: the Gravity NSM input of FeMM was removed
        public override Guid ComponentGuid => new Guid("dd9360b8-20ab-4a62-aa15-bbad93f0f9b6");
    }
}
