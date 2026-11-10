using Arachne.Common.Helpers;
using GPC.Model.ElementProperties;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A plate property: a GPC <see cref="ElasticPlateProperty"/> (material and thickness, document units) or, with a Membrane
    /// Material, the GPC <see cref="MembranePlateProperty"/> of the fabric.</summary>
    public class GH_PlateProperty : GpcGoo<PlateProperty>
    {
        public override string TypeName => "PlateProperty";
        public override string TypeDescription => "Define a plate property";
        public GH_PlateProperty() { }
        public GH_PlateProperty(PlateProperty property) : base(property) { }
        public override bool IsValid => Value is ElasticPlateProperty || Value is MembranePlateProperty;
        // The property is immutable (GpcGoo): a copy of the data shares it, as in FeMM.
        public override IGH_Goo Duplicate() => new GH_PlateProperty(Value);
        protected override byte[] Archive(PlateProperty value) => ContractArchive.Member(value);
        protected override PlateProperty Restore(byte[] data) => ContractArchive.ReadMember<PlateProperty>(data);
        public override bool CastFrom(object source)
        {
            if (source is ElasticPlateProperty || source is MembranePlateProperty) { Value = Copy((PlateProperty)source); return true; }
            return false;
        }
        public override string ToString()
        {
            switch (Value)
            {
                case ElasticPlateProperty e: return $"Plate property {e.Name}: thickness {e.MembraneThickness}, material {e.Material?.Name}";
                case MembranePlateProperty m: return $"Plate property {m.Name}: membrane, weight {m.MassPerArea} t/mm²";
                default: return "Null PlateProperty";
            }
        }
    }
}
