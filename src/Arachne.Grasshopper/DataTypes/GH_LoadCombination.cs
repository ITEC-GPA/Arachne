using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GH_IO.Serialization;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_LoadCombination : GH_Goo<LoadCombinationModel>
    {
        public List<LoadCaseModel> LoadCases => Value.Values.Keys.ToList();

        public override bool IsValid => Value.Name != string.Empty && Value.Values.Count > 0;

        public override string IsValidWhyNot => "Not propely defined";

        public override string TypeName => "Load Combination";

        public override string TypeDescription => "The load cases combination";

        public GH_LoadCombination()
        {
        }

        public GH_LoadCombination(string name)
        {
            Value = new LoadCombinationModel(name);
        }

        public override bool CastFrom(object source)
        {
            // TODO: Commentata perchè per commutare i nomi dei load case on oggetti LoadCaseModel bisogna reperirli dal 
            //  componte FemExport o FemAnalysis
            /*if (source.GetType() == typeof(GH_String))
            {
                string[] toc = source.ToString().Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries).
                    Select(s => s.Trim()).ToArray();
                if (toc.Length != 2)
                    throw new FormatException("Invalid format. Must be like \"ComboName=LC1*LI1+LC2*LI2+...\"");
                Value = new LoadCombinationModel(toc[0]);
                string[] itms = toc[1].Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
                if (itms.Length <= 0)
                    throw new FormatException("Invalid format. Must be like \"ComboName=LC1*LI1+LC2*LI2+...\"");
                foreach (string itm in itms)
                {
                    string[] parts = itm.Trim().Split(new[] { '*' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        throw new FormatException("Invalid format. Must be like \"ComboName=LC1*LI1+LC2*LI2+...\"");
                    _loadCases.Add(parts[0].Trim());
                    if (!double.TryParse(parts[1].Trim(), out double f))
                        throw new InvalidCastException("Invalid load increment format");
                    _increments.Add(f);
                }
                return true;
            }*/
            return false;
        }

        public override bool CastTo<Q>(ref Q target)
        {
            target = default;
            return false;
        }

        public override IGH_Goo Duplicate()
        {
            GH_LoadCombination value = new GH_LoadCombination(Value.Name);
            foreach (KeyValuePair<LoadCaseModel, double> item in Value.Values)
                value.Value.Values.Add(item.Key, item.Value);
            return value;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader))
                return false;

            try
            {
                Value = SerializationHelper.FromBytes<LoadCombinationModel>(reader.GetByteArray("Data"));
                return true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message, "GH_LoadCombination.Read()");
                return false;
            }
        }

        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer))
                return false;

            try
            {
                writer.SetByteArray("Data", SerializationHelper.ToBytes(Value ?? new LoadCombinationModel("")));
                return true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message, "GH_LoadCombination.Write()");
                return false;
            }
        }
        public override string ToString()
        {
            string str = $"{Value.Name} = ";

            bool first = true;
            foreach (KeyValuePair<LoadCaseModel, double> item in Value.Values)
            {
                if (!first)
                    str += " + ";
                str += $"{item.Key.Name}*{item.Value}";
                first = false;
            }

            return str;
        }
    }
}
