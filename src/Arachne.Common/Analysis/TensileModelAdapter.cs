using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Arachne.Common.Analysis
{
/// <summary>Maps the normalized GPC graph to a detached TensileLib solver graph. Input units: N, mm, s, tonne.</summary>
public static class TensileModelAdapter
{
    private static int Slot(Element element) => int.TryParse(element.Source?.OriginalId,
        System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int id) ? id : element.Id;
    public sealed class Exported
    {
        public TModel Solver { get; internal set; } public SolverIdentityMap Map { get; internal set; }
        public Dictionary<Guid, int> Ids { get; internal set; }
        public FormFindingUnits Units { get; internal set; }
    }
    public static Exported Export(Model model, FormFindingUnits units = null)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        units = units ?? FormFindingUnits.Nmm;
        double length = units.LengthToMm, force = units.ForceToN;
        var ff = model.FormFinding ?? throw new ArgumentException("MissingFormFindingDefinition"); ff.Validate(model);
        if (PhysicalAssignments.HasData(model) || model.MovingLoads!=null) throw new NotSupportedException("UnsupportedFormFindingPhysicalAssignment");
        if (model.VolumeElements.Count != 0 || model.ModelLoads.Count != 0 || model.Costrains.Count != 0 || model.Stages.Count != 0 || model.Combinations.Count != 0)
            throw new NotSupportedException("UnsupportedFormFindingModelInput");
        var cs = ff.ProjectionPlane ?? CoordinateSystem.Global;
        var plane = new Plane(new Point3d(cs.Origin.X / length, cs.Origin.Y / length, cs.Origin.Z / length), cs.V3);
        var result = new Exported { Solver = new TModel { Plane = plane, Pressure = ff.Pressure / units.PressureToMPa, Gravity = ff.Gravity / length }, Ids = new Dictionary<Guid, int>(), Units = units };
        var native = result.Solver;
        // Order by explicit source slots to reproduce the F1 algebraic ordering; IDs remain a separate map.
        foreach (var node in model.NodesElements.Values.OrderBy(Slot).ThenBy(n => n.Guid))
        {
            if (node.Assignments.Restrains.Count > 1 || node.Assignments.GroundSprings.Count != 0 || node.Assignments.Links.Count != 0 || node.Assignments.Mass != null || node.Assignments.Active != null || node.Assignments.Dofs != null || node.Attributes.Count != 0)
                throw new NotSupportedException("UnsupportedFormFindingNodeAssignment");
            int id = native.AddNewNode(node.Position.X / length, node.Position.Y / length, node.Position.Z / length); result.Ids.Add(node.Guid, id); var n = native.Nodes[id];
            var restraint = node.Assignments.Restrains.SingleOrDefault();
            if (restraint != null)
            {
                if (restraint.Case != null || restraint.Phase != null || restraint.FreedomCase != null || restraint.Restrain == null
                    || restraint.Restrain.Restrains.Any(d => d.HasStiffness || d.HasImposedDisplacement || (int)d.Dof > 2 && d.IsRestrained)) throw new NotSupportedException("UnsupportedFormFindingRestraint");
                var r = restraint.Restrain; var axes = r.CoordinateSystem ?? CoordinateSystem.Global;
                n.SetRestraintAxes(axes.V1, axes.V2);
                n.Tx = r.Restrains.Any(d => d.Dof == GeometryRestrain.DOF.DX && d.IsRestrained); n.Ty = r.Restrains.Any(d => d.Dof == GeometryRestrain.DOF.DY && d.IsRestrained); n.Tz = r.Restrains.Any(d => d.Dof == GeometryRestrain.DOF.DZ && d.IsRestrained);
            }
            var f = new Vector3d(0, 0, 0);
            foreach (var item in node.Loads.Values)
            {
                if (!(item is PointLoad p)) throw new NotSupportedException("UnsupportedFormFindingNodalLoad");
                var g = p.ToGlobal(); if (g.M1 != 0 || g.M2 != 0 || g.M3 != 0) throw new NotSupportedException("FormFindingDoesNotSupportNodalMoments");
                f += new Vector3d(g.F1, g.F2, g.F3);
            }
            n.SetForce(f.X / force, f.Y / force, f.Z / force);
            var cg = ff.CoordinateGoals.SingleOrDefault(g => g.NodeGuid == node.Guid);
            if (cg != null) { if (cg.Weights.X > 0) n.FixNLCoordX(cg.Weights.X); if (cg.Weights.Y > 0) n.FixNLCoordY(cg.Weights.Y); if (cg.Weights.Z > 0) n.FixNLCoordZ(cg.Weights.Z); }
        }
        foreach (var beam in model.BeamElements.Values.OrderBy(Slot).ThenBy(b => b.Guid))
        {
            if (beam.Loads.Count != 0 || beam.Attributes.Count != 0 || beam.BeamProperty != null) throw new NotSupportedException("UnsupportedFormFindingBeamPropertyOrLoad");
            var a=beam.Assignments;
            if (a.Formulation!=BeamFormulation.Cable || a.OffsetI!=null || a.OffsetJ!=null || a.OrientationNodeId!=null || a.SectionAxes!=null || a.SectionGeometryAxes!=null || a.SectionCentroidOffset!=null || a.RigidLengthI!=0 || a.RigidLengthJ!=0 || a.Sections.Count!=0 || a.OtherAssignments.Count!=0) throw new NotSupportedException("UnsupportedFormFindingBeamAssignment");
            if (a.AnalysisProfile == null) throw new NotSupportedException("MissingFormFindingLineMass");
            a.AnalysisProfile.Validate(); var start=a.AnalysisProfile.At(0); var end=a.AnalysisProfile.At(1);
            if(a.AnalysisProfile.Stations.Count!=2 || a.AnalysisProfile.Interpolation!=ProfileInterpolation.Constant || start.MassPerLength!=end.MassPerLength || new[]{start.EA,start.EI1,start.EI2,start.GJ,start.GA1,start.GA2,end.EA,end.EI1,end.EI2,end.GJ,end.GA1,end.GA2}.Any(x=>x!=0))throw new NotSupportedException("UnsupportedFormFindingStiffnessProfile");
            if(ff.Goals.Count(g=>g.BeamGuid==beam.Guid && g.Kind!=FormFindingGoalKind.Length && g.Kind!=FormFindingGoalKind.AxialForce)!=1)throw new NotSupportedException("RequiresOnePrimaryGoalPerBeam");
            int id = native.AddNewBeam(result.Ids[beam.NodeI.Guid], result.Ids[beam.NodeJ.Guid], plane); result.Ids.Add(beam.Guid, id); var b = native.Beams[id];
            // A unit reference area preserves the physical line mass exactly.
            b.Area = 1; b.Density = beam.Assignments.AnalysisProfile.At(0).MassPerLength * length * length / force;
            ApplyBeamLoads(b, beam, units);
            foreach (var g in ff.Goals.Where(g => g.BeamGuid == beam.Guid).OrderBy(g=>g.Kind==FormFindingGoalKind.Length?1:g.Kind==FormFindingGoalKind.AxialForce?2:0))
            {
                switch (g.Kind)
                {
                    case FormFindingGoalKind.ForceDensity: b.TargetForceDensity = g.Target / units.ForceDensityToNPerMm; break;
                    case FormFindingGoalKind.CableForce: b.TargetAxialForce = g.Target / force; break;
                    case FormFindingGoalKind.CurvatureRatio: b.TargetCurvatureRatio = g.Target; b.CurveChord = g.ChordLengthMm.Value / length; break;
                    case FormFindingGoalKind.MembraneForcePerWidth: b.TargetForceAreaDensity = g.Target / units.ForceDensityToNPerMm; b.MembraneType = g.Direction == FabricDirection.Warp ? TBeam.MembraneTypes.Warp : g.Direction == FabricDirection.Weft ? TBeam.MembraneTypes.Weft : TBeam.MembraneTypes.Undefined; break;
                    case FormFindingGoalKind.Length: b.FixNLLength(g.Target / length, g.Weight); continue;
                    case FormFindingGoalKind.AxialForce:
                        var source=b.ForceDensitySource;
                        if(source==TBeam.ForceDensitySources.FromAxialForce && b.TargetAxialForce!=g.Target / force) throw new NotSupportedException("ConflictingTensileAxialForceTargets");
                        b.TargetAxialForce = g.Target / force; b.ForceDensitySource=source; b.FixNLAxialForce(g.Weight); continue;
                    default: throw new NotSupportedException("UnsupportedFormFindingGoal");
                }
                b.Project = g.ProjectToPlane; if (g.Weight > 0) b.FixNLForceDensity(g.Weight);
            }
        }
        foreach (var area in model.AreaElements.Values.OrderBy(Slot).ThenBy(a => a.Guid))
        {
            if (area.PlateProperty != null || area.Loads.Count != 0 || area.Attributes.Count != 0) throw new NotSupportedException("UnsupportedFormFindingAreaPropertyOrLoad");
            var ns = area.Nodes.Select(n => native.Nodes[result.Ids[n.Guid]]).ToArray();
            var edges = Enumerable.Range(0, ns.Length).Select(i => native.Beams.FirstOrDefault(b => b.Node1.Id == ns[i].Id && b.Node2.Id == ns[(i + 1) % ns.Length].Id || b.Node2.Id == ns[i].Id && b.Node1.Id == ns[(i + 1) % ns.Length].Id)).Where(b => b != null).ToArray();
            var p = new TensileLib.Geometry.Plate(ns, edges, plane) { Id = native.Plates.Count + 1, MemThick = 1, Density = ff.SurfaceMasses.Single(m => m.AreaGuid == area.Guid).MassPerArea * length * length * length / force };
            native.Plates.Add(p); result.Ids.Add(area.Guid, p.Id);
        }
        result.Map = SolverIdentityMap.Create(model, "TensileLib-2.5.0.1", result.Ids); return result;
    }
    private static void ApplyBeamLoads(TBeam target, BeamElement beam, FormFindingUnits units)
    {
        foreach (var assignment in beam.Assignments.Loads)
        {
            if (assignment == null || assignment.Eccentricity != null || assignment.IsEquivalentNodalRepresentation
                || assignment.Start < 0 || assignment.Start > 1 || assignment.End < assignment.Start || assignment.End > 1)
                throw new NotSupportedException("UnsupportedFormFindingBeamLoad");
            if (assignment.Concentrated != null)
            {
                if (assignment.StartIntensity != null || assignment.EndIntensity != null || assignment.Start != assignment.End)
                    throw new ArgumentException("AmbiguousConcentratedBeamLoad");
                var p = assignment.Concentrated.ToGlobal();
                if (p.M1 != 0 || p.M2 != 0 || p.M3 != 0) throw new NotSupportedException("FormFindingDoesNotSupportBeamMoments");
                target.AddLoad(p.F1 / units.ForceToN, p.F2 / units.ForceToN, p.F3 / units.ForceToN, target.Length * assignment.Start);
                continue;
            }
            if (assignment.StartIntensity == null || assignment.EndIntensity == null)
                throw new ArgumentException("MissingBeamLoadIntensities");
            // GetGlobalLoadVector returns the total load on Line, not intensity.
            // Here the assigned interval and its projection determine the integration length.
            (Vector3d force, Vector3d moment) Intensity(LineLoad load)
            {
                var global = load.ToGlobal();
                return (new Vector3d(global.F1, global.F2, global.F3), new Vector3d(global.M1, global.M2, global.M3));
            }
            var a = Intensity(assignment.StartIntensity);
            var b = Intensity(assignment.EndIntensity);
            if (a.moment.Length != 0 || b.moment.Length != 0) throw new NotSupportedException("FormFindingDoesNotSupportBeamMoments");
            double span = beam.Length * (assignment.End - assignment.Start);
            if (assignment.LengthConvention == GPC.Model.Loads.Assignments.BeamLoadLengthConvention.ProjectedLength)
            {
                var normal = assignment.ProjectionPlaneNormal;
                if (normal == null || !(normal.Length > 0)) throw new ArgumentException("MissingProjectionPlaneNormal");
                var axis = new Vector3d(beam.EndPoint.X - beam.StartPoint.X, beam.EndPoint.Y - beam.StartPoint.Y, beam.EndPoint.Z - beam.StartPoint.Z);
                span *= axis.CrossProduct(normal).Length / (axis.Length * normal.Length);
            }
            else if (assignment.LengthConvention != GPC.Model.Loads.Assignments.BeamLoadLengthConvention.ActualLength)
                throw new ArgumentException("UnknownBeamLoadLengthConvention");
            var sum = a.force + b.force;
            if (sum.Length > 0 && a.force.CrossProduct(b.force).Length <= 1e-14 * a.force.Length * b.force.Length)
            {
                // Preserve the established single-resultant representation when its centroid
                // exists. A zero resultant may still carry a first moment and needs two forces.
                double av, bv;
                if (Math.Abs(sum.X) >= Math.Max(Math.Abs(sum.Y), Math.Abs(sum.Z))) { av = a.force.X; bv = b.force.X; }
                else if (Math.Abs(sum.Y) >= Math.Abs(sum.Z)) { av = a.force.Y; bv = b.force.Y; }
                else { av = a.force.Z; bv = b.force.Z; }
                var resultant = sum * (span / (2 * units.ForceToN));
                double station = assignment.Start + (assignment.End - assignment.Start) * (av + 2 * bv) / (3 * (av + bv));
                target.AddLoad(resultant.X, resultant.Y, resultant.Z, target.Length * station);
                continue;
            }
            // Exact equivalent forces at the ends of the loaded interval retain both force and
            // first moment, also for a sign-changing distribution with a zero resultant.
            var left = (a.force * 2 + b.force) * (span / (6 * units.ForceToN));
            var right = (a.force + b.force * 2) * (span / (6 * units.ForceToN));
            target.AddLoad(left.X, left.Y, left.Z, target.Length * assignment.Start);
            target.AddLoad(right.X, right.Y, right.Z, target.Length * assignment.End);
        }
    }
    public static Solver.ResultCodes Solve(TModel native, FormFindingDefinition ff)
    {
        if(!Enum.IsDefined(typeof(FormFindingMethod),ff.Method)) throw new NotSupportedException("UnknownFormFindingMethod");
        ForceDensitySolver solver = ff.Method == FormFindingMethod.NonlinearForceDensity
            ? (ForceDensitySolver)new NonLinearForceDensitySolver(native, ff.MaxIterations, ff.RelativeTolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations)
            : new LinearForceDensitySolver(native, ff.MaxIterations, ff.RelativeTolerance, ff.SolverToleranceA, ff.SolverToleranceB, ff.SolverMaxIterations);
        if(ff.Method!=FormFindingMethod.SteppedLinearForceDensity) return solver.SolveForceDensity();
        var lengths=native.Beams.Where(b=>b.HasNLFixedLength).ToArray();
        var forces=native.Beams.Where(b=>b.HasNLFixedAxialForce).ToArray();
        if(lengths.Any(b=>!(b.NLFixedLength>0)) || forces.Any(b=>b.TargetAxialForce==0)) throw new ArgumentException("InvalidSteppedTarget");
        for(int iteration=0;iteration<ff.MaxIterations;iteration++)
        {
            var status=solver.SolveForceDensity(); bool converged=true;
            foreach(var beam in native.Beams) beam.TargetForceDensity=beam.ForceDensity;
            foreach(var beam in lengths)
                if(Math.Abs((beam.Length-beam.NLFixedLength)/beam.NLFixedLength)>ff.RelativeTolerance)
                { converged=false; beam.TargetForceDensity*=beam.Length/beam.NLFixedLength; }
            foreach(var beam in forces)
                if(Math.Abs((beam.AxialForce-beam.TargetAxialForce)/beam.TargetAxialForce)>ff.RelativeTolerance)
                {
                    if(beam.AxialForce==0) throw new InvalidOperationException("ZeroForceInSteppedUpdate");
                    converged=false; beam.TargetForceDensity*=beam.TargetAxialForce/beam.AxialForce;
                }
            if(converged) return status;
        }
        return Solver.ResultCodes.NotconvergedAtMaxIterationNumber;
    }
    public static void Attach(Model model, Exported exported, Solver.ResultCodes status, LoadCaseBase resultCase = null)
    {
        if (model.AnalysisFingerprint() != exported.Map.AnalysisFingerprint) throw new InvalidOperationException("StaleFormFindingResult");
        if (status == Solver.ResultCodes.NotconvergedAtMaxIterationNumber) throw new InvalidOperationException("UnconvergedFormFindingResult");
        model.CaptureAnalysis(); var load = resultCase ?? model.LoadCases.Values.Single();
        double length = exported.Units.LengthToMm, force = exported.Units.ForceToN;
        model.Datasets.Add("form-finding", new AnalysisDataset { Id="form-finding", Program="TensileLib", SolverVersion="2.5.0.1", AnalysisId="FormFinding/"+status, InputFingerprint=model.Analysis.InputFingerprint, AnalysisSnapshotId=model.Analysis.Id, NormalizedUnits="N,mm,s,tonne", Semantics=AnalysisSemantics.NonlinearStatic });
        var sums=exported.Solver.Nodes.ToDictionary(n=>n.Id,n=>n.NodeForce+n.BeamSelfWeightForce+n.PlateSelfWeightForce+n.BeamLoadForce+n.PressureForce);
        foreach(var b in exported.Solver.Beams) { var axis=new Vector3d(b.Node2.X-b.Node1.X,b.Node2.Y-b.Node1.Y,b.Node2.Z-b.Node1.Z); var pull=axis*(b.AxialForce/b.Length); sums[b.Node1.Id]+=pull; sums[b.Node2.Id]-=pull; }
        Func<ResultState> state = () => new ResultState { DatasetId = "form-finding", InputFingerprint = model.Analysis.InputFingerprint, Step = "equilibrium", Semantics=AnalysisSemantics.NonlinearStatic, SourceRecord="TensileLib form-finding: "+status, Components=new[]{ComponentAvailability.Available,ComponentAvailability.Available,ComponentAvailability.Available,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable} };
        foreach (var node in model.NodesElements.Values)
        {
            var n = exported.Solver.Nodes[exported.Ids[node.Guid]];
            if(n.HasRestraint) { var reaction=n.RestrainedPart(sums[n.Id]*-1); node.AddResult(new NodeResult(new List<INodeResultLocation>{ new NodeResultForces(load,new ResultBeamForces(reaction.Z * force,reaction.X * force,reaction.Y * force,0,0,0,CoordinateSystem.Global)){Kind=NodalForceKind.SupportReaction,State=state()} })); }
            node.AddResult(new NodeResult(new List<INodeResultLocation> { new NodeResultDisplacement(load, new ResultDisplacement(n.DX * length, n.DY * length, n.DZ * length, 0, 0, 0)) { State = state() } }));
        }
        foreach (var beam in model.BeamElements.Values)
        {
            var b = exported.Solver.Beams[exported.Ids[beam.Guid]];
            var axial = new Vector3d(b.Node2.X-b.Node1.X,b.Node2.Y-b.Node1.Y,b.Node2.Z-b.Node1.Z); axial.Unitize();
            var transverse = Math.Abs(axial.Z) < .9 ? new Vector3d(0,0,1).CrossProduct(axial) : new Vector3d(1,0,0).CrossProduct(axial); transverse.Unitize();
            var axes = new CoordinateSystem(new Point3d(b.Node1.X * length,b.Node1.Y * length,b.Node1.Z * length), transverse, axial.CrossProduct(transverse));
            ResultState beamState() { var value=state(); value.Components=new[]{ComponentAvailability.Available,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable,ComponentAvailability.NotApplicable}; return value; }
            beam.Results.Add(new BeamResult(new[] { new StationResultBeamForces(load, new ResultBeamForces(b.AxialForce * force,0,0,0,0,0,axes),0) { State = beamState() }, new StationResultBeamForces(load, new ResultBeamForces(b.AxialForce * force,0,0,0,0,0,axes),1) { State = beamState() } }));
        }
        model.CompactResults();
    }
}
}
