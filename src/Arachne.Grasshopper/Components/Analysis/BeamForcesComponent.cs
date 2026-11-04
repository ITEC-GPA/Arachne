using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Analysis
{
    /// <summary>
    /// The axial forces of a form found model. The Form Find component stores them
    /// on the beams as tension pre-loads.
    /// </summary>
    public class BeamForcesComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamForcesComponent class.
        /// </summary>
        public BeamForcesComponent()
          : base("Beam Forces", "BF", "The axial forces of the beams of a form found model", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ANALYSIS)
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
            pManager.AddLineParameter("Lines", "Ln", "The beams", GH_ParamAccess.list);
            pManager.AddNumberParameter("Axial Forces", "N", "The axial forces (tension > 0). The beams without force density, fixed axial force or fixed length carry no force", GH_ParamAccess.list);
            pManager.AddNumberParameter("Force Densities", "q", "The force densities: axial force / length", GH_ParamAccess.list);
            pManager.AddNumberParameter("Lengths", "L", "The beam lengths", GH_ParamAccess.list);
            pManager.AddTextParameter("IDs", "ID", "The beam IDs", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Model model = null;
            if (!DA.GetData(0, ref model) || model?.Value == null) return;
            var units = model.Scale;
            var beams = model.Value.BeamElements.Values.ToList();
            var preloads = beams.Select(b => FormFindingOutputProjection.Preload(b, units)).ToList();
            if (!preloads.Any(p => p.HasValue))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The model has no axial forces: connect the model computed by the Form Find component");
                return;
            }
            var lines = new List<Line>(beams.Count); var forces = new List<double>(beams.Count); var densities = new List<double>(beams.Count);
            var lengths = new List<double>(beams.Count); var ids = new List<string>(beams.Count);
            for (int i = 0; i < beams.Count; i++)
            {
                var beam = beams[i];
                var line = new Line(FormFindingOutputProjection.Position(beam.NodeI, units), FormFindingOutputProjection.Position(beam.NodeJ, units));
                // The beams without force density, fixed axial force or fixed length carry no force
                double force = preloads[i] ?? 0;
                lines.Add(line); forces.Add(force); densities.Add(line.Length > 0 ? force / line.Length : 0); lengths.Add(line.Length); ids.Add(beam.Name);
            }
            DA.SetDataList(0, lines);
            DA.SetDataList(1, forces);
            DA.SetDataList(2, densities);
            DA.SetDataList(3, lengths);
            DA.SetDataList(4, ids);
        }


        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("5f9a26f9-c55b-496a-8153-459d8e9a56db");
    }
}
