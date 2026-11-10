using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.DataTypes;
using GPC.Model.Analysis;
using GPC.Model.Loads;
using GPC.Model.Results.Locations;
using GPC.Model.Results.State;
using Rhino.Geometry;

// What the checks read of a model, in the document units: the GPC model of a GH_Model seen as the former FeMM lists
// (positions, restraints, loads, pre-loads, reactions). Test-side view only.
internal sealed class ModelView
{
    internal sealed class LoadView { public string LoadCase; public Vector3d Value; public bool IsPressure; }
    internal sealed class NodeView
    {
        public string NodeId; public Point3d Position; public bool Restrained; public bool HasWeight;
        public List<LoadView> Loads = new List<LoadView>(); public List<Vector3d> Reactions = new List<Vector3d>();
    }
    internal sealed class BeamView
    {
        public string BeamId; public Point3d PointFrom, PointTo; public double? PreLoad; public string PreLoadCase;
        public int ForceDensities, FixedLengths, FixedAxialForces; public double SectionArea;
    }
    public List<NodeView> Nodes = new List<NodeView>();
    public List<BeamView> Beams = new List<BeamView>();
    public int Plates;

    public static ModelView Of(GH_Model model)
    {
        var u = model.Scale; var view = new ModelView { Plates = model.Value.AreaElements.Count };
        foreach (var n in model.Value.NodesElements.Values)
        {
            var node = new NodeView { NodeId = n.Name, Position = FormFindingOutputProjection.Position(n, u), Restrained = n.Assignments.Restrains.Count > 0,
                HasWeight = n.Attributes.Values.OfType<FormFindingCoordinateAttribute>().Any() };
            foreach (var p in n.Loads.Values.OfType<PointLoad>())
                node.Loads.Add(new LoadView { LoadCase = p.LoadCase?.Name, Value = new Vector3d(p.F1 / u.ForceToN, p.F2 / u.ForceToN, p.F3 / u.ForceToN) });
            foreach (var f in n.Results.SelectMany(r => r.Results).OfType<NodeResultForces>().Where(f => f.State?.DatasetId == "form-finding"))
            {
                var v = FormFindingOutputProjection.Force(f, u);
                if (f.Kind == NodalForceKind.AppliedPressure) node.Loads.Add(new LoadView { LoadCase = ((GPC.Model.LoadCases.LoadCaseBase)f.Case).Name, Value = v, IsPressure = true });
                else if (f.Kind == NodalForceKind.SupportReaction) node.Reactions.Add(v);
            }
            view.Nodes.Add(node);
        }
        foreach (var b in model.Value.BeamElements.Values)
        {
            var goals = Arachne.Common.Analysis.ElementDrafts.Goals(b).ToList();
            var station = b.Results.SelectMany(r => r.Results).OfType<StationResultBeamForces>().FirstOrDefault(s => s.State?.DatasetId == "form-finding");
            var section = (b.BeamProperty as GPC.Model.ElementProperties.ElasticBeamProperty)?.Section as GPC.Model.Sections.Section;
            view.Beams.Add(new BeamView { BeamId = b.Name, PointFrom = FormFindingOutputProjection.Position(b.NodeI, u), PointTo = FormFindingOutputProjection.Position(b.NodeJ, u),
                PreLoad = FormFindingOutputProjection.Preload(b, u), PreLoadCase = ((GPC.Model.LoadCases.LoadCaseBase)station?.Case)?.Name,
                ForceDensities = goals.Count(g => ElementDrafts.IsPrimary(g.Goal.Kind)), FixedLengths = goals.Count(g => g.Goal.Kind == FormFindingGoalKind.Length),
                FixedAxialForces = goals.Count(g => g.Goal.Kind == FormFindingGoalKind.AxialForce),
                SectionArea = section == null ? 0 : section.Area / (u.LengthToMm * u.LengthToMm) });
        }
        return view;
    }
}
