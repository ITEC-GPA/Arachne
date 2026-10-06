using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
    [Serializable]
    public class LoadCombinationModel : CaseModel
    {
        protected Dictionary<LoadCaseModel, double> _values;

        public Dictionary<LoadCaseModel, double> Values => _values;

        public LoadCombinationModel()
            : base("")
        {
            _values = new Dictionary<LoadCaseModel, double>();
        }

        public LoadCombinationModel(string name)
            : base(name)
        {
            _values = new Dictionary<LoadCaseModel, double>();
        }

        public LoadCombinationModel(LoadCombinationModel combo)
            : base(combo)
        {
            _values = new Dictionary<LoadCaseModel, double>(combo.Values);
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "LoadCombinationModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 2;

        protected LoadCombinationModel(SerializationInfo info, StreamingContext context)
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
            if (savedRelease <= 1)
                _values = (Dictionary<LoadCaseModel, double>)info.GetValue("Values", typeof(Dictionary<LoadCaseModel, double>));
            else
            {
                _values = new Dictionary<LoadCaseModel, double>();
                int count = info.GetInt32("Count");
                var keys = (List<LoadCaseModel>)info.GetValue("Keys", typeof(List<LoadCaseModel>));
                var values = (List<double>)info.GetValue("Values", typeof(List<double>));
                for (int i = 0; i < count; i++)
                {
                    _values.Add(keys[i], values[i]);
                }
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Count", _values.Count);
            List<double> values = new List<double>();
            List<LoadCaseModel> keys = new List<LoadCaseModel>();
            foreach (var kvp in _values)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
            info.AddValue("Keys", keys);
            info.AddValue("Values", values);
        }

        #endregion
    }
}
