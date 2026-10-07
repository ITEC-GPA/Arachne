using System;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_SolverOptions : GH_Goo<SolverOptionsModel>
    {
        public GH_SolverOptions()
        {
            Value = new SolverOptionsModel();
        }

        public GH_SolverOptions(SolverOptionsModel options)
        {
            Value = options;
        }

        public override bool IsValid => Value != null;

        public override string TypeName => "SolverOptions";

        public override string TypeDescription => "The options of the form finding";

        public override IGH_Goo Duplicate()
        {
            return new GH_SolverOptions(new SolverOptionsModel(Value));
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
                    System.Diagnostics.Debug.WriteLine("GH_SolverOptions.Write: {0}", e.Message);
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
                    Value = SerializationHelper.FromBytes<SolverOptionsModel>(reader.GetByteArray("Data"));
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_SolverOptions.Read: {0}", e.Message);
                    return false;
                }
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            return Value == null ? "Null Solver options" : Value.ToString();
        }
    }
}
