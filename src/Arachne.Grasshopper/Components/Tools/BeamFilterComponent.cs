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
    public class BeamFilterComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the ElementFilterComponent class.
        /// </summary>
        public BeamFilterComponent()
          : base("Beam Filter", "BF", "Beam filter", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_TOOLS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "Bs", "The beams to filter", GH_ParamAccess.list);
            pManager.AddTextParameter("IDs", "ID", "The element IDs to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddPointParameter("Ini Pos", "IPs", "The initial positions to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddPointParameter("End Pos", "EPs", "The end positions to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddIntegerParameter("Units", "U", "The measure units", GH_ParamAccess.item, 2);
            Param_Integer type_param = (Param_Integer)pManager[pManager.ParamCount - 1];
            Array names = Enum.GetValues(typeof(ModelModel.ModelUnits));
            foreach (ModelModel.ModelUnits name in names)
                type_param.AddNamedValue(name.GetDescription(), (int)name);
            pManager.AddTextParameter("Properties name", "PNs", "The beam property names to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddGenericParameter("Groups", "G", "The node groups to search", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beams", "Bs", "The filtered beams", GH_ParamAccess.list);
            pManager.AddGenericParameter("Others", "Os", "The other beams", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GH_Beam> beams = new List<GH_Beam>();
            List<string> ids = new List<string>();
            List<Point3d> iniPositions = new List<Point3d>();
            List<Point3d> endPositions = new List<Point3d>();
            int units = -1;
            List<string> props = new List<string>();
            List<GH_Group> groups = new List<GH_Group>();

            if (!DA.GetDataList(0, beams))
                return;
            DA.GetDataList(1, ids);
            DA.GetDataList(2, iniPositions);
            DA.GetDataList(3, endPositions);
            if (!DA.GetData(4, ref units))
                return;
            DA.GetDataList(5, props);
            DA.GetDataList(6, groups);

            if (ids.Count == 0 && iniPositions.Count == 0 && endPositions.Count == 0 && props.Count == 0 && groups.Count == 0)
            {
                DA.AbortComponentSolution();
                return;
            }

            List<GH_Beam> filtered = new List<GH_Beam>();
            List<GH_Beam> others = new List<GH_Beam>();
            double tol = Helpers.ModelHelper.GetTolerance();

            for (int i = 0; i < beams.Count; i++)
            {
                GH_Beam b = beams[i];
                bool add = false;
                for (int i1 = 0; i1 < ids.Count; i1++)
                {
                    string id = ids[i1];
                    if (b.Value.HasId(id))
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
                        if (b.Value.HasGroup(g.Value))
                        {
                            add = true;
                            break;
                        }
                    }
                    if (!add)
                    {
                        for (int k = 0; k < props.Count; k++)
                        {
                            string bp = props[k];
                            if (bp == b.Value.BeamProperty.Name)
                            {
                                add = true;
                                break;
                            }
                        }
                        if (!add)
                        {
                            for (int k = 0; k < iniPositions.Count; k++)
                            {
                                Point3d p = iniPositions[k];
                                if (b.Value.PointFrom.DistanceTo(p) < tol)
                                {
                                    add = true;
                                    break;
                                }
                            }
                            if (!add)
                            {
                                for (int j = 0; j < endPositions.Count; j++)
                                {
                                    Point3d p = endPositions[j];
                                    if (b.Value.PointTo.DistanceTo(p) < tol)
                                    {
                                        add = true;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
                if (add)
                    filtered.Add(b);
                else
                    others.Add(b);
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
        public override Guid ComponentGuid => new Guid("8d3cb7f2-7de6-4594-a437-9756dc36b47c");
    }
}