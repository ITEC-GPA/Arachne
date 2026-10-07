using System;
using System.Collections.Generic;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace Arachne.Grasshopper.Components.ElementProperties
{
    public class PlatePropertyComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PlatePropertyComponent class.
        /// </summary>
        public PlatePropertyComponent()
          : base("Plate Property", "PP", "Plate properties", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PROPERTIES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Property name", GH_ParamAccess.item);
            int i = pManager.AddIntegerParameter("Type", "T", "The property type", GH_ParamAccess.item);
            Param_Integer mtParam = pManager[i] as Param_Integer;
            Array values = Enum.GetValues(typeof(PlatePropertyType));
            foreach (PlatePropertyType val in values)
            {
                if (val != PlatePropertyType.Undefined)
                    mtParam.AddNamedValue(val.GetDescription(), (int)val);
            }
            i = pManager.AddNumberParameter("Membrane Thickness", "MT", "The membrane thickness value", GH_ParamAccess.item);
            pManager[i].Optional = true;
            i = pManager.AddNumberParameter("Bending Thickness", "BT", "The bending thickness value", GH_ParamAccess.item, 0);
            pManager[i].Optional = true;
            i = pManager.AddGenericParameter("Material", "M", "Material", GH_ParamAccess.item);
            pManager[i].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Property", "PP", "Plate property", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int type = 0;
            double mem_thick = 0;
            double ben_thick = 0;
            GH_Material material = null;
            if (!DA.GetData("Name", ref name))
                return;
            if (!DA.GetData("Type", ref type))
                return;
            DA.GetData("Membrane Thickness", ref mem_thick);
            DA.GetData("Bending Thickness", ref ben_thick);
            bool has_material = DA.GetData("Material", ref material);

            if (type != 5)
            {
                bool missing_values = false;
                if (mem_thick == 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter Membrane Thickness failed to collect data");
                    missing_values = true;
                }
                if (!has_material)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter Material failed to collect data");
                    missing_values = true;
                }
                if (missing_values)
                    return;
            }

            PlatePropertyModel pp = new PlatePropertyModel
            {
                Name = name,
                Material = material?.Value,
                PropertyType = (PlatePropertyType)type,
                MembraneThickness = mem_thick,
                BendingThickness = ben_thick
            };

            Message = pp.PropertyType.GetDescription();

            GH_PlateProperty gh_pp = new GH_PlateProperty(pp);
            DA.SetData(0, gh_pp);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("aa289e2e-280d-4984-ab23-8baabcbddc03");
    }
}