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
    /// TensileLib model is solved in the document units, as before, so that its tolerances keep their meaning. The masses of the
    /// solver are in the mass unit consistent with its force and length units (force = mass x acceleration): t in Nmm and kNm, kg in
    /// SI, as GPCUtilities gives them; in IPS lbf·s²/in, i.e. the pound-mass of the documents (lb/in³, the unit of the Custom Material
    /// component) divided by 386.0886 (standard gravity in in/s²). User decision 4/11/2026 («metti quello che pensi meglio»): Arachne
    /// 7b1108d used the pound-mass itself as solver mass, so that the weights in IPS were 386 times too large and did not converge.</summary>
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
        /// <summary>The masses of <see cref="Mass"/> in one mass unit of the solver: 1, except in IPS (lb per lbf·s²/in).</summary>
        public double MassPerSolverMass { get; }
        /// <summary>Standard gravity in in/s² (9.80665 m/s², 1 in = 25.4 mm): the pound-mass in one lbf·s²/in.</summary>
        public const double PoundsPerSlinch = 9806.65 / 25.4;
        private FormFindingUnits(ModelUnits units, double length, double force, UnitsConvert.LengthUnits lengthUnits, UnitsConvert.MassUnits massUnits, double massPerSolverMass = 1)
        { Units = units; LengthToMm = length; ForceToN = force; Length = lengthUnits; Mass = massUnits; MassPerSolverMass = massPerSolverMass; }
        public static readonly FormFindingUnits Nmm = new FormFindingUnits(ModelUnits.Nmm, 1, 1, UnitsConvert.LengthUnits.mm, UnitsConvert.MassUnits.ton);
        private static readonly FormFindingUnits Si = new FormFindingUnits(ModelUnits.SI, 1000, 1, UnitsConvert.LengthUnits.m, UnitsConvert.MassUnits.kg);
        private static readonly FormFindingUnits Knm = new FormFindingUnits(ModelUnits.Knm, 1000, 1000, UnitsConvert.LengthUnits.m, UnitsConvert.MassUnits.ton);
        private static readonly FormFindingUnits Ips = new FormFindingUnits(ModelUnits.IPS, 25.4, 4.4482216152605, UnitsConvert.LengthUnits.inch, UnitsConvert.MassUnits.lb, PoundsPerSlinch);
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
        /// <summary>A volume density in t/mm³ in the solver units (GPCUtilities, as the former assembly, then the solver mass unit).</summary>
        public double SolverDensity(double tonnePerMm3) => UnitsConvert.ConvertFromDefaultUnits(tonnePerMm3, Mass, 1, Length, -3) / MassPerSolverMass;
        /// <summary>A surface density in t/mm² in the solver units (GPCUtilities, as the former assembly, then the solver mass unit).</summary>
        public double SolverAreaDensity(double tonnePerMm2) => UnitsConvert.ConvertFromDefaultUnits(tonnePerMm2, Mass, 1, Length, -2) / MassPerSolverMass;
    }
}
