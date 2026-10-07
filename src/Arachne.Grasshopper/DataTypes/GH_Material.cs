using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using System;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_Material : GH_Goo<MaterialModel>
    {
        public GH_Material()
        {
            Value = new MaterialModel();
        }

        public GH_Material(MaterialModel material)
        {
            Value = material;
        }

        public override bool IsValid => Value == null ? false : Value.IsValid;

        public override string TypeName => "Material";

        public override string TypeDescription => "Material definition";

        public override IGH_Goo Duplicate()
        {
            return new GH_Material(Value);
        }

        public override bool Write(GH_IWriter writer)
        {
            if (base.Write(writer))
            {
                try
                {
                    writer.SetByteArray("Data", SerializationHelper.ToBytes(Value));
                    return true;

                    //writer.SetString("Id", Value.Id);
                    //writer.SetString("Name", Value.Name);
                    //writer.SetInt32("Kind", (int)Value.Kind);
                    //writer.SetDouble("Modulus", Value.Modulus);
                    //writer.SetDouble("PoissonRatio", Value.PoissonRatio);
                    //writer.SetDouble("ShearModulus", Value.ShearModulus);
                    //writer.SetBoolean("UsePoisson", Value.UsePoisson);
                    //writer.SetDouble("Density", Value.Density);
                    //writer.SetDouble("ViscousDamping", Value.ViscousDamping);
                    //writer.SetDouble("DampingRatio", Value.DampingRatio);
                    //writer.SetDouble("ThermalExpansion", Value.ThermalExpansion);
                    //writer.SetInt32("TemperatureOnTime", (int)Value.TemperatureOnTime);
                    //writer.SetInt32("Mode", (int)Value.Mode);
                    //writer.SetInt32("Hardening", (int)Value.Hardening);
                    //writer.SetDouble("SpecificHeat", Value.SpecificHeat);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Material.Write: {0}", e.Message);
                    return false;
                }
                //return true;
            }

            return false;
        }

        public override bool Read(GH_IReader reader)
        {
            if (base.Read(reader))
            {
                try
                {
                    Value = SerializationHelper.FromBytes<MaterialModel>(reader.GetByteArray("Data"));
                    //MaterialModel material = new MaterialModel(reader.GetString("Id"));
                    //material.Name = reader.GetString("Name");
                    //material.Kind = (MaterialModel.MaterialKind)reader.GetInt32("Kind");
                    //material.Modulus = reader.GetDouble("Modulus");
                    //material.PoissonRatio = reader.GetDouble("PoissonRatio");
                    //material.ShearModulus = reader.GetDouble("ShearModulus");
                    //material.UsePoisson = reader.GetBoolean("UsePoisson");
                    //material.Density = reader.GetDouble("Density");
                    //material.ViscousDamping = reader.GetDouble("ViscousDamping");
                    //material.DampingRatio = reader.GetDouble("DampingRatio");
                    //material.ThermalExpansion = reader.GetDouble("ThermalExpansion");
                    //material.TemperatureOnTime = (MaterialModel.TemperatureOnTimeType)reader.GetInt32("TemperatureOnTime");
                    //material.Mode = (MaterialModel.MaterialMode)reader.GetInt32("Mode");
                    //material.Hardening = (MaterialModel.HardeningType)reader.GetInt32("Hardening");
                    //material.SpecificHeat = reader.GetDouble("SpecificHeat");
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Material.Read: {0}", e.Message);
                    return false;
                }
                return true;
            }

            return false;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Material";
            else
                return Value.ToString();
        }
    }
}
