using System;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
	[Serializable]
	public class MaterialModel : ISerializable
	{
		public enum MaterialKind
		{
			Undefined = -1,
			Steel,
			Concrete,
			Aluminium,
			Rebar,
			Membrane
		}

		public enum TemperatureOnTimeType
		{
			Elastic,
			Plastic
		}

		public enum MaterialMode
		{
			Elastic,
			ElasticPlastic
		}

		public enum HardeningType
		{
			Isotropic,
			Kinematic,
			Takeda
		}

		public enum AlumiumTypes
		{
			Wrought,
			CastMold,
			CastSand,
		}

		protected string _id;
		protected string _name;
		protected MaterialKind _materialKind;
		protected double _modulus;
		protected double _poissonRatio;
		protected double _shearModulus;
		protected bool _usePoisson;
		protected double _density;
		protected double _viscousDamping;
		protected double _dampingRatio;
		protected double _thermalExpansion;
		protected TemperatureOnTimeType _temperatureOnTime;
		protected MaterialMode _materialMode;
		protected HardeningType _hardening;
		protected double _specificHeat;
		protected double _conductivity;
		protected double _minimumYieldStress;
		protected double _minimumTensileStress;
		protected double _expectedYieldStress;
		protected double _expectedTensileStress;
		protected double _specificCompressiveStrength;
		protected double _expectedCompressiveStrength;
		protected AlumiumTypes _alumiumType;
		protected string _alloy;
		protected double _compressiveYieldStrength;
		protected double _tensileYieldStrength;
		protected double _tensileUltimateStrength;
		protected double _shearUltimateStrength;

		// Membranes (coated fabrics and foils): values per unit width, in t, N and mm
		protected double _areaDensity;
		protected double _strengthWarp;
		protected double _strengthWeft;
		protected double _stiffnessWarp;
		protected double _stiffnessWeft;
		protected double _shearStiffness;

		#region Properties

		public string Id => _id;

		public string Name
		{
			get => _name;
			set => _name = value;
		}

		public MaterialKind Kind
		{
			get => _materialKind;
			set => _materialKind = value;
		}

		public double Modulus
		{
			get => _modulus;
			set => _modulus = value;
		}

		public double PoissonRatio
		{
			get => _poissonRatio;
			set => _poissonRatio = value;
		}

		public double ShearModulus
		{
			get => _shearModulus;
			set => _shearModulus = value;
		}

		public bool UsePoisson
		{
			get => _usePoisson;
			set => _usePoisson = value;
		}

		public double Density
		{
			get => _density;
			set => _density = value;
		}

		public double ViscousDamping
		{
			get => _viscousDamping;
			set => _viscousDamping = value;
		}

		public double DampingRatio
		{
			get => _dampingRatio;
			set => _dampingRatio = value;
		}

		public double ThermalExpansion
		{
			get => _thermalExpansion;
			set => _thermalExpansion = value;
		}

		public TemperatureOnTimeType TemperatureOnTime
		{
			get => _temperatureOnTime;
			set => _temperatureOnTime = value;
		}

		public MaterialMode Mode
		{
			get => _materialMode;
			set => _materialMode = value;
		}

		public HardeningType Hardening
		{
			get => _hardening;
			set => _hardening = value;
		}

		public double SpecificHeat
		{
			get => _specificHeat;
			set => _specificHeat = value;
		}

		public double Conductivity
		{
			get => _conductivity;
			set => _conductivity = value;
		}

		public double MinimumYieldStress
		{
			get => _minimumYieldStress;
			set => _minimumYieldStress = value;
		}

		public double MinimumTensileStress
		{
			get => _minimumTensileStress;
			set => _minimumTensileStress = value;
		}

		public double ExpectedYieldStress
		{
			get => _expectedYieldStress;
			set => _expectedYieldStress = value;
		}

		public double ExpectedTensileStress
		{
			get => _expectedTensileStress;
			set => _expectedTensileStress = value;
		}

		public double SpecificCompressiveStrength
		{
			get => _specificCompressiveStrength;
			set => _specificCompressiveStrength = value;
		}

		public double ExpectedCompressiveStrength
		{
			get => _expectedCompressiveStrength;
			set => _expectedCompressiveStrength = value;
		}

		public AlumiumTypes AlumiumType
		{
			get => _alumiumType;
			set => _alumiumType = value;
		}

		public string Alloy
		{
			get => _alloy;
			set => _alloy = value;
		}

		public double CompressiveYieldStrength
		{
			get => _compressiveYieldStrength;
			set => _compressiveYieldStrength = value;
		}

		public double TensileYieldStrength
		{
			get => _tensileYieldStrength;
			set => _tensileYieldStrength = value;
		}

		public double TensileUltimateStrength
		{
			get => _tensileUltimateStrength;
			set => _tensileUltimateStrength = value;
		}

		public double ShearUltimateStrength
		{
			get => _shearUltimateStrength;
			set => _shearUltimateStrength = value;
		}

		/// <summary>Membrane mass per unit area, t/mm².</summary>
		public double AreaDensity
		{
			get => _areaDensity;
			set => _areaDensity = value;
		}

		/// <summary>Membrane tensile strength along the warp, per unit width, N/mm.</summary>
		public double StrengthWarp
		{
			get => _strengthWarp;
			set => _strengthWarp = value;
		}

		/// <summary>Membrane tensile strength along the weft, per unit width, N/mm.</summary>
		public double StrengthWeft
		{
			get => _strengthWeft;
			set => _strengthWeft = value;
		}

		/// <summary>Membrane tensile stiffness E·t along the warp, N/mm.</summary>
		public double StiffnessWarp
		{
			get => _stiffnessWarp;
			set => _stiffnessWarp = value;
		}

		/// <summary>Membrane tensile stiffness E·t along the weft, N/mm.</summary>
		public double StiffnessWeft
		{
			get => _stiffnessWeft;
			set => _stiffnessWeft = value;
		}

		/// <summary>Membrane shear stiffness G·t, N/mm.</summary>
		public double ShearStiffness
		{
			get => _shearStiffness;
			set => _shearStiffness = value;
		}

		public bool IsValid => _materialKind != MaterialKind.Undefined;

		#endregion

		public MaterialModel()
		{
			_materialKind = MaterialKind.Undefined;
			_usePoisson = true;
		}

		public MaterialModel(string id)
			: this()
		{
			_id = id;
		}

		public MaterialModel(MaterialModel material)
		{
			CopyFrom(material);
		}

		#region Serialization
		private string SERIALIZATION_RELEASE_NAME = "MaterialModel_SERIALIZATION_RELEASE"; //do not modify the name!
		// Release 2: membrane data; the SAP stress-strain table is no longer stored
		private int SERIALIZATION_RELEASE = 2;

		protected MaterialModel(SerializationInfo info, StreamingContext context)
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
			_materialKind = (MaterialKind)info.GetValue("MaterialKind", typeof(MaterialKind));
			_modulus = info.GetDouble("Modulus");
			_poissonRatio = info.GetDouble("PoissonRatio");
			_shearModulus = info.GetDouble("ShearModulus");
			_usePoisson = info.GetBoolean("UsePoisson");
			_density = info.GetDouble("Density");
			_viscousDamping = info.GetDouble("ViscousDamping");
			_dampingRatio = info.GetDouble("DampingRatio");
			_thermalExpansion = info.GetDouble("ThermalExpansion");
			_temperatureOnTime = (TemperatureOnTimeType)info.GetValue("TemperatureOnTime", typeof(TemperatureOnTimeType));
			_materialMode = (MaterialMode)info.GetValue("MaterialMode", typeof(MaterialMode));
			_hardening = (HardeningType)info.GetValue("Hardening", typeof(HardeningType));
			_specificHeat = info.GetDouble("SpecificHeat");
			_conductivity = info.GetDouble("Conductivity");
			_minimumYieldStress = info.GetDouble("MinimumYieldStress");
			_minimumTensileStress = info.GetDouble("MinimumTensileStress");
			_expectedYieldStress = info.GetDouble("ExpectedYieldStress");
			_expectedTensileStress = info.GetDouble("ExpectedTensileStress");
			_specificCompressiveStrength = info.GetDouble("SpecificCompressiveStrength");
			_expectedCompressiveStrength = info.GetDouble("ExpectedCompressiveStrength");
			_alumiumType = (AlumiumTypes)info.GetValue("AlumiumType", typeof(AlumiumTypes));
			_alloy = info.GetString("Alloy");
			_compressiveYieldStrength = info.GetDouble("CompressiveYieldStrength");
			_tensileYieldStrength = info.GetDouble("TensileYieldStrength");
			_tensileUltimateStrength = info.GetDouble("TensileUltimateStrength");
			_shearUltimateStrength = info.GetDouble("ShearUltimateStrength");
			if (savedRelease >= 2)
			{
				_areaDensity = info.GetDouble("AreaDensity");
				_strengthWarp = info.GetDouble("StrengthWarp");
				_strengthWeft = info.GetDouble("StrengthWeft");
				_stiffnessWarp = info.GetDouble("StiffnessWarp");
				_stiffnessWeft = info.GetDouble("StiffnessWeft");
				_shearStiffness = info.GetDouble("ShearStiffness");
			}
		}


		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("Id", _id);
			info.AddValue("Name", _name);
			info.AddValue("MaterialKind", _materialKind, typeof(MaterialKind));
			info.AddValue("Modulus", _modulus);
			info.AddValue("PoissonRatio", _poissonRatio);
			info.AddValue("ShearModulus", _shearModulus);
			info.AddValue("UsePoisson", _usePoisson);
			info.AddValue("Density", _density);
			info.AddValue("ViscousDamping", _viscousDamping);
			info.AddValue("DampingRatio", _dampingRatio);
			info.AddValue("ThermalExpansion", _thermalExpansion);
			info.AddValue("TemperatureOnTime", _temperatureOnTime, typeof(TemperatureOnTimeType));
			info.AddValue("MaterialMode", _materialMode, typeof(MaterialMode));
			info.AddValue("Hardening", _hardening, typeof(HardeningType));
			info.AddValue("SpecificHeat", _specificHeat);
			info.AddValue("Conductivity", _conductivity);
			info.AddValue("MinimumYieldStress", _minimumYieldStress);
			info.AddValue("MinimumTensileStress", _minimumTensileStress);
			info.AddValue("ExpectedYieldStress", _expectedYieldStress);
			info.AddValue("ExpectedTensileStress", _expectedTensileStress);
			info.AddValue("SpecificCompressiveStrength", _specificCompressiveStrength);
			info.AddValue("ExpectedCompressiveStrength", _expectedCompressiveStrength);
			info.AddValue("AlumiumType", _alumiumType, typeof(AlumiumTypes));
			info.AddValue("Alloy", _alloy);
			info.AddValue("CompressiveYieldStrength", _compressiveYieldStrength);
			info.AddValue("TensileYieldStrength", _tensileYieldStrength);
			info.AddValue("TensileUltimateStrength", _tensileUltimateStrength);
			info.AddValue("ShearUltimateStrength", _shearUltimateStrength);
			info.AddValue("AreaDensity", _areaDensity);
			info.AddValue("StrengthWarp", _strengthWarp);
			info.AddValue("StrengthWeft", _strengthWeft);
			info.AddValue("StiffnessWarp", _stiffnessWarp);
			info.AddValue("StiffnessWeft", _stiffnessWeft);
			info.AddValue("ShearStiffness", _shearStiffness);
		}

		#endregion

		public void CopyFrom(MaterialModel material)
		{
			if (material._id != null)
				_id = material._id;
			_name = material._name;
			_materialKind = material._materialKind;
			_modulus = material._modulus;
			_poissonRatio = material._poissonRatio;
			_shearModulus = material._shearModulus;
			_usePoisson = material._usePoisson;
			_density = material._density;
			_viscousDamping = material._viscousDamping;
			_dampingRatio = material._dampingRatio;
			_thermalExpansion = material._thermalExpansion;
			_temperatureOnTime = material._temperatureOnTime;
			_materialMode = material._materialMode;
			_hardening = material._hardening;
			_specificHeat = material._specificHeat;
			_conductivity = material._conductivity;
			_minimumYieldStress = material._minimumYieldStress;
			_minimumTensileStress = material._minimumTensileStress;
			_expectedYieldStress = material._expectedYieldStress;
			_expectedTensileStress = material._expectedTensileStress;
			_specificCompressiveStrength = material._specificCompressiveStrength;
			_expectedCompressiveStrength = material._expectedCompressiveStrength;
			_alumiumType = material._alumiumType;
			_alloy = material._alloy;
			_compressiveYieldStrength = material._compressiveYieldStrength;
			_tensileYieldStrength = material._tensileYieldStrength;
			_tensileUltimateStrength = material._tensileUltimateStrength;
			_shearUltimateStrength = material._shearUltimateStrength;
			_areaDensity = material._areaDensity;
			_strengthWarp = material._strengthWarp;
			_strengthWeft = material._strengthWeft;
			_stiffnessWarp = material._stiffnessWarp;
			_stiffnessWeft = material._stiffnessWeft;
			_shearStiffness = material._shearStiffness;
		}

		public override string ToString()
		{
			string text = string.Empty;

			if (_materialKind == MaterialKind.Membrane)
			{
				// In the units of the fabric data sheets
				text = $"Membrane: {Name};\n" +
					$"Weight: {_areaDensity * 1e12:0.#} g/m²;\n" +
					$"Strength warp/weft: {_strengthWarp * 50:0.#} / {_strengthWeft * 50:0.#} N/5cm;\n";
				if (_stiffnessWarp > 0 || _stiffnessWeft > 0)
					text += $"Stiffness warp/weft: {_stiffnessWarp:0.#} / {_stiffnessWeft:0.#} kN/m;\n";
				if (_poissonRatio > 0)
					text += $"Poisson ratio: {_poissonRatio};\n";
				if (_shearStiffness > 0)
					text += $"Shear stiffness: {_shearStiffness:0.#} kN/m;\n";
				return text;
			}

			if (string.IsNullOrEmpty(_id) || string.IsNullOrWhiteSpace(_id))
			{
				if (_materialKind == MaterialKind.Concrete)
				{
					text = $"FeMM Material Name: {Name}; \n" +
						$"Kind: {_materialKind};\n" +
						$"Compressive strength stress:{_expectedCompressiveStrength};\n" +
						$"Elastic Modulus:{_modulus};\n" +
						$"Poisson ratio:{_poissonRatio};\n" +
						$"Id:{_id};\n";
				}
				else
				{

					text = $"FeMM Material Name: {Name}; \n" +
						$"Kind: {_materialKind};\n" +
						$"Minimum yielding stress:{_minimumYieldStress};\n" +
						$"Minimum ultimate stress:{_minimumTensileStress};\n" +
						$"Elastic Modulus:{_modulus};\n" +
						$"Poisson ratio:{_poissonRatio};\n" +
						$"Id:{_id};\n";
				}
			}
			else
			{
				text = $"FeMM Material Name: {Name}; \n" +
					$"Id:{_id};\n" +
					$"Kind: {_materialKind};\n" +
					$"Minimum yielding stress:{_minimumYieldStress};\n" +
					$"Minimum ultimate stress:{_minimumTensileStress};\n" +
					$"Elastic Modulus:{_modulus};\n" +
					$"Poisson ratio:{_poissonRatio};\n" +
					$"Id:{_id};\n";

			}

			return text;
		}
	}
}
