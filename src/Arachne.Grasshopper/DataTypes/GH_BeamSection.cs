using GPC.Model.Sections;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A beam section: a GPC <see cref="Section"/> with the properties computed as in FeMM (declared) and the outline for the
    /// previews, in the document units (scaled to mm by Build Model).</summary>
    public class GH_BeamSection : GpcGoo<Section>
    {
        public override string TypeName => "BeamSection";
        public override string TypeDescription => "Defines a beam section";
        public GH_BeamSection() { }
        public GH_BeamSection(Section section) : base(section) { }
        // The section is immutable (GpcGoo): a copy of the data shares it.
        public override IGH_Goo Duplicate() => new GH_BeamSection(Value);
        public override bool CastFrom(object source)
        {
            if (source is Section s) { Value = Copy(s); return true; }
            return false;
        }
        public override string ToString() => Value == null ? "Null Section" : $"Section {Value.Name}: area {Value.Area}";
    }
}
