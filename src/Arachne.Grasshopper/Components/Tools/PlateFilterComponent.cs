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
    public class PlateFilterComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ElementFilterComponent class.
        /// </summary>
        public PlateFilterComponent()
          : base("Plate Filter", "PF", "Plate filter", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_TOOLS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Plates", "Ps", "The plates to filter", GH_ParamAccess.list);

            pManager.AddTextParameter("IDs", "ID", "The element IDs to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;

            pManager.AddPointParameter("Positions", "Ps", "The positions to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;

            // Default as in the Beam Filter: without it the filter did nothing until Units was connected
            pManager.AddIntegerParameter("Units", "U", "The measure units", GH_ParamAccess.item, 2);
            Param_Integer type_param = (Param_Integer)pManager[pManager.ParamCount - 1];
            Array names = Enum.GetValues(typeof(ModelModel.ModelUnits));
            foreach (ModelModel.ModelUnits name in names)
                type_param.AddNamedValue(name.GetDescription(), (int)name);

            pManager.AddTextParameter("Properties name", "PNs", "The plate property names to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;

            pManager.AddGenericParameter("Groups", "G", "The node groups to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Plates", "Ps", "The filtered plates", GH_ParamAccess.list);
            pManager.AddGenericParameter("Others", "Os", "The other plates", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GH_Plate> plates = new List<GH_Plate>();
            List<string> ids = new List<string>();
            List<Point3d> positions = new List<Point3d>();
            int units = -1;
            List<string> props = new List<string>();
            List<GH_Group> groups = new List<GH_Group>();

            if (!DA.GetDataList(0, plates))
                return;
            DA.GetDataList(1, ids);
            DA.GetDataList(2, positions);
            if (!DA.GetData(3, ref units))
                return;
            DA.GetDataList(4, props);
            DA.GetDataList(5, groups);

            if (ids.Count == 0 && positions.Count == 0 && props.Count == 0 && groups.Count == 0)
            {
                DA.AbortComponentSolution();
                return;
            }

            List<GH_Plate> filtered = new List<GH_Plate>();
            List<GH_Plate> others = new List<GH_Plate>();
            double tol = Helpers.ModelHelper.GetTolerance();

            for (int i = 0; i < plates.Count; i++)
            {
                GH_Plate p = plates[i];
                bool add = false;
                for (int j = 0; j < ids.Count; j++)
                {
                    string id = ids[j];
                    if (p.Value.HasId(id))
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
                        if (p.Value.HasGroup(g.Value))
                        {
                            add = true;
                            break;
                        }
                    }
                    if (!add)
                    {
                        for (int j = 0; j < props.Count; j++)
                        {
                            string bp = props[j];
                            if (bp == p.Value.PlateProperty.Name)
                            {
                                add = true;
                                break;
                            }
                        }
                        if (!add)
                        {
                            for (int j = 0; j < positions.Count; j++)
                            {
                                Point3d point = positions[j];
                                for (int k = 0; k < p.Value.Points.Count; k++)
                                {
                                    Point3d plateP = p.Value.Points[k];
                                    if (plateP.DistanceTo(point) < tol)
                                    {
                                        add = true;
                                        break;
                                    }
                                }
                                if (add) break;
                            }
                        }
                    }
                }
                if (add)
                    filtered.Add(p);
                else
                    others.Add(p);
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
        public override Guid ComponentGuid => new Guid("087ad97f-842c-41f7-826b-1a96656ffe77");
    }
}