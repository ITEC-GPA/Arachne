using System;
using Arachne.Common.Models;

namespace Arachne.Common.Analysis
{
    /// <summary>Solver presentation units. The GPC graph always uses N, mm, s, tonne.
    /// Solver mass is derived from force and acceleration, including the lbf/in system.</summary>
    public sealed class FormFindingUnits
    {
        public double LengthToMm { get; }
        public double ForceToN { get; }
        public double PressureToMPa => ForceToN / (LengthToMm * LengthToMm);
        public double ForceDensityToNPerMm => ForceToN / LengthToMm;
        private FormFindingUnits(double length, double force) { LengthToMm = length; ForceToN = force; }
        public static readonly FormFindingUnits Nmm = new FormFindingUnits(1, 1);
        public static FormFindingUnits From(ModelModel.ModelUnits units)
        {
            switch (units)
            {
                case ModelModel.ModelUnits.Nmm: return Nmm;
                case ModelModel.ModelUnits.SI: return new FormFindingUnits(1000, 1);
                case ModelModel.ModelUnits.Knm: return new FormFindingUnits(1000, 1000);
                case ModelModel.ModelUnits.IPS: return new FormFindingUnits(25.4, 4.4482216152605);
                default: throw new ArgumentOutOfRangeException(nameof(units));
            }
        }
    }
}
