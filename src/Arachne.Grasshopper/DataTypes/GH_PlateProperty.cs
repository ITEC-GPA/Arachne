using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Grasshopper.Kernel.Types;
using GH_IO.Serialization;
using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_PlateProperty : GH_Goo<PlatePropertyModel>
    {
        GH_Material _material;

        public GH_PlateProperty(PlatePropertyModel plateProperty)
        {
            Value = plateProperty;
            _material = new GH_Material(Value.Material);
        }

        public GH_PlateProperty(PlatePropertyModel plateProperty, GH_Material material)
        {
            Value = plateProperty;
            _material = material;
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

        public override bool Write(GH_IWriter writer)
        {
            if (base.Write(writer))
            {
                try
                {
                    writer.SetString("Id", Value.Id);
                    writer.SetString("Name", Value.Name);

                    _material.Write(writer);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_PlateProperty.Write: {0}", e.Message);
                    return false;
                }
                return true;
            }

            return false;
        }

        public override bool Read(GH_IReader reader)
        {
            if (base.Read(reader))
            {
                try
                {
                    PlatePropertyModel plateProperty = new PlatePropertyModel(reader.GetString("Id"));
                    plateProperty.Name = reader.GetString("Name");

                    Value = plateProperty;
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
