using System;
using Grasshopper.Kernel;
using GH_IO.Serialization;
using Rhino.Geometry;
using Arachne.Common.Models;
using Arachne.Grasshopper.Helpers;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Components.Parameters;

namespace Arachne.Grasshopper.Components.Loads
{
    public class BeamPointLoadComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamPointLoadComponent class.
        /// </summary>
        public BeamPointLoadComponent()
          : base("Beam Point Load", "BPL", "Defines the beam point load force", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_LOADS)
        {
        }

        public override void CreateAttributes()
        {
            m_attributes = new ComponentAttributes.ComponentWithImageAttributes(this, ComponentImages.Get("BeamPointLoadImage"));
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the point force load", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            CoordinateSystemParam cs_param = new CoordinateSystemParam();
            cs_param.Optional = true;
            pManager.AddParameter(cs_param, "Coordinate System", "CS", "The coordinate system of the load (Global if not given)", GH_ParamAccess.item);
            pManager.AddNumberParameter("P1", "P1", "The load in the 1 axis", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("P2", "P2", "The load in the 2 axis", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("P3", "P3", "The load in the 3 axis", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("a", "a", "The distance ratio (0-1) from the starting node", GH_ParamAccess.item);
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
            GH_CoordinateSystem cs = null;
            if (!DA.GetData("Beam", ref beam))
                return;
            if (!DA.GetData("LoadCase", ref lc))
                return;
            bool has_cs = DA.GetData("Coordinate System", ref cs);

            double p1 = 0, p2 = 0, p3 = 0;
            double a = 0;
            bool p1_has_value = DA.GetData("P1", ref p1);
            bool p2_has_value = DA.GetData("P2", ref p2);
            bool p3_has_value = DA.GetData("P3", ref p3);
            bool a_has_value = DA.GetData("a", ref a);

            // The form finding does not handle the beam local axes: no coordinate system means Global
            CoordinateSystemModel coordinateSystem = cs?.Value ?? CoordinateSystemModel.Global;
            Message = string.Format("Coordinate System: {0}", coordinateSystem.Name);

            if (a < 0 || a >= 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The 'a' value must be between 0 and 1");
                return;
            }
            if (lc.Value.NonStructuralMassAcceleration)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The connected LoadCase have the Non-structural mass flag set to on");

            BeamPointLoadModel.LoadData load_value = new BeamPointLoadModel.LoadData()
            {
                CoordinateSystem = coordinateSystem,
                P = new Vector3d(p1, p2, p3),
                A = a
            };
            BeamPointLoadModel load = new BeamPointLoadModel(lc.Value, load_value, BeamPointLoadModel.LoadType.Force);
            GH_Beam newBeam = new GH_Beam(beam);
            newBeam.Value.Loads.Add(load);
            DA.SetData(0, newBeam);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7f58c892-a469-400c-bc46-e2c9c60ad280");
    }
}