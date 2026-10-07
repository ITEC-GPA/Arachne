using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Arachne.Common.Models;
using Arachne.Grasshopper.Helpers;
using Arachne.Grasshopper.DataTypes;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class FixedLengthComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FixedLengthComponent class.
        /// </summary>
        public FixedLengthComponent()
          : base("Fixed Length", "FL", "The fixed length attribute", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the Fixed Length attribute", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddNumberParameter("Value", "V", "The fixed length value", GH_ParamAccess.item);
            pManager.AddNumberParameter("Weight", "W", "The weight of the fixed length value in the non-linear solver", GH_ParamAccess.item, 1);
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
            double weight = 0;

            if (!DA.GetData("Beam", ref beam))
                return;
            if (!DA.GetData("LoadCase", ref lc))
                return;
            if (!DA.GetData("Value", ref value))
                return;
            DA.GetData("Weight", ref weight);

            if (weight <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Weight parameter must be > 0");
                return;
            }

            BeamModel newBeam = new BeamModel(beam.Value);
            newBeam.Loads.Add(new FixedLengthModel(lc.Value, value, weight));
            DA.SetData(0, new GH_Beam(newBeam, beam.Guid)); // same identity, so Build Model merges it with the other copies
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("cb4a245d-aec7-441d-b7a2-0e98aae9e065");
    }
}