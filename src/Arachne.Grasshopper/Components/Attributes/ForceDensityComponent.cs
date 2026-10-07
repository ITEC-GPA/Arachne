using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class ForceDensityComponent : GH_Component
    {
        protected Dictionary<int, string> _propertyTypeOptions = new Dictionary<int, string>()
        {
            { 0, "Force Density" },
            { 1, "Cable" },
            { 2, "Membrane" }
        };

        /// <summary>
        /// Initializes a new instance of the ForceDensityComponent class.
        /// </summary>
        public ForceDensityComponent()
          : base("Force Density", "FD", "A force densisty load", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the Force Density attribute", GH_ParamAccess.item);
            int i = pManager.AddIntegerParameter("Type", "T", "The property type", GH_ParamAccess.item);
            Param_Integer mtParam = pManager[i] as Param_Integer;
            foreach (KeyValuePair<int, string> v in _propertyTypeOptions)
                mtParam.AddNamedValue(v.Value, v.Key);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddNumberParameter("Value", "V", "The force density value", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Project", "P", "If true, use the length projected on the xy plane, else use the real length", GH_ParamAccess.item);
            int j = pManager.AddNumberParameter("Weight", "W", "The weight of the force density value in the non-linear solver", GH_ParamAccess.item, 1.0);
            pManager[j].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The modified beam", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Beam beam = null;
            GH_LoadCase lc = null;
            int type = 0;
            double value = 0;
            double weight = 0;
            bool project = false;

            if (!DA.GetData("Beam", ref beam))
                return;
            if (!DA.GetData("Type", ref type))
                return;
            if (!DA.GetData("LoadCase", ref lc))
                return;
            if (!DA.GetData("Value", ref value))
                return;
            if (!DA.GetData("Project", ref project))
                return;
            DA.GetData("Weight", ref weight);

            GH_Beam new_beam = new GH_Beam(beam);
            new_beam.Value.Loads.Add(new ForceDensityModel(lc.Value, value, (ForceDensityModel.ForceDensityTypes)type, project, weight, 0, ForceDensityModel.MembraneTypes.Undefined));
            DA.SetData(0, new_beam);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("969824ee-693f-42be-977b-4f977b7af300");
    }
}