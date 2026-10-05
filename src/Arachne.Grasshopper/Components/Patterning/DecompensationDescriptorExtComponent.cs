using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class DecompensationDescriptorExtComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public DecompensationDescriptorExtComponent()
          : base("DecompensationDescriptorExt", "DDE", "Create a descriptor for decompensation", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Length", "L", "The decompensation length", GH_ParamAccess.list);
            pManager.AddBooleanParameter("Normalized", "N", "If true length is a fraction of the curve", GH_ParamAccess.list);
            pManager.AddNumberParameter("X Scale", "X", "Scale on X axis", GH_ParamAccess.list);
            pManager.AddNumberParameter("Y Scale", "Y", "Scale on Y axis", GH_ParamAccess.list);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Descriptor", "D", "The decompensation descriptor for a curve", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<double> length = new List<double>();
            List<double> warp = new List<double>();
            List<double> weft = new List<double>();
            List<bool> normalized = new List<bool>();

            if (!DA.GetDataList(0, length))
                return;
            if (!DA.GetDataList(1, normalized))
                return;
            if (!DA.GetDataList(2, warp))
                return;
            if (!DA.GetDataList(3, weft))
                return;

            if (length.Count != normalized.Count || length.Count != warp.Count || length.Count != weft.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "All lists must have the same length");
                return;
            }

            DecompensationDescriptor descriptor = new DecompensationDescriptor();
            for (int i = 0; i < length.Count; i++)
            {
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(normalized[i], length[i], 1 - warp[i], 1 - weft[i]));
            }

            DA.SetData(0, descriptor);
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("CompensationIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("af556c97-1cca-549b-9094-36dd61c91595");
    }
}
