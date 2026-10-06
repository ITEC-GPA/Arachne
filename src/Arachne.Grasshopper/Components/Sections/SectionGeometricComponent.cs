using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using System;

namespace Arachne.Grasshopper.Components.Sections
{
    public class SectionGeometricComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamPropertyComponent class.
        /// </summary>
        public SectionGeometricComponent()
          : base("Section Geometry", "SG", "Section geometries", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
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
            pManager.AddTextParameter("Type", "T", "Section type", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_T2", "ME_T2", "ME_T2", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_T3", "ME_T3", "ME_T3", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_T2B", "ME_T2B", "ME_T2B", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_T3B", "ME_T3B", "ME_T3B", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_T2E", "ME_T2E", "ME_T2E", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_T3E", "ME_T3E", "ME_T3E", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_DIS", "ME_DIS", "ME_DIS", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_R1", "ME_R1", "ME_R1", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_R2", "ME_R2", "ME_R2", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_TF", "ME_TF", "ME_TF", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_TW", "ME_TW", "ME_TW", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_TFB", "ME_TFB", "ME_TFB", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_TWB", "ME_TWB", "ME_TWB", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_TFE", "ME_TFE", "ME_TFE", GH_ParamAccess.item);
            pManager.AddNumberParameter("ME_TWE", "ME_TWE", "ME_TWE", GH_ParamAccess.item);
            pManager.AddNumberParameter("R_i", "R_i", "R_i", GH_ParamAccess.item);
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
            string type = string.Empty;
            double ME_T2 = 0;
            double ME_T3 = 0;
            double ME_T2B = 0;
            double ME_T3B = 0;
            double ME_T2E = 0;
            double ME_T3E = 0;
            double ME_DIS = 0;
            double ME_R1 = 0;
            double ME_R2 = 0;
            double ME_TF = 0;
            double ME_TW = 0;
            double ME_TFB = 0;
            double ME_TWB = 0;
            double ME_TFE = 0;
            double ME_TWE = 0;
            double R_i = 0;

            if (section.SectionType != SectionModel.SectionTypes.Generic)
            {
                name = section.Name;
                if (section.SectionType == SectionModel.SectionTypes.SolidCircle)
                {
                    type = "SolidCircle";
                    ME_T3 = section.D;
                }
                else if (section.SectionType == SectionModel.SectionTypes.SolidRectangle)
                {
                    name = section.Name;
                    type = "SolidRectangle";
                    ME_T2 = section.B;
                    ME_T3 = section.D;
                }
                else if (section.SectionType == SectionModel.SectionTypes.I)
                {
                    name = section.Name;
                    type = "ISection";
                    ME_T2 = section.B1;
                    ME_T3 = section.D;
                    if (section.B2 != section.B1)
                        ME_T2B = section.B2;
                    ME_R1 = section.R;
                    ME_TF = section.T1;
                    if (section.T2 != section.T1)
                        ME_TFB = section.T2;
                    ME_TW = section.T3;
                    R_i = section.R;
                }
                else if (section.SectionType == SectionModel.SectionTypes.T)
                {
                    name = section.Name;
                    type = "TSection";
                    ME_T2 = section.B;
                    ME_T3 = section.D;
                    ME_TF = section.T1;
                    ME_TW = section.T2;
                    ME_R1 = section.R;
                }
                else if (section.SectionType == SectionModel.SectionTypes.Angle)
                {
                    name = section.Name;
                    ME_T2 = section.B;
                    ME_T3 = section.D;
                    ME_TF = section.T1;
                    ME_TW = section.T2;
                    ME_R1 = section.R;

                    if (section.Mirror == SectionModel.MirrorTypes.None)
                    {
                        type = "Angle";
                    }
                    else if (section.Mirror == SectionModel.MirrorTypes.Left)
                    {
                        type = "DoubleAngle";
                        ME_DIS = section.MirrorGapA;
                    }
                }
                else if (section.SectionType == SectionModel.SectionTypes.C)
                {
                    name = section.Name;
                    type = "CSection";
                    ME_T2 = section.B;
                    ME_T3 = section.D;
                    ME_TF = section.T1;
                    ME_TW = section.T2;
                    ME_R1 = section.R;
                }
                else if (section.SectionType == SectionModel.SectionTypes.HollowRectangle)
                {
                    name = section.Name;
                    type = "HollowRectangle";
                    ME_T2 = section.B;
                    ME_T3 = section.D;
                    ME_TF = section.T1;
                    ME_TW = section.T2;
                    ME_R1 = section.R;
                }
                else if (section.SectionType == SectionModel.SectionTypes.HollowCircle)
                {
                    name = section.Name;
                    type = "HollowCircle";
                    ME_T3 = section.D;
                    ME_TW = section.T;
                }
            }

            int item = 0;
            DA.SetData(item++, name);
            DA.SetData(item++, type);
            DA.SetData(item++, ME_T2);
            DA.SetData(item++, ME_T3);
            DA.SetData(item++, ME_T2B);
            DA.SetData(item++, ME_T3B);
            DA.SetData(item++, ME_T2E);
            DA.SetData(item++, ME_T3E);
            DA.SetData(item++, ME_DIS);
            DA.SetData(item++, ME_R1);
            DA.SetData(item++, ME_R2);
            DA.SetData(item++, ME_TF);
            DA.SetData(item++, ME_TW);
            DA.SetData(item++, ME_TFB);
            DA.SetData(item++, ME_TWB);
            DA.SetData(item++, ME_TFE);
            DA.SetData(item++, ME_TWE);
            DA.SetData(item++, R_i);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("9bc5f380-7ef0-40b5-86e7-bed43439766c");
    }
}