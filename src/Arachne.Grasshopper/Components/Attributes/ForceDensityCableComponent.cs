using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class ForceDensityCableComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ForceDensityComponent class.
        /// </summary>
        public ForceDensityCableComponent()
          // FCC: FeMM used FC, the nickname of Fixed compensation too
          : base("Fixed curvature cable", "FCC", "Fix the chord and the arrow ratio in force densisty analysis", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the Force Density attribute", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddNumberParameter("Chord", "C", "The chord length", GH_ParamAccess.item);
            pManager.AddNumberParameter("1/Ratio", "1/R", "The denominator to determine the arrow of the curve", GH_ParamAccess.item);
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
            double chord = 0;
            double ratio = 0;

            if (!DA.GetData("Beam", ref beam))
                return;
            if (!DA.GetData("LoadCase", ref lc))
                return;
            if (!DA.GetData("Chord", ref chord))
                return;
            if (!DA.GetData(3, ref ratio))
                return;

            GH_Beam new_beam = new GH_Beam(beam);
            new_beam.Value.Loads.Add(new ForceDensityModel(lc.Value, ratio, ForceDensityModel.ForceDensityTypes.Curvature, true, 1.0, chord, ForceDensityModel.MembraneTypes.Undefined));
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
        public override Guid ComponentGuid => new Guid("92d964b3-ad4d-4c30-a1c6-8e37ac0a7b85");
    }
}