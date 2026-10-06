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

    public class DecompensationComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public DecompensationComponent()
          : base("Decompensation", "DC", "Decompensate the flatten patches", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The flatten panel to decompensate", GH_ParamAccess.list);
            pManager.AddGenericParameter("Descriptors", "D", "The decompensation descriptors for each curve", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The decompensated panel curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var curves = new List<GH_Curve>();
            var objects = new List<object>();
            if (!DA.GetDataList(0, curves) || !DA.GetDataList(1, objects)) return;
            if (curves.Count == 0 || curves.Count != objects.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The number of curves and descriptors must match");
                return;
            }

            try
            {
                var descriptors = objects.Select(o => (o is GH_ObjectWrapper w ? w.Value : o) as DecompensationDescriptor).ToArray();
                DA.SetDataList(0, Services.DecompensationService.Apply(curves.Select(c => c.Value).ToArray(), descriptors));
            }
            catch (ArgumentException error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.Message);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("9bcc0da9-b5e3-5b52-9801-47c48ae166f1");
    }
}