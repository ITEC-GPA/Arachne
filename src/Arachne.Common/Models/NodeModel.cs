using System;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;

namespace Arachne.Common.Models
{
    [Serializable]
    public class NodeModel : ElementModel
    {
        protected string _nodeId;
        protected Point3d _position;

        public string NodeId
        {
            get => _nodeId;
            set => _nodeId = value;
        }

        public Point3d Position
        {
            get => _position;
            set => _position = value;
        }

        public bool IsValid => _position != Point3d.Unset;

        //public bool HasRestraint => NodeRestraintAttributeModel.HasRestraint(this);

        public NodeModel(string nodeId = "", Point3d? position = null)
            : base()
        {
            _nodeId = nodeId;
            _position = position.HasValue ? position.Value : Point3d.Unset;
        }

        public NodeModel(Point3d position)
            : base()
        {
            _nodeId = "";
            _position = position;
        }

        public NodeModel(NodeModel node)
            : base(node)
        {
            _nodeId = (string)node._nodeId.Clone();
            _position = node._position;

            _attributes = new System.Collections.ObjectModel.ObservableCollection<AttributeModel>();
            for (int i = 0; i < node._attributes.Count;i++)
                _attributes.Add(node._attributes[i]);

            _groups = new System.Collections.ObjectModel.ObservableCollection<GroupModel>();
            for (int i = 0; i < node._groups.Count; i++)
                _groups.Add(node._groups[i]);

            _loads = new System.Collections.ObjectModel.ObservableCollection<LoadModel>();
            for (int i = 0; i < node._loads.Count; i++)
                _loads.Add(node._loads[i]);

            _results = new System.Collections.ObjectModel.ObservableCollection<ResultModel>();
            for (int i = 0; i < node._results.Count; i++)
                _results.Add(node._results[i]);
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected NodeModel(SerializationInfo info, StreamingContext context)
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
            _nodeId = info.GetString("NodeId");
            _position = (Point3d)info.GetValue("Position", typeof(Point3d));
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("NodeId", _nodeId);
            info.AddValue("Position", _position, typeof(Point3d));
        }

        #endregion

        /*public override void Merge(ElementModel other)
        {
            base.Merge(other);
        }*/

        public override void DrawWireframe(DisplayPipeline display, RhinoViewport viewport, Color color)
        {
            // https://discourse.mcneel.com/t/drawlinearrow-with-fixed-screen-size/69629            
            display.DrawPoint(_position, color);
        }

        public override string ToString()
        {
            string text = "FeMM Node";
            if (_nodeId != string.Empty)
                text += $" #{_nodeId}";
            text += "\nGroups: " + string.Join(", ", _groups);
            text += $"\nPosition {_position}";
            text += "\n" + base.ToString();
            return text;
        }

        public static int Comparer(NodeModel a, NodeModel b)
        {
            var rx = a.Position.X.CompareTo(b.Position.X);
			if (rx == 0)
            {
				var ry = a.Position.Y.CompareTo(b.Position.Y);

                if (ry == 0)
                    return a.Position.Z.CompareTo(b.Position.Z);
                else
                    return ry;
			}
            return rx;
		}
    }
}
