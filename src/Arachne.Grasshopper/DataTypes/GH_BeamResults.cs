using GPC.Model.Core;
using GPC.Model.Elements;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>The results of a beam: the GPC element with its results (form-finding dataset).</summary>
    public class GH_BeamResults : GpcGoo<BeamElement>
    {
        public override string TypeName => "Beam Result";
        public override string TypeDescription => "The beam analysis results";
        public GH_BeamResults() { }
        public GH_BeamResults(GH_Beam element) : base(element?.Value == null ? null : ModelValues.CopyValue(element.Value, Arachne.Common.Analysis.SharedCopies.Options)) { }
        public GH_BeamResults(BeamElement element) : base(element) { }
        public override IGH_Goo Duplicate() => new GH_BeamResults(CopySharing(Value));
        public override string ToString() => Value == null ? "No results" : "Beam Results: " + Value.Results.Count;
    }
}
