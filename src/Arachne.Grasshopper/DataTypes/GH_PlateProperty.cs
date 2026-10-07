using System;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_PlateProperty : GH_Goo<PlatePropertyModel>
    {
        // Needed by Grasshopper to read the internalised data
        public GH_PlateProperty()
        {
            Value = new PlatePropertyModel();
        }

        public GH_PlateProperty(PlatePropertyModel plateProperty)
        {
            Value = plateProperty;
        }

        #region Properties

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string TypeName => "PlateProperty";

        public override string TypeDescription => "Define a plate property";

        #endregion

        public override IGH_Goo Duplicate()
        {
            return new GH_PlateProperty(Value);
        }

        // The whole property, material and thickness included: FeMM wrote the material but read
        // back only Id and Name
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
                    System.Diagnostics.Debug.WriteLine("GH_PlateProperty.Write: {0}", e.Message);
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
                    Value = SerializationHelper.FromBytes<PlatePropertyModel>(reader.GetByteArray("Data"));
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_PlateProperty.Read: {0}", e.Message);
                    return false;
                }
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Plate property";
            else
                return Value.ToString();
        }
    }
}
