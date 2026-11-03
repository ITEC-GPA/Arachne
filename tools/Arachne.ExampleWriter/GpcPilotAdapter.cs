#if GPC_PILOT
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GPC.Geometry;
using GPC.Model.Analysis;
using GPC.Model.Core.Identity;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Loads;
using GPC.Model.Models;
using GPC.Model.Restraints;
using GPC.Model.Structure.Assignments;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.Storage;
using GPC.Model.Results.State;
using TensileLib.FormFinding;
using TModel = TensileLib.Geometry.Model;
using TBeam = TensileLib.Geometry.Beam;

// F4 transitional boundary: import the explicit assembly produced by the existing GH
// preprocessor, then rebuild the solver graph exclusively from GPC. No solver instance
// is retained in the domain/archive. Full component migration belongs to F6.
internal static class GpcPilotAdapter
{
    internal static Guid Identity(Guid owner, string key)
    {
        using (var sha = SHA256.Create()) return new Guid(sha.ComputeHash(Encoding.UTF8.GetBytes(owner.ToString("D") + "\n" + key)).Take(16).ToArray());
    }
    internal static Model Import(TModel source, Guid owner, FormFindingDefinition settings)
    {
        var elements = new List<Element>();
        var load = new LoadCaseBase(Identity(owner, "load:applied"), "Applied");
        var nodes = source.Nodes.ToDictionary(n => n.Id, n => new NodeElement(Identity(owner, "node:" + n.Id), new Point3d(n.X, n.Y, n.Z), name: "node:" + n.Id));
        // Keys are the ordered topology slots of this fixed-topology pilot, not coordinates.
        // A production component must persist its own topology-key registry.
        foreach (var old in source.Nodes)
        {
            if (old.DX != 0 || old.DY != 0 || old.DZ != 0) throw new NotSupportedException("PilotRequiresUnsolvedInput");
            var node = nodes[old.Id]; node.Source = new SourceIdentity("Arachne.F4", owner.ToString(), EntityFamily.Node, old.Id.ToString());
            var dofs = new[] { old.Tx, old.Ty, old.Tz }.Select((v, i) => new DofRestrain((GeometryRestrain.DOF)i, v)).ToList();
            var axes = new CoordinateSystem(node.Position, old.RestraintAxisX, old.RestraintAxisY);
            node.Assignments.Restrains.Add(new RestrainAssignment { Restrain = new NodeRestrain(node, axes, dofs) });
            node.Loads.Add(new PointLoad(old.ForceX, old.ForceY, old.ForceZ, 0, 0, 0, node.Position, load));
            settings.CoordinateGoals.Add(new FormFindingCoordinateGoal { NodeGuid = node.Guid,
                Weights = new Vector3d(old.HasNLFixedXCoord ? old.NLWeigthCoordX : 0, old.HasNLFixedYCoord ? old.NLWeigthCoordY : 0, old.HasNLFixedZCoord ? old.NLWeigthCoordZ : 0) });
            elements.Add(node);
        }
        foreach (var old in source.Beams)
        {
            if (old.Load.Length != 0 || old.ForceDensitySource == TBeam.ForceDensitySources.None)
                throw new NotSupportedException("PilotUnsupportedBeamInput");
            var beam = new BeamElement(Identity(owner, "beam:" + old.Id), nodes[old.Node1.Id].Position, nodes[old.Node2.Id].Position, null, name: "beam:" + old.Id);
            beam.ConnectNodes(nodes[old.Node1.Id], nodes[old.Node2.Id]);
            beam.Assignments.Formulation = BeamFormulation.Cable;
            beam.Source = new SourceIdentity("Arachne.F4", owner.ToString(), EntityFamily.Beam, old.Id.ToString());
            beam.Assignments.AnalysisProfile = new BeamAnalysisProfile();
            foreach (double s in new[] { 0.0, 1.0 }) beam.Assignments.AnalysisProfile.Stations.Add(new BeamAnalysisStation { Station = s, EA = 0, GA1 = 0, GA2 = 0, GJ = 0, EI1 = 0, EI2 = 0, MassPerLength = old.Area * old.Density });
            var goal = new FormFindingGoal { BeamGuid = beam.Guid, ProjectToPlane = old.Project, Weight = old.HasNLFixedForceDensity ? old.NLFixedForceDensityWeigth : 0 };
            switch (old.ForceDensitySource)
            {
                case TBeam.ForceDensitySources.ByForceDensityValue: goal.Kind = FormFindingGoalKind.ForceDensity; goal.Target = old.TargetForceDensity; break;
                case TBeam.ForceDensitySources.FromAxialForce: goal.Kind = FormFindingGoalKind.CableForce; goal.Target = old.TargetAxialForce; break;
                case TBeam.ForceDensitySources.FromFixedCurvatureRatio:
                    goal.Kind = FormFindingGoalKind.CurvatureRatio; goal.Target = old.TargetCurvatureRatio; goal.ChordLengthMm = old.CurveChord; break;
                case TBeam.ForceDensitySources.FromMembrane:
                    goal.Kind = FormFindingGoalKind.MembraneForcePerWidth; goal.Target = old.TargetForceAreaDensity;
                    goal.Direction = old.MembraneType == TBeam.MembraneTypes.Warp ? FabricDirection.Warp : old.MembraneType == TBeam.MembraneTypes.Weft ? FabricDirection.Weft : FabricDirection.Unspecified; break;
                default: throw new NotSupportedException("PilotForceDensitySource");
            }
            settings.Goals.Add(goal);
            if (old.HasNLFixedLength) settings.Goals.Add(new FormFindingGoal { BeamGuid = beam.Guid, Kind = FormFindingGoalKind.Length, Target = old.NLFixedLength, Weight = old.NLFixedLengthWeigth });
            if (old.HasNLFixedAxialForce) settings.Goals.Add(new FormFindingGoal { BeamGuid = beam.Guid, Kind = FormFindingGoalKind.AxialForce, Target = old.TargetAxialForce, Weight = old.NLFixedAxialForceWeigth });
            elements.Add(beam);
        }
        foreach (var old in source.Plates)
        {
            if (old.SumPressures != 0 || old.SumStresses != 0) throw new NotSupportedException("PilotUnsupportedPlateInput");
            var ns = new[] { old.Node1, old.Node2, old.Node3, old.Node4 }.Where(n => n != null).Select(n => nodes[n.Id]).ToArray();
            var area = new AreaElement(Identity(owner, "area:" + old.Id), null, null, name: "area:" + old.Id); area.ConnectNodes(ns);
            area.Source = new SourceIdentity("Arachne.F4", owner.ToString(), EntityFamily.Shell, old.Id.ToString());
            settings.SurfaceMasses.Add(new FormFindingSurfaceMass { AreaGuid = area.Guid, MassPerArea = old.MemThick * old.Density }); elements.Add(area);
        }
        settings.Pressure = source.Pressure; settings.Gravity = source.Gravity; settings.ProjectionPlane = source.CoordinateSystem;
        var model = ModelAssembly.Assemble(elements, owner, "F4 membrane", supportsWarpedQuads: true);
        model.FormFinding = settings; settings.Validate(model); return model;
    }

    internal static Arachne.Common.Analysis.TensileModelAdapter.Exported Export(Model model) => Arachne.Common.Analysis.TensileModelAdapter.Export(model);
    internal static Solver.ResultCodes Solve(TModel model, FormFindingDefinition settings) => Arachne.Common.Analysis.TensileModelAdapter.Solve(model, settings);
    internal static void Attach(Model model, Arachne.Common.Analysis.TensileModelAdapter.Exported exported, Solver.ResultCodes status) => Arachne.Common.Analysis.TensileModelAdapter.Attach(model, exported, status);
}
#endif
