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
            Value = new Vector3d(attr.Value);
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeWeightAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected NodeWeightAttributeModel(SerializationInfo info, StreamingContext context)
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

            Value = (Vector3d)info.GetValue("Value", typeof(Vector3d));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Value", Value, typeof(Vector3d));
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
