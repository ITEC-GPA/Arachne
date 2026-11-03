using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public class CaseModel : ISerializable
    {
        // An empty name is an unfinished UI draft; the analysis boundary rejects it.
        protected GPC.Model.LoadCases.LoadCaseBase input = CreateInput(Guid.NewGuid(), null);
        internal GPC.Model.LoadCases.LoadCaseBase Input => input;
        public Guid Identity => input.Guid;
        public static Guid LegacyIdentity(string name) => Analysis.FormFindingInputBuilder.Identity(
            new Guid("dd9360b8-20ab-4a62-aa15-bbad93f0f9b6"), "legacy-case:" + name);
        protected string _name { get => input.Name; set => input.Name = value; }
        private static GPC.Model.LoadCases.LoadCaseBase CreateInput(Guid identity, string name)
            => new GPC.Model.LoadCases.LoadCaseBase(identity, string.IsNullOrWhiteSpace(name) ? "Draft" : name) { Name = name };
        public void BindInputIdentity(Guid identity)
        {
            if (identity == Guid.Empty) throw new ArgumentException("EmptyCaseInputIdentity");
            if (identity == input.Guid) return;
            var replacement = CreateInput(identity, input.Name);
            replacement.InertiaMasses = GPC.Model.Core.ModelValues.CopyValue(input.InertiaMasses);
            input = replacement;
        }

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
            input = GPC.Model.Core.ModelValues.CopyValue(c.input);
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "CaseModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 2;

		protected CaseModel(SerializationInfo info, StreamingContext context)
        {
            int savedRelease = Arachne.Common.Helpers.SerializationHelper.ReadVersion(info, SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            if (savedRelease >= 2)
                input = (GPC.Model.LoadCases.LoadCaseBase)info.GetValue("Input", typeof(GPC.Model.LoadCases.LoadCaseBase)) ?? throw new SerializationException("MissingCaseInput");
            else
                input = CreateInput(LegacyIdentity(info.GetString("Name")), info.GetString("Name"));
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Input", input, typeof(GPC.Model.LoadCases.LoadCaseBase));
        }

        #endregion
    }
}
