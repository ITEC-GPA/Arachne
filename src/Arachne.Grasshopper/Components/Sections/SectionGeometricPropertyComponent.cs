using Arachne.Common.Geometry;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using System;

namespace Arachne.Grasshopper.Components.Sections
{
    public class SectionGeometricPropertyComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamPropertyComponent class.
        /// </summary>
        public SectionGeometricPropertyComponent()
          : base("Section Geometric Property", "BGP", "Section geometric properties", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {

        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Section Property", "BP", "Section Property", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Section section name", GH_ParamAccess.item);
            pManager.AddNumberParameter("Area", "A", "Section section area", GH_ParamAccess.item);
            pManager.AddNumberParameter("xg", "xg", "X coordinate of centroid", GH_ParamAccess.item);
            pManager.AddNumberParameter("yg", "yg", "Y coordinate of centroid", GH_ParamAccess.item);
            pManager.AddNumberParameter("Jxx", "Jxx", "Second moment of area  respect x-x", GH_ParamAccess.item);
            pManager.AddNumberParameter("Jyy", "Jyy", "Second moment of area  respect y-y", GH_ParamAccess.item);
            pManager.AddNumberParameter("Angle", "A", "Angle", GH_ParamAccess.item);
            pManager.AddNumberParameter("J₁₁", "J₁₁", "Second moment of area respect 1-1", GH_ParamAccess.item);
            pManager.AddNumberParameter("J₂₂", "J₂₂", "Second moment of area respect 2-2", GH_ParamAccess.item);
            pManager.AddNumberParameter("W₁₁", "W₁₁", "Elastic section modulus respect 1-1", GH_ParamAccess.item);
            pManager.AddNumberParameter("W₂₂", "W₂₂", "Elastic section modulus respect 2-2", GH_ParamAccess.item);
            pManager.AddNumberParameter("ρ₁₁", "ρ₁₁", "Radius of gyration respect 1-1", GH_ParamAccess.item);
            pManager.AddNumberParameter("ρ₂₂", "ρ₂₂", "Radius of gyration respect 2-2", GH_ParamAccess.item);
            pManager.AddNumberParameter("J", "J", "Torsional moment of area", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_BeamSection gH_BeamProperty = null;
            if (!DA.GetData(0, ref gH_BeamProperty) || gH_BeamProperty.Value == null) return;
            var section = gH_BeamProperty.Value;
            // The values declared by the section components (computed as FeMM). The radii of gyration are those of FeMM: computed
            // for the sections given by their outline (with the X-Y moments), 0 for circles and generic sections.
            bool outlined = section.Jxx != 0 || section.Jyy != 0;
            double rho1 = outlined && section.Area > 0 ? Math.Sqrt(section.J11 / section.Area) : 0;
            double rho2 = outlined && section.Area > 0 ? Math.Sqrt(section.J22 / section.Area) : 0;
            int item = 0;
            DA.SetData(item++, string.IsNullOrEmpty(section.Name) ? null : section.Name);
            DA.SetData(item++, section.Area);
            DA.SetData(item++, section.Centroid.X);
            DA.SetData(item++, section.Centroid.Y);
            DA.SetData(item++, section.Jxx);
            DA.SetData(item++, section.Jyy);
            DA.SetData(item++, section.AngleX1);
            DA.SetData(item++, section.J11);
            DA.SetData(item++, section.J22);
            DA.SetData(item++, section.Wel1);
            DA.SetData(item++, section.Wel2);
            DA.SetData(item++, rho1);
            DA.SetData(item++, rho2);
            DA.SetData(item++, section.Jt);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("1da0a1a0-214f-4a76-9f34-b50fadca708d");
    }
}
