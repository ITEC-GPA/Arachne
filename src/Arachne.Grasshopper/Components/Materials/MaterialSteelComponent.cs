using System;
using System.Windows.Forms;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;

namespace Arachne.Grasshopper.Components.Materials
{
    public class MaterialSteelComponent : GH_Component
    {
        protected static string[] _grades = { "S235", "S275", "S355", "S450" };

        /// <summary>
        /// Initializes a new instance of the SteelMaterialComponent class.
        /// </summary>
        public MaterialSteelComponent()
          : base("Steel", "SM", "Steel Material", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MATERIALS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The name of the material", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Grade", "G", "The steel grade", GH_ParamAccess.item);
            Param_Integer param = (Param_Integer)pManager[1];
            for (int i = 0; i < _grades.Length; i++)
                param.AddNamedValue(_grades[i], i);
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
            int grade = 0;
            if (!DA.GetData("Name", ref name))
                return;
            if (!DA.GetData("Grade", ref grade))
                return;

            Message = _grades[grade];

            MaterialModel material = new MaterialModel
            {
                Kind = MaterialModel.MaterialKind.Steel,
                Name = name
            };
            // Note: Values are in N, m, C. Remember to convert them in the export if the model units are different
            switch (grade)
            {
                case 0: // S235
                    material.Modulus = 2.1E+5;//N/mm2=MPa
                    material.PoissonRatio = 0.3;
                    material.ThermalExpansion = 1.170E-05;
                    material.ShearModulus = 8.077E+4;//N/mm2=MPa
                    material.UsePoisson = true;
                    material.Density = 7.849E-9; // t/mm3
                    material.MinimumYieldStress = 235;//N/mm2=MPa
                    material.MinimumTensileStress = 360;//N/mm2=MPa
                    material.ExpectedYieldStress = 282;//N/mm2=MPa
                    material.ExpectedTensileStress = 396;//N/mm2=MPa
                    break;
                case 1: // S275
                    material.Modulus = 2.1E+5;//N/mm2=MPa
                    material.PoissonRatio = 0.3;
                    material.ThermalExpansion = 1.170E-05;
                    material.ShearModulus = 8.077E+4;//N/mm2=MPa
                    material.UsePoisson = true;
                    material.Density = 7.849E-9; // t/mm3
                    material.MinimumYieldStress = 275;//N/mm2=MPa
                    material.MinimumTensileStress = 430;//N/mm2=MPa
                    material.ExpectedYieldStress = 316.3;//N/mm2=MPa
                    material.ExpectedTensileStress = 473;//N/mm2=MPa
                    break;
                case 2: // S355
                    material.Modulus = 2.1E+5;//N/mm2=MPa
                    material.PoissonRatio = 0.3;
                    material.ThermalExpansion = 1.170E-05;
                    material.ShearModulus = 8.077E+4;//N/mm2=MPa
                    material.UsePoisson = true;
                    material.Density = 7.849E-9; // t/mm3
                    material.MinimumYieldStress = 355;//N/mm2=MPa
                    material.MinimumTensileStress = 510;//N/mm2=MPa
                    material.ExpectedYieldStress = 390.5;//N/mm2=MPa
                    material.ExpectedTensileStress = 561;//N/mm2=MPa
                    break;
                case 3: // S450
                    material.Modulus = 2.1E+5;//N/mm2=MPa
                    material.PoissonRatio = 0.3;
                    material.ThermalExpansion = 1.170E-05;
                    material.ShearModulus = 8.077E+4;//N/mm2=MPa
                    material.UsePoisson = true;
                    material.Density = 7.849E-9; // t/mm3
                    material.MinimumYieldStress = 440;//N/mm2=MPa
                    material.MinimumTensileStress = 550;//N/mm2=MPa
                    material.ExpectedYieldStress = 484;//N/mm2=MPa
                    material.ExpectedTensileStress = 605;//N/mm2=MPa
                    break;
            }

            DA.SetData(0, new GH_Material(material));
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("d6938aa3-6c82-4648-8d33-f61505ddd262");
    }
}
