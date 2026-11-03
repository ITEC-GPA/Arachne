using System;
using System.Collections.Generic;
using Arachne.Common.Analysis;
using Arachne.Common.Helpers;
using GH_IO.Serialization;
using GPC.Model.Analysis;
using GPC.Model.ElementProperties;
using GPC.Model.LoadCases;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>The options of the Form Find: the GPC <see cref="FormFindingDefinition"/> of the solver settings and the GPC cases and
    /// plate property of the Arachne choices (<see cref="FormFindingOptions"/>).</summary>
    public class GH_SolverOptions : GH_Goo<FormFindingOptions>
    {
        public override bool IsValid => Value != null;
        public override string TypeName => "SolverOptions";
        public override string TypeDescription => "The options of the form finding";
        public GH_SolverOptions() { }
        public GH_SolverOptions(FormFindingOptions options) { Value = options; }
        public override IGH_Goo Duplicate() => new GH_SolverOptions(Value?.Copy());
        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            if (Value == null) return true;
            writer.SetByteArray("Settings", ContractArchive.Value(Value.Settings));
            if (Value.PressureLoadCase != null) writer.SetByteArray("PressureLoadCase", ContractArchive.Value(Value.PressureLoadCase));
            writer.SetInt32("LoadCaseCount", Value.LoadCases.Count);
            for (int i = 0; i < Value.LoadCases.Count; i++) writer.SetByteArray("LoadCase", i, ContractArchive.Value(Value.LoadCases[i]));
            writer.SetBoolean("CreateMesh", Value.CreateMesh);
            if (Value.PlateProperty != null) writer.SetByteArray("PlateProperty", ContractArchive.Value(Value.PlateProperty));
            return true;
        }
        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            try
            {
                if (!reader.ItemExists("Settings"))
                {
                    if (reader.ItemExists("Data")) throw new System.Runtime.Serialization.SerializationException(ContractArchive.LegacyMessage);
                    Value = null; return true;
                }
                var settings = ContractArchive.ReadValue<FormFindingDefinition>(reader.GetByteArray("Settings"));
                var pressure = reader.ItemExists("PressureLoadCase") ? ContractArchive.ReadValue<LoadCaseBase>(reader.GetByteArray("PressureLoadCase")) : null;
                int count = reader.GetInt32("LoadCaseCount");
                if (count < 0 || count > 100000) throw new System.Runtime.Serialization.SerializationException("InvalidLoadCaseCount");
                var cases = new List<LoadCaseBase>();
                for (int i = 0; i < count; i++) cases.Add(ContractArchive.ReadValue<LoadCaseBase>(reader.GetByteArray("LoadCase", i)));
                var property = reader.ItemExists("PlateProperty") ? ContractArchive.ReadValue<PlateProperty>(reader.GetByteArray("PlateProperty")) : null;
                Value = new FormFindingOptions(settings, pressure, cases, reader.GetBoolean("CreateMesh"), property);
                return true;
            }
            catch (Exception e) { reader.AddMessage("Solver Options: " + e.Message, GH_Message_Type.error); return false; }
        }
        public override string ToString() => Value == null ? "Null options" : Value.ToString();
    }
}
