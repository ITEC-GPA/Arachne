using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.Tools
{
    public class NodeFilterComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ElementFilterComponent class.
        /// </summary>
        public NodeFilterComponent()
          : base("Node Filter", "NF", "Node filter", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_TOOLS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "Ns", "The nodes to filter", GH_ParamAccess.list);

            pManager.AddTextParameter("IDs", "ID", "The element IDs to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;

            pManager.AddPointParameter("Positions", "P", "The node positions to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;

            // Default as in the Beam Filter: without it the filter did nothing until Units was connected
            pManager.AddIntegerParameter("Units", "U", "The measure units", GH_ParamAccess.item, 2);
            Param_Integer type_param = (Param_Integer)pManager[pManager.ParamCount - 1];
            Array names = Enum.GetValues(typeof(ModelModel.ModelUnits));
            foreach (ModelModel.ModelUnits name in names)
                type_param.AddNamedValue(name.GetDescription(), (int)name);

            pManager.AddGenericParameter("Groups", "G", "The node groups to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "Ns", "The filtered nodes", GH_ParamAccess.list);
            pManager.AddGenericParameter("Others", "Os", "The other nodes", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GH_Node> nodes = new List<GH_Node>();
            List<string> ids = new List<string>();
            List<Point3d> positions = new List<Point3d>();
            int units = -1;
            List<GH_Group> groups = new List<GH_Group>();
            if (!DA.GetDataList(0, nodes))
                return;
            DA.GetDataList(1, ids);
            DA.GetDataList(2, positions);
            if (!DA.GetData(3, ref units)) return;
            DA.GetDataList(4, groups);

            if (ids.Count == 0 && positions.Count == 0 && groups.Count == 0)
            {
                DA.AbortComponentSolution();
                return;
            }

            List<GH_Node> filtered = new List<GH_Node>();
            List<GH_Node> others = new List<GH_Node>();
            double tol = Helpers.ModelHelper.GetTolerance();

            for (int i = 0; i < nodes.Count; i++)
            {
                GH_Node n = nodes[i];
                bool add = false;
                for (int j = 0; j < ids.Count; j++)
                {
                    string id = ids[j];
                    if (n.Value.HasId(id))
                    {
                        add = true;
                        break;
                    }
                }
                if (!add)
                {
                    for (int j = 0; j < groups.Count; j++)
                    {
                        GH_Group g = groups[j];
                        if (n.Value.HasGroup(g.Value))
                        {
                            add = true;
                            break;
                        }
                    }
                    if (!add)
                    {
                        for (int k = 0; k < positions.Count; k++)
                        {
                            Point3d p = positions[k];
                            if (n.Value.Position.DistanceTo(p) < tol)
                            {
                                add = true;
                                break;
                            }
                        }
                    }
                }
                if (add)
                    filtered.Add(n);
                else
                    others.Add(n);
            }

            DA.SetDataList(0, filtered);
            DA.SetDataList(1, others);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("f322b12d-ddb7-4287-aaa6-67813e6cc864");
    }
}