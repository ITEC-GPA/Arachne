using System;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionHollowCircleComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionHollowCircleComponent class.
        /// </summary>
        public BeamSectionHollowCircleComponent()
          : base("Hollow Circle", "HC", "Hollow circle beam section", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Diameter", "D", "The section diameter", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness", "T", "The section thickness", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "S", "The hollow circular section", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double d = 0, t = 0;
            if (!DA.GetData(0, ref d))
                return;
            if (!DA.GetData(1, ref t))
                return;

            SectionModel sectionModel = new SectionModel()
            {
                SectionType = SectionModel.SectionTypes.HollowCircle,
                D = d,
                T = t
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
        public override Guid ComponentGuid => new Guid("540b17d6-3c75-457f-8e5b-6b76f349b363");
    }
}