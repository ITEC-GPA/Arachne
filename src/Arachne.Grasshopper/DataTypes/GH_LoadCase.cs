using System;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_LoadCase : GH_Goo<LoadCaseModel>
    {
        public override bool IsValid => Value == null ? false : Value.Name != string.Empty;

        public override string IsValidWhyNot => "Not propely defined";

        public override string TypeName => "LoadCase";

        public override string TypeDescription => "The load case definition";

        public GH_LoadCase()
        {
        }

        public GH_LoadCase(LoadCaseModel lc)
        {
            Value = lc;
        }

        public GH_LoadCase(GH_LoadCase lc)
        {
            Value = new LoadCaseModel(lc.Value);
        }

        public override IGH_Goo Duplicate()
        {
            return new GH_LoadCase(this);
        }

        public override bool Read(GH_IReader reader)
        {
            try
            {
                LoadCaseModel lc = new LoadCaseModel("", reader.GetString("name"));
                Value = lc;
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override bool Write(GH_IWriter writer)
        {
            try
            {
                writer.SetString("name", Value.Name);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null LoadCase property";
            else
                return Value.ToString();
        }
    }
}
