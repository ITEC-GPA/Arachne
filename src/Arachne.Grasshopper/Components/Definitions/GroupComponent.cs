using System;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Definitions
{
    public class GroupComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the GroupComponent class.
        /// </summary>
        public GroupComponent()
          : base("Group", "G", "Defines a group", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_DEFINITIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The load case name", GH_ParamAccess.item);
            pManager.AddGenericParameter("Parent", "P", "The parent group", GH_ParamAccess.item);
            pManager[1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Group", "G", "The defined group", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            GH_Group parent = null;
            if (!DA.GetData(0, ref name))
                return;
            bool haveParent = DA.GetData(1, ref parent);

            Message = name;

            GroupModel group = new GroupModel(name, haveParent ? parent.Value : null);
            DA.SetData(0, new GH_Group(group));
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b9dfba03-6308-4ee0-ab3c-076b1fe0b0a8");
    }
}