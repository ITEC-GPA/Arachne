using System;
using System.Collections.Generic;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Materials
{
    public class MaterialTableComponent : GH_Component
    {

        public MaterialTableComponent()
          : base("Table Material", "TM", "Table material", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_MATERIALS)
        {

        }

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {

            pManager.AddTextParameter("Name", "N", "The name of the material", GH_ParamAccess.item);

            pManager.AddBooleanParameter("Use Poisson", char.ConvertFromUtf32(0x3BD) + "?", "Use Poisson", GH_ParamAccess.item, true);

            pManager.AddNumberParameter("Poisson Ratio", char.ConvertFromUtf32(0x3BD), "Poisson Ratio", GH_ParamAccess.item);

            pManager.AddNumberParameter("Shear Modulus", "G", "Shear Modulus", GH_ParamAccess.item);

            pManager.AddNumberParameter("Thermal Expansion", char.ConvertFromUtf32(0x3B1), "Thermal Expansion", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;

            pManager.AddNumberParameter("Density", char.ConvertFromUtf32(0x3C1), "Density", GH_ParamAccess.item);
            pManager[pManager.ParamCount - 1].Optional = true;

            pManager.AddNumberParameter("Stresses", char.ConvertFromUtf32(0x03C3), "Stresses", GH_ParamAccess.list);

            pManager.AddNumberParameter("Strains", char.ConvertFromUtf32(0x03B5), "Strains", GH_ParamAccess.list);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Material", "M", "The table material", GH_ParamAccess.item);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {

            string name = string.Empty;
            double g = 0;
            double ni = 0;
            double alpha = 0;
            double rho = 0;

            List<double> stresses = new List<double>();
            List<double> strains = new List<double>();

            bool usePoisson = false;

            if (!DA.GetData(0, ref name))
                return;

            if (!DA.GetData(1, ref usePoisson))
                return;

            if (!DA.GetData(2, ref ni))
                return;

            if (!DA.GetData(3, ref g))
                return;

            if (!DA.GetData(4, ref alpha))
                if (!DA.GetData(5, ref rho))

                    if (!DA.GetDataList("Stresses", stresses))
                        return;

            if (!DA.GetDataList("Strains", strains))
                return;

            if (stresses.Count == strains.Count)
            {

                MaterialModel material = new MaterialModel
                {
                    Name = name,
                    PoissonRatio = ni,
                    ThermalExpansion = alpha,
                    ShearModulus = g,
                    UsePoisson = usePoisson,
                    Density = rho
                };

                List<(double stress, double strain)> stressStrainTable = new List<(double stress, double strain)>();

                for (int i = 0; i < stresses.Count; i++)
                {
                    stressStrainTable.Add((stresses[i], strains[i]));
                }

                material.StressStrainTable = stressStrainTable.ToArray();

                DA.SetData(0, new GH_Material(material));
            }
            else
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Stress list length != strain list length");


        }


        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        public override Guid ComponentGuid => new Guid("f8dbf21e-db9f-4511-83f4-ad302452a3b3");

    }
}
