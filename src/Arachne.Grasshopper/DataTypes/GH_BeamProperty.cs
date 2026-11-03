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

        public GH_BeamProperty()
        {
        }

        public GH_BeamProperty(BeamPropertyModel beamProperty)
        {
            Value = beamProperty;
        }

        public GH_BeamProperty(BeamPropertyModel beamProperty, GH_Material material)
        {
            Value = beamProperty;
            if (material != null) Value.Material = material.Value;
        }

        #region Properties

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string TypeName => "BeamProperty";

        public override string TypeDescription => "Define a beam property";

        #endregion

        public override IGH_Goo Duplicate()
        {
            return new GH_BeamProperty(Arachne.Common.Helpers.SerializationHelper.Clone(Value));
        }

        public override bool Write(GH_IWriter writer)
        {
            if (base.Write(writer))
            {
                try
                {
                    writer.SetByteArray("GpcData", Arachne.Common.Helpers.SerializationHelper.ToBytes(Value));
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
                    if (!reader.ItemExists("GpcData"))
                        throw new System.Runtime.Serialization.SerializationException("IncompleteLegacyBeamPropertyArchive: the old writer did not save the section. Recompute the upstream property component.");
                    Value = Arachne.Common.Helpers.SerializationHelper.FromBytes<BeamPropertyModel>(reader.GetByteArray("GpcData"));
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
