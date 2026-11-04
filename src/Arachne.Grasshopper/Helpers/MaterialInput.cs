using System;
using GPC.Model.Materials;

namespace Arachne.Grasshopper.Helpers
{
    /// <summary>The values typed in the material components (N, mm, t, as FeMM): a builder of the GPC <see cref="Material"/>, not stored.</summary>
    internal sealed class MaterialInput
    {
        public enum MaterialKind { Undefined = -1, Steel, Concrete, Aluminium, Rebar, Membrane }
        public enum AlumiumTypes { Wrought, CastMold, CastSand }
        public MaterialKind Kind { get; set; } = MaterialKind.Undefined;
        public string Name { get; set; } = "";
        public double Modulus { get; set; }
        public double PoissonRatio { get; set; }
        public double ShearModulus { get; set; }
        public bool UsePoisson { get; set; }
        public double Density { get; set; }
        public double ThermalExpansion { get; set; }
        public double MinimumYieldStress { get; set; }
        public double MinimumTensileStress { get; set; }
        public double ExpectedYieldStress { get; set; }
        public double ExpectedTensileStress { get; set; }
        public double SpecificCompressiveStrength { get; set; }
        public double ExpectedCompressiveStrength { get; set; }
        public AlumiumTypes AlumiumType { get; set; }
        public string Alloy { get; set; }
        public double CompressiveYieldStrength { get; set; }
        public double TensileYieldStrength { get; set; }
        public double TensileUltimateStrength { get; set; }
        public double ShearUltimateStrength { get; set; }

        /// <summary>The GPC material: elastic constants, density and thermal expansion; the strengths as the yield and ultimate stresses.</summary>
        public Material ToGpc()
        {
            var material = new Material(Name ?? "", Modulus, PoissonRatio, Density, ThermalExpansion);
            double yield = Kind == MaterialKind.Aluminium ? TensileYieldStrength : Kind == MaterialKind.Concrete ? SpecificCompressiveStrength : MinimumYieldStress;
            double ultimate = Kind == MaterialKind.Aluminium ? TensileUltimateStrength : MinimumTensileStress;
            if (yield > 0) material.StressYTension = yield;
            if (ultimate > 0) material.StressUTension = ultimate;
            if (Kind == MaterialKind.Aluminium && CompressiveYieldStrength > 0) material.StressYCompression = CompressiveYieldStrength;
            return material;
        }
    }
}
