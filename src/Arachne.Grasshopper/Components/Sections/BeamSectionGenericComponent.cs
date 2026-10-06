using System;
using System.Collections.Generic;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionGenericComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionGenericComponent class.
        /// </summary>
        public BeamSectionGenericComponent()
          : base("Generic Section", "G", "Defines a generic section", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Area", "SA", "Section area [L²]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Second moment 1", "I11", "Second moment of area about the principal 1 axis [L⁴]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Second moment 2", "I22", "Second moment of area about the principal 2 axis [L⁴]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Torsion constant", "J", "The torsional constant. [L⁴]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Shear L1", "SL1", "Shear centre offset in the principal 1 axis direction [L]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Shear L2", "SL2", "Shear centre offset in the principal 2 axis direction [L]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Shear A1", "SA1", "Shear area in the principal 1 axis direction. [L²]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Shear A2", "SA2", "Shear area in the principal 2 axis direction. [L²]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Bending modulus 1", "S11", "The section modulus for bending about the local 1 axis. [L³]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Bending modulus 2", "S22", "The section modulus for bending about the local 2 axis. [L³]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Offset 1", "O1", "Centroid offset in the principal 1 axis direction", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Offset 2", "O2", "Centroid offset in the principal 2 axis direction", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Angle", "A", "Principal axis 1 angle w.r.t. the local section coordinates", GH_ParamAccess.item, 0);
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
            double area = 0, j = 0;
            double i11 = 0, i22 = 0;
            double sl1 = 0, sl2 = 0;
            double sa1 = 0, sa2 = 0;
            double w11 = 0, w22 = 0;
            double x = 0, y = 0;
            double ang = 0;
            if (!DA.GetData(0, ref area))
                return;
            if (!DA.GetData(1, ref i11) || !DA.GetData(2, ref i22))
                return;
            if (!DA.GetData(3, ref j))
                return;
            if (!DA.GetData(4, ref sl1) || !DA.GetData(5, ref sl2))
                return;
            if (!DA.GetData(6, ref sa1) || !DA.GetData(7, ref sa2))
                return;
            if (!DA.GetData(8, ref w11) || !DA.GetData(9, ref w22))
                return;
            if (!DA.GetData(10, ref x) || !DA.GetData(11, ref y))
                return;
            if (!DA.GetData(12, ref ang))
                return;

            SectionModel sectionModel = new SectionModel()
            {
                SectionType = SectionModel.SectionTypes.Generic,
                SectionArea = area,
                I11 = i11,
                I22 = i22,
                J = j,
                ShearL1 = sl1,
                ShearL2 = sl2,
                ShearA1 = sa1,
                ShearA2 = sa2,
                W11 = w11,
                W22 = w22,
                Centroid = new Point2d(x, y),
                AngleX1Rad = ang
            };

            GH_BeamSection section = new GH_BeamSection(sectionModel);
            DA.SetData(0, section);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("794e34ed-861f-4f67-8466-cf74fda4d459");
    }
}