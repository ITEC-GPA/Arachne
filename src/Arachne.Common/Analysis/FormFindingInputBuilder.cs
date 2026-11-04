using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Models;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using G = GPC.Geometry;
using R = Rhino.Geometry;

namespace Arachne.Common.Analysis
{
    /// <summary>
    /// The analysis graph of one Form Find run: a copy of the Build Model output (GPC, N-mm) with the loads of the selected cases
    /// only, the element objectives moved into <see cref="FormFindingDefinition"/>, no results of a previous run, and the cases of the
    /// results. The messages are the ones of the former assembly (Arachne 7b1108d, FormFindingHelper.AssemblyModel).
    /// </summary>
    public static class FormFindingInputBuilder
    {
        public sealed class Input
        {
            public Model Model { get; internal set; }
            public FormFindingUnits Units { get; internal set; }
            public int SolverType { get; internal set; }
            public LoadCaseBase ReactionCase { get; internal set; }
            public LoadCaseBase PressureCase { get; internal set; }
            /// <summary>The case of the pre-load of each beam with a force density, fixed axial force or fixed length (FeMM order).</summary>
            public Dictionary<Guid, LoadCaseBase> PreloadCases { get; } = new Dictionary<Guid, LoadCaseBase>();
            /// <summary>Beams with more than one objective of a family: FeMM used the first and said so.</summary>
            public HashSet<Guid> MultipleForceDensities { get; } = new HashSet<Guid>();
            public HashSet<Guid> MultipleLengths { get; } = new HashSet<Guid>();
            public HashSet<Guid> MultipleAxialForces { get; } = new HashSet<Guid>();
            public bool CreateMesh { get; internal set; }
            /// <summary>The property of the generated plates, in N-mm (null: weightless plates).</summary>
            public PlateProperty GeneratedPlateProperty { get; internal set; }
            /// <summary>The projection plane of the solver in the document units, exactly as given.</summary>
            public G.Plane SolverPlane { get; internal set; }
            public List<string> Remarks { get; } = new List<string>();
            /// <summary>Messages written after the solver model is assembled (FeMM order).</summary>
            public List<string> FinalRemarks { get; } = new List<string>();
        }

        public static Input Build(Model source, ModelUnits documentUnits, int solverType, FormFindingOptions options, R.Plane plane)
        {
            if (source == null || options == null) throw new ArgumentNullException();
            if (solverType < 0 || solverType > 2) throw new ArgumentException("Unknown solver type");
            if (options.MaxIterations < 1) throw new ArgumentException("Max Iter must be at least 1");
            var u = FormFindingUnits.From(documentUnits);
            var input = new Input { Units = u, SolverType = solverType, CreateMesh = options.CreateMesh };
            var model = ModelValues.Copy(source);
            input.Model = model;
            // The pressure forces of a previous form finding are results, not loads.
            bool previousPressure = model.NodesElements.Values.Any(n => n.Results.SelectMany(r => r.Results).OfType<NodeResultForces>().Any(f => f.Kind == NodalForceKind.AppliedPressure));
            foreach (var e in model.AllElements) if (e.Results.Count != 0) e.Results.Clear();
            model.Datasets.Remove("form-finding");

            var selected = new HashSet<Guid>((options.LoadCases ?? new List<LoadCaseBase>()).Where(c => c != null).Select(c => c.Guid));
            var used = new HashSet<Guid>();
            bool Applies(LoadCaseBase loadCase)
            {
                if (selected.Count == 0) return true;
                if (loadCase == null || !selected.Contains(loadCase.Guid)) return false;
                used.Add(loadCase.Guid); return true;
            }
            var ff = ModelValues.CopyValue(options.Settings);
            ff.Goals.Clear(); ff.CoordinateGoals.Clear(); ff.SurfaceMasses.Clear();
            ff.Method = solverType == 0 ? FormFindingMethod.LinearForceDensity : solverType == 1 ? FormFindingMethod.SteppedLinearForceDensity : FormFindingMethod.NonlinearForceDensity;
            ff.Gravity = options.Gravity * u.LengthToMm; ff.Pressure = options.Pressure * u.PressureToMPa;
            if (plane == R.Plane.Unset) input.SolverPlane = new G.Plane(G.Point3d.Origin, G.Vector3d.ZAxis);
            else input.SolverPlane = new G.Plane(new G.Point3d(plane.Origin.X, plane.Origin.Y, plane.Origin.Z), new G.Vector3d(plane.Normal.X, plane.Normal.Y, plane.Normal.Z));
            var projection = new G.Plane(new G.Point3d(input.SolverPlane.Origin.X * u.LengthToMm, input.SolverPlane.Origin.Y * u.LengthToMm, input.SolverPlane.Origin.Z * u.LengthToMm), input.SolverPlane.Normal);
            ff.ProjectionPlane = new G.CoordinateSystem(projection.P1, projection.P2, projection.P3);

            foreach (var node in model.NodesElements.Values)
            {
                foreach (var load in node.Loads.Values.ToArray())
                    if (!(load is PointLoad) || !Applies(load.LoadCase)) node.Loads.Remove(load.Id);
                var weight = node.Attributes.Values.OfType<FormFindingCoordinateAttribute>().FirstOrDefault();
                if (weight != null)
                {
                    ff.CoordinateGoals.Add(new FormFindingCoordinateGoal { NodeGuid = node.Guid, Weights = ModelValues.CopyValue(weight.Weights) });
                    if (solverType == 0) input.Remarks.Add("Weights on nodes coordinates are ignored on the linear solver.");
                }
                Strip(node);
            }
            foreach (var beam in model.BeamElements.Values)
            {
                for (int i = beam.Assignments.Loads.Count - 1; i >= 0; i--)
                {
                    var a = beam.Assignments.Loads[i];
                    var loadCase = (a.Concentrated ?? (Load)a.StartIntensity)?.LoadCase;
                    if (!Applies(loadCase)) beam.Assignments.Loads.RemoveAt(i);
                }
                var goals = ElementDrafts.Goals(beam).ToList();
                var primary = goals.Where(g => ElementDrafts.IsPrimary(g.Goal.Kind)).ToList();
                var lengths = goals.Where(g => g.Goal.Kind == FormFindingGoalKind.Length).ToList();
                var axial = goals.Where(g => g.Goal.Kind == FormFindingGoalKind.AxialForce).ToList();
                if (primary.Count > 0)
                {
                    var goal = Bound(primary[0].Goal, beam.Guid); ff.Goals.Add(goal);
                    if (goal.Weight > 0 && solverType == 0) input.Remarks.Add("Weights on force densities are ignored on the linear solver.");
                    if (primary.Count > 1) input.MultipleForceDensities.Add(beam.Guid);
                }
                if (lengths.Count > 0)
                {
                    ff.Goals.Add(Bound(lengths[0].Goal, beam.Guid));
                    if (solverType == 0) input.Remarks.Add("Fixed length is used only on the non-linear solver. In linear solver is ignored.");
                    if (lengths.Count > 1) { input.MultipleLengths.Add(beam.Guid); input.Remarks.Add("More than one fixed length is set on a beam. It's considered only the first one."); }
                }
                if (axial.Count > 0)
                {
                    ff.Goals.Add(Bound(axial[0].Goal, beam.Guid));
                    if (solverType == 0) input.Remarks.Add("Fixed axial force is used only on the non-linear solver. In linear solver is ignored.");
                    if (axial.Count > 1) { input.MultipleAxialForces.Add(beam.Guid); input.Remarks.Add("More than one fixed axial force is set on a beam. It's considered only the first one."); }
                }
                // The pre-load uses the case of the force density, else of the fixed axial force, else of the fixed length.
                var preload = primary.FirstOrDefault() ?? axial.FirstOrDefault() ?? lengths.FirstOrDefault();
                if (preload != null) input.PreloadCases[beam.Guid] = Registered(model, preload.LoadCase ?? throw new ArgumentException("MissingFormFindingLoadCase"));
                Strip(beam);
            }
            foreach (var area in model.AreaElements.Values) Strip(area);
            if (!options.CreateMesh)
                foreach (var area in model.AreaElements.Values.ToArray()) model.AreaElements.Remove(area.Id);
            input.GeneratedPlateProperty = new ElementDrafts.Scale(u, true).Property(options.PlateProperty);
            input.ReactionCase = Registered(model, InputIdentity.LoadCase("FF", "Form Finding"));
            if (options.PressureLoadCase != null) input.PressureCase = Registered(model, options.PressureLoadCase);
            model.FormFinding = ff;
            // Validated by the preflight of the export (TensileModelAdapter.Preflight)

            foreach (var lc in (options.LoadCases ?? new List<LoadCaseBase>()).Where(c => c != null && !used.Contains(c.Guid)))
                input.FinalRemarks.Add($"No node or beam load in the load case {lc.Name}.");
            if (previousPressure)
                input.FinalRemarks.Add("The pressure forces of a previous form finding are not applied: the Pressure of the Solver Options acts on the new shape.");
            return input;
        }

        private static FormFindingGoal Bound(FormFindingGoal goal, Guid beam) { var copy = ModelValues.CopyValue(goal); copy.BeamGuid = beam; return copy; }
        private static void Strip(Element element)
        {
            foreach (var attribute in element.Attributes.Values.Where(a => a is FormFindingGoalAttribute || a is FormFindingCoordinateAttribute).ToArray())
                element.Attributes.Remove(attribute.Id);
        }
        /// <summary>The case of the model with the identity of <paramref name="value"/>, registering it when missing (a case of the same
        /// name with another identity is used instead: one name, one case in a GPC model).</summary>
        internal static LoadCaseBase Registered(Model model, LoadCaseBase value)
        {
            var same = model.LoadCases.Values.FirstOrDefault(c => c.Guid == value.Guid);
            if (same != null) return same;
            if (model.LoadCases.TryGetValue(value.Name, out var named)) return named;
            var copy = ModelValues.CopyValue(value); model.AddLoadCase(copy); return copy;
        }
    }
}
