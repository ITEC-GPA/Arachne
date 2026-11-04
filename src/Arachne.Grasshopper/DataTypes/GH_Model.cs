using System;
using System.Linq;
using Arachne.Common.Analysis;
using Arachne.Common.Helpers;
using GH_IO.Serialization;
using GPC.Model.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using GModel = GPC.Model.Models.Model;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>The model of Build Model and Form Find: a GPC <see cref="GModel"/> in N and mm (with the results of the form finding
    /// after Form Find), and the document units chosen in Build Model, used by the components that show its values.</summary>
    public class GH_Model : GH_Goo<GModel>, IGH_BakeAwareData
    {
        public ModelUnits Units { get; set; } = ModelUnits.Nmm;
        public FormFindingUnits Scale => FormFindingUnits.From(Units);
        public override bool IsValid => Value != null && Value.AllElements.Any();
        public override string TypeName => "FEM Model";
        public override string TypeDescription => "The FEM model definition";

        public GH_Model() { }
        public GH_Model(GModel model, ModelUnits units) { Value = model; Units = units; }

        /// <summary>A real copy of the model (GPC model copy: elements, nodes, cases, groups, results) that shares the property, material
        /// and section instances of the source (user decision of 4/11/2026, see <see cref="GpcGoo{T}"/>): its elements cite the properties
        /// registered in the copy, which are those of the source.</summary>
        public override IGH_Goo Duplicate() => new GH_Model(Value == null ? null : ModelValues.Copy(Value, ModelCopyOptions.ShareProperties), Units);

        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            writer.SetInt32("Units", (int)Units);
            if (Value != null) writer.SetByteArray("Model", ContractArchive.Model(Value));
            return true;
        }

        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            try
            {
                if (reader.ItemExists("Data") && !reader.ItemExists("Model")) throw new System.Runtime.Serialization.SerializationException(ContractArchive.LegacyMessage);
                int units = 2; reader.TryGetInt32("Units", ref units);
                if (!Enum.IsDefined(typeof(ModelUnits), units)) throw new System.Runtime.Serialization.SerializationException("UnknownModelUnits");
                Units = (ModelUnits)units;
                Value = reader.ItemExists("Model") ? ContractArchive.ReadModel(reader.GetByteArray("Model")) : null;
                return true;
            }
            catch (Exception e) { reader.AddMessage("Model: " + e.Message, GH_Message_Type.error); return false; }
        }

        // Arachne has no FeMM Rhino plug-in objects: the elements are baked as plain geometry, in the document units.
        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            obj_guid = Guid.Empty;
            if (Value == null) return false;
            var back = new ElementDrafts.Scale(Scale, false);
            foreach (var node in Value.NodesElements.Values) new GH_Node(node) { }.Let(n => back.Apply(n.Value)).BakeGeometry(doc, att, out _);
            foreach (var beam in Value.BeamElements.Values) new GH_Beam(beam).Let(b => back.Apply(b.Value, true)).BakeGeometry(doc, att, out _);
            foreach (var plate in Value.AreaElements.Values) new GH_Plate(plate).Let(p => back.Apply(p.Value, true)).BakeGeometry(doc, att, out _);
            return true;
        }

        public override string ToString()
        {
            if (Value == null) return "Null Model";
            if (!Value.AllElements.Any()) return "Empty Model";
            return $"Model {Value.Name}\nMeasure units: {Units.GetDescription()}\n{Value.NodesElements.Count} Nodes, {Value.BeamElements.Count} Beams, {Value.AreaElements.Count} Plates";
        }
    }

    internal static class GooExtensions
    {
        internal static T Let<T>(this T value, Action<T> action) { action(value); return value; }
    }
}
