using System;
using System.Collections.Generic;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Attributes
{
    public class NodeRestraintsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the NodeRestraintsComponent class.
        /// </summary>
        public NodeRestraintsComponent()
          : base("Restraints", "NR", "Define the restraints of a node, along X, Y, Z or along the axes of a plane \n false = not restrained; true = restrained", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ATTRIBUTES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        // The Rotation input of FeMM is removed: the form finding does not use rotations
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Node", "N", "The node where define the restraints", GH_ParamAccess.item);
            int i = pManager.AddGenericParameter("Translational", "T", "The translational restraints along X, Y, Z", GH_ParamAccess.list);
            pManager[i].Optional = true;
            i = pManager.AddPlaneParameter("Coordinate System", "CS", "The axes of the restraints: X, Y and normal Z of the plane. " +
                "For a node sliding on an inclined line restrain Y and Z, on an inclined plane only Z. If not given, the global axes", GH_ParamAccess.item);
            pManager[i].Optional = true;
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
            if (!DA.GetData("Node", ref node))
                return;

            GH_Node newNode = new GH_Node(node);

            List<IGH_Goo> trans = new List<IGH_Goo>();
            DA.GetDataList(1, trans);
            Plane plane = Plane.Unset;
            DA.GetData(2, ref plane);

            NodeRestraintAttributeModel.AttributeData attr = new NodeRestraintAttributeModel.AttributeData();
            for (int i = 0; i < trans.Count && i < 3; i++)
            {
                bool restrained;
                if (trans[i].CastTo(out bool b))
                    restrained = b;
                else if (trans[i].CastTo(out double _))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Defines the displacement using Displacement load");
                    DA.AbortComponentSolution();
                    return;
                }
                else if (trans[i].CastTo(out string s) && bool.TryParse(s, out b))
                    restrained = b;
                else if (trans[i].CastTo(out s) && double.TryParse(s, out _))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Defines the displacement using Displacement load");
                    DA.AbortComponentSolution();
                    return;
                }
                else
                    continue;

                if (i == 0)
                    attr.Tx = restrained;
                else if (i == 1)
                    attr.Ty = restrained;
                else
                    attr.Tz = restrained;
            }

            if (plane.IsValid)
                attr.CoordinateSystem = CoordinateSystemModel.FromPlane(plane, "Restraint plane");

            newNode.Value.Attributes.Add(new NodeRestraintAttributeModel(attr));

            DA.SetData("Node", newNode);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// New GUID: no Rotation input, new Coordinate System input.
        /// </summary>
        public override Guid ComponentGuid => new Guid("70c7c8e2-0ed7-4e5b-95db-9b8837813feb");
    }
}
