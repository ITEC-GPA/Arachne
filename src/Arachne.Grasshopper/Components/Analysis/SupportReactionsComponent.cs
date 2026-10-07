using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Analysis
{
    /// <summary>
    /// The support reactions of a form found model. The Form Find component computes them
    /// from the equilibrium of the restrained nodes, with the loads applied by the solver.
    /// </summary>
    public class SupportReactionsComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the SupportReactionsComponent class.
        /// </summary>
        public SupportReactionsComponent()
          : base("Support Reactions", "SR", "The forces of the supports of a form found model", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ANALYSIS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "The model computed by the Form Find component", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter("Points", "P", "The restrained nodes", GH_ParamAccess.list);
            pManager.AddVectorParameter("Reactions", "R", "The forces that the supports exert on the structure, along the restrained directions only", GH_ParamAccess.list);
            pManager.AddTextParameter("IDs", "ID", "The node IDs", GH_ParamAccess.list);
            pManager.AddVectorParameter("Total", "T", "The sum of the reactions: it balances the total load", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model model = null;
            if (!DA.GetData(0, ref model) || model?.Value == null)
                return;

            List<Point3d> points = new List<Point3d>();
            List<Vector3d> reactions = new List<Vector3d>();
            List<string> ids = new List<string>();
            Vector3d total = Vector3d.Zero;
            foreach (NodeModel node in model.Value.Nodes)
            {
                NodeReactResultModel reaction = node.Results.OfType<NodeReactResultModel>().LastOrDefault();
                if (reaction == null)
                    continue;
                points.Add(node.Position);
                reactions.Add(reaction.Force);
                ids.Add(node.NodeId);
                total += reaction.Force;
            }

            if (points.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The model has no reactions: connect the model computed by the Form Find component. " +
                    "The reactions are stored only when the form finding converges and the model has restrained nodes.");
                return;
            }

            DA.SetDataList(0, points);
            DA.SetDataList(1, reactions);
            DA.SetDataList(2, ids);
            DA.SetData(3, total);
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("2df8465b-e430-4c4c-b077-6f64ee5150e9");
    }
}
