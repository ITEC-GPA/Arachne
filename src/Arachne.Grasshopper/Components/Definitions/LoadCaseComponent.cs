using System;
using System.Collections.Generic;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

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
            pManager.AddBooleanParameter("Gravity NSM", "NSM", "Apply gravity to non-structural mass", GH_ParamAccess.item, false);

            //pManager.AddGenericParameter("Stages", "Ss", "Stages for construction stage analysis SAP2000", GH_ParamAccess.list);
            //pManager[pManager.ParamCount - 1].Optional = true;
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
            bool nsm = false;
            List<GH_LoadCaseStage> stages = new List<GH_LoadCaseStage>();

            if (!DA.GetData(0, ref name))
                return;
            if (!DA.GetData(1, ref nsm))
                return;

            // DA.GetDataList(2, stages);

            LoadCaseModel lc;
            if (name == LoadCaseModel.Dead.Name)
            {
                if (nsm)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid nsm");
                    return;
                }
                lc = new LoadCaseModel(LoadCaseModel.Dead);
            }
            else
            {
                lc = new LoadCaseModel("", name);
                if (nsm)
                {
                    lc.GlobalInertiaLoad = LoadCaseModel.GlobalInertiaLoads.Gravity;
                    lc.GravityDirection = LoadCaseModel.GravityDirections.Z;
                    lc.GravityValue = -ModelModel.GRAVITY_mmSuS2;
                    lc.StructuralMassAcceleration = false;
                    lc.NonStructuralMassAcceleration = true;
                }
            }

            Message = name;

            if (stages.Count > 0)
            {
                lc.Stages = new List<LoadCaseModel.Stage>();
                foreach (GH_LoadCaseStage s in stages)
                {
                    lc.Stages.Add(new LoadCaseModel.Stage(s.Value));
                }
            }

            DA.SetData(0, new GH_LoadCase(lc));
        }

        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        public override Guid ComponentGuid => new Guid("d36a3ace-b39c-450c-b059-e743137cde26");
    }
}