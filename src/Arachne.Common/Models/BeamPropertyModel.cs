using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
	public enum BeamPropertyType
	{
		Undefined = -1,
		SpringDamper,
		Cable,
		Truss,
		CutoffBar,
		PointContact,
		Beam,
		UserDefined,
		Pipe,
		Connection
	}

	public enum CutoffType
	{
		Undefined = -1,
		Brittle,
		Ductile
	}

	[Serializable]
	public class BeamPropertyModel : BeamPropertyBaseModel, ISerializable
	{
		protected BeamPropertyType _beamPropertyType;
		protected SectionModel _section;

		// CutoffBar specific properties
		protected double _maxTension;
		protected double _maxCompression;
		protected CutoffType _cutoffType;
		protected bool _keepMass;


		#region Properties

		public BeamPropertyType PropertyType
		{
			get => _beamPropertyType;
			set => _beamPropertyType = value;
		}

		public SectionModel Section
		{
			get => _section;
			set => _section = value;
		}

		public double SectionArea => _section.SectionArea;
		public double Ixx => _section.Ixx;
		public double Iyy => _section.Iyy;		
		public double Ixy => _section.Ixy;
		public double I11 => _section.I11;		
		public double I22 => _section.I22;
		public double J => _section.J;
		public double W11 => _section.W11;
		public double W22 => _section.W22;
		public double Rho11 => _section.Rho11;
		public double Rho22 => _section.Rho22;
		public double Wxx => _section.Wxx;
		public double Wyy => _section.Wyy;
		public double AngleX1Rad => _section.AngleX1Rad;		
		public double ShearL1 => _section.ShearL1;
		public double ShearL2 => _section.ShearL2;
		public double ShearA1 => _section.ShearA1;
		public double ShearA2 => _section.ShearA2;
		public int IntegrationPoints => _section.IntegrationPoints;
		public SectionModel.SectionTypes SectionType => _section.SectionType;
		public double D => _section.D;
		public double B =>	_section.B;
		public double B1 => _section.B1;
		public double B2 => _section.B2;
		public double L => _section.L;
		public double T => _section.T;
		public double T1 => _section.T1;
		public double T2 => _section.T2;
		public double T3 => _section.T3;
		public double R => _section.R;
		public SectionModel.MirrorTypes Mirror => _section.Mirror;
		public double MirrorGapA => _section.MirrorGapA;
		public double MirrorGapB => _section.MirrorGapB;
		public Point2d Centroid => _section.Centroid;
		public bool IncludeTorsionalStiffness => _section.IncludeTorsionalStiffness;

		#region CutoffBar specific properties

		public double MaxTension
		{
			get => _maxTension;
			set => _maxTension = value;
		}

		public double MaxCompression
		{
			get => _maxCompression;
			set => _maxCompression = value;
		}

		public CutoffType CutoffType
		{
			get => _cutoffType;
			set => _cutoffType = value;
		}

		public bool KeepMass
		{
			get => _keepMass;
			set => _keepMass = value;
		}

		public bool IsDBSection => _section.IsDBSection;
		public Geometry.Shapes GenericShapes => _section.GenericShapes;

		#endregion

		public string SectionCode => _section.Code;
		public new bool IsValid
		{
			get
			{
				if (_material == null)
					return false;
				if (!_section.IsValid)
					return false;
				if (_cutoffType == CutoffType.Undefined)
					return false;
				if (_beamPropertyType == BeamPropertyType.CutoffBar)
					return false;
				return true;
			}
		}

		#endregion

		public BeamPropertyModel()
		{
			_section= null;
			_cutoffType = CutoffType.Undefined;
			_beamPropertyType = BeamPropertyType.Undefined;
		}

		public BeamPropertyModel(string id)
			: this()
		{
			_id = id;
		}

		public BeamPropertyModel(BeamPropertyModel bp)
		{
			CopyFrom(bp);
		}

		#region Serialization

		private readonly string SERIALIZATION_RELEASE_NAME = "SERIALIZATION_RELEASE"; //do not modify the name!
		private readonly int SERIALIZATION_RELEASE = 2;

		protected BeamPropertyModel(SerializationInfo info, StreamingContext context)
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
			_beamPropertyType = (BeamPropertyType)info.GetValue("BeamPropertyType", typeof(BeamPropertyType));
			_section = (SectionModel)info.GetValue("SectionModel", typeof(SectionModel));
			_material = (MaterialModel)info.GetValue("Material", typeof(MaterialModel));
			_maxTension = info.GetDouble("MaxTension");
			_maxCompression = info.GetDouble("MaxCompression");
			_cutoffType = (CutoffType)info.GetValue("CutoffType", typeof(CutoffType));
			_keepMass = info.GetBoolean("KeepMass");
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");
			base.GetObjectData(info, context);
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("Id", _id);
			info.AddValue("Name", _name);
			info.AddValue("BeamPropertyType", _beamPropertyType, typeof(BeamPropertyType));
			info.AddValue("SectionModel", _section, typeof(SectionModel));
			info.AddValue("Material", _material, typeof(MaterialModel));
			info.AddValue("MaxTension", _maxTension);
			info.AddValue("MaxCompression", _maxCompression);
			info.AddValue("CutoffType", _cutoffType, typeof(CutoffType));
			info.AddValue("KeepMass", _keepMass);
		}

		#endregion

		public void CopyFrom(BeamPropertyModel bp)
		{
			if (bp._id != null)
				_id = bp._id;
			_name = bp._name;
			_beamPropertyType = bp._beamPropertyType;
			_section = bp.Section;
			_material = bp._material;
			_maxTension = bp._maxTension;
			_maxCompression = bp._maxCompression;
			_cutoffType = bp._cutoffType;
			_keepMass = bp.KeepMass;
		}

		public override string ToString()
		{
			string text = "FeMM beam Property";
			if (_id != null && _id != string.Empty && _id != "")
				text += $" #{_id}";
			if (_name != null && _name != string.Empty && _name != "")
				text += $" #{_name}";
			if (_section.Id != null && _section.Id != string.Empty && _section.Id != "")
				text += $"\nSection Name: {_section.Id}";
			if (_section.Name != null && _section.Name != string.Empty && _section.Name != "")
				text += $"\nSection Name: {_section.Name}";
			text += $"\nSection Type: {_section.SectionType}";
			text += string.Format("\nArea: {0:F2}", _section.SectionArea);
			text += string.Format($"\nCentroid: {Math.Round(_section.Centroid.X, 2)}, {Math.Round(_section.Centroid.Y, 2)}");
			text += string.Format("\nPrincipal angle: {0:F4} deg", -Rhino.RhinoMath.ToDegrees(_section.AngleX1Rad));
			text += $"\nMaterial: {_material}";
			return text;
		}
	}
}
