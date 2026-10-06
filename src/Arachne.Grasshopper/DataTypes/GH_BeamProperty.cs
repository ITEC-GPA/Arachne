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
    public class GH_BeamProperty : GH_Goo<BeamPropertyModel>
    {
        private readonly GH_Material _material;

        public GH_BeamProperty()
        {
        }

        public GH_BeamProperty(BeamPropertyModel beamProperty)
        {
            Value = beamProperty;
            _material = new GH_Material(Value.Material);
        }

        public GH_BeamProperty(BeamPropertyModel beamProperty, GH_Material material)
        {
            Value = beamProperty;
            _material = material;
        }

        #region Properties

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string TypeName => "BeamProperty";

        public override string TypeDescription => "Define a beam property";

        #endregion

        public override IGH_Goo Duplicate()
        {
            return new GH_BeamProperty(Value);
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
                    System.Diagnostics.Debug.WriteLine("GH_BeamProperty.Write: {0}", e.Message);
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
                    BeamPropertyModel beamProperty = new BeamPropertyModel(reader.GetString("Id"));
                    beamProperty.Name = reader.GetString("Name");

                    Value = beamProperty;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_BeamProperty.Read: {0}", e.Message);
                    return false;
                }
                return true;
            }
            return false;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Beam property";
            else
                return Value.ToString();
        }
    }
}
