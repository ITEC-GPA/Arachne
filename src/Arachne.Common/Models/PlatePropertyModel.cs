using System;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
    [Serializable]
    public class PlatePropertyModel
    {
        protected string _id;
        protected string _name;
        protected MaterialModel _material;
        // One thickness: FeMM had the SAP shell type with membrane and bending thickness
        [OptionalField]
        protected double _thickness;

        public string Id => _id;

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

        public double Thickness
        {
            get => _thickness;
            set => _thickness = value;
        }

        public bool IsValid => _material != null && _thickness > 0;

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

        public void CopyFrom(PlatePropertyModel pp)
        {
            if (pp._id != null)
                _id = pp._id;
            _name = pp._name;
            _material = pp._material;
            _thickness = pp._thickness;
        }

        public override string ToString()
        {
            string text = string.Empty;

            if (string.IsNullOrEmpty(_id) || string.IsNullOrWhiteSpace(_id))
            {
                text = $"Plate Property Name: {Name}; Thickness: {_thickness}; {_material}";
            }
            else
            {
                text = $"Plate Property Name: {Name}; Id:{_id}; Thickness: {_thickness}; {_material}";
            }

            return text;
        }
    }
}
