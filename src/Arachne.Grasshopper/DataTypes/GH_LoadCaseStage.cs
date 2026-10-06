using System;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_LoadCaseStage : GH_Goo<LoadCaseModel.Stage>
    {
        public override bool IsValid => Value == null ? false : Value.Name != string.Empty;

        public override string IsValidWhyNot => "Not propely defined";

        public override string TypeName => "LoadCase stage";

        public override string TypeDescription => "The load case stage definition";

        public GH_LoadCaseStage()
        {
        }

        public GH_LoadCaseStage(LoadCaseModel.Stage stage)
        {
            Value = stage;
        }

        public GH_LoadCaseStage(GH_LoadCaseStage lc)
        {
            Value = new LoadCaseModel.Stage(lc.Value);
        }

        public override IGH_Goo Duplicate()
        {
            return new GH_LoadCaseStage(this);
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null LoadCase stage";
            else
                return Value.Name;
        }
    }
}
