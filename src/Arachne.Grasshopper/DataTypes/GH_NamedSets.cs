using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_NamedSets : GH_Goo<NamedSetsModel>
    {
        public override bool IsValid => Value != null && Value.Name != string.Empty;

        public override string IsValidWhyNot => "Not propely defined";

        public override string TypeName => "Named sets";

        public override string TypeDescription => "The named sets";

        public GH_NamedSets()
        {
        }

        public GH_NamedSets(NamedSetsModel namedSet)
        {
            Value = namedSet.Clone();
        }

        public override IGH_Goo Duplicate()
        {
            return new GH_NamedSets(Value);
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null value";
            else
                return Value.ToString();
        }
    }
}
