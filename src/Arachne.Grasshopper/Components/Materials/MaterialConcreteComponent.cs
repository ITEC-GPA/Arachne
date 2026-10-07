using System;
using System.Collections.Generic;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Materials
{
    public class MaterialConcreteComponent : GH_Component
    {
        protected static string[] _grades = { "C8/10", "C12/15", "C16/20", "C20/25", "C25/30", "C28/35", "C32/40", "C35/45", "C40/50",
            "C45/55", "C50/60", "C55/67", "C60/75", "C70/85", "C80/95", "C90/105" };

        /// <summary>
        /// Initializes a new instance of the MaterialConcreteComponent class.
        /// </summary>
        public MaterialConcreteComponent()
          : base("Concrete", "CM", "Concrete Material", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MATERIALS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The name of the material", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Grade", "G", "The steel grade", GH_ParamAccess.item);
            Param_Integer param = pManager[1] as Param_Integer;
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
                Kind = MaterialModel.MaterialKind.Concrete,
                Name = name
            };
            // Note: Values are in N, m, C. Remember to convert them in the export if the model units are different
            switch (grade)
            {
                case 0: // C8/10
                    material.Modulus = 2.533E+4;//N/mm2=MPa
                    material.ShearModulus = 1.055E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 8;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 8;//N/mm2=MPa
                    break;
                case 1: // C12/15
                    material.Modulus = 2.709E+4;//N/mm2=MPa
                    material.ShearModulus = 1.129E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 12;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 12;//N/mm2=MPa
                    break;
                case 2: // C16/20
                    material.Modulus = 2.861E+4;//N/mm2=MPa
                    material.ShearModulus = 1.192E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 16;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 16;//N/mm2=MPa
                    break;
                case 3: // C20/25
                    material.Modulus = 2.996E+4;//N/mm2=MPa
                    material.ShearModulus = 1.248E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 20;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 20;//N/mm2=MPa
                    break;
                case 4: // C25/30
                    material.Modulus = 3.148E+4;//N/mm2=MPa
                    material.ShearModulus = 1.312E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 25;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 25;//N/mm2=MPa
                    break;
                case 5: // C28/35
                    material.Modulus = 3.231E+4;//N/mm2=MPa
                    material.ShearModulus = 1.346E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 28;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 28;//N/mm2=MPa
                    break;
                case 6: // C32/40
                    material.Modulus = 3.335E+4;//N/mm2=MPa
                    material.ShearModulus = 1.389E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 32;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 32;//N/mm2=MPa
                    break;
                case 7: // C35/45
                    material.Modulus = 3.408E+4;//N/mm2=MPa
                    material.ShearModulus = 1.420E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 35;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 35;//N/mm2=MPa
                    break;
                case 8: // C40/50
                    material.Modulus = 3.522E+4;//N/mm2=MPa
                    material.ShearModulus = 1.468E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 40;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 40;//N/mm2=MPa
                    break;
                case 9: // C45/55
                    material.Modulus = 3.628E+4;//N/mm2=MPa
                    material.ShearModulus = 1.512E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 45;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 45;//N/mm2=MPa
                    break;
                case 10: // C50/60
                    material.Modulus = 3.728E+4;//N/mm2=MPa
                    material.ShearModulus = 1.553E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 50;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 50;//N/mm2=MPa
                    break;
                case 11: // C55/67
                    material.Modulus = 3.821E+4;//N/mm2=MPa
                    material.ShearModulus = 1.592E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 55;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 55;//N/mm2=MPa
                    break;
                case 12: // C60/75
                    material.Modulus = 3.910E+4;//N/mm2=MPa
                    material.ShearModulus = 1.629E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 60;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 60;//N/mm2=MPa
                    break;
                case 13: // C70/85
                    material.Modulus = 4.074E+4;//N/mm2=MPa
                    material.ShearModulus = 1.698E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 70;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 70;//N/mm2=MPa
                    break;
                case 14: // C80/95
                    material.Modulus = 4.224E+4;//N/mm2=MPa
                    material.ShearModulus = 1.760E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 80;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 80;//N/mm2=MPa
                    break;
                case 15: // C90/105
                    material.Modulus = 4.363E+4;//N/mm2=MPa
                    material.ShearModulus = 1.818E+4;//N/mm2=MPa
                    material.SpecificCompressiveStrength = 90;//N/mm2=MPa
                    material.ExpectedCompressiveStrength = 90;//N/mm2=MPa
                    break;
            }
            material.PoissonRatio = 0.2;
            material.ThermalExpansion = 1.0E-05;
            material.UsePoisson = true;
            material.Density = 2.549E-09;//t/mm3

            DA.SetData(0, new GH_Material(material));
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("76802e52-c065-47ff-9c04-97094ab9edc8");
    }
}
