using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
    [Serializable]
    public class SuperElementAttributeModel : AttributeModel<string>, IEquatable<SuperElementAttributeModel>
    {
        public override string Description => "Super Element";

        public SuperElementAttributeModel()
        {
        }

        public SuperElementAttributeModel(string name)
        {
            Value = name;
        }

        public SuperElementAttributeModel(SuperElementAttributeModel se)
        {
            Value = se.Value;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "SuperElementAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected SuperElementAttributeModel(SerializationInfo info, StreamingContext context)
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

        public static string GetSuperElement(ElementModel element)
        {
            if (element.HasAttribute(typeof(SuperElementAttributeModel)))
            {
                return element.Attributes.Where(n => n.GetType() == typeof(SuperElementAttributeModel)).
                    Cast<SuperElementAttributeModel>().First().Value;
            }
            return null;
        }

        public static bool operator ==(SuperElementAttributeModel se1, SuperElementAttributeModel se2)
        {
            if (ReferenceEquals(se1, se2))
                return true;
            return !(se1 is null) && se1.Equals(se2);
        }

        public static bool operator !=(SuperElementAttributeModel se1, SuperElementAttributeModel se2)
        {
            return !(se1 == se2);
        }

        public bool Equals(SuperElementAttributeModel other)
        {
            return !(other is null) && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (obj is SuperElementAttributeModel se)
                return Equals(se);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return -1440511887 + EqualityComparer<string>.Default.GetHashCode(Value);
        }

        public override string ToString()
        {
            return $"Super-Element: {Value}";
        }
    }
}
