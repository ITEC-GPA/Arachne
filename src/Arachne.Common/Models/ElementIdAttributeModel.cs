using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
    [Serializable]
    public class ElementIdAttributeModel : AttributeModel<string>, IEquatable<ElementIdAttributeModel>
    {
        public override string Description => "Element ID";

        public ElementIdAttributeModel()
        {
        }

        public ElementIdAttributeModel(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new NotSupportedException("Empty name not admitted");
            Value = name;
        }

        public ElementIdAttributeModel(ElementIdAttributeModel se)
        {
            Value = se.Value;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "ElementIdAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;
		protected ElementIdAttributeModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            int savedRelease;
            try
            {
                savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);
            }
            catch
            {
                savedRelease = 0;
            }
            Value = info.GetString("Value");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Value", Value);
        }

        #endregion

        public static bool operator ==(ElementIdAttributeModel id1, ElementIdAttributeModel id2)
        {
            if (ReferenceEquals(id1, id2))
                return true;
            return !(id1 is null) && id1.Equals(id2);
        }

        public static bool operator !=(ElementIdAttributeModel id1, ElementIdAttributeModel id2)
        {
            return !(id1 == id2);
        }

        public bool Equals(ElementIdAttributeModel other)
        {
            return !(other is null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (obj is ElementIdAttributeModel eid)
                return Equals(eid);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return -1440511887 + EqualityComparer<string>.Default.GetHashCode(Value);
        }

        public override string ToString()
        {
            return "ID: " + Value;
        }
    }
}
