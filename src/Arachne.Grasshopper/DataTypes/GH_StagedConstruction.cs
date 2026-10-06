using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using System;
using System.Linq;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_StagedConstruction : GH_Goo<StagedConstructionModel>
    {
        public override bool IsValid => Value != null;

        public override string TypeName => "Staged Construction";

        public override string TypeDescription => "The Staged construction definition";

        public GH_StagedConstruction()
        {
        }

        public GH_StagedConstruction(StagedConstructionModel stagedConstruction)
        {
            Value = stagedConstruction;
        }

        public GH_StagedConstruction(GH_StagedConstruction stagedConstruction)
        {
            Value = new StagedConstructionModel(stagedConstruction.Value);
        }

        public override IGH_Goo Duplicate()
        {
            GH_StagedConstruction copy = new GH_StagedConstruction();
            copy.Value = new StagedConstructionModel(Value);
            return copy;
        }

        public override bool Write(GH_IWriter writer)
        {
            if (base.Write(writer))
            {
                try
                {
                    writer.SetByteArray("Data", SerializationHelper.ToBytes(Value));
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_StagedConstruction.Write: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        public override bool Read(GH_IReader reader)
        {
            if (base.Read(reader))
            {
                try
                {
                    Value = SerializationHelper.FromBytes<StagedConstructionModel>(reader.GetByteArray("Data"));
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_StagedConstruction.Read: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Staged Construcion";
            else if (Value.Groups.Count() == 0)
                return "Empty Staged Construction";
            else
                return Value.ToString();
        }
    }
}
