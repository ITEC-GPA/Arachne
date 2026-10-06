using System;
using System.Drawing;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Permissions;
using Rhino.Display;

namespace Arachne.Common.Models
{
    [Serializable]
    public abstract class LoadModel : ICloneable, ISerializable
    {
        protected LoadCaseModel _loadCase;

        public LoadCaseModel LoadCase
        {
            get => _loadCase;
            set => _loadCase = value;
        }

        public abstract string Description
        {
            get;
        }

        public LoadModel()
        {
        }

        public LoadModel(LoadCaseModel loadCase)
        {
            _loadCase = loadCase;
        }

        public LoadModel(LoadModel lm)
        {
            _loadCase = lm._loadCase;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "LoadModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected LoadModel(SerializationInfo info, StreamingContext context)
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
            _loadCase = (LoadCaseModel)info.GetValue("LoadCase", typeof(LoadCaseModel));
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("LoadCase", _loadCase, typeof(LoadCaseModel));
        }

        #endregion

        /// <summary>
        /// Make a deep clone of the Model including all of his components
        /// </summary>
        /// <returns>The deep copy</returns>
        public virtual object Clone()
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, this);
                ms.Position = 0;

                return formatter.Deserialize(ms);
            }
        }

        public virtual void Draw(DisplayPipeline display, RhinoViewport viewport, Color color, ElementModel element)
        {
        }

        public virtual LoadModel Union(LoadModel other)
        {
            LoadModel union = (LoadModel)Clone();
            if (_loadCase != other._loadCase)
                union._loadCase = null;

            return union;
        }
    }

    public abstract class LoadModel<T> : LoadModel
    {
        protected T _value;

        public virtual T Value
        {
            get => _value;
            set => _value = value;
        }

        public Type Type => typeof(T);

        public LoadModel()
        {
        }

        public LoadModel(LoadModel lm)
            : base(lm)
        {
        }

        public LoadModel(LoadCaseModel loadCase, T value)
            : base(loadCase)
        {
            _value = value;
        }

        public LoadModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
