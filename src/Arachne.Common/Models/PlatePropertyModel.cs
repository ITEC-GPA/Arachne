using System;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    public enum PlatePropertyType
    {
        Undefined = -1,
        [Description("Shell-Thin")]
        ShellThin,
        [Description("Shell-Thick")]
        ShellThick,
        [Description("Plate-Thin")]
        PlateThin,
        [Description("Plate-Thick")]
        PlateThick,
        Membrane,
        [Description("Load Patch")]
        LoadPatch,
        [Description("Shear Panel")]
        ShearPanel
    }

    [Serializable]
    public class PlatePropertyModel
    {
        protected string _id;
        protected string _name;
        protected PlatePropertyType _platePropertyType;
        protected MaterialModel _material;
        protected double _membraneThickness;
        protected double _bendingThickness;

        public string Id => _id;

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public PlatePropertyType PropertyType
        {
            get => _platePropertyType;
            set => _platePropertyType = value;
        }

        public MaterialModel Material
        {
            get => _material;
            set => _material = value;
        }

        public double MembraneThickness
        {
            get => _membraneThickness;
            set => _membraneThickness = value;
        }

        public double BendingThickness
        {
            get => _bendingThickness;
            set => _bendingThickness = value;
        }

        public bool IsValid
        {
            get
            {
                if (_material == null)
                    return false;
                if (_membraneThickness == 0 && _bendingThickness == 0)
                    return false;
                return true;
            }
        }

        public PlatePropertyModel()
        {
        }

        public PlatePropertyModel(string id)
            : this()
        {
            _id = id;
        }

        public PlatePropertyModel(PlatePropertyModel pp)
        {
            CopyFrom(pp);
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "PlatePropertyModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected PlatePropertyModel(SerializationInfo info, StreamingContext context)
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
            _platePropertyType = (PlatePropertyType)info.GetValue("PropertyType", typeof(PlatePropertyType));
            _material = (MaterialModel)info.GetValue("Material", typeof(MaterialModel));
            _membraneThickness = info.GetDouble("MembraneThickness");
            _bendingThickness = info.GetDouble("BendingThickness");
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Id", _id);
            info.AddValue("Name", _name);
            info.AddValue("PropertyType", _platePropertyType, typeof(PlatePropertyType));
            info.AddValue("Material", _material, typeof(MaterialModel));
            info.AddValue("MembraneThickness", _membraneThickness);
            info.AddValue("BendingThickness", _bendingThickness);
        }

        #endregion

        public void CopyFrom(PlatePropertyModel pp)
        {
            if (pp._id != null)
                _id = pp._id;
            _name = pp._name;
            _platePropertyType = pp._platePropertyType;
            _material = pp._material;
            _membraneThickness = pp._membraneThickness;
            _bendingThickness = pp._bendingThickness;
        }

        public override string ToString()
        {
            string text = string.Empty;

            if (string.IsNullOrEmpty(_id) || string.IsNullOrWhiteSpace(_id))
            {
                text = $"FeMM plate Property Name: {Name}; {_material}";
            }
            else
            {
                text = $"FeMM plate Property Name: {Name}; Id:{_id}; {_material}";
            }

            return text;
        }
    }
}
