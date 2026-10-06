using System;
using System.Linq;
using System.Threading;
#if TENSILELIB
using System.Collections.Generic;
using GPC.Geometry;
using TensileLib.FormFinding;
using TensileLib.Geometry;
#endif

namespace Arachne.Core
{
    public sealed class TensileFormFindingSolver
    {
        public static bool IsAvailable
        {
            get {
#if TENSILELIB
                return true;
#else
                return false;
#endif
            }
        }

        public FormResult Solve(FormModel model, SolverSettings settings, CancellationToken cancellation = default, Action<SolverIteration> progress = null)
        {
            var result = new FormResult { Status = SolveStatus.Failed };
            try
            {
                if (model == null) throw new ArgumentNullException(nameof(model));
                Validation.Settings(settings);
                cancellation.ThrowIfCancellationRequested();
                model=model.Snapshot();settings=settings.Copy();result.Model=model;result.Settings=settings.Copy();
                var checks=ModelChecks.Check(model);
                if(!checks.Valid)throw new ArgumentException(string.Join(" ",checks.Errors));
#if TENSILELIB
                SolveAvailable(model, settings, cancellation, result, progress);
#else
                result.Status = SolveStatus.DependencyMissing;
                result.Messages.Add("TensileLib non disponibile: compilare il progetto adiacente in Release o impostare TensileLibPath e ricompilare. Nessun calcolo eseguito.");
#endif
            }
            catch (OperationCanceledException) { result.Status = SolveStatus.Cancelled; result.Messages.Add("Calculation cancelled."); }
            catch (Exception error) { result.Status = SolveStatus.Failed; result.Messages.Add(error.Message); }
            // Never publish partial or stale geometry as a valid form-finding result.
            if (result.Status != SolveStatus.Converged)
            {
                result.Positions.Clear(); result.AxialForces.Clear(); result.ForceDensities.Clear();
                result.PressureForces.Clear(); result.Faces.Clear();
                result.Solved=null;
            }
            return result;
        }

#if TENSILELIB
        // Adapter for TensileLib 2.1.0.0: GPC geometry, oriented pressure and iteration callbacks.
        private static void SolveAvailable(FormModel source, SolverSettings settings, CancellationToken cancellation, FormResult result, Action<SolverIteration> progress)
        {
            var model = new Model();
            var plane = new Plane(settings.PlaneOrigin, settings.PlaneNormal);
            var nodes = new List<Node>();
            var beams = new List<Beam>();
            var edgesByNodes = new Dictionary<(int, int), Beam>();
            foreach (var node in source.Nodes)
            {
                model.AddNewNode(node.Position.X, node.Position.Y, node.Position.Z);
                var target = model.Nodes.Last(); // No assumption that node IDs are list indices.
                nodes.Add(target);
                target.Tx = node.FixX; target.Ty = node.FixY; target.Tz = node.FixZ;
                target.SetForce(node.Force.X, node.Force.Y, node.Force.Z);
                if (node.PositionWeight.X > 0) target.FixNLCoordX(node.PositionWeight.X);
                if (node.PositionWeight.Y > 0) target.FixNLCoordY(node.PositionWeight.Y);
                if (node.PositionWeight.Z > 0) target.FixNLCoordZ(node.PositionWeight.Z);
                if (settings.Kind != SolverKind.NonLinear && (node.PositionWeight.X > 0 || node.PositionWeight.Y > 0 || node.PositionWeight.Z > 0))
                    Warn(result, "Position weights are applied only by the non-linear solver.");
            }
            foreach (var edge in source.Edges)
            {
                var cable = edge.Cable;
                model.AddNewBeam(nodes[edge.Start].Id, nodes[edge.End].Id, plane);
                var beam = model.Beams.Last();
                beams.Add(beam);
                edgesByNodes.Add((edge.Start, edge.End), beam);
                edgesByNodes.Add((edge.End, edge.Start), beam);
                beam.Area = cable.Area; beam.Density = cable.Density;
                switch (cable.TargetKind)
                {
                    case TargetKind.ForceDensity: beam.TargetForceDensity = cable.Target; break;
                    case TargetKind.CableForce: beam.TargetAxialForce = cable.Target; break;
                    case TargetKind.Membrane:
                        beam.TargetForceAreaDensity = cable.Target;
                        beam.MembraneType = (Beam.MembraneTypes)(int)cable.Direction;
                        break;
                    case TargetKind.Curvature: beam.TargetCurvatureRatio = cable.Target; beam.CurveChord = cable.Chord; break;
                }
                beam.Project = cable.Project;
                if (cable.TargetWeight > 0)
                {
                    beam.FixNLForceDensity(cable.TargetWeight);
                    if (settings.Kind != SolverKind.NonLinear) Warn(result, "Target weights are applied only by the non-linear solver.");
                }
                if (cable.FixedLength.HasValue) beam.FixNLLength(cable.FixedLength.Value, cable.ConstraintWeight);
                if (cable.FixedForce.HasValue) { beam.TargetAxialForce = cable.FixedForce.Value; beam.FixNLAxialForce(cable.ConstraintWeight); }
                if (settings.Kind == SolverKind.Linear && (cable.FixedLength.HasValue || cable.FixedForce.HasValue))
                    Warn(result, "Fixed length and fixed force require Linear iterative or Non-linear.");
                foreach (var load in cable.Loads)
                    beam.AddLoad(load.Force.X, load.Force.Y, load.Force.Z, Validation.Distance(cable.Start, cable.End) * load.Fraction);
            }
            foreach (var face in source.Faces)
            {
                var boundaries = new List<Beam>();
                for (int i=0; i<face.Length; i++)
                    if (edgesByNodes.TryGetValue((face[i], face[(i+1)%face.Length]), out var boundary)) boundaries.Add(boundary);
                var plate = new Plate(face.Select(i => nodes[i]).ToArray(), boundaries.ToArray(), plane) { Id = model.Plates.Count + 1 };
                model.Plates.Add(plate);
            }
            model.Gravity = settings.Gravity; model.Pressure = settings.Pressure;
            // This engine always performs its internal mesh initialization. CreateMesh
            // controls only the output returned by Arachne, not the engine's topology.
            model.Plane = plane;
            if (model.Plates.Count == 0) GeneratePlatesForConnectedParts(source, model, nodes, plane, edgesByNodes, cancellation);
            if (settings.Pressure != 0 && model.Plates.Count == 0) throw new InvalidOperationException("Pressure requires valid faces; supply a mesh to Model.");
            if (settings.Gravity != 0 && source.Edges.Any(e => e.Cable.Area == 0 || e.Cable.Density == 0))
                Warn(result, "Some cables have zero area or density and therefore no self-weight.");

            double algebraicTolerance = settings.AlgebraicToleranceA > 0 ? settings.AlgebraicToleranceA : 1e-5;
            int algebraicIterations = settings.AlgebraicMaxIterations > 0 ? settings.AlgebraicMaxIterations : 10000;
            ForceDensitySolver solver = settings.Kind == SolverKind.NonLinear
                ? (ForceDensitySolver)new NonLinearForceDensitySolver(model, settings.MaxIterations, settings.Tolerance, algebraicTolerance, algebraicIterations)
                : new LinearForceDensitySolver(model, settings.MaxIterations, settings.Tolerance, algebraicTolerance, algebraicIterations);
            bool acceptedCode = false;
            var watch=System.Diagnostics.Stopwatch.StartNew();
            solver.CancellationToken=cancellation;
            bool converged = IterationControl.Run(settings.Kind == SolverKind.LinearIterative ? settings.MaxIterations : 1, iteration =>
            {
                result.OuterIterations=iteration;
                solver.IterationCompleted=(inner,criterion,metric)=>{
                    var step=new SolverIteration(iteration,inner,criterion,metric,watch.Elapsed.TotalMilliseconds);
                    result.History.Add(step);progress?.Invoke(step);
                };
                var code = solver.SolveForceDensity();
                cancellation.ThrowIfCancellationRequested();
                // Unknown or failure codes never count as success.
                string name = code.ToString();
                acceptedCode = name == "Converged" || name == "ConvergedUsingSlowMethodWithoutWeigths";
                if (!acceptedCode)
                {
                    result.Status = name == "NotconvergedAtMaxIterationNumber" ? SolveStatus.NotConverged : SolveStatus.Failed;
                    result.Messages.Add("TensileLib: " + name);
                    return true; // Stop the outer loop; final status remains unsuccessful.
                }
                if (name != "Converged") Warn(result, "TensileLib converged using its fallback method without weights.");
                if (settings.Kind != SolverKind.LinearIterative) return true;
                bool satisfied = true;
                for (int i=0; i<beams.Count; i++)
                {
                    var beam = beams[i]; var cable = source.Edges[i].Cable;
                    if (cable.FixedLength.HasValue && IterationControl.RelativeError(beam.Length, cable.FixedLength.Value) > settings.Tolerance)
                    {
                        satisfied = false;
                        beam.TargetForceDensity = IterationControl.RescaleDensity(beam.TargetForceDensity, beam.Length, cable.FixedLength.Value);
                    }
                    if (cable.FixedForce.HasValue && IterationControl.RelativeError(beam.AxialForce, cable.FixedForce.Value) > settings.Tolerance)
                    {
                        satisfied = false;
                        beam.TargetForceDensity = IterationControl.RescaleDensity(beam.TargetForceDensity, cable.FixedForce.Value, beam.AxialForce);
                    }
                }
                return satisfied;
            }, cancellation, out int iterations);
            result.OuterIterations = iterations;
            if (!acceptedCode) return;
            if (!converged) { result.Status = SolveStatus.NotConverged; result.Messages.Add("Maximum outer iterations reached."); return; }
            foreach (var node in nodes)
            {
                var point = new Point3d(node.X, node.Y, node.Z); Validation.Point(point);
                result.Positions.Add(point);
                var pressure = new Vector3d(node.PressureX, node.PressureY, node.PressureZ); Validation.Vector(pressure);
                result.PressureForces.Add(pressure);
            }
            foreach (var beam in beams)
            {
                Validation.Positive(beam.Length, "Solved cable length"); Validation.Finite(beam.AxialForce, "Solved force");
                double density = beam.AxialForce / beam.Length; Validation.Finite(density, "Solved force density");
                result.AxialForces.Add(beam.AxialForce); result.ForceDensities.Add(density);
            }
            // Retain topology for result interrogation even when preview mesh output is disabled.
            {
                var nodeIndices = nodes.Select((n, i) => new { n.Id, Index = i }).ToDictionary(n => n.Id, n => n.Index);
                foreach (var plate in model.Plates)
                {
                    var face = new List<int> { nodeIndices[plate.Node1.Id], nodeIndices[plate.Node2.Id], nodeIndices[plate.Node3.Id] };
                    if (plate.PlateType == Plate.PlateTypes.Quad4) face.Add(nodeIndices[plate.Node4.Id]);
                    result.Faces.Add(face.ToArray());
                }
            }
            result.Solved=new SolvedModel(source,result.Positions,result.AxialForces,result.ForceDensities,
                nodes.Select(n=>new Vector3d(n.BeamSelfWeightForce)).ToArray(),nodes.Select(n=>new Vector3d(n.BeamLoadForce)).ToArray(),result.PressureForces,result.Faces);
            if(result.AxialForces.Any(f=>f<0))Warn(result,"Negative axial forces found: these members are in compression and are not valid tension-only cables.");
            result.Status = SolveStatus.Converged;
        }
        private static void GeneratePlatesForConnectedParts(FormModel source, Model target, List<Node> nodes, Plane plane,
            Dictionary<(int,int),Beam> edgesByNodes, CancellationToken cancellation)
        {
            // TensileLib starts face traversal from beam 1, so disconnected nets need separate traversals.
            var adjacent=source.Nodes.Select(_=>new List<int>()).ToArray();
            foreach(var edge in source.Edges){adjacent[edge.Start].Add(edge.End);adjacent[edge.End].Add(edge.Start);}
            var seen=new HashSet<int>();var parts=new List<List<int>>();
            for(int start=0;start<nodes.Count;start++)
            {
                if(!seen.Add(start))continue;var part=new List<int>();var queue=new Queue<int>();queue.Enqueue(start);
                while(queue.Count>0){int index=queue.Dequeue();part.Add(index);foreach(int next in adjacent[index])if(seen.Add(next))queue.Enqueue(next);}
                parts.Add(part);
            }
            if(parts.Count==1){target.GeneratePlatesFromBeams();return;}
            foreach(var part in parts)
            {
                cancellation.ThrowIfCancellationRequested();
                var local=new Model{Plane=plane};var localIds=new Dictionary<int,int>();var globalIndices=new Dictionary<int,int>();
                foreach(int index in part){var p=source.Nodes[index].Position;int id=local.AddNewNode(p.X,p.Y,p.Z);localIds.Add(index,id);globalIndices.Add(id,index);}
                foreach(var edge in source.Edges)if(localIds.ContainsKey(edge.Start))local.AddNewBeam(localIds[edge.Start],localIds[edge.End],plane);
                if(local.Beams.Count==0)continue;
                local.GeneratePlatesFromBeams();
                foreach(var plate in local.Plates)
                {
                    var ids=new List<int>{globalIndices[plate.Node1.Id],globalIndices[plate.Node2.Id],globalIndices[plate.Node3.Id]};
                    if(plate.PlateType==Plate.PlateTypes.Quad4)ids.Add(globalIndices[plate.Node4.Id]);
                    var boundary=new List<Beam>();
                    for(int i=0;i<ids.Count;i++)if(edgesByNodes.TryGetValue((ids[i],ids[(i+1)%ids.Count]),out var beam))boundary.Add(beam);
                    target.Plates.Add(new Plate(ids.Select(i=>nodes[i]).ToArray(),boundary.ToArray(),plane){Id=target.Plates.Count+1});
                }
            }
        }
        private static void Warn(FormResult result, string message)
        { if (!result.Messages.Contains(message)) result.Messages.Add(message); }
#endif
    }
}
