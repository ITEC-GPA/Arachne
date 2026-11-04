using System;
using Arachne.Common.Helpers;
using GH_IO.Serialization;
using GPC.Model.Core;
using Grasshopper.Kernel.Types;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>Base of the Grasshopper data that carry one GPC value: detached copies by GPC contract (no serialization) and
    /// persistence by the GPC codec (<see cref="ContractArchive"/>).</summary>
    public abstract class GpcGoo<T> : GH_Goo<T> where T : class
    {
        protected GpcGoo() { }
        protected GpcGoo(T value) { Value = value; }
        public override bool IsValid => Value != null;
        public override string IsValidWhyNot => Value == null ? "No value" : string.Empty;
        protected static T Copy(T value) => value == null ? null : ModelValues.CopyValue(value);
        /// <summary>The archive of the value; the data carried by a base type override it with <see cref="ContractArchive.Member"/>.</summary>
        protected virtual byte[] Archive(T value) => ContractArchive.Value(value);
        protected virtual T Restore(byte[] data) => ContractArchive.ReadValue<T>(data);
        protected virtual void WriteExtra(GH_IWriter writer) { }
        protected virtual void ReadExtra(GH_IReader reader) { }
        public override bool Write(GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            if (Value != null) writer.SetByteArray("Data", Archive(Value));
            WriteExtra(writer);
            return true;
        }
        public override bool Read(GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            try { Value = reader.ItemExists("Data") ? Restore(reader.GetByteArray("Data")) : null; ReadExtra(reader); return true; }
            catch (Exception e) { reader.AddMessage(TypeName + ": " + e.Message, GH_Message_Type.error); return false; }
        }
    }
}
