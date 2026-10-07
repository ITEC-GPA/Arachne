using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using System;

namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionHollowRectComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionHollowRectComponent class.
        /// </summary>
        public BeamSectionHollowRectComponent()
            : base("Hollow Rect", "HR", "Hollow rect beam section", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Height", "D", "The section height", GH_ParamAccess.item);
            pManager.AddNumberParameter("Base", "B", "The section base", GH_ParamAccess.item);
            pManager.AddNumberParameter("Horizontal Thickness", "T1", "The horizontal thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Vertical Thickness", "T2", "The vertical thickness", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "S", "The hollow rect section", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double d = 0, b = 0, t1 = 0, t2 = 0;
            if (!DA.GetData(0, ref d))
                return;
            if (!DA.GetData(1, ref b))
                return;
            if (!DA.GetData(2, ref t1))
                return;
            if (!DA.GetData(3, ref t2))
                return;

            SectionModel sectionModel = new SectionModel()
            {
                SectionType = SectionModel.SectionTypes.HollowRectangle,
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
        public override Guid ComponentGuid => new Guid("c37923d7-be85-4846-92f3-79ae601f4f52");
    }
}