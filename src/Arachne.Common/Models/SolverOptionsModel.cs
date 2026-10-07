using System;

namespace Arachne.Common.Models
{
    /// <summary>
    /// Options of the form finding, given by the Solver Options component. The defaults are the
    /// FeMM defaults of the Form Find inputs.
    /// </summary>
    [Serializable]
    public class SolverOptionsModel
    {
        /// <summary>Gravity acceleration for the self weight, in model units (9806.65 in N/mm); 0: no self weight.</summary>
        public double Gravity { get; set; }

        /// <summary>Normal pressure on the plates.</summary>
        public double Pressure { get; set; }

        /// <summary>Load case of the pressure forces stored in the result.</summary>
        public LoadCaseModel PressureLoadCase { get; set; }

        /// <summary>Maximum number of iterations of the solution.</summary>
        public int MaxIterations { get; set; } = 20;

        /// <summary>Tolerance on the relative error of the solution.</summary>
        public double Tolerance { get; set; } = 1e-4;

        /// <summary>Maximum number of iterations of the algebraic solver; 0: automatic.</summary>
        public int SolverMaxIterations { get; set; }

        /// <summary>Tolerance of the algebraic solver; 0: automatic.</summary>
        public double SolverTolerance { get; set; }

        /// <summary>Create the plates and the mesh of the result.</summary>
        public bool CreateMesh { get; set; } = true;

        /// <summary>Property given to the plates generated from the beams.</summary>
        public PlatePropertyModel PlateProperty { get; set; }

        public SolverOptionsModel()
        {
        }

        public SolverOptionsModel(SolverOptionsModel options)
        {
            Gravity = options.Gravity;
            Pressure = options.Pressure;
            PressureLoadCase = options.PressureLoadCase;
            MaxIterations = options.MaxIterations;
            Tolerance = options.Tolerance;
            SolverMaxIterations = options.SolverMaxIterations;
            SolverTolerance = options.SolverTolerance;
            CreateMesh = options.CreateMesh;
            PlateProperty = options.PlateProperty;
        }

        public override string ToString()
        {
            return $"Solver options:\nGravity: {Gravity}\nPressure: {Pressure}" + (PressureLoadCase == null ? "" : $" ({PressureLoadCase.Name})") +
                $"\nMax Iter: {MaxIterations}\nTolerance: {Tolerance}\nMax Iter,s: {SolverMaxIterations}\nTolerance,s: {SolverTolerance}" +
                $"\nCreate mesh: {CreateMesh}" + (PlateProperty == null ? "" : $"\nPlate Property: {PlateProperty.Name}");
        }
    }
}
