using System;
using System.Runtime.Serialization;
using Rhino.Geometry;

namespace Arachne.Common.Models
{
    /// <summary>
    /// The nodal force of the pressure on the shape found by the Form Find. It is a result, as
    /// the pre-load of the beams: it stays in the load case given for the pressure, for the
    /// analyses of the model, but a new form finding does not apply it (it computes the pressure
    /// again on the new shape) and replaces it. FeMM saved it as a plain node force.
    /// </summary>
    [Serializable]
    public class NodePressureLoadModel : NodeForceLoadModel
    {
        public override string Description => "Node Pressure Load";

        public NodePressureLoadModel()
        {
        }

        public NodePressureLoadModel(LoadCaseModel loadCase, Vector3d value)
            : base(loadCase, value)
        {
        }

        protected NodePressureLoadModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public override string ToString()
        {
            return $"Pressure load of the form finding: Load Case: {_loadCase}, V={_value}";
        }
    }
}
