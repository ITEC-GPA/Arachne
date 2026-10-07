using Arachne.Common.Models;
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

            if (!DA.GetData(0, ref gH_BeamProperty))
                return;

            SectionModel section = gH_BeamProperty.Value;

            string name = string.Empty;
            double area = 0;
            double j11 = 0;
            double j22 = 0;
            double jxx = 0;
            double jyy = 0;
            double w11 = 0;
            double w22 = 0;
            double j = 0;
            double xg = 0;
            double yg = 0;
            double angle = 0;
            double rho1 = 0;
            double rho2 = 0;

            if (section.SectionType != SectionModel.SectionTypes.Generic || section.SectionType != SectionModel.SectionTypes.Composite)
            {
                name = section.Name;
                area = section.SectionArea;
                xg = section.Centroid.X;
                yg = section.Centroid.Y;
                jxx = section.Ixx;
                jyy = section.Iyy;
                angle = section.AngleX1Rad;
                j11 = section.I11;
                j22 = section.I22;
                w11 = section.W11;
                w22 = section.W22;
                rho1 = section.Rho11;
                rho2 = section.Rho22;
                j = section.J;
            }

            if (section.SectionType != SectionModel.SectionTypes.Composite)
            {
                name = section.Name;
                area = section.SectionArea;
                xg = section.Centroid.X;
                yg = section.Centroid.Y;
                jxx = section.Ixx;
                jyy = section.Iyy;
                angle = section.AngleX1Rad;
                j11 = section.I11;
                j22 = section.I22;
                w11 = section.W11;
                w22 = section.W22;
                rho1 = section.Rho11;
                rho2 = section.Rho22;
                j = section.J;
            }

            int item = 0;
            DA.SetData(item++, name);
            DA.SetData(item++, area);
            DA.SetData(item++, xg);
            DA.SetData(item++, yg);
            DA.SetData(item++, jxx);
            DA.SetData(item++, jyy);
            DA.SetData(item++, angle);
            DA.SetData(item++, j11);
            DA.SetData(item++, j22);
            DA.SetData(item++, w11);
            DA.SetData(item++, w22);
            DA.SetData(item++, rho1);
            DA.SetData(item++, rho2);
            DA.SetData(item++, j);
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