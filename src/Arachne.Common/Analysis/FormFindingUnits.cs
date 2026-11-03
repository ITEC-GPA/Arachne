using System;
using System.ComponentModel;
using GPC.Utilities.Units;

namespace Arachne.Common.Analysis
{
    /// <summary>The document units chosen in Build Model, as in FeMM. Same values and order as before: the integer of the Units
    /// inputs of saved definitions keeps its meaning.</summary>
    public enum ModelUnits
    {
        [Description("kNm (m, kN, T, kPa, s, kJ, C)")]
        Knm,
        [Description("SI (m, N, kg, Pa, s, J, C)")]
        SI,
        [Description("Nmm (mm, N, T, MPa, s, mJ, C)")]
        Nmm,
        [Description("PSI (in, lbf, lb, psi, s, btu, F)")]
        IPS
    }

    /// <summary>Conversion between the document units and the GPC units (N, mm, s, t). The GPC graph always uses N and mm; the
    /// TensileLib model is solved in the document units, as before, so that its tolerances keep their meaning. Masses of the
    /// solver follow GPCUtilities in the document mass unit, as before (in IPS: lb, see correzioni-P2A.md).</summary>
    public sealed class FormFindingUnits
    {
        public const double GravityMmPerS2 = 9806.65;
        public ModelUnits Units { get; }
        public double LengthToMm { get; }
        public double ForceToN { get; }
        public double PressureToMPa => ForceToN / (LengthToMm * LengthToMm);
        public double ForceDensityToNPerMm => ForceToN / LengthToMm;
        public UnitsConvert.LengthUnits Length { get; }
        public UnitsConvert.MassUnits Mass { get; }
        private FormFindingUnits(ModelUnits units, double length, double force, UnitsConvert.LengthUnits lengthUnits, UnitsConvert.MassUnits massUnits)
        { Units = units; LengthToMm = length; ForceToN = force; Length = lengthUnits; Mass = massUnits; }
        public static readonly FormFindingUnits Nmm = new FormFindingUnits(ModelUnits.Nmm, 1, 1, UnitsConvert.LengthUnits.mm, UnitsConvert.MassUnits.ton);
        private static readonly FormFindingUnits Si = new FormFindingUnits(ModelUnits.SI, 1000, 1, UnitsConvert.LengthUnits.m, UnitsConvert.MassUnits.kg);
        private static readonly FormFindingUnits Knm = new FormFindingUnits(ModelUnits.Knm, 1000, 1000, UnitsConvert.LengthUnits.m, UnitsConvert.MassUnits.ton);
        private static readonly FormFindingUnits Ips = new FormFindingUnits(ModelUnits.IPS, 25.4, 4.4482216152605, UnitsConvert.LengthUnits.inch, UnitsConvert.MassUnits.lb);
        public static FormFindingUnits From(ModelUnits units)
        {
            switch (units)
            {
                case ModelUnits.Nmm: return Nmm;
                case ModelUnits.SI: return Si;
                case ModelUnits.Knm: return Knm;
                case ModelUnits.IPS: return Ips;
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
        /// <summary>A volume density in t/mm³ in the document units (GPCUtilities, as the former assembly).</summary>
        public double SolverDensity(double tonnePerMm3) => UnitsConvert.ConvertFromDefaultUnits(tonnePerMm3, Mass, 1, Length, -3);
        /// <summary>A surface density in t/mm² in the document units (GPCUtilities, as the former assembly).</summary>
        public double SolverAreaDensity(double tonnePerMm2) => UnitsConvert.ConvertFromDefaultUnits(tonnePerMm2, Mass, 1, Length, -2);
    }
}
