using GPC.Model;
using GPC.Model.ElementProperties;
using GPC.Model.Materials;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A material: a GPC <see cref="Material"/> (values in N, mm, t, as FeMM), or for Membrane Material a GPC
    /// <see cref="MembranePlateProperty"/>: in GPC a coated fabric is a property per unit width, not an isotropic material (review B3).</summary>
    public class GH_Material : GpcGoo<ModelObject>
    {
        public override string TypeName => "Material";
        public override string TypeDescription => "Material definition";
        public GH_Material() { }
        public GH_Material(ModelObject material) : base(Copy(material)) { }
        public Material Material => Value as Material;
        public MembranePlateProperty Membrane => Value as MembranePlateProperty;
        public override bool IsValid => Value is Material || Value is MembranePlateProperty;
        public override IGH_Goo Duplicate() => new GH_Material(Value);
        public override bool CastFrom(object source)
        {
            if (source is Material || source is MembranePlateProperty) { Value = Copy((ModelObject)source); return true; }
            return false;
        }
        public override string ToString()
        {
            if (Value is MembranePlateProperty m)
                return $"Membrane: {m.Name};\nWeight: {m.MassPerArea} t/mm²;\nStrength warp: {m.StrengthWarp} N/mm;\nStrength weft: {m.StrengthWeft} N/mm;\n" +
                    $"Stiffness warp: {m.StiffnessWarp} N/mm;\nStiffness weft: {m.StiffnessWeft} N/mm;\nPoisson ratio: {m.NuWarpWeft};\nShear stiffness: {m.ShearStiffness} N/mm";
            if (Value is Material mat) return $"Material: {mat.Name};\nE: {mat.ElasticModulusTension} MPa;\nPoisson ratio: {mat.Ni};\nDensity: {mat.Density} t/mm³;\nThermal expansion: {mat.AlfaThermalExpansion}";
            return "Null Material";
        }
    }
}
