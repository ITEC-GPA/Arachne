using System;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using Arachne.Common.Helpers;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public abstract class NamedSetsModel : ISerializable
    {
        protected string _name;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        protected NamedSetsModel(string name)
        {
            _name = name;
        }

        public abstract NamedSetsModel Clone();

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NamedSetsModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected NamedSetsModel(SerializationInfo info, StreamingContext context)
        {
            int savedRelease;
            savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);
            _name = info.GetString("Name");
        }

        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Name", _name);
        }

        #endregion

        public override string ToString()
        {
            return "FeMM Named set " + _name;
        }
    }
}
