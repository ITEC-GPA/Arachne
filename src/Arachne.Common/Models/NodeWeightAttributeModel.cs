using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Rhino.Geometry;

namespace Arachne.Common.Models
{
    [Serializable]
    public class NodeWeightAttributeModel : AttributeModel<Vector3d>, IEquatable<NodeWeightAttributeModel>
    {
        private GPC.Model.Analysis.FormFindingCoordinateGoal goal;
        public GPC.Model.Analysis.FormFindingCoordinateGoal Goal => goal ?? (goal = new GPC.Model.Analysis.FormFindingCoordinateGoal { Weights = new GPC.Geometry.Vector3d(0, 0, 0) });
        protected override Vector3d _value
        {
            get => new Vector3d(Goal.Weights.X, Goal.Weights.Y, Goal.Weights.Z);
            set => Goal.Weights = new GPC.Geometry.Vector3d(value.X, value.Y, value.Z);
        }
        public override string Description => "Node weight";

        public NodeWeightAttributeModel()
        {
            Value = Vector3d.Zero;
        }

        public NodeWeightAttributeModel(Vector3d weight)
        {
            Value = weight;
        }

        public NodeWeightAttributeModel(NodeWeightAttributeModel attr)
        {
            goal = GPC.Model.Core.ModelValues.CopyValue(attr.Goal);
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeWeightAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 2;

		protected NodeWeightAttributeModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            int savedRelease = Arachne.Common.Helpers.SerializationHelper.ReadVersion(info, SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);

            if (savedRelease >= 2)
                goal = (GPC.Model.Analysis.FormFindingCoordinateGoal)info.GetValue("Goal", typeof(GPC.Model.Analysis.FormFindingCoordinateGoal)) ?? throw new SerializationException("MissingCoordinateGoal");
            else Value = (Vector3d)info.GetValue("Value", typeof(Vector3d));
            if (Goal.Weights == null) throw new SerializationException("MissingCoordinateGoalWeights");
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Goal", Goal, typeof(GPC.Model.Analysis.FormFindingCoordinateGoal));
        }

        #endregion

        public static bool operator ==(NodeWeightAttributeModel m1, NodeWeightAttributeModel m2)
        {
            if (ReferenceEquals(m1, m2))
                return true;
            return !(m1 is null) && m1.Equals(m2);
        }

        public static bool operator !=(NodeWeightAttributeModel m1, NodeWeightAttributeModel m2)
        {
            return !(m1 == m2);
        }

        public bool Equals(NodeWeightAttributeModel other)
        {
            return !(other is null) && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is NodeWeightAttributeModel model)
                return Equals(model);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 63277979;
            hashCode = hashCode * 64857271 + EqualityComparer<Vector3d>.Default.GetHashCode(Value);
            return hashCode;
        }

        public static Vector3d GetWeight(NodeModel node)
        {
            IEnumerable<AttributeModel> attr = node.Attributes.Where(n => n.GetType() == typeof(NodeWeightAttributeModel));
            if (attr.Count() > 0)
                return attr.Cast<NodeWeightAttributeModel>().First().Value;
            return Vector3d.Unset;
        }
    }
}
