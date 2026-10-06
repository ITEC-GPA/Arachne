using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;
using System;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class KeepPositionComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the KeepPositionComponent class.
        /// </summary>
        public KeepPositionComponent()
          : base("Keep Position", "KP", "Keeps the node position in form finding analysis", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The node to keep in position", GH_ParamAccess.item);
            pManager.AddVectorParameter("Weight", "W", "The weight in the XYZ axis", GH_ParamAccess.item, new Vector3d(1, 1, 1));
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
            Vector3d weight = Vector3d.Unset;

            if (!DA.GetData("Node", ref node))
                return;
            DA.GetData("Weight", ref weight);

            if (weight.X <= 0 || weight.Y <= 0 || weight.Z <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Each component of weight vector must be > 0");
                return;
            }

            GH_Node new_node = new GH_Node(node); // same identity, so Build Model merges it with the other copies
            new_node.Value.Attributes.Add(new NodeWeightAttributeModel(weight));
            DA.SetData("Node", new_node);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("a5502518-f224-4d21-a275-705cbc41eda5");
    }
}