using System;
using System.Collections.Generic;
using Arachne.Common.Models;
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
          : base("Decompose Model", "DM", "Decompose a FEM model in his elements", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MODEL)
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
            if (!DA.GetData("Model", ref model))
                return;

            if (model.Value == null)
                return;

            List<GH_Node> nodes = new List<GH_Node>();
            List<GH_Beam> beams = new List<GH_Beam>();
            List<GH_Plate> plates = new List<GH_Plate>();

            try
            {
                foreach (NodeModel node in model.Value.Nodes)
                    nodes.Add(new GH_Node(node, model.Guids.TryGetValue(node, out Guid nodeGuid) ? nodeGuid : (Guid?)null));
                foreach (BeamModel beam in model.Value.Beams)
                    beams.Add(new GH_Beam(beam, model.Guids.TryGetValue(beam, out Guid beamGuid) ? beamGuid : (Guid?)null));
                foreach (PlateModel plate in model.Value.Plates)
                    plates.Add(new GH_Plate(plate, model.Guids.TryGetValue(plate, out Guid plateGuid) ? plateGuid : (Guid?)null));
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
            }

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