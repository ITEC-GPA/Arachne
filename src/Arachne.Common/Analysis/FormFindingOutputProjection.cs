using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Core.Identity;
using GPC.Model.ElementProperties;
using GPC.Model.Elements;
using GPC.Model.LoadCases;
using GPC.Model.Models;
using GPC.Model.Results;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using GPC.Model.Results.Storage;
using G = GPC.Geometry;
using R = Rhino.Geometry;

namespace Arachne.Common.Analysis
{
    /// <summary>
    /// The outputs of a completed run, from the GPC analysis and its results only (no solver): the input model with the found
    /// positions and the results of the run (pre-loads, pressure forces, reactions), the plates generated from the beams when the
    /// options give their property, and the mesh. As the former Form Find (Arachne 7b1108d); the exact solver coordinates are those
    /// kept in the results, so a restored output is identical to the computed one.
    /// </summary>
    public static class FormFindingOutputProjection
    {
        public sealed class Output
        {
            public Model Model { get; internal set; }
            public R.Mesh Mesh { get; internal set; }
        }

        internal static IEnumerable<T> Samples<T>(Element element) where T : ResultLocation
            => element.Results.SelectMany(r => r.Results).OfType<T>().Where(r => r.State?.DatasetId == "form-finding");

        /// <summary>The exact solver value of a sample (document units), else the GPC value converted.</summary>
        internal static double[] Solver(ResultLocation sample, int count)
        {
            var original = sample.State?.Original;
            if (original?.ReaderVersion == "Arachne.FormFinding/1" && original.Values?.Length == count && original.Values.All(v => v.HasValue))
                return original.Values.Select(v => v.Value).ToArray();
            return null;
        }

        /// <summary>The position of a node in the document units: the exact solver coordinates after a form finding, else its position.</summary>
        public static R.Point3d Position(NodeElement node, FormFindingUnits units)
        {
            var sample = Samples<NodeResultDisplacement>(node).FirstOrDefault();
            var exact = sample == null ? null : Solver(sample, 3);
            if (exact != null && sample.State.Original.LengthToMm == units.LengthToMm) return new R.Point3d(exact[0], exact[1], exact[2]);
            double l = units.LengthToMm;
            return l == 1 ? new R.Point3d(node.Position.X, node.Position.Y, node.Position.Z) : new R.Point3d(node.Position.X / l, node.Position.Y / l, node.Position.Z / l);
        }

        /// <summary>A force sample in the document units (exact solver value when kept).</summary>
        public static R.Vector3d Force(NodeResultForces sample, FormFindingUnits units)
        {
            var exact = Solver(sample, 3);
            if (exact != null) return new R.Vector3d(exact[0], exact[1], exact[2]);
            double f = units.ForceToN;
            return new R.Vector3d(sample.Fx / f, sample.Fy / f, sample.Fz / f);
        }

        /// <summary>The pre-load (axial force) of a beam in the document units, or null without one.</summary>
        public static double? Preload(BeamElement beam, FormFindingUnits units)
        {
            var sample = Samples<StationResultBeamForces>(beam).FirstOrDefault();
            if (sample == null) return null;
            var exact = Solver(sample, 1);
            return exact != null ? exact[0] : sample.ResultBeamForces.N / units.ForceToN;
        }

        public static Output Project(Model source, Model analysis, FormFindingOptions options, ModelUnits documentUnits)
        {
            if (source == null || analysis == null || options == null) throw new ArgumentNullException();
            // The binding of the dataset to the graph is checked where the graph comes from: Attach takes the fingerprint of the
            // exported graph, the sidecar compares it with the graph it reads (ExternalModelArchive.Open). It is not recomputed here.
            if (!analysis.Datasets.TryGetValue("form-finding", out var dataset) || string.IsNullOrEmpty(dataset.InputFingerprint) || dataset.Semantics != AnalysisSemantics.FormFinding)
                throw new InvalidOperationException("MissingFormFindingDataset");
            var units = FormFindingUnits.From(documentUnits); double length = units.LengthToMm;
            var output = ModelValues.Copy(source);
            foreach (var e in output.AllElements) if (e.Results.Count != 0) e.Results.Clear();
            output.Datasets.Remove("form-finding");
            output.Datasets.Add("form-finding", ModelValues.CopyValue(dataset));
            LoadCaseBase Case(LoadCaseBase value) => value == null ? null : FormFindingInputBuilder.Registered(output, value);
            ResultState State(ResultLocation sample) => ModelValues.CopyValue(sample.State);

            var analysisNodes = analysis.NodesElements.Values.ToDictionary(n => n.Guid);
            var outputNodes = output.NodesElements.Values.ToDictionary(n => n.Guid);
            foreach (var node in output.NodesElements.Values)
            {
                if (!analysisNodes.TryGetValue(node.Guid, out var solved)) throw new InvalidOperationException("MissingFormFindingNode");
                var samples = new List<INodeResultLocation>();
                foreach (var d in Samples<NodeResultDisplacement>(solved))
                {
                    var exact = Solver(d, 3);
                    node.Position = exact != null && length == 1 ? new G.Point3d(exact[0], exact[1], exact[2])
                        : exact != null ? new G.Point3d(exact[0] * length, exact[1] * length, exact[2] * length)
                        : new G.Point3d(solved.Position.X + d.ResultDisplacement.D1, solved.Position.Y + d.ResultDisplacement.D2, solved.Position.Z + d.ResultDisplacement.D3);
                    var r = d.ResultDisplacement;
                    samples.Add(new NodeResultDisplacement(Case((LoadCaseBase)d.Case), new ResultDisplacement(r.D1, r.D2, r.D3, r.R1, r.R2, r.R3)) { State = State(d) });
                }
                foreach (var f in Samples<NodeResultForces>(solved))
                {
                    var v = f.ResultBeamForces;
                    samples.Add(new NodeResultForces(Case((LoadCaseBase)f.Case), new ResultBeamForces(v.N, v.V1, v.V2, v.T, v.M1, v.M2, G.CoordinateSystem.Global))
                    { Kind = f.Kind, Body = f.Body, State = State(f) });
                }
                if (samples.Count != 0) node.AddResult(new NodeResult(samples));
            }
            var analysisBeams = analysis.BeamElements.Values.ToDictionary(b => b.Guid);
            foreach (var beam in output.BeamElements.Values)
            {
                if (!analysisBeams.TryGetValue(beam.Guid, out var solved)) throw new InvalidOperationException("MissingFormFindingBeam");
                var stations = Samples<StationResultBeamForces>(solved).Select(s =>
                {
                    var v = s.ResultBeamForces;
                    return new StationResultBeamForces(Case((LoadCaseBase)s.Case), new ResultBeamForces(v.N, v.V1, v.V2, v.T, v.M1, v.M2, ModelValues.CopyValue(v.CoordinateSystem)), s.ParametricDistance) { State = State(s) };
                }).ToArray();
                if (stations.Length != 0) beam.Results.Add(new BeamResult(stations));
            }

            // Plates: the explicit ones follow their nodes; the ones generated from the beams are added with the property of the options.
            var generated = analysis.AreaElements.Values.Where(a => a.Source?.OriginalId?.StartsWith("generated:", StringComparison.Ordinal) == true).ToList();
            if (options.CreateMesh && output.AreaElements.Count == 0 && generated.Count != 0 && options.PlateProperty != null)
            {
                var property = new ElementDrafts.Scale(units, true).Property(options.PlateProperty);
                if (output.GetPlateProperty(property.Name) == null) output.AddProperty(property);
                else property = output.GetPlateProperty(property.Name);
                foreach (var area in generated)
                {
                    var plate = new AreaElement(area.Guid, null, property, null, area.Name, area.Id);
                    plate.ConnectNodes(area.Nodes.Select(n => outputNodes[n.Guid]).ToArray());
                    plate.Source = new SourceIdentity("Arachne", output.Guid.ToString("D"), EntityFamily.Shell, area.Id.ToString(CultureInfo.InvariantCulture));
                    output.AreaElements.Add(plate);
                }
            }
            output.CompactResults();
            if (!options.CreateMesh) return new Output { Model = output };

            // The mesh of every solver node and plate, in the solver order, as the former Form Find
            var mesh = new R.Mesh();
            var vertex = new Dictionary<Guid, int>();
            foreach (var node in analysis.NodesElements.Values) vertex.Add(node.Guid, mesh.Vertices.Add(Position(node, units)));
            foreach (var area in analysis.AreaElements.Values.OrderBy(a => a.Id))
            {
                var ids = area.Nodes.Select(n => vertex[n.Guid]).ToArray();
                if (ids.Length == 4) mesh.Faces.AddFace(ids[0], ids[1], ids[2], ids[3]);
                else if (ids.Length == 3) mesh.Faces.AddFace(ids[0], ids[1], ids[2]);
            }
            mesh.Compact(); mesh.RebuildNormals();
            return new Output { Model = output, Mesh = mesh };
        }
    }
}
