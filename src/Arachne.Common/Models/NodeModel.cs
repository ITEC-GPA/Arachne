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
        private GPC.Model.Elements.NodeElement input = new GPC.Model.Elements.NodeElement(new GPC.Geometry.Point3d(0, 0, 0));
        internal GPC.Model.Elements.NodeElement Input => input;
        public Guid Identity => input.Guid;
        internal bool NeedsLegacyIdentity { get; private set; }
        protected string _nodeId { get => input.Name; set => input.Name = value; }
        protected Point3d _position
        {
            get => new Point3d(input.Position.X, input.Position.Y, input.Position.Z);
            set => input.Position = new GPC.Geometry.Point3d(value.X, value.Y, value.Z);
        }
        /// <summary>Assigns the GH owner's identity before a draft node receives domain assignments.
        /// Already bound identities are immutable once assignments or results exist.</summary>
        public void BindInputIdentity(Guid identity)
        {
            if (identity == Guid.Empty) throw new ArgumentException("EmptyNodeInputIdentity");
            if (identity != input.Guid)
            {
                var a = input.Assignments;
                if (a.Restrains.Count != 0 || a.GroundSprings.Count != 0 || a.Links.Count != 0 || a.Active.HasValue || a.Dofs != null || a.Mass != null || a.Physical != null
                    || input.Loads.Count != 0 || input.Attributes.Count != 0 || input.Groups.Count != 0 || input.Results.Count != 0)
                    throw new InvalidOperationException("CannotReidentifyAssignedNodeInput");
                input = new GPC.Model.Elements.NodeElement(identity, input.Position, input.CoordinateSystem, input.Name, input.Id) { Source = input.Source };
            }
            NeedsLegacyIdentity = false;
        }

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

        // Groups, loads, attributes and results are copied by ElementModel: copying them again here
        // replaced the attributes without the handler that tells an attribute its element
        public NodeModel(NodeModel node)
            : base()
        {
            var copy = Helpers.SerializationHelper.Clone(node);
            AdoptCollections(copy);
            input = copy.input;
            NeedsLegacyIdentity = copy.NeedsLegacyIdentity;
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "NodeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 2;

		protected NodeModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            int savedRelease = Arachne.Common.Helpers.SerializationHelper.ReadVersion(info, SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            if (savedRelease >= 2)
                input = (GPC.Model.Elements.NodeElement)info.GetValue("Input", typeof(GPC.Model.Elements.NodeElement)) ?? throw new SerializationException("MissingNodeInput");
            else
            {
                _nodeId = info.GetString("NodeId");
                _position = (Point3d)info.GetValue("Position", typeof(Point3d));
                NeedsLegacyIdentity = true;
            }
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Input", input, typeof(GPC.Model.Elements.NodeElement));
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
