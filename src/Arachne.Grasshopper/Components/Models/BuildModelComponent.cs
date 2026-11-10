using Arachne.Common.Helpers;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.Components.Definitions;
using Arachne.Grasshopper.Components.Parameters;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Components.Models
{
    public class BuildModelComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BuildModelComponent class.
        /// </summary>
        public BuildModelComponent()
          : base("Build Model", "BM", "Build the FEM model", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MODEL_BUILD)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Elements", "E", "The model elements", GH_ParamAccess.list);
            pManager.AddTextParameter("Name", "N", "The name of the model", GH_ParamAccess.item, "");
            pManager.AddIntegerParameter("Units", "U", "The measure units", GH_ParamAccess.item, 2);
            Param_Integer type_param = (Param_Integer)pManager[2];
            Array names = Enum.GetValues(typeof(ModelUnits));
            int i = 0;
            foreach (ModelUnits name in names)
                type_param.AddNamedValue(name.GetDescription(), i++);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "The FEM model", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            try
            {
                var elements = new List<IGH_GeometricGoo>();
                string name = "";
                int units = 0;
                if (!DA.GetDataList(0, elements)) return;
                DA.GetData(1, ref name);
                if (!DA.GetData(2, ref units)) return;
                if (!Enum.IsDefined(typeof(ModelUnits), units)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Unknown units"); return; }
                // Load combinations, named sets and staged constructions served only the FEM programs
                var messages = new List<string>();
                var model = ModelBuilder.Build(elements.OfType<GH_Node>().Select(n => n.Value), elements.OfType<GH_Beam>().Select(b => b.Value),
                    elements.OfType<GH_Plate>().Select(p => p.Value), (ModelUnits)units, name, Helpers.ComponentInputIdentity.For(this, DA), Helpers.ModelHelper.GetTolerance(), messages);
                foreach (var message in messages) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, message);
                DA.SetData(0, new GH_Model(model, (ModelUnits)units));
                Message = ((ModelUnits)units).GetDescription();
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("36226b88-8b00-470b-abf2-26f40b07f0ca");
    }
}
