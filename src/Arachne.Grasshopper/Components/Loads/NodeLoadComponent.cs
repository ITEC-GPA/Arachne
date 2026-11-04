using System;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.Helpers;
using Arachne.Grasshopper.DataTypes;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Loads
{
    public class NodeLoadComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the NodeLoadComponent class.
        /// </summary>
        public NodeLoadComponent()
          : base("Node Load", "NL", "Defines a node load", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_LOADS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The node where define the restraits", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddVectorParameter("Force", "F", "The vector force load in the global XYZ direction", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The modified node", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Node node = null;
            GH_LoadCase lc = null;
            Vector3d force = Vector3d.Unset;
            if (!DA.GetData("Node", ref node)) return;
            if (!DA.GetData("LoadCase", ref lc)) return;
            if (!DA.GetData("Force", ref force)) return;
            var copy = new GH_Node(node);
            copy.Value.Loads.Add(new GPC.Model.Loads.PointLoad(force.X, force.Y, force.Z, 0, 0, 0, copy.Value.Position, GPC.Model.Core.ModelValues.CopyValue(lc.Value), GPC.Geometry.CoordinateSystem.Global));
            DA.SetData("Node", copy);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("705f086f-9f47-46f9-92b0-9c20e9115891");
    }
}
