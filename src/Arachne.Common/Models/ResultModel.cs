using System;
using System.Drawing;
using System.Runtime.Serialization;
using System.Security.Permissions;
using Rhino.Display;

namespace Arachne.Common.Models
{
    [Serializable]
    public class ResultModel : ISerializable
    {
        protected CaseModel _case;
        protected double _stepNum;
        protected string _stepType;

        public CaseModel LoadCase => _case;

        public double StepNum => _stepNum;

        public string StepType => _stepType;

        public ResultModel(CaseModel caseModel, double stepNum, string stepType)
        {
            _case = caseModel;
            _stepNum = stepNum;
            _stepType = stepType;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "ResultModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected ResultModel(SerializationInfo info, StreamingContext context)
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
            _case = (CaseModel)info.GetValue("Case", typeof(CaseModel));
            _stepNum = info.GetDouble("StepNum");
            _stepType = info.GetString("StepType");
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Case", _case, typeof(LoadCaseModel));
            info.AddValue("StepNum", _stepNum);
            info.AddValue("StepType", _stepType);
        }

        #endregion

        public virtual void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
        }
    }
}
