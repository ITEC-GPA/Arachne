using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GPC.Model.Elements;
using GPC.Model.Core;
using GPC.Model.Models;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using R = Rhino.Geometry;

namespace Arachne.Common.Analysis
{
    /// <summary>Compatibility output views of a completed GPC analysis. No TensileLib model or solver
    /// is needed to rebuild the GH geometry. The engineering input and its stored datasets are read only.</summary>
    public static class FormFindingOutputProjection
    {
        public sealed class Output
        {
            public ModelModel Model { get; internal set; }
            public R.Mesh Mesh { get; internal set; }
        }

        public static Output Project(ModelModel source, GPC.Model.Models.Model analysis, SolverOptionsModel options)
        {
            if (source == null || analysis == null || options == null) throw new ArgumentNullException();
            if (analysis.Analysis == null || analysis.Analysis.InputFingerprint != analysis.AnalysisFingerprint())
                throw new InvalidOperationException("StaleFormFindingAnalysis");
            if (!analysis.Datasets.TryGetValue("form-finding", out var dataset) || dataset.InputFingerprint != analysis.Analysis.InputFingerprint)
                throw new InvalidOperationException("MissingFormFindingDataset");
            var units = FormFindingUnits.From(source.Units);
            var output = new Output { Model = new ModelModel(source) };
            var nodes = analysis.NodesElements.Values.ToDictionary(n => n.Guid);
            if (options.Pressure != 0 && nodes.Values.Any(n => Samples<NodeResultDisplacement>(n).Single().State.Original?.ReaderVersion != "Arachne.FormFinding/1"))
                throw new InvalidOperationException("SavedPressureResultsUnavailableRerunAnalysis");
            var positions = nodes.ToDictionary(n => n.Key, n => Position(n.Value, units));
            Guid Identity(ElementModel element, string slot) => output.Model.InputIdentities.TryGetValue(element, out var identity)
                ? identity : element is NodeModel node ? node.Identity : FormFindingInputBuilder.Identity(source.InputOwner, slot);
            var pressureCase = options.PressureLoadCase == null ? null : new LoadCaseModel(options.PressureLoadCase);
            var reactionCase = FormFindingHelper.FormFindingCase();
            for (int i = 0; i < output.Model.Nodes.Count; i++)
            {
                var node = output.Model.Nodes[i]; var domain = nodes[Identity(node, "node:" + i)];
                node.Position = positions[domain.Guid];
                for (int j = node.Loads.Count - 1; j >= 0; j--) if (node.Loads[j] is NodePressureLoadModel) node.Loads.RemoveAt(j);
                for (int j = node.Results.Count - 1; j >= 0; j--) if (node.Results[j] is NodeReactResultModel) node.Results.RemoveAt(j);
                foreach (var force in Samples<NodeResultForces>(domain))
                {
                    var value = new R.Vector3d(force.Fx / units.ForceToN, force.Fy / units.ForceToN, force.Fz / units.ForceToN);
                    if (force.Kind == NodalForceKind.SupportReaction)
                        node.Results.Add(new NodeReactResultModel(reactionCase) { Force = value });
                    else if (force.Kind == NodalForceKind.AppliedPressure && value.Length != 0)
                    {
                        if (pressureCase == null) throw new InvalidOperationException("MissingSavedPressureCase");
                        node.Loads.Add(new NodePressureLoadModel(pressureCase, value));
                    }
                }
            }
            var beams = analysis.BeamElements.Values.ToDictionary(b => b.Guid);
            for (int i = 0; i < output.Model.Beams.Count; i++)
            {
                var beam = output.Model.Beams[i]; var domain = beams[Identity(beam, "beam:" + i)];
                beam.PointFrom = positions[domain.NodeI.Guid]; beam.PointTo = positions[domain.NodeJ.Guid];
                var goal = beam.Loads.FirstOrDefault(l => l.GetType() == typeof(ForceDensityModel))
                    ?? beam.Loads.FirstOrDefault(l => l.GetType() == typeof(FixedAxialForceModel))
                    ?? beam.Loads.FirstOrDefault(l => l.GetType() == typeof(FixedLengthModel));
                if (goal == null) continue;
                var force = Samples<StationResultBeamForces>(domain).First();
                for (int j = beam.Loads.Count - 1; j >= 0; j--) if (beam.Loads[j] is BeamPreLoadModel) beam.Loads.RemoveAt(j);
                beam.Loads.Add(new BeamPreLoadModel(goal.LoadCase, force.ResultBeamForces.N / units.ForceToN, BeamPreLoadModel.LoadType.Tension));
            }
            if (!options.CreateMesh) return output;
            var areas = analysis.AreaElements.Values.OrderBy(a => int.Parse(a.Source.OriginalId, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var mesh = new R.Mesh();
            var meshNodes = analysis.NodesElements.Values.OrderBy(n => int.Parse(n.Source.OriginalId, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            var vertexIds = meshNodes.ToDictionary(n => n.Guid, n => mesh.Vertices.Add(positions[n.Guid]));
            foreach (var area in areas)
            {
                var ids = area.Nodes.Select(n => vertexIds[n.Guid]).ToArray();
                if (ids.Length == 3) mesh.Faces.AddFace(ids[0], ids[1], ids[2]);
                else if (ids.Length == 4) mesh.Faces.AddFace(ids[0], ids[1], ids[2], ids[3]);
                else throw new NotSupportedException("UnsupportedFormFindingOutputTopology");
            }
            mesh.Compact(); mesh.RebuildNormals(); output.Mesh = mesh;
            if (output.Model.Plates.Count != 0)
            {
                var byId = areas.ToDictionary(a => a.Guid);
                for (int i = 0; i < output.Model.Plates.Count; i++)
                {
                    var plate = output.Model.Plates[i]; var area = byId[Identity(plate, "area:" + i)];
                    plate.Points = new ObservableCollection<R.Point3d>(area.Nodes.Select(n => positions[n.Guid]));
                    plate.CreateShapes();
                }
            }
            else if (options.PlateProperty != null)
            {
                var property = SerializationHelper.Clone(options.PlateProperty);
                var plates = areas.Select(a => new PlateModel(a.Nodes.Select(n => positions[n.Guid]).ToArray(), property)
                    { PlateId = a.Source.OriginalId }).ToArray();
                output.Model.AddElements(plates);
                for (int i = 0; i < plates.Length; i++) output.Model.InputIdentities.Add(plates[i], areas[i].Guid);
            }
            return output;
        }

        private static IEnumerable<T> Samples<T>(Element element) where T : ResultLocation
            => element.Results.SelectMany(r => r.Results).OfType<T>().Where(r => r.State?.DatasetId == "form-finding");

        private static R.Point3d Position(NodeElement node, FormFindingUnits units)
        {
            var sample = Samples<NodeResultDisplacement>(node).Single();
            var original = sample.State.Original;
            // Preserve the exact solver coordinates when available. Subtracting then re-adding
            // a displacement can lose significant bits for a short movement at a large coordinate.
            if (original?.ReaderVersion == "Arachne.FormFinding/1" && original.ComponentOrder == "X,Y,Z" && original.Values?.Length == 3)
            {
                double factor = original.LengthToMm / units.LengthToMm;
                return new R.Point3d(original.Values[0].Value * factor, original.Values[1].Value * factor, original.Values[2].Value * factor);
            }
            var delta = sample.ResultDisplacement;
            return new R.Point3d((node.Position.X + delta.D1) / units.LengthToMm,
                (node.Position.Y + delta.D2) / units.LengthToMm, (node.Position.Z + delta.D3) / units.LengthToMm);
        }
    }
}
