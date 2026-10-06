using Rhino.Display;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;

namespace Arachne.Common.Models
{
	public class FunctionModel : ICloneable, ISerializable
	{
		public virtual string Description { get; set;  }

		public FunctionModel()
		{
		}

		public FunctionModel(FunctionModel attribute)
		{
		}

		#region Serialization

		private readonly string SERIALIZATION_RELEASE_NAME = "FunctionModel_SERIALIZATION_RELEASE"; //do not modify the name!
		private readonly int SERIALIZATION_RELEASE = 1;

		[SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
		}

		protected FunctionModel(SerializationInfo info, StreamingContext context)
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

	public abstract class FunctionModel<T> : FunctionModel
	{
		protected T _value;

		public virtual T Value
		{
			get => _value;
			set => _value = value;
		}

		public Type Type => typeof(T);


		public FunctionModel()
		{
		}

		public FunctionModel(FunctionModel attribute)
			: base(attribute)
		{
		}


		public FunctionModel(T value)
		{
			_value = value;
		}

		#region Serialization

		private readonly string SERIALIZATION_RELEASE_NAME = "FunctionModel<T>_SERIALIZATION_RELEASE"; //do not modify the name!
		private readonly int SERIALIZATION_RELEASE = 1;

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
		}

		protected FunctionModel(SerializationInfo info, StreamingContext context)
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
