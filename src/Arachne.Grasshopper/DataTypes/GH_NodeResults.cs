using GPC.Model.Core;
using GPC.Model.Elements;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>The results of a node: the GPC element with its results (form-finding dataset).</summary>
    public class GH_NodeResults : GpcGoo<NodeElement>
    {
        public override string TypeName => "Node Result";
        public override string TypeDescription => "The node analysis results";
        public GH_NodeResults() { }
        public GH_NodeResults(GH_Node element) : base(element?.Value == null ? null : ModelValues.CopyValue(element.Value, ModelCopyOptions.ShareProperties)) { }
        public GH_NodeResults(NodeElement element) : base(element) { }
        public override IGH_Goo Duplicate() => new GH_NodeResults(CopySharing(Value));
        public override string ToString() => Value == null ? "No results" : "Node Results: " + Value.Results.Count;
    }
}
