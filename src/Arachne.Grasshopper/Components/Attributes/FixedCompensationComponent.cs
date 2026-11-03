using System;
using System.Collections.Generic;
using Arachne.Common.Analysis;
using GPC.Model.Analysis;
using GPC.Model.Attributes;
using GPC.Model.Core;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class FixedCompensationComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FixedLengthComponent class.
        /// </summary>
        public FixedCompensationComponent()
          : base("Fixed compensation", "FC", "The fixed compensation attribute: a fixed length equal to the length of the beam reduced by C%. The Compensation keeps it on the edges of the flat panel with the same end points; in the Form Find it is a fixed length", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the Fixed Length attribute", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddNumberParameter("Compensation(%)", "C%", "The fixed deformation value in % (greater than 0 = reduction)", GH_ParamAccess.item);
            pManager.AddNumberParameter("Weight", "W", "The weight of the compensated length in the Compensation (and in the non-linear Form Find), against the other edges (weight 1)", GH_ParamAccess.item, 1);
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
            double value = 0, weight = 0;
            if (!DA.GetData(0, ref beam)) return;
            if (!DA.GetData(1, ref lc)) return;
            if (!DA.GetData(2, ref value)) return;
            DA.GetData(3, ref weight);
            if (weight <= 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Weight parameter must be > 0"); return; }
            // > 0: reduction of the beam length
            double newLength = beam.PointFrom.DistanceTo(beam.PointTo) * (1 - value / 100);
            var goal = new FormFindingGoal { Kind = FormFindingGoalKind.Length, Target = newLength, Weight = weight };
            // Same identity, so Build Model merges it with the other copies
            var copy = new GH_Beam(beam);
            copy.Value.Attributes.Add(new FormFindingGoalAttribute(goal, ModelValues.CopyValue(lc.Value)));
            DA.SetData(0, copy);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("2a20c8a1-9837-4b21-a0ec-70d22e92bb6e");
    }
}
