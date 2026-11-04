using GPC.Model.Core;
using GPC.Model.Elements;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>The results of a plate: the GPC element with its results (form-finding dataset).</summary>
    public class GH_PlateResults : GpcGoo<AreaElement>
    {
        public override string TypeName => "Plate Result";
        public override string TypeDescription => "The plate analysis results";
        public GH_PlateResults() { }
        public GH_PlateResults(GH_Plate element) : base(element?.Value == null ? null : ModelValues.CopyValue(element.Value)) { }
        public GH_PlateResults(AreaElement element) : base(element) { }
        public override IGH_Goo Duplicate() => new GH_PlateResults(Copy(Value));
        public override string ToString() => Value == null ? "No results" : "Plate Results: " + Value.Results.Count;
    }
}
