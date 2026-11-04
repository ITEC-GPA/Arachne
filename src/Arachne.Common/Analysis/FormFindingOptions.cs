using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.ElementProperties;
using GPC.Model.LoadCases;

namespace Arachne.Common.Analysis
{
    /// <summary>
    /// The options of the Form Find (Solver Options component). The solver settings are a GPC <see cref="FormFindingDefinition"/>;
    /// gravity and pressure are in the document units until the Form Find converts them, as the other draft inputs. The selection of
    /// the load cases, the case of the pressure forces, the mesh switch and the property of the generated plates are the Arachne choices
    /// that the definition does not hold. Every part is a GPC object.
    /// </summary>
    public sealed class FormFindingOptions
    {
        public const double DefaultSolverTolerance = 1e-6;
        /// <summary>Solver settings (Method, plane and goals are set by the Form Find). Defaults as FeMM: 20 iterations, 1e-4, 1e-6.</summary>
        public FormFindingDefinition Settings { get; private set; } = new FormFindingDefinition
        { MaxIterations = 20, RelativeTolerance = 1e-4, SolverMaxIterations = 0, SolverToleranceA = DefaultSolverTolerance, SolverToleranceB = DefaultSolverTolerance };
        public LoadCaseBase PressureLoadCase { get; set; }
        public List<LoadCaseBase> LoadCases { get; private set; } = new List<LoadCaseBase>();
        public bool CreateMesh { get; set; } = true;
        /// <summary>The property of the plates generated from the beams (draft, document units).</summary>
        public PlateProperty PlateProperty { get; set; }

        public double Gravity { get => Settings.Gravity; set => Settings.Gravity = value; }
        public double Pressure { get => Settings.Pressure; set => Settings.Pressure = value; }
        public int MaxIterations { get => Settings.MaxIterations; set => Settings.MaxIterations = value; }
        public double Tolerance { get => Settings.RelativeTolerance; set => Settings.RelativeTolerance = value; }
        public int SolverMaxIterations { get => Settings.SolverMaxIterations; set => Settings.SolverMaxIterations = value; }
        public double SolverToleranceA { get => Settings.SolverToleranceA; set => Settings.SolverToleranceA = value; }
        public double SolverToleranceB { get => Settings.SolverToleranceB; set => Settings.SolverToleranceB = value; }

        public FormFindingOptions() { }
        public FormFindingOptions(FormFindingDefinition settings, LoadCaseBase pressureCase, IEnumerable<LoadCaseBase> loadCases, bool createMesh, PlateProperty plateProperty)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            PressureLoadCase = pressureCase; LoadCases = (loadCases ?? Enumerable.Empty<LoadCaseBase>()).ToList(); CreateMesh = createMesh; PlateProperty = plateProperty;
        }

        /// <summary>A detached copy (GPC contract copies; the selected cases keep their identities).</summary>
        public FormFindingOptions Copy() => new FormFindingOptions(ModelValues.CopyValue(Settings), ModelValues.CopyValue(PressureLoadCase),
            LoadCases.Select(c => ModelValues.CopyValue(c)), CreateMesh, ModelValues.CopyValue(PlateProperty));

        public override string ToString()
            => $"Solver options:\nGravity: {Gravity}\nPressure: {Pressure}" + (PressureLoadCase == null ? "" : $" ({PressureLoadCase.Name})") +
                "\nLoad cases: " + (LoadCases.Count == 0 ? "all" : string.Join(", ", LoadCases.Select(lc => lc.Name))) +
                $"\nMax Iter: {MaxIterations}\nTolerance: {Tolerance}\nMax Iter,s: {SolverMaxIterations}\nToleranceA,s: {SolverToleranceA}\nToleranceB,s: {SolverToleranceB}" +
                $"\nCreate mesh: {CreateMesh}" + (PlateProperty == null ? "" : $"\nPlate Property: {PlateProperty.Name}");
    }
}
