using Arachne.Common.Analysis;
using GPC.Model.LoadCases;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A load case: a GPC <see cref="LoadCaseBase"/>. As in FeMM, the case is its name: two Load Case components with the
    /// same name give the same case (same identity).</summary>
    public class GH_LoadCase : GpcGoo<LoadCaseBase>
    {
        public override string TypeName => "LoadCase";
        public override string TypeDescription => "The load case definition";
        public GH_LoadCase() { }
        public GH_LoadCase(LoadCaseBase loadCase) : base(Copy(loadCase)) { }
        public GH_LoadCase(string name) : base(name == "Dead" ? InputIdentity.LoadCase("1", "Dead") : InputIdentity.LoadCase("", name)) { }
        public override IGH_Goo Duplicate() => new GH_LoadCase(Value);
        public override bool CastFrom(object source)
        {
            if (source is LoadCaseBase lc) { Value = Copy(lc); return true; }
            if (source is GH_String s && !string.IsNullOrEmpty(s.Value)) { Value = new GH_LoadCase(s.Value).Value; return true; }
            if (source is string text && !string.IsNullOrEmpty(text)) { Value = new GH_LoadCase(text).Value; return true; }
            return false;
        }
        public override string ToString() => Value == null ? "Null LoadCase" : $"LoadCase: {Value.Name}";
    }
}
