using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Analysis;
using GPC.Model.Analysis;
using GPC.Model.Attributes;
using GPC.Model.Core;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class ElementIdComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SuperElementComponent class.
        /// </summary>
        public ElementIdComponent()
          : base("Element Id", "EI", "Defines user element ID for node/beam/plate", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Element", "E", "The element where assign the element ID", GH_ParamAccess.item);
            pManager.AddTextParameter("ID", "ID", "The ID assigned by user", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Element", "E", "The modified element", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            IGH_GeometricGoo element = null;
            string id = null;
            if (!DA.GetData(0, ref element)) return;
            if (!DA.GetData(1, ref id)) return;
            if (string.IsNullOrEmpty(id)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Empty string not admitted"); return; }
            GPC.Model.Elements.Element target; string kind;
            if (element is GH_Beam beam) { var copy = new GH_Beam(beam); target = copy.Value; element = copy; kind = "Beam"; }
            else if (element is GH_Plate plate) { var copy = new GH_Plate(plate); target = copy.Value; element = copy; kind = "Plate"; }
            else if (element is GH_Node node) { var copy = new GH_Node(node); target = copy.Value; element = copy; kind = "Node"; }
            else { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported element type"); return; }
            string other = ElementDrafts.Label(target);
            if (other != null) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, kind + " " + other + " has already an ID"); return; }
            target.Attributes.Add(new ElementLabelAttribute(id));
            DA.SetData(0, element);
        }


        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("92cb7203-eb55-4cc4-a322-8e4ccd2bed08");
    }
}
