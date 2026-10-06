using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Models;
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
            if (!DA.GetData(0, ref element))
                return;
            if (!DA.GetData(1, ref id))
                return;

            if (string.IsNullOrEmpty(id))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Empty string not admitted");
                return;
            }

            ElementIdAttributeModel se = new ElementIdAttributeModel(id);
            if (element is GH_Beam beam)
            {
                ElementIdAttributeModel other = GetOtherId(beam.Value);
                if (other != null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Beam " + other.Value + " has already an ID");
                    return;
                }
                GH_Beam new_beam = new GH_Beam(beam);
                new_beam.Value.Attributes.Add(se);
                DA.SetData(0, new_beam);
            }
            else if (element is GH_Plate plate)
            {
                ElementIdAttributeModel other = GetOtherId(plate.Value);
                if (other != null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Plate " + other.Value + " has already an ID");
                    return;
                }
                GH_Plate new_plate = new GH_Plate(plate);
                new_plate.Value.Attributes.Add(se);
                DA.SetData(0, new_plate);
            }
            else if (element is GH_Node node)
            {
                ElementIdAttributeModel other = GetOtherId(node.Value);
                if (other != null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Node " + other.Value + " has already an ID");
                    return;
                }
                GH_Node new_node = new GH_Node(node);
                new_node.Value.Attributes.Add(se);
                DA.SetData(0, new_node);
            }
            else
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unsupported element type");
            }
        }

        private ElementIdAttributeModel GetOtherId(ElementModel el)
        {
            return (ElementIdAttributeModel)el.Attributes.FirstOrDefault(a => a is ElementIdAttributeModel);
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