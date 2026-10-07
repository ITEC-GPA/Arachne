using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using System;

namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionIComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionIComponent class.
        /// </summary>
        public BeamSectionIComponent()
          : base("I Section", "I", "Defines an I section", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Bottom flange width", "BFW", "The bottom flange width", GH_ParamAccess.item);
            pManager.AddNumberParameter("Top flange width", "TFW", "The top flange width", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "The total section height", GH_ParamAccess.item);
            pManager.AddNumberParameter("Bottom flange thickness", "BFT", "The bottom flange thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Top flange thickness", "TFT", "The top flange thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Web thickness", "WT", "The web thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Radius", "R", "The radius", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "S", "The I section", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double b1 = 0, b2 = 0, d = 0, t1 = 0, t2 = 0, t3 = 0, r = 0;
            if (!DA.GetData(0, ref b1))
                return;
            if (!DA.GetData(1, ref b2))
                return;
            if (!DA.GetData(2, ref d))
                return;
            if (!DA.GetData(3, ref t1))
                return;
            if (!DA.GetData(4, ref t2))
                return;
            if (!DA.GetData(5, ref t3))
                return;
            if (!DA.GetData(6, ref r))
                return;

            SectionModel sectionModel = new SectionModel()
            {
                SectionType = SectionModel.SectionTypes.I,
                D = d,
                B1 = b1,
                B2 = b2,
                T1 = t1,
                T2 = t2,
                T3 = t3,
                R = r,
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
        public override Guid ComponentGuid => new Guid("79118195-ca0f-409f-ac01-55b72161131a");
    }
}