using Arachne.Common.Analysis;
using GPC.Model.Attributes;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A group: a GPC <see cref="Group"/>, identified by its full name (parent path) as in FeMM.</summary>
    public class GH_Group : GpcGoo<Group>
    {
        public override string TypeName => "Group";
        public override string TypeDescription => "The group definition";
        public GH_Group() { }
        public GH_Group(Group group) : base(Copy(group)) { }
        public GH_Group(string name, Group parent) : base(InputIdentity.Group(name, parent == null ? null : Copy(parent))) { }
        public override IGH_Goo Duplicate() => new GH_Group(Value);
        public override bool CastFrom(object source)
        {
            if (source is Group g) { Value = Copy(g); return true; }
            return false;
        }
        public override string ToString() => Value == null ? "Null Group" : "Group: " + InputIdentity.FullName(Value).TrimStart('/');
    }
}
