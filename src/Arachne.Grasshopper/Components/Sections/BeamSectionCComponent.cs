using System;
using System.Collections.Generic;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionCComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionCComponent class.
        /// </summary>
        public BeamSectionCComponent()
          : base("C Section", "C", "Defines a C section", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Width", "W", "The section width", GH_ParamAccess.item);
            pManager.AddNumberParameter("Flange thickness", "FT", "The flange thickness", GH_ParamAccess.item);
            pManager.AddNumberParameter("Height", "H", "The total section height", GH_ParamAccess.item);
            pManager.AddNumberParameter("Web thickness", "WT", "The web thickness", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Double Section", "DS", "The section is composed by 2 mirrored channels", GH_ParamAccess.item, false);
            pManager.AddNumberParameter("Gap", "G", "The gap from the 2 mirrored sections", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "S", "The C section", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            double b = 0, d = 0, t1 = 0, t2 = 0;
            bool mirrored = false;
            double gap = 0;
            if (!DA.GetData(0, ref b))
                return;
            if (!DA.GetData(1, ref t1))
                return;
            if (!DA.GetData(2, ref d))
                return;
            if (!DA.GetData(3, ref t2))
                return;
            DA.GetData(4, ref mirrored);
            DA.GetData(5, ref gap);

            SectionModel sectionModel = new SectionModel()
            {
                SectionType = SectionModel.SectionTypes.C,
                D = d,
                B = b,
                T1 = t1,
                T2 = t2
            };
            sectionModel.CalculateSection();

            if (mirrored)
            {
                sectionModel.Mirror = SectionModel.MirrorTypes.Left;
                sectionModel.MirrorGapA = gap;
            }

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
        public override Guid ComponentGuid => new Guid("d915ff63-5959-4f99-ac4a-8c3d7fb20713");
    }
}