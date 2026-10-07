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
    public abstract class AttributeModel : ICloneable, ISerializable
    {
        public abstract string Description { get; }

        public AttributeModel()
        {
        }

        public AttributeModel(AttributeModel attribute)
        {
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "AttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
        }

		protected AttributeModel(SerializationInfo info, StreamingContext context)
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

        /// <summary>
        /// Override this function if the attribute needs to change something in the container element
        /// </summary>
        /// <param name="element">The container Element</param>
        public virtual void UpdateElement(ElementModel element)
        {
        }
    }

    public abstract class AttributeModel<T> : AttributeModel
    {
        protected T _value;

        public virtual T Value
        {
            get => _value;
            set => _value = value;
        }

        public Type Type => typeof(T);


        public AttributeModel()
        {
        }

        public AttributeModel(AttributeModel attribute)
            : base(attribute)
        {
        }


        public AttributeModel(T value)
        {
            _value = value;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "AttributeModel<T>_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
        }

		protected AttributeModel(SerializationInfo info, StreamingContext context) 
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
        }

        #endregion

    }
}
