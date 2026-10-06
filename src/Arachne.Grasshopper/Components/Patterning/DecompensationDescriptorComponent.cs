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
    public class DecompensationDescriptorComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public DecompensationDescriptorComponent()
          : base("DecompensationDescriptor", "DD", "Create a descriptor for decompensation", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("Length", "L", "The decompensation length", GH_ParamAccess.item, 0.1);
            pManager.AddBooleanParameter("Normalized", "N", "If true length is a fraction of the curve", GH_ParamAccess.item, true);
            pManager.AddNumberParameter("X Scale Ends", "XE", "Scale in X axis at curve ends", GH_ParamAccess.item);
            pManager.AddNumberParameter("X Scale Center", "XC", "Scale in X axis at curve center", GH_ParamAccess.item);
            pManager.AddNumberParameter("Y Scale Ends", "YE", "Scale in Y axis at curve ends", GH_ParamAccess.item);
            pManager.AddNumberParameter("Y Scale Center", "YC", "Scale in Y axis at curve center", GH_ParamAccess.item);

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
            double length = 0.0;
            double warpmin = 0.0;
            double warpmax = 0.0;
            double weftmin = 0.0;
            double weftmax = 0.0;
            bool normalized = true;

            if (!DA.GetData(0, ref length))
                return;
            if (!DA.GetData(1, ref normalized))
                return;
            if (!DA.GetData(2, ref warpmin))
                return;
            if (!DA.GetData(3, ref warpmax))
                return;
            if (!DA.GetData(4, ref weftmin))
                return;
            if (!DA.GetData(5, ref weftmax))
                return;

            warpmin = 1.0 - warpmin;
            warpmax = 1.0 - warpmax;
            weftmin = 1.0 - weftmin;
            weftmax = 1.0 - weftmax;

            DecompensationDescriptor descriptor = new DecompensationDescriptor();
            if (length == 0)
            {
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(true, 0, warpmax, weftmax));
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(true, 1, warpmax, weftmax));
            }
            else
            {
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(true, 0, warpmin, weftmin));
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(normalized, length, warpmax, weftmax));
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(normalized, -length, warpmax, weftmax));
                descriptor.Points.Add(new DecompensationDescriptor.DecompensationPoint(true, 1, warpmin, weftmin));
            }

            DA.SetData(0, descriptor);
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("88842b12-f723-5963-aa1f-5d80a18b50f5");
    }
}