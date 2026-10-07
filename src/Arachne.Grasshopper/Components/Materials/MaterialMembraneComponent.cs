using System;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.Materials
{
    /// <summary>
    /// Membrane material (coated fabric or foil), from the values of its data sheet.
    /// </summary>
    public class MaterialMembraneComponent : GH_Component
    {
        public MaterialMembraneComponent()
          : base("Membrane Material", "MM", "Membrane material (coated fabric or foil), with the values of its data sheet", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MATERIALS)
        {
        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The name of the material", GH_ParamAccess.item);
            pManager.AddNumberParameter("Weight", "W", "Weight of the membrane [g/m²]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Strength Warp", "Fw", "Tensile strength along the warp [N/5cm]", GH_ParamAccess.item);
            pManager.AddNumberParameter("Strength Weft", "Ff", "Tensile strength along the weft [N/5cm]", GH_ParamAccess.item);
            int i = pManager.AddNumberParameter("Stiffness Warp", "Ew", "Tensile stiffness E·t along the warp [kN/m]", GH_ParamAccess.item);
            pManager[i].Optional = true;
            i = pManager.AddNumberParameter("Stiffness Weft", "Ef", "Tensile stiffness E·t along the weft [kN/m]", GH_ParamAccess.item);
            pManager[i].Optional = true;
            i = pManager.AddNumberParameter("Poisson Ratio", char.ConvertFromUtf32(0x3BD), "Poisson ratio warp-weft", GH_ParamAccess.item);
            pManager[i].Optional = true;
            i = pManager.AddNumberParameter("Shear Stiffness", "G", "Shear stiffness G·t [kN/m]", GH_ParamAccess.item);
            pManager[i].Optional = true;
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Material", "M", "The membrane material", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            double weight = 0, strengthWarp = 0, strengthWeft = 0;
            double stiffnessWarp = 0, stiffnessWeft = 0, poisson = 0, shear = 0;

            if (!DA.GetData(0, ref name) || !DA.GetData(1, ref weight) || !DA.GetData(2, ref strengthWarp) || !DA.GetData(3, ref strengthWeft))
                return;
            DA.GetData(4, ref stiffnessWarp);
            DA.GetData(5, ref stiffnessWeft);
            DA.GetData(6, ref poisson);
            DA.GetData(7, ref shear);

            if (!(weight > 0) || !(strengthWarp > 0) || !(strengthWeft > 0))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Weight and strengths must be greater than 0");
                return;
            }
            if (stiffnessWarp < 0 || stiffnessWeft < 0 || shear < 0 || poisson < 0 || poisson >= 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Stiffnesses must not be negative and the Poisson ratio must be between 0 and 1");
                return;
            }

            // From the units of the data sheets to t, N and mm (the default units of the model)
            var material = new MaterialModel
            {
                Kind = MaterialModel.MaterialKind.Membrane,
                Name = name,
                AreaDensity = weight * 1e-12,      // g/m² -> t/mm²
                StrengthWarp = strengthWarp / 50,  // N/5cm -> N/mm
                StrengthWeft = strengthWeft / 50,
                StiffnessWarp = stiffnessWarp,     // kN/m = N/mm
                StiffnessWeft = stiffnessWeft,
                PoissonRatio = poisson,
                ShearStiffness = shear
            };

            Message = $"{weight:0.#} g/m²";
            DA.SetData(0, new GH_Material(material));
        }

        protected override System.Drawing.Bitmap Icon => ComponentIcons.Get(GetType().Name);

        public override Guid ComponentGuid => new Guid("3653a652-deb4-4695-b52d-1fce5edfe629");
    }
}
