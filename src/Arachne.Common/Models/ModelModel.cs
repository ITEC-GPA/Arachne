using Arachne.Common.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
	[Serializable]
	public class ModelModel : ISerializable, ICloneable
	{
		public Guid InputOwner { get; set; } = Guid.NewGuid();
        public Dictionary<ElementModel, Guid> InputIdentities { get; private set; } = new Dictionary<ElementModel, Guid>();
        public const double GRAVITY_mmSuS2 = 9806.65;

		public enum ModelUnits
		{
			[Description("kNm (m, kN, T, kPa, s, kJ, C)")]
			Knm,
			[Description("SI (m, N, kg, Pa, s, J, C)")]
			SI,
			[Description("Nmm (mm, N, T, MPa, s, mJ, C)")]
			Nmm,
			[Description("PSI (in, lbf, lb, psi, s, btu, F)")]
			IPS
		}

		protected string _name;
		protected ModelUnits _units;
		protected List<ElementModel> _elements;
		protected List<NodeModel> _nodes;
		protected List<BeamModel> _beams;
		protected List<PlateModel> _plates;
		protected List<LoadCaseModel> _loadCases;
		protected Dictionary<string, int> _nodesIndexes;
		protected Dictionary<string, int> _beamsIndexes;
		protected Dictionary<string, int> _platesIndexes;

		public string Name
		{
			get => _name;
			set => _name = value;
		}

		public ModelUnits Units => _units;

		public List<ElementModel> Elements => _elements;

		public List<NodeModel> Nodes => _nodes;

		public List<BeamModel> Beams => _beams;

		public List<PlateModel> Plates => _plates;

		public List<LoadCaseModel> LoadCases => _loadCases;

		public ModelModel(ModelUnits units)
		{
			_name = "";
			_units = units;
			_elements = new List<ElementModel>();
			_nodes = new List<NodeModel>();
			_beams = new List<BeamModel>();
			_plates = new List<PlateModel>();
			_loadCases = new List<LoadCaseModel>();
			_nodesIndexes = new Dictionary<string, int>();
			_beamsIndexes = new Dictionary<string, int>();
			_platesIndexes = new Dictionary<string, int>();
		}

		public ModelModel(ModelModel model)
		{
            var copy = SerializationHelper.Clone(model);
            InputOwner = copy.InputOwner; InputIdentities = copy.InputIdentities;
            _name = copy._name; _units = copy._units; _elements = copy._elements;
            _nodes = copy._nodes; _beams = copy._beams; _plates = copy._plates;
            _loadCases = copy._loadCases;
            _nodesIndexes = copy._nodesIndexes; _beamsIndexes = copy._beamsIndexes; _platesIndexes = copy._platesIndexes;
		}

		#region Serialization

		private string SERIALIZATION_RELEASE_NAME = "ModelModel_SERIALIZATION_RELEASE"; //do not modify the name!
		// Release 5: load combinations, named sets, staged constructions and functions removed (FEM analysis only)
		private int SERIALIZATION_RELEASE = 6;

		protected ModelModel(SerializationInfo info, StreamingContext context)
		{
            int savedRelease = SerializationHelper.ReadVersion(info, SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            InputOwner = SerializationHelper.ReadOptional(info, "InputOwner", Guid.NewGuid());
            InputIdentities = SerializationHelper.ReadOptional(info, "InputIdentities", new Dictionary<ElementModel, Guid>());
			_name = info.GetString("Name");
			_units = (ModelUnits)info.GetValue("Units", typeof(ModelUnits));
			_elements = (List<ElementModel>)info.GetValue("Elements", typeof(List<ElementModel>));
			_loadCases = (List<LoadCaseModel>)info.GetValue("LoadCases", typeof(List<LoadCaseModel>));
			_nodes = new List<NodeModel>();
			_beams = new List<BeamModel>();
			_plates = new List<PlateModel>();
			// These indexes are provided to improve the performance getting elements
			_nodesIndexes = new Dictionary<string, int>();
			_beamsIndexes = new Dictionary<string, int>();
			_platesIndexes = new Dictionary<string, int>();
			//Sync(0);//here cannot be done this because _elements is full of nulls! 
		}

		[SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			if (info == null)
				throw new ArgumentNullException("info");
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("InputOwner", InputOwner);
            info.AddValue("InputIdentities", InputIdentities);
            info.AddValue("Name", _name);
			info.AddValue("Units", _units, typeof(ModelUnits));
			info.AddValue("Elements", _elements, typeof(List<ElementModel>));
			info.AddValue("LoadCases", _loadCases, typeof(List<LoadCaseModel>));
		}

		#endregion

		public void AddElements(IEnumerable<ElementModel> elements)
		{
			int starting = _elements.Count();
			_elements.AddRange(elements);
			Sync(starting);
		}

		public void AddLoadCases(IEnumerable<LoadCaseModel> loadCases)
		{
			_loadCases.AddRange(loadCases);
		}
		public NodeModel GetNode(string id)
		{
			if (_nodesIndexes.TryGetValue(id, out int index))
				return (NodeModel)_elements[index];
			return null;
		}

		public BeamModel GetBeam(string id)
		{
			if (_beamsIndexes.TryGetValue(id, out int index))
				return (BeamModel)_elements[index];
			return null;
		}

		public PlateModel GetPlate(string id)
		{
			if (_platesIndexes.TryGetValue(id, out int index))
				return (PlateModel)_elements[index];
			return null;
		}

		/// <summary>
		/// Synchronizes the indexes to the elements arrays
		/// </summary>
		/// <param name="starting">The index starting item</param>
		protected void Sync(int starting)
		{
			for (int i = starting; i < _elements.Count; i++)
			{
				if (_elements[i] is NodeModel node)
				{
					if (!_nodesIndexes.ContainsKey(node.NodeId))
					{
						_nodes.Add(node);
						_nodesIndexes.Add(node.NodeId, i);
					}
					else
					{
                        NodeModel nn = (NodeModel)_elements[_nodesIndexes[node.NodeId]];
						nn.Merge(node);
                    }
				}
				else if (_elements[i] is BeamModel beam)
				{
					if (!_beamsIndexes.ContainsKey(beam.BeamId))
					{
						_beams.Add(beam);
						_beamsIndexes.Add(beam.BeamId, i);
					}
					else
					{
                        BeamModel nn = (BeamModel)_elements[_beamsIndexes[beam.BeamId]];
                        nn.Merge(beam);
                    }
				}
				else if (_elements[i] is PlateModel plate)
				{
					if (!_platesIndexes.ContainsKey(plate.PlateId))
					{
						_plates.Add(plate);
						_platesIndexes.Add(plate.PlateId, i);
					}
					else
                    {
                        PlateModel nn = (PlateModel)_elements[_platesIndexes[plate.PlateId]];
                        nn.Merge(plate);
                    }
                }
				else
					throw new InvalidCastException("Unknown Element type");
			}
		}

		/// <summary>
		/// Make a deep clone of the Model including all of his components
		/// </summary>
		/// <returns>The deep copy</returns>
		public object Clone()
		{
            return Arachne.Common.Helpers.SerializationHelper.Clone(this);
        }

        internal void RebuildIndexes() => Sync(0);

		/*/// <summary>
        /// Setup the conversion parameter for converting measures through the Utilities library
        /// </summary>
        /// <param name="units">The model units</param>
        /// <param name="lengthUnits"></param>
        /// <param name="forceUnits"></param>
        /// <param name="massUnits"></param>
        /// <param name="pressureUnits"></param>
        /// <param name="temperatureUnits"></param>
        public static void GetUtilitiesConversionParams(
            ModelUnits units,
            out Utilities.Units.LengthUnits lengthUnits,
            out Utilities.Units.ForceUnits forceUnits,
            out Utilities.Units.MassUnits massUnits,
            out Utilities.Units.PressureUnits pressureUnits,
            out Utilities.Units.TemperatureUnits temperatureUnits)
        {
            switch (units)
            {
                case ModelUnits.Knm:
                    lengthUnits = Utilities.Units.LengthUnits.m;
                    forceUnits = Utilities.Units.ForceUnits.kN;
                    massUnits = Utilities.Units.MassUnits.ton;
                    pressureUnits = Utilities.Units.PressureUnits.kPa;
                    temperatureUnits = Utilities.Units.TemperatureUnits.C;
                    break;
                case ModelUnits.Nmm:
                    lengthUnits = Utilities.Units.LengthUnits.mm;
                    forceUnits = Utilities.Units.ForceUnits.N;
                    massUnits = Utilities.Units.MassUnits.ton;
                    pressureUnits = Utilities.Units.PressureUnits.MPa;
                    temperatureUnits = Utilities.Units.TemperatureUnits.C;
                    break;
                case ModelUnits.SI:
                    lengthUnits = Utilities.Units.LengthUnits.m;
                    forceUnits = Utilities.Units.ForceUnits.N;
                    massUnits = Utilities.Units.MassUnits.kg;
                    pressureUnits = Utilities.Units.PressureUnits.Pa;
                    temperatureUnits = Utilities.Units.TemperatureUnits.C;
                    break;
                case ModelUnits.IPS:
                    lengthUnits = Utilities.Units.LengthUnits.inch;
                    forceUnits = Utilities.Units.ForceUnits.lbf;
                    massUnits = Utilities.Units.MassUnits.lb;
                    pressureUnits = Utilities.Units.PressureUnits.psi;
                    temperatureUnits = Utilities.Units.TemperatureUnits.F;
                    break;
                default:
                    throw new NotSupportedException();
            }
        }*/

		public override string ToString()
		{
			string text = $"Model {_name}";
			text += "\nMeasure units: " + _units.GetDescription();
			text += string.Format("\n{0} Nodes, {1} Beams, {2} Plates", _nodes.Count, _beams.Count, _plates.Count);
			return text;
		}
	}
}
