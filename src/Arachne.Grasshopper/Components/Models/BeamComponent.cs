using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Models
{
    public class BeamComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamComponent class.
        /// </summary>
        public BeamComponent()
          : base("Beam", "BM", "Define a Beam", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MODEL)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Line", "L", "The generating line", GH_ParamAccess.item);
            pManager.AddGenericParameter("Property", "BP", "The property to assign to the generated beam", GH_ParamAccess.item);
            pManager.AddGenericParameter("Groups", "G", "The groups to assign to the beam", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Angle", "A", "The rotation angle along the principal axis [deg]", GH_ParamAccess.item, 0);
            pManager.AddVectorParameter("Offset", "O", "The translation offset along the principal axis", GH_ParamAccess.item);
            pManager[4].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The generated beam", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Curve line = null;
            GH_BeamProperty bp = null;
            List<GH_Group> groups = new List<GH_Group>();
            double angle = 0;
            Vector3d offset = Vector3d.Unset;

            if (!DA.GetData("Line", ref line))
                return;
            if (!DA.GetData("Property", ref bp))
                return;
            DA.GetDataList("Groups", groups);
            DA.GetData("Angle", ref angle);
            bool has_offset = DA.GetData("Offset", ref offset);

            GH_Beam beam = new GH_Beam(line.PointAtStart, line.PointAtEnd, bp, groups.ToArray());
            if (angle != 0)
                beam.Value.AngleDeg = angle;
            if (has_offset)
                beam.Value.Offset = new Vector2d(offset.X, offset.Y);

            DA.SetData("Beam", beam);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("03f091b7-2779-4f4b-ae65-9953fe26a366");
    }
}