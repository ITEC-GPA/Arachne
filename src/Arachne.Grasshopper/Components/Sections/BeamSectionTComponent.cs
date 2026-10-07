using System;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionTComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionTComponent class.
        /// </summary>
        public BeamSectionTComponent()
          : base("T Section", "T", "Defines a T section", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Flange width", "FW", "The flange width", GH_ParamAccess.item);
            pManager.AddNumberParameter("Flange thickness", "FT", "The flange thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "The total section height", GH_ParamAccess.item);
            pManager.AddNumberParameter("Web thickness", "WT", "The web thickness", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "S", "The T section", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double b = 0, d = 0, t1 = 0, t2 = 0;
            if (!DA.GetData(0, ref b))
                return;
            if (!DA.GetData(1, ref t1))
                return;
            if (!DA.GetData(2, ref d))
                return;
            if (!DA.GetData(3, ref t2))
                return;

            SectionModel sectionModel = new SectionModel()
            {
                SectionType = SectionModel.SectionTypes.T,
                D = d,
                B = b,
                T1 = t1,
                T2 = t2
            };
            sectionModel.CalculateSection();

            GH_BeamSection section = new GH_BeamSection(sectionModel);
            DA.SetData(0, section);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("4573bce8-dc25-4b73-a0d5-cdce1f86846f");
    }
}