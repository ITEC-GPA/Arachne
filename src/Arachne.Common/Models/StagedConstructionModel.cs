using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public class StagedConstructionModel
    {
        protected string _name;
        protected List<GroupModel> _groups;
        protected List<LoadCombinationModel> _loadCombinations;
        protected bool _morph;
        protected bool _rotate;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public IEnumerable<GroupModel> Groups => _groups;

        public IEnumerable<LoadCombinationModel> LoadCombinations => _loadCombinations;

        public bool Marph
        {
            get => _morph;
            set => _morph = value;
        }

        public bool Rotate
        {
            get => _rotate;
            set => _rotate = value;
        }

        public StagedConstructionModel(string name = "")
        {
            _name = name;
            _groups = new List<GroupModel>();
            _loadCombinations = new List<LoadCombinationModel>();
            _morph = false;
            _rotate = false;
        }

        public StagedConstructionModel(StagedConstructionModel stagedConstruction)
        {
            _name = stagedConstruction._name;
            _groups = new List<GroupModel>();
            foreach (GroupModel group in stagedConstruction._groups)
                _groups.Add(new GroupModel(group));
            _loadCombinations = new List<LoadCombinationModel>();
            foreach (LoadCombinationModel lc in stagedConstruction._loadCombinations)
                _loadCombinations.Add(new LoadCombinationModel(lc));
            _morph = stagedConstruction._morph;
            _rotate = stagedConstruction._rotate;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "StagedConstructionModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

        protected StagedConstructionModel(SerializationInfo info, StreamingContext context)
        {
            int savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);

            _name = info.GetString("Name");
            _groups = (List<GroupModel>)info.GetValue("Groups", typeof(List<GroupModel>));
            _loadCombinations = (List<LoadCombinationModel>)info.GetValue("LoadCombinations", typeof(List<LoadCombinationModel>));
            _morph = info.GetBoolean("Morph");
            _rotate = info.GetBoolean("Rotate");
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Name", _name);
            info.AddValue("Groups", _groups, typeof(List<GroupModel>));
            info.AddValue("LoadCombinations", _loadCombinations, typeof(List<LoadCombinationModel>));
            info.AddValue("Morph", _morph);
            info.AddValue("Rotate", _rotate);
        }

        #endregion

        public void AddGroups(IEnumerable<GroupModel> groups)
        {
            _groups.AddRange(groups);
        }

        public void AddLoadCombinations(IEnumerable<LoadCombinationModel> combinations)
        {
            _loadCombinations.AddRange(combinations);
        }
    }
}
