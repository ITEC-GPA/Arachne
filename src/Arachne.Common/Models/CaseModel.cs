using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public class CaseModel : ISerializable
    {
        protected string _name;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public CaseModel()
        {
        }

        public CaseModel(string name)
        {
            _name = name;
        }

        public CaseModel(CaseModel c)
        {
            _name = c._name;
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "CaseModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected CaseModel(SerializationInfo info, StreamingContext context)
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
            _name = info.GetString("Name");
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Name", _name);
        }

        #endregion
    }
}
