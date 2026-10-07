using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public class LoadCaseModel : CaseModel, IComparable<LoadCaseModel>, IEquatable<LoadCaseModel>
    {
        protected string _id;

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public readonly static LoadCaseModel Dead = new LoadCaseModel("1", "Dead");

        public LoadCaseModel(string id, string name = "")
            : base(name)
        {
            _id = id;
        }

        public LoadCaseModel()
            : base("")
        {

        }

        public LoadCaseModel(LoadCaseModel loadCase)
            : base(loadCase)
        {
            _id = loadCase._id;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "LoadCaseModel_SERIALIZATION_RELEASE"; //do not modify the name!
        // Release 3: the SAP construction stages are no longer stored
        // Release 4: the SAP inertia data (gravity, mass acceleration, temperature) are no longer stored
        private int SERIALIZATION_RELEASE = 4;

        protected LoadCaseModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            _id = info.GetString("Id");
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Id", _id);
        }

        #endregion

        public int CompareTo(LoadCaseModel other)
        {
            if (other == null)
            {
                return -1;
            }
            else if (Name.Equals(other.Name))
            {
                return 0;
            }
            else if (Name == Dead.Name)
            {
                return -1;
            }
            else if (other.Name == Dead.Name)
            {
                return 1;
            }
            else
            {
                return Name.CompareTo(other.Name);
            }
        }

        public static bool operator ==(LoadCaseModel lc1, LoadCaseModel lc2)
        {
            if (ReferenceEquals(lc1, lc2))
                return true;
            return !(lc1 is null) && lc1.Equals(lc2);
        }

        public static bool operator !=(LoadCaseModel lc1, LoadCaseModel lc2)
        {
            return !(lc1 == lc2);
        }

        public bool Equals(LoadCaseModel other)
        {
            return !(other is null) && _id == other._id && _name == other._name;
        }

        public override bool Equals(object obj)
        {
            if (obj is LoadCaseModel lc)
                return Equals(lc);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = -23;
            hashCode = hashCode * -17 + (_id?.GetHashCode() ?? 0);
            hashCode = hashCode * -17 + (_name?.GetHashCode() ?? 0);
            return hashCode;
        }

        public override string ToString()
        {
            return $"LoadCase: {Name}";
        }
    }
}
