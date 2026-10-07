using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public class GroupModel : ISerializable, IEquatable<GroupModel>
    {
        protected string _name;
        protected GroupModel _parent;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public GroupModel Parent
        {
            get => _parent;
            set => _parent = value;
        }

        public string FullName
        {
            get
            {
                string name = "";
                GroupModel group = this;
                while (group != null)
                {
                    if (name != "")
                        name = "\\" + name;
                    name = group._name + name;
                    group = group.Parent;
                }
                return name;
            }
        }

        public GroupModel()
        {
        }

        public GroupModel(string name, GroupModel parent = null)
        {
            _name = name;
            _parent = parent;
        }

        public GroupModel(GroupModel group)
        {
            _name = group.Name;
            _parent = group.Parent;
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "GroupModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 2;

		protected GroupModel(SerializationInfo info, StreamingContext context)
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
            if (savedRelease > 1)
                _parent = (GroupModel)info.GetValue("Parent", typeof(GroupModel));

        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Name", _name);
            info.AddValue("Parent", _parent);
		}

        #endregion

        public static bool operator ==(GroupModel grp1, GroupModel grp2)
        {
            if (ReferenceEquals(grp1, grp2))
                return true;
            return grp1.Equals(grp2);
        }

        public static bool operator !=(GroupModel grp1, GroupModel grp2)
        {
            return !(grp1 == grp2);
        }

        public bool Equals(GroupModel other)
        {
            return !(other is null) && FullName == other.FullName;
        }

        public override bool Equals(object obj)
        {
            if (obj is GroupModel grp)
            {
                return Equals(grp);
            }
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            return 733961487 + EqualityComparer<string>.Default.GetHashCode(FullName);
        }

        public static List<GroupModel> GetBranch(GroupModel group)
        {
            List<GroupModel> branch = new List<GroupModel>() { group };
            GroupModel curr_parent = group.Parent;
            while (curr_parent != null)
            {
                branch.Insert(0, curr_parent);
                curr_parent = curr_parent.Parent;
            }
            return branch;
        }

        public override string ToString()
        {
            return $"FeMM Group: {FullName}";
        }
    }
}
