using GH_IO.Serialization;
using GPC.Model.ElementProperties;
using GPC.Model.Structure.Assignments;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A beam property: a GPC <see cref="ElasticBeamProperty"/> (material and section). The type chosen in Beam Property
    /// (cable, truss, beam, cut-off bar...) is the <see cref="BeamFormulation"/> that Beam assigns to its elements.</summary>
    public class GH_BeamProperty : GpcGoo<ElasticBeamProperty>
    {
        public BeamFormulation Formulation { get; set; } = BeamFormulation.StraightTwoNode;
        public override string TypeName => "BeamProperty";
        public override string TypeDescription => "Define a beam property";
        public GH_BeamProperty() { }
        public GH_BeamProperty(ElasticBeamProperty property, BeamFormulation formulation) : base(property) { Formulation = formulation; }
        public override IGH_Goo Duplicate() => new GH_BeamProperty(Copy(Value), Formulation);
        public override bool CastFrom(object source)
        {
            if (source is ElasticBeamProperty p) { Value = Copy(p); return true; }
            return false;
        }
        protected override void WriteExtra(GH_IWriter writer) => writer.SetInt32("Formulation", (int)Formulation);
        protected override void ReadExtra(GH_IReader reader) { int f = 0; if (reader.TryGetInt32("Formulation", ref f)) Formulation = (BeamFormulation)f; }
        public override string ToString() => Value == null ? "Null BeamProperty" : $"Beam property {Value.Name} ({Formulation}): section {Value.Section?.Name}, material {Value.Material?.Name}";
    }
}
