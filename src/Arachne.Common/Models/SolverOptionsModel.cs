using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Common.Models
{
    /// <summary>
    /// Options of the form finding, given by the Solver Options component. The defaults are the
    /// FeMM defaults of the Form Find inputs, except the tolerances of the algebraic solver.
    /// </summary>
    [Serializable]
    public class SolverOptionsModel
    {
        /// <summary>
        /// Default tolerances A and B of the algebraic solver: the ALGLIB defaults (read from the
        /// library), also the defaults of SciPy and MATLAB LSQR, about 6 correct digits. FeMM gave
        /// 0, which ALGLIB turns into 1e-6 only when also Max Iter,s is 0.
        /// </summary>
        public const double DefaultSolverTolerance = 1e-6;

        /// <summary>Gravity acceleration for the self weight, in model units (9806.65 in N/mm); 0: no self weight.</summary>
        public double Gravity { get; set; }

        /// <summary>Normal pressure on the plates.</summary>
        public double Pressure { get; set; }

        /// <summary>Load case of the pressure forces stored in the result.</summary>
        public LoadCaseModel PressureLoadCase { get; set; }

        /// <summary>
        /// The load cases whose loads (node loads, beam loads) are applied; empty: all the loads.
        /// The attributes of the form finding (force densities, fixed lengths...) do not depend on it.
        /// </summary>
        public List<LoadCaseModel> LoadCases { get; set; } = new List<LoadCaseModel>();

        /// <summary>Maximum number of iterations of the solution.</summary>
        public int MaxIterations { get; set; } = 20;

        /// <summary>Tolerance on the relative error of the solution.</summary>
        public double Tolerance { get; set; } = 1e-4;

        /// <summary>Maximum number of iterations of the algebraic solver; 0: no limit.</summary>
        public int SolverMaxIterations { get; set; }

        /// <summary>
        /// Tolerance A of the algebraic solver: relative residual of the conjugate gradient
        /// (Linear), least squares residual |A^T r| / (|A| |r|) of LSQR (Non-Linear).
        /// </summary>
        public double SolverToleranceA { get; set; } = DefaultSolverTolerance;

        /// <summary>Tolerance B of the algebraic solver: LSQR (Non-Linear) stops when |r| &lt;= B |b|.</summary>
        public double SolverToleranceB { get; set; } = DefaultSolverTolerance;

        /// <summary>Create the plates and the mesh of the result.</summary>
        public bool CreateMesh { get; set; } = true;

        /// <summary>Property of the plates generated from the beams: their weight and the plates of the result.</summary>
        public PlatePropertyModel PlateProperty { get; set; }

        public SolverOptionsModel()
        {
        }

        public SolverOptionsModel(SolverOptionsModel options)
        {
            Gravity = options.Gravity;
            Pressure = options.Pressure;
            PressureLoadCase = options.PressureLoadCase;
            LoadCases = new List<LoadCaseModel>(options.LoadCases ?? new List<LoadCaseModel>());
            MaxIterations = options.MaxIterations;
            Tolerance = options.Tolerance;
            SolverMaxIterations = options.SolverMaxIterations;
            SolverToleranceA = options.SolverToleranceA;
            SolverToleranceB = options.SolverToleranceB;
            CreateMesh = options.CreateMesh;
            PlateProperty = options.PlateProperty;
        }

        public override string ToString()
        {
            return $"Solver options:\nGravity: {Gravity}\nPressure: {Pressure}" + (PressureLoadCase == null ? "" : $" ({PressureLoadCase.Name})") +
                "\nLoad cases: " + (LoadCases == null || LoadCases.Count == 0 ? "all" : string.Join(", ", LoadCases.Select(lc => lc.Name))) +
                $"\nMax Iter: {MaxIterations}\nTolerance: {Tolerance}\nMax Iter,s: {SolverMaxIterations}\nToleranceA,s: {SolverToleranceA}\nToleranceB,s: {SolverToleranceB}" +
                $"\nCreate mesh: {CreateMesh}" + (PlateProperty == null ? "" : $"\nPlate Property: {PlateProperty.Name}");
        }
    }
}
