using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Text;
using System.Threading.Tasks;

namespace Arachne.Common.Models
{
	[Serializable]
	public class BeamPropertyBaseModel : ISerializable
	{
		protected string _id;
		protected string _name;
		protected MaterialModel _material;

		public string Id
		{
			get => _id;
			set => _id = value;
		}

		public string Name
		{
			get => _name;
			set => _name = value;
		}

		public MaterialModel Material
		{
			get => _material;
			set => _material = value;
		}

		public bool IsValid
		{
			get
			{
				if (_material == null)
					return false;
				return true;
			}
		}

		public BeamPropertyBaseModel(string id)
		{
			_id = id;
		}

		public BeamPropertyBaseModel()
		{

		}

		#region Serialization

		private readonly string SERIALIZATION_RELEASE_NAME = "SERIALIZATION_RELEASE"; //do not modify the name!
		private readonly int SERIALIZATION_RELEASE = 2;
		protected BeamPropertyBaseModel(SerializationInfo info, StreamingContext context)
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
			_id = info.GetString("Id");
			_name = info.GetString("Name");
			_material = (MaterialModel)info.GetValue("Material", typeof(MaterialModel));
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("Id", _id);
			info.AddValue("Name", _name);
			info.AddValue("Material", _material, typeof(MaterialModel));
		}

		#endregion
	}
}
