using Arachne.Common.Helpers;
using System.Linq;
using System;
using System.Collections.Generic;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Models
{
    public class DecomposeModelComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the DecomposeModelComponent class.
        /// </summary>
        public DecomposeModelComponent()
          : base("Decompose Model", "DM", "Decompose a FEM model in his elements", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MODEL_BUILD)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "The FEM model", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Nodes", "N", "The nodes results", GH_ParamAccess.list);
            pManager.AddGenericParameter("Beams", "B", "The beams results", GH_ParamAccess.list);
            pManager.AddGenericParameter("Plates", "P", "The plates results", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model model = null;
            if (!DA.GetData("Model", ref model)) return;
            if (model.Value == null) return;
            var nodes = new List<GH_Node>(); var beams = new List<GH_Beam>(); var plates = new List<GH_Plate>();
            try
            {
                // Elements in the document units again (Build Model converts them to N and mm), with their identity and results;
                // after a form finding, at the exact solver coordinates.
                var back = new ElementDrafts.Scale(model.Scale, false);
                var exact = model.Value.NodesElements.Values.ToDictionary(n => n.Guid, n => FormFindingOutputProjection.Position(n, model.Scale));
                void Place(GPC.Model.Elements.NodeElement n) { if (exact.TryGetValue(n.Guid, out var p)) n.Position = new GPC.Geometry.Point3d(p.X, p.Y, p.Z); }
                foreach (var node in model.Value.NodesElements.Values) { var item = new GH_Node(node); back.Apply(item.Value); Place(item.Value); nodes.Add(item); }
                foreach (var beam in model.Value.BeamElements.Values) { var item = new GH_Beam(beam); back.Apply(item.Value, true); Place(item.Value.NodeI); Place(item.Value.NodeJ); beams.Add(item); }
                foreach (var plate in model.Value.AreaElements.Values) { var item = new GH_Plate(plate); back.Apply(item.Value, true); foreach (var n in item.Value.Nodes) Place(n); plates.Add(item); }
            }
            catch (Exception e) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message); }
            DA.SetDataList(0, nodes);
            DA.SetDataList(1, beams);
            DA.SetDataList(2, plates);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("c4146124-3da2-491d-96e4-7e0f565ece4b");
    }
}
