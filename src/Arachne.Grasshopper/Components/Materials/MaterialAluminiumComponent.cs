using System;
using System.Collections.Generic;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace Arachne.Grasshopper.Components.Materials
{
    public class MaterialAluminiumComponent : GH_Component
    {
        protected static string[] _types = { "Wrought", "Cast-Mold", "Cast-Sand" };

        /// <summary>
        /// Initializes a new instance of the MaterialAluminiumComponent class.
        /// </summary>
        public MaterialAluminiumComponent()
          : base("Aluminium", "AM", "Aluminium material", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MATERIALS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The name of the material", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Type", "T", "The aluminium type", GH_ParamAccess.item);
            Param_Integer param = (Param_Integer)pManager[1];
            for (int i = 0; i < _types.Length; i++)
                param.AddNamedValue(_types[i], i);
            pManager.AddTextParameter("Alloy", "A", "The aluminium alloy designation", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Material", "M", "The Steel material", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int type_idx = 0;
            string alloy = "";
            if (!DA.GetData("Name", ref name))
                return;
            if (!DA.GetData("Type", ref type_idx))
                return;
            if (!DA.GetData("Alloy", ref alloy))
                return;

            MaterialModel material = new MaterialModel
            {
                Kind = MaterialModel.MaterialKind.Aluminium,
                Name = name,
                AlumiumType = (MaterialModel.AlumiumTypes)type_idx,
                Alloy = alloy,
                // Note: Values are in N, m, C. Remember to convert them in the export if the model units are different
                Modulus = 69637.05,//N/mm2=MPa
                PoissonRatio = 0.33,
                ThermalExpansion = 2.358E-05,
                ShearModulus = 26179.344,//N/mm2=MPa
                UsePoisson = true,
                Density = 2.714E-09, // t/mm3
                CompressiveYieldStrength = 241.3,//N/mm2=MPa
                TensileYieldStrength = 241.3,//N/mm2=MPa
                TensileUltimateStrength = 262,//N/mm2=MPa
                ShearUltimateStrength = 165.5//N/mm2=MPa
            };

            DA.SetData(0, new GH_Material(material));
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("9baa5979-36ea-4247-baff-7d9b4e8a832d");
    }
}
