using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Arachne.Common.Models
{
    [Serializable]
    public class FixedAxialForceModel : LoadModel<double>, IEquatable<FixedAxialForceModel>
    {
        private GPC.Model.Analysis.FormFindingGoal goal = new GPC.Model.Analysis.FormFindingGoal { Kind = GPC.Model.Analysis.FormFindingGoalKind.AxialForce };
        public GPC.Model.Analysis.FormFindingGoal Goal => goal;
        protected override double _value { get => goal.Target; set => goal.Target = value; }
        protected double _weight { get => goal.Weight; set => goal.Weight = value; }

        public override string Description => "Axial Force";

        public double Weight
        {
            get => _weight;
            set => _weight = value;
        }

        public FixedAxialForceModel()
        {
            _weight = 0;
        }

        public FixedAxialForceModel(LoadCaseModel loadCase, double value, double weight = 0)
            : base(loadCase, value)
        {
            _weight = weight;
        }

        public FixedAxialForceModel(FixedAxialForceModel af)
        {
            _loadCase = Helpers.SerializationHelper.Clone(af._loadCase);
            goal = GPC.Model.Core.ModelValues.CopyValue(af.goal);
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "FixedAxialForceModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected FixedAxialForceModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            int savedRelease = Arachne.Common.Helpers.SerializationHelper.ReadVersion(info, SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            _value = info.GetDouble("Value");
            _weight = info.GetDouble("Weight");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Value", _value);
            info.AddValue("Weight", _weight);
        }

        #endregion

        public static bool operator ==(FixedAxialForceModel m1, FixedAxialForceModel m2)
        {
            if (ReferenceEquals(m1, m2))
                return true;
            return !(m1 is null) && m1.Equals(m2);
        }

        public static bool operator !=(FixedAxialForceModel m1, FixedAxialForceModel m2)
        {
            return !(m1 == m2);
        }

        public bool Equals(FixedAxialForceModel other)
        {
            return !(other is null) && LoadCase == other.LoadCase && Weight == other.Weight && Value == other.Value;
        }

        public override bool Equals(object obj)
        {
            if (obj is FixedAxialForceModel model)
                return Equals(model);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = 23;
                hashCode = hashCode * -17 + LoadCase.GetHashCode();
                hashCode = hashCode * -17 + Weight.GetHashCode();
                hashCode = hashCode * -17 + Value.GetHashCode();

                return hashCode;
            }
        }

        public override string ToString()
        {
            string text = $"FeMM Fixed Axial Force: ";
            text += $"Load Case: {_loadCase}; ";
            text += $"N={_value}; ";
			text += $"Weight={_weight}";
			return text;
        }
    }
}
