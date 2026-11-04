using Arachne.Common.Analysis;
using GPC.Utilities.Units;
using System;

namespace Arachne.Common.Helpers
{
    public static class ModelHelper
    {
        /// <summary>The GPCUtilities units of the document units (as FeMM).</summary>
        public static void GetUtilitiesConversionParams(ModelUnits units, out UnitsConvert.LengthUnits lengthUnits, out UnitsConvert.ForceUnits forceUnits,
            out UnitsConvert.MassUnits massUnits, out UnitsConvert.PressureUnits pressureUnits, out UnitsConvert.TemperatureUnits temperatureUnits)
        {
            switch (units)
            {
                case ModelUnits.Knm: lengthUnits = UnitsConvert.LengthUnits.m; forceUnits = UnitsConvert.ForceUnits.kN; massUnits = UnitsConvert.MassUnits.ton; pressureUnits = UnitsConvert.PressureUnits.kPa; temperatureUnits = UnitsConvert.TemperatureUnits.C; break;
                case ModelUnits.Nmm: lengthUnits = UnitsConvert.LengthUnits.mm; forceUnits = UnitsConvert.ForceUnits.N; massUnits = UnitsConvert.MassUnits.ton; pressureUnits = UnitsConvert.PressureUnits.MPa; temperatureUnits = UnitsConvert.TemperatureUnits.C; break;
                case ModelUnits.SI: lengthUnits = UnitsConvert.LengthUnits.m; forceUnits = UnitsConvert.ForceUnits.N; massUnits = UnitsConvert.MassUnits.kg; pressureUnits = UnitsConvert.PressureUnits.Pa; temperatureUnits = UnitsConvert.TemperatureUnits.C; break;
                case ModelUnits.IPS: lengthUnits = UnitsConvert.LengthUnits.inch; forceUnits = UnitsConvert.ForceUnits.lbf; massUnits = UnitsConvert.MassUnits.lb; pressureUnits = UnitsConvert.PressureUnits.psi; temperatureUnits = UnitsConvert.TemperatureUnits.F; break;
                default: throw new NotSupportedException();
            }
        }
    }
}
