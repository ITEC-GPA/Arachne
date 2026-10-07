using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Drawing;
using System.Runtime.Serialization;
using System.Security.Permissions;
using Rhino.Display;
using System.Collections.ObjectModel;
using Rhino.Geometry;

namespace Arachne.Common.Models
{
    [Serializable]
    public class ElementModel : ISerializable
    {
        protected ObservableCollection<GroupModel> _groups;
        protected ObservableCollection<LoadModel> _loads;
        protected ObservableCollection<AttributeModel> _attributes;
        protected ObservableCollection<ResultModel> _results;

        public ObservableCollection<GroupModel> Groups => _groups;

        public ObservableCollection<LoadModel> Loads => _loads;

        public ObservableCollection<AttributeModel> Attributes => _attributes;

        public ObservableCollection<ResultModel> Results => _results;

        public ElementModel()
        {
            _groups = new ObservableCollection<GroupModel>();
            _loads = new ObservableCollection<LoadModel>();
            _attributes = new ObservableCollection<AttributeModel>();
            _attributes.CollectionChanged += Attributes_CollectionChanged;
            _results = new ObservableCollection<ResultModel>();
        }

        private void Attributes_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add)
            {
                foreach (AttributeModel attribute in e.NewItems)
                    attribute.UpdateElement(this);
            }
        }

        public ElementModel(ElementModel element)
        {
            _groups = new ObservableCollection<GroupModel>(element._groups);
            _loads = new ObservableCollection<LoadModel>(element._loads);
            _attributes = new ObservableCollection<AttributeModel>(element._attributes);
            _attributes.CollectionChanged += Attributes_CollectionChanged;
            _results = new ObservableCollection<ResultModel>(element._results);
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "ElementModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected ElementModel(SerializationInfo info, StreamingContext context)
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
            _groups = (ObservableCollection<GroupModel>)info.GetValue("Groups", typeof(ObservableCollection<GroupModel>));
            _loads = (ObservableCollection<LoadModel>)info.GetValue("Loads", typeof(ObservableCollection<LoadModel>));
            _attributes = (ObservableCollection<AttributeModel>)info.GetValue("Attributes", typeof(ObservableCollection<AttributeModel>));
            _results = (ObservableCollection<ResultModel>)info.GetValue("Results", typeof(ObservableCollection<ResultModel>));
        }

        [SecurityPermission(SecurityAction.Demand, SerializationFormatter = true)]
        public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException("info");
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Groups", _groups, typeof(ObservableCollection<GroupModel>));
            info.AddValue("Loads", _loads, typeof(ObservableCollection<LoadModel>));
            info.AddValue("Attributes", _attributes, typeof(ObservableCollection<AttributeModel>));
            info.AddValue("Results", _results, typeof(ObservableCollection<ResultModel>));
        }

        #endregion

        public bool HasAttribute(Type type)
        {
            if (_attributes.Count(p => p.GetType() == type) > 0)
                return true;
            return false;
        }

        public bool HasLoad(Type type)
        {
            if (_loads.Count(p => p.GetType() == type) > 0)
                return true;
            return false;
        }

        public bool HasGroup(GroupModel g)
        {
            foreach (GroupModel other in Groups)
            {
                if (other.Equals(g)) return true;
            }
            return false;
        }

        public bool HasId(string id)
        {
            ElementIdAttributeModel attr = (ElementIdAttributeModel)Attributes.FirstOrDefault(a => a is ElementIdAttributeModel);
            return attr != null && attr.Value.Equals(id);
        }

        public virtual void Merge(ElementModel other)
        {
            foreach (GroupModel group in other.Groups)
            {
                if (!_groups.Contains(group))
                    _groups.Add(group);
            }

            foreach (LoadModel load in other.Loads)
            {
                if (!_loads.Contains(load))
                    _loads.Add(load);
            }

            foreach (AttributeModel attribute in other.Attributes)
            {
                if (!_attributes.Contains(attribute))
                    _attributes.Add(attribute);
            }

            foreach (ResultModel result in other.Results)
            {
                if (!_results.Contains(result))
                    _results.Add(result);
            }
        }

        public virtual void CreateShapes()
        {
        }

        public virtual void DrawWireframe(DisplayPipeline display, RhinoViewport viewport, Color color)
        {
        }

        public virtual void DrawSolid(DisplayPipeline display, RhinoViewport viewport, Color color)
        {
        }

        public virtual void DrawAttributes(DisplayPipeline display, RhinoViewport viewport)
        {
            foreach (LoadModel load in _loads)
                load.Draw(display, viewport, Color.IndianRed, this);
            foreach (AttributeModel attribute in _attributes)
                attribute.Draw(display, viewport, Color.IndianRed, this);
            foreach (ResultModel result in _results)
                result.Draw(display, viewport, Color.Beige, this);
        }

        public override string ToString()
        {
            string text = "";
            if (_attributes.Count > 0)
            {
                text += "Attributes: \n";
                foreach (AttributeModel attribute in _attributes)
                    text += " - " + attribute.ToString() + "\n";
            }
            if (_loads.Count > 0)
            {
                if (text != "")
                    text += "\n";
                text += "Loads: \n";
                foreach (LoadModel load in _loads)
                    text += " - " + load.ToString() + "\n";
            }

            if (_results.Count > 0)
                text += "\nWith results";
            return text;
        }
    }
}
