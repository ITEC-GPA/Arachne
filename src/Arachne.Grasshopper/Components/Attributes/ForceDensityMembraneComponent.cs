using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class ForceDensityMembraneComponent : GH_Component
    {
        protected Dictionary<int, string> _propertyTypeOptions = new Dictionary<int, string>()
        {
            { 0, "Warp" },
            { 1, "Weft" },
        };

        /// <summary>
        /// Initializes a new instance of the ForceDensityComponent class.
        /// </summary>
        public ForceDensityMembraneComponent()
          : base("Membrane type", "M", "Crate the membrane in force densisty analysis with fixed curvature method", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the Force Density attribute", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddNumberParameter("Value", "V", "The force density value", GH_ParamAccess.item);
            int i = pManager.AddIntegerParameter("Type", "T", "The property type", GH_ParamAccess.item);
            Param_Integer mtParam = pManager[i] as Param_Integer;
            foreach (KeyValuePair<int, string> v in _propertyTypeOptions)
                mtParam.AddNamedValue(v.Value, v.Key);
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
            double value = 0;
            int type = 0;

            if (!DA.GetData("Beam", ref beam))
                return;
            if (!DA.GetData("LoadCase", ref lc))
                return;
            if (!DA.GetData("Value", ref value))
                return;
            if (!DA.GetData("Type", ref type))
                return;

            GH_Beam new_beam = new GH_Beam(beam);
            new_beam.Value.Loads.Add(new ForceDensityModel(lc.Value, value, ForceDensityModel.ForceDensityTypes.Membrane, true, 1.0, value, (ForceDensityModel.MembraneTypes)type));
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
        public override Guid ComponentGuid => new Guid("6176f23d-8401-4c8d-ae54-ed469c846fcf");
    }
}