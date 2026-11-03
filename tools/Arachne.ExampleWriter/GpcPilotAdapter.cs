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
            if (old.Load.Length != 0 || old.ForceDensitySource == TBeam.ForceDensitySources.FromFixedCurvatureRatio || old.ForceDensitySource == TBeam.ForceDensitySources.None)
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

    internal sealed class Exported
    {
        internal TModel Solver; internal SolverIdentityMap Map;
        internal Dictionary<Guid, int> Ids;
    }
    internal static Exported Export(Model model)
    {
        var ff = model.FormFinding ?? throw new ArgumentException("MissingFormFindingDefinition"); ff.Validate(model);
        if (model.VolumeElements.Count != 0 || model.ModelLoads.Count != 0 || model.Costrains.Count != 0 || model.Stages.Count != 0 || model.Combinations.Count != 0)
            throw new NotSupportedException("PilotUnsupportedModelInput");
        var cs = ff.ProjectionPlane ?? CoordinateSystem.Global;
        var plane = new Plane(cs.Origin, cs.V3);
        var result = new Exported { Solver = new TModel { Plane = plane, Pressure = ff.Pressure, Gravity = ff.Gravity }, Ids = new Dictionary<Guid, int>() };
        var native = result.Solver;
        // Order by explicit source slots to reproduce the F1 algebraic ordering; IDs remain a separate map.
        foreach (var node in model.NodesElements.Values.OrderBy(n => int.Parse(n.Source.OriginalId)))
        {
            if (node.Assignments.Restrains.Count != 1 || node.Assignments.GroundSprings.Count != 0 || node.Assignments.Links.Count != 0 || node.Assignments.Mass != null || node.Assignments.Active != null || node.Assignments.Dofs != null || node.Attributes.Count != 0)
                throw new NotSupportedException("PilotUnsupportedNodeAssignment");
            int id = native.AddNewNode(node.Position.X, node.Position.Y, node.Position.Z); result.Ids.Add(node.Guid, id); var n = native.Nodes[id];
            var restraint = node.Assignments.Restrains.Single();
            if (restraint.Case != null || restraint.Phase != null || restraint.Restrain.Restrains.Any(d => d.HasStiffness || d.HasImposedDisplacement || (int)d.Dof > 2)) throw new NotSupportedException("PilotUnsupportedRestraint");
            var r = restraint.Restrain; n.SetRestraintAxes(r.CoordinateSystem.V1, r.CoordinateSystem.V2);
            n.Tx = r.Restrains.Any(d => d.Dof == GeometryRestrain.DOF.DX && d.IsRestrained); n.Ty = r.Restrains.Any(d => d.Dof == GeometryRestrain.DOF.DY && d.IsRestrained); n.Tz = r.Restrains.Any(d => d.Dof == GeometryRestrain.DOF.DZ && d.IsRestrained);
            var f = new Vector3d(0, 0, 0);
            foreach (var item in node.Loads.Values)
            {
                if (!(item is PointLoad p)) throw new NotSupportedException("PilotUnsupportedNodalLoad");
                var g = p.ToGlobal(); if (g.M1 != 0 || g.M2 != 0 || g.M3 != 0) throw new NotSupportedException("PilotNodalMoment");
                f += new Vector3d(g.F1, g.F2, g.F3);
            }
            n.SetForce(f.X, f.Y, f.Z);
            var cg = ff.CoordinateGoals.SingleOrDefault(g => g.NodeGuid == node.Guid);
            if (cg != null) { if (cg.Weights.X > 0) n.FixNLCoordX(cg.Weights.X); if (cg.Weights.Y > 0) n.FixNLCoordY(cg.Weights.Y); if (cg.Weights.Z > 0) n.FixNLCoordZ(cg.Weights.Z); }
        }
        foreach (var beam in model.BeamElements.Values.OrderBy(b => int.Parse(b.Source.OriginalId)))
        {
            if (beam.Loads.Count != 0 || beam.Attributes.Count != 0 || beam.BeamProperty != null) throw new NotSupportedException("PilotUnsupportedBeamPropertyOrLoad");
            var a=beam.Assignments;
            if (a.Formulation!=BeamFormulation.Cable || a.OffsetI!=null || a.OffsetJ!=null || a.OrientationNodeId!=null || a.SectionAxes!=null || a.SectionGeometryAxes!=null || a.SectionCentroidOffset!=null || a.RigidLengthI!=0 || a.RigidLengthJ!=0 || a.Loads.Count!=0 || a.Sections.Count!=0 || a.OtherAssignments.Count!=0) throw new NotSupportedException("PilotUnsupportedBeamAssignment");
            a.AnalysisProfile.Validate(); var start=a.AnalysisProfile.At(0); var end=a.AnalysisProfile.At(1);
            if(a.AnalysisProfile.Stations.Count!=2 || a.AnalysisProfile.Interpolation!=ProfileInterpolation.Constant || start.MassPerLength!=end.MassPerLength || new[]{start.EA,start.EI1,start.EI2,start.GJ,start.GA1,start.GA2,end.EA,end.EI1,end.EI2,end.GJ,end.GA1,end.GA2}.Any(x=>x!=0))throw new NotSupportedException("PilotOnlyLineMassProfile");
            if(ff.Goals.Count(g=>g.BeamGuid==beam.Guid && g.Kind!=FormFindingGoalKind.Length && g.Kind!=FormFindingGoalKind.AxialForce)!=1)throw new NotSupportedException("PilotRequiresOnePrimaryGoalPerBeam");
            int id = native.AddNewBeam(result.Ids[beam.NodeI.Guid], result.Ids[beam.NodeJ.Guid], plane); result.Ids.Add(beam.Guid, id); var b = native.Beams[id];
            // A unit reference area preserves the physical line mass exactly.
            b.Area = 1; b.Density = beam.Assignments.AnalysisProfile.At(0).MassPerLength;
            foreach (var g in ff.Goals.Where(g => g.BeamGuid == beam.Guid).OrderBy(g=>g.Kind==FormFindingGoalKind.Length?1:g.Kind==FormFindingGoalKind.AxialForce?2:0))
            {
                switch (g.Kind)
                {
                    case FormFindingGoalKind.ForceDensity: b.TargetForceDensity = g.Target; break;
                    case FormFindingGoalKind.CableForce: b.TargetAxialForce = g.Target; break;
                    case FormFindingGoalKind.MembraneForcePerWidth: b.TargetForceAreaDensity = g.Target; b.MembraneType = g.Direction == FabricDirection.Warp ? TBeam.MembraneTypes.Warp : g.Direction == FabricDirection.Weft ? TBeam.MembraneTypes.Weft : TBeam.MembraneTypes.Undefined; break;
                    case FormFindingGoalKind.Length: b.FixNLLength(g.Target, g.Weight); continue;
                    case FormFindingGoalKind.AxialForce: b.TargetAxialForce = g.Target; b.FixNLAxialForce(g.Weight); continue;
                    default: throw new NotSupportedException("PilotGoal");
                }
                b.Project = g.ProjectToPlane; if (g.Weight > 0) b.FixNLForceDensity(g.Weight);
            }
        }
        foreach (var area in model.AreaElements.Values.OrderBy(a => int.Parse(a.Source.OriginalId)))
        {
            if (area.PlateProperty != null || area.Loads.Count != 0 || area.Attributes.Count != 0) throw new NotSupportedException("PilotUnsupportedAreaPropertyOrLoad");
            var ns = area.Nodes.Select(n => native.Nodes[result.Ids[n.Guid]]).ToArray();
            var edges = Enumerable.Range(0, ns.Length).Select(i => native.Beams.SingleOrDefault(b => b.Node1.Id == ns[i].Id && b.Node2.Id == ns[(i + 1) % ns.Length].Id || b.Node2.Id == ns[i].Id && b.Node1.Id == ns[(i + 1) % ns.Length].Id)).Where(b => b != null).ToArray();
            var p = new TensileLib.Geometry.Plate(ns, edges, plane) { Id = native.Plates.Count + 1, MemThick = 1, Density = ff.SurfaceMasses.Single(m => m.AreaGuid == area.Guid).MassPerArea };
            native.Plates.Add(p); result.Ids.Add(area.Guid, p.Id);
        }
        result.Map = model.AssemblyMap.ToSolver(model, "TensileLib-2.5.0.1", result.Ids); return result;
    }
    internal static Solver.ResultCodes Solve(TModel native, FormFindingDefinition ff)
    {
        ForceDensitySolver solver = ff.Method == FormFindingMethod.LinearForceDensity
            ? (ForceDensitySolver)new LinearForceDensitySolver(native, ff.MaxIterations, ff.RelativeTolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations)
            : new NonLinearForceDensitySolver(native, ff.MaxIterations, ff.RelativeTolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations);
        return solver.SolveForceDensity();
    }
    internal static void Attach(Model model, Exported exported, Solver.ResultCodes status)
    {
        if (model.AnalysisFingerprint() != exported.Map.AnalysisFingerprint) throw new InvalidOperationException("StaleFormFindingResult");
        if (status == Solver.ResultCodes.NotconvergedAtMaxIterationNumber) throw new InvalidOperationException("UnconvergedFormFindingResult");
        model.CaptureAnalysis(); var load = model.LoadCases.Values.Single();
        model.Datasets.Add("form-finding", new AnalysisDataset { Id="form-finding", Program="TensileLib", SolverVersion="2.5.0.1", AnalysisId="FormFinding/"+status, InputFingerprint=model.Analysis.InputFingerprint, AnalysisSnapshotId=model.Analysis.Id, NormalizedUnits="N,mm,s,tonne", Semantics=AnalysisSemantics.NonlinearStatic });
        var sums=exported.Solver.Nodes.ToDictionary(n=>n.Id,n=>n.NodeForce+n.BeamSelfWeightForce+n.PlateSelfWeightForce+n.BeamLoadForce+n.PressureForce);
        foreach(var b in exported.Solver.Beams) { var axis=new Vector3d(b.Node2.X-b.Node1.X,b.Node2.Y-b.Node1.Y,b.Node2.Z-b.Node1.Z); var pull=axis*(b.AxialForce/b.Length); sums[b.Node1.Id]+=pull; sums[b.Node2.Id]-=pull; }
        Func<ResultState> state = () => new ResultState { DatasetId = "form-finding", InputFingerprint = model.Analysis.InputFingerprint, Step = "equilibrium", Semantics=AnalysisSemantics.NonlinearStatic, SourceRecord="TensileLib form-finding: "+status, Components=new[]{ComponentAvailability.Available,ComponentAvailability.Available,ComponentAvailability.Available,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable} };
        foreach (var node in model.NodesElements.Values)
        {
            var n = exported.Solver.Nodes[exported.Ids[node.Guid]];
            if(n.HasRestraint) { var reaction=n.RestrainedPart(sums[n.Id]*-1); node.AddResult(new NodeResult(new List<INodeResultLocation>{ new NodeResultForces(load,new ResultBeamForces(reaction.Z,reaction.X,reaction.Y,0,0,0,CoordinateSystem.Global)){Kind=NodalForceKind.SupportReaction,State=state()} })); }
            node.AddResult(new NodeResult(new List<INodeResultLocation> { new NodeResultDisplacement(load, new ResultDisplacement(n.DX, n.DY, n.DZ, 0, 0, 0)) { State = state() } }));
        }
        foreach (var beam in model.BeamElements.Values)
        {
            var b = exported.Solver.Beams[exported.Ids[beam.Guid]];
            var axial = new Vector3d(b.Node2.X-b.Node1.X,b.Node2.Y-b.Node1.Y,b.Node2.Z-b.Node1.Z); axial.Unitize();
            var transverse = Math.Abs(axial.Z) < .9 ? new Vector3d(0,0,1).CrossProduct(axial) : new Vector3d(1,0,0).CrossProduct(axial); transverse.Unitize();
            var axes = new CoordinateSystem(b.Node1.Position, transverse, axial.CrossProduct(transverse));
            ResultState beamState() { var value=state(); value.Components=new[]{ComponentAvailability.Available,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable}; return value; }
            beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(load, new ResultBeamForces(b.AxialForce,0,0,0,0,0,axes),0) { State = beamState() }, new StationResultBeamForces(load, new ResultBeamForces(b.AxialForce,0,0,0,0,0,axes),1) { State = beamState() } }));
        }
        model.CompactResults();
    }
}
#endif
