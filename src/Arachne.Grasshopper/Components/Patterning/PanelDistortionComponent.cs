using System;
using System.Collections.Generic;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    /// <summary>
    /// Compares a panel with its flattening: the flattening keeps the vertices of the
    /// panel in the same order, so the edges are compared vertex by vertex.
    /// </summary>
    public class PanelDistortionComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PanelDistortionComponent class.
        /// </summary>
        public PanelDistortionComponent()
          : base("Panel Distortion", "PD", "The length and area changes of a panel made by its flattening", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PATTERNING)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Panel", "P", "The 3D panel", GH_ParamAccess.item);
            pManager.AddMeshParameter("Flatten", "F", "The flatten panel, from the Flattening or Flattening ARAP component, before the compensation", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddLineParameter("Edges", "E", "The edges of the flatten panel", GH_ParamAccess.list);
            pManager.AddNumberParameter("Strains (%)", "S", "The length change of each edge in % (greater than 0 = longer in the flatten panel)", GH_ParamAccess.list);
            pManager.AddNumberParameter("Max Strain (%)", "MS", "The largest length change in absolute value, in %", GH_ParamAccess.item);
            pManager.AddNumberParameter("Area Change (%)", "AC", "The area change in % (greater than 0 = larger flatten panel)", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh panel = null;
            Mesh flat = null;
            if (!DA.GetData(0, ref panel) || !DA.GetData(1, ref flat))
                return;
            if (panel == null || flat == null || !panel.IsValid || !flat.IsValid || panel.Faces.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Valid meshes are required.");
                return;
            }
            if (panel.Vertices.Count != flat.Vertices.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The meshes have a different number of vertices: connect a panel and its flattening.");
                return;
            }

            List<Line> edges = new List<Line>(panel.TopologyEdges.Count);
            List<double> strains = new List<double>(panel.TopologyEdges.Count);
            double maxStrain = 0;
            for (int i = 0; i < panel.TopologyEdges.Count; i++)
            {
                Rhino.IndexPair ends = panel.TopologyEdges.GetTopologyVertices(i);
                int a = panel.TopologyVertices.MeshVertexIndices(ends.I)[0];
                int b = panel.TopologyVertices.MeshVertexIndices(ends.J)[0];
                double length = panel.Vertices.Point3dAt(a).DistanceTo(panel.Vertices.Point3dAt(b));
                if (length == 0)
                    continue;
                Line edge = new Line(flat.Vertices.Point3dAt(a), flat.Vertices.Point3dAt(b));
                double strain = (edge.Length - length) / length * 100;
                edges.Add(edge);
                strains.Add(strain);
                maxStrain = Math.Max(maxStrain, Math.Abs(strain));
            }

            double panelArea = AreaMassProperties.Compute(panel)?.Area ?? 0;
            double flatArea = AreaMassProperties.Compute(flat)?.Area ?? 0;

            DA.SetDataList(0, edges);
            DA.SetDataList(1, strains);
            DA.SetData(2, maxStrain);
            if (panelArea > 0)
                DA.SetData(3, (flatArea - panelArea) / panelArea * 100);
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("44d42704-4c53-4dbc-a113-c2f9a3b6388d");
    }
}
