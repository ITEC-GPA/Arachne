using System;
using System.Collections.Generic;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using GPC.Utilities.Units;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Materials
{
    public class MaterialCustomComponent : GH_Component
    {
        private List<int> _paramsIdx;
        private List<string> _paramsName;

        /// <summary>
        /// Initializes a new instance of the MaterialCustomComponent class.
        /// </summary>
        public MaterialCustomComponent()
          : base("Custom Material", "CM", "Custom material", Helpers.CategoryNameConstants.CATEGORY_ARACHNE, Helpers.CategoryNameConstants.SUBCATEGORY_MATERIALS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "The name of the material", GH_ParamAccess.item);
            // Kind param
            pManager.AddIntegerParameter("Kind", "K", "The kind of the material", GH_ParamAccess.item);
            Param_Integer param = (Param_Integer)pManager[1];
            Array values = Enum.GetValues(typeof(MaterialModel.MaterialKind));
            foreach (MaterialModel.MaterialKind val in values)
                param.AddNamedValue(val.GetDescription(), (int)val);
            pManager.AddIntegerParameter("Units", "U", "The measure units", GH_ParamAccess.item);
            // Mesaure units param
            Param_Integer type_param = (Param_Integer)pManager[2];
            values = Enum.GetValues(typeof(ModelModel.ModelUnits));
            foreach (ModelModel.ModelUnits val in values)
                type_param.AddNamedValue(val.GetDescription(), (int)val);
            // Material data params
            _paramsIdx = new List<int>()
            {
                pManager.AddNumberParameter("Modulus", "E", "Modulus", GH_ParamAccess.item),
                pManager.AddNumberParameter("Poisson Ratio", char.ConvertFromUtf32(0x3BD), "Poisson Ratio", GH_ParamAccess.item),
                pManager.AddNumberParameter("Thermal Expansion", char.ConvertFromUtf32(0x3B1), "Thermal Expansion", GH_ParamAccess.item),
                pManager.AddNumberParameter("Shear Modulus", "G", "Shear Modulus", GH_ParamAccess.item),
                pManager.AddBooleanParameter("Use Poisson", char.ConvertFromUtf32(0x3BD) + "?", "Use Poisson", GH_ParamAccess.item),
                pManager.AddNumberParameter("Density", char.ConvertFromUtf32(0x3C1), "Density", GH_ParamAccess.item),
                pManager.AddNumberParameter("Yielding", "fk", "Yielding", GH_ParamAccess.item),
                pManager.AddNumberParameter("Strength for Steel", "ftk", "Strength", GH_ParamAccess.item),
            };

            _paramsName = new List<string>();
            foreach (int i in _paramsIdx)
            {
                pManager[i].Optional = true;
                _paramsName.Add(pManager[i].Name);
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Material", "M", "The custom material", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int kind = 0;
            int units = 0;
            double modulus = 0, ni = 0, alpha = 0, g = 0, rho = 0, fk = 0, ftk = 0;
            bool use_poisson = false;

            if (!DA.GetData("Units", ref units))
                return;
            UpdateInputDescriptions((ModelModel.ModelUnits)units);

            if (!DA.GetData("Kind", ref kind))
                return;
            Message = ((MaterialModel.MaterialKind)kind).ToString();
            if (!DA.GetData("Name", ref name))
                return;
            if (!DA.GetData(_paramsIdx[0], ref modulus))
                return;
            if (!DA.GetData(_paramsIdx[1], ref ni))
                return;
            if (!DA.GetData(_paramsIdx[2], ref alpha))
                return;
            if (!DA.GetData(_paramsIdx[3], ref g))
                return;
            if (!DA.GetData(_paramsIdx[4], ref use_poisson))
                return;
            if (!DA.GetData(_paramsIdx[5], ref rho))
                return;
            if (!DA.GetData(_paramsIdx[6], ref fk))
                return;
            if (!DA.GetData(_paramsIdx[7], ref ftk))
                return;

            // Convert to Nmm
            ModelHelper.GetUtilitiesConversionParams(
                (ModelModel.ModelUnits)units,
                out UnitsConvert.LengthUnits lengthUnits,
                out UnitsConvert.ForceUnits forceUnits,
                out UnitsConvert.MassUnits massUnits,
                out UnitsConvert.PressureUnits pressureUnits,
                out UnitsConvert.TemperatureUnits temperatureUnits);

            modulus = UnitsConvert.ConvertToDefaultUnits(modulus, pressureUnits, 1);
            g = UnitsConvert.ConvertToDefaultUnits(g, pressureUnits, 1);
            rho = UnitsConvert.ConvertToDefaultUnits(rho, massUnits, 1, lengthUnits, -3);
            fk = UnitsConvert.ConvertToDefaultUnits(fk, pressureUnits, 1);
            ftk = UnitsConvert.ConvertToDefaultUnits(ftk, pressureUnits, 1);

            MaterialModel material = new MaterialModel
            {
                Kind = (MaterialModel.MaterialKind)kind,
                Name = name,
                Modulus = modulus,
                PoissonRatio = ni,
                ThermalExpansion = alpha,
                ShearModulus = g,
                UsePoisson = use_poisson,
                Density = rho
            };

            switch (material.Kind)
            {
                case MaterialModel.MaterialKind.Aluminium:
                    //material.MinimumYieldStress = mys;
                    //material.MinimumTensileStress = mts;
                    //material.ExpectedYieldStress = eys;
                    //material.ExpectedTensileStress = ets;
                    break;
                case MaterialModel.MaterialKind.Concrete:
                    material.SpecificCompressiveStrength = fk;
                    break;
                case MaterialModel.MaterialKind.Steel:
                    material.MinimumYieldStress = fk;
                    material.MinimumTensileStress = ftk;
                    material.ExpectedYieldStress = 1.1 * fk;
                    material.ExpectedTensileStress = 1.1 * ftk;
                    break;
                case MaterialModel.MaterialKind.Undefined:
                    break;
                default:
                    throw new NotSupportedException();
            }

            DA.SetData(0, new GH_Material(material));
        }

        private void UpdateInputDescriptions(ModelModel.ModelUnits units)
        {
            switch (units)
            {
                //do not change the variables here. also default units in utilities are affected
                case ModelModel.ModelUnits.Knm:
                    Params.Input[_paramsIdx[0]].NickName = _paramsName[0] + " (kPa)";
                    Params.Input[_paramsIdx[2]].NickName = _paramsName[2] + " (1/C)";
                    Params.Input[_paramsIdx[3]].NickName = _paramsName[3] + " (kPa)";
                    Params.Input[_paramsIdx[5]].NickName = _paramsName[5] + " (ton/m³)";
                    Params.Input[_paramsIdx[6]].NickName = _paramsName[6] + " (kPa)";
                    Params.Input[_paramsIdx[7]].NickName = _paramsName[7] + " (kPa)";
                    break;
                case ModelModel.ModelUnits.SI:
                    Params.Input[_paramsIdx[0]].NickName = _paramsName[0] + " (Pa)";
                    Params.Input[_paramsIdx[2]].NickName = _paramsName[2] + " (1/C)";
                    Params.Input[_paramsIdx[3]].NickName = _paramsName[3] + " (Pa)";
                    Params.Input[_paramsIdx[5]].NickName = _paramsName[5] + " (kg/m³)";
                    Params.Input[_paramsIdx[6]].NickName = _paramsName[6] + " (Pa)";
                    Params.Input[_paramsIdx[7]].NickName = _paramsName[7] + " (Pa)";
                    break;
                case ModelModel.ModelUnits.Nmm:
                    Params.Input[_paramsIdx[0]].NickName = _paramsName[0] + " (MPa)";
                    Params.Input[_paramsIdx[2]].NickName = _paramsName[2] + " (1/C)";
                    Params.Input[_paramsIdx[3]].NickName = _paramsName[3] + " (MPa)";
                    Params.Input[_paramsIdx[5]].NickName = _paramsName[5] + " (ton/mm³)";
                    Params.Input[_paramsIdx[6]].NickName = _paramsName[6] + " (MPa)";
                    Params.Input[_paramsIdx[7]].NickName = _paramsName[7] + " (MPa)";
                    break;
                case ModelModel.ModelUnits.IPS:
                    Params.Input[_paramsIdx[0]].NickName = _paramsName[0] + " (psi)";
                    Params.Input[_paramsIdx[2]].NickName = _paramsName[2] + " (1/F)";
                    Params.Input[_paramsIdx[3]].NickName = _paramsName[3] + " (psi)";
                    Params.Input[_paramsIdx[5]].NickName = _paramsName[5] + " (lb/in³)";
                    Params.Input[_paramsIdx[6]].NickName = _paramsName[6] + " (psi)";
                    Params.Input[_paramsIdx[7]].NickName = _paramsName[7] + " (psi)";
                    break;
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("7d402b00-2590-463f-950a-acf6d0e970f4");
    }
}
