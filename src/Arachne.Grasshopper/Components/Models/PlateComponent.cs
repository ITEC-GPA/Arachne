using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Models
{
    public class PlateComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PlateComponent class.
        /// </summary>
        public PlateComponent()
          : base("Plate", "PL", "Define a Beam", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MODEL)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The generating mesh face", GH_ParamAccess.item);
            pManager.AddGenericParameter("Property", "PP", "The property to assign to the generated plate", GH_ParamAccess.item);
            pManager.AddGenericParameter("Groups", "G", "The group to assign to the plate", GH_ParamAccess.list);
            pManager[2].Optional = true;
            pManager.AddNumberParameter("Angle", "A", "The local axis angle [deg]", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Offset", "O", "The plate offset", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Plate", "P", "The generated plate", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh face = new Mesh();
            GH_PlateProperty pp = null;
            List<GH_Group> groups = new List<GH_Group>();
            double angle = 0;
            double offset = 0;

            if (!DA.GetData("Mesh", ref face))
                return;
            if (!DA.GetData("Property", ref pp))
                return;
            DA.GetDataList("Groups", groups);
            DA.GetData("Angle", ref angle);
            DA.GetData("Offset", ref offset);

            if (face.Vertices.Count < 3)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Mesh with less than 3 vertices");
                return;
            }
            if (face.Vertices.Count > 4)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Warning: Mesh have more than 4 vertices");

            Polyline edges = face.GetNakedEdges()?.FirstOrDefault();
            if (edges == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The mesh has no open border to define the plate");
                return;
            }

            Point3d[] vertexes = new Point3d[edges.Count - 1];
            for (int i = 0; i < edges.Count - 1; i++)
                vertexes[i] = edges[i];

            GH_Plate plate = new GH_Plate(vertexes, pp, groups.ToArray());

            if (angle != 0)
                plate.Value.AngleDeg = angle;

            if (offset != 0)
                plate.Value.Offset = offset;

            DA.SetData("Plate", plate);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("ad3fea4a-1c4b-4923-bb06-3e58ae1f3247");
    }
}