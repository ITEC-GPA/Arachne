using System;
using System.Linq;
using System.Drawing;
using System.Runtime.Serialization;
using Rhino.Display;
using Rhino.Geometry;
using System.Collections.Generic;
using System.ComponentModel;

namespace Arachne.Common.Models
{
    [Serializable]
    public class BeamTaperAttributeModel : AttributeModel<BeamTaperAttributeModel.AttributeData>, IEquatable<BeamTaperAttributeModel>
    {
        public enum AlignmentCodes
        {
            [Description("Top")] Top = 0,
            [Description("Center")] Center = 1,
            [Description("Bottom")] Bottom = 2,
        }

        public class AttributeData : IEquatable<AttributeData>
        {

            public double NFromX { get; set; }
            public double NToX { get; set; }
            public double NFromY { get; set; }
            public double NToY { get; set; }
            public AlignmentCodes AlignmentX { get; set; }
            public AlignmentCodes AlignmentY { get; set; }

            public AttributeData()
            {
                NFromX = 1;
                NToX = 1;
                NFromY = 1;
                NToY = 1;
                AlignmentX = AlignmentCodes.Center;
                AlignmentY = AlignmentCodes.Center;
            }

            public override bool Equals(object obj)
            {
                return Equals(obj as AttributeData);
            }

            public bool Equals(AttributeData other)
            {
                return other != null &&
                       NFromX == other.NFromX &&
                       NToX == other.NToX &&
                       NFromY == other.NFromY &&
                       NToY == other.NToY &&
                       AlignmentX == other.AlignmentX &&
                       AlignmentY == other.AlignmentY;
            }

            public override int GetHashCode()
            {
                var hashCode = 17;
                hashCode = hashCode * -23 + NFromX.GetHashCode();
                hashCode = hashCode * -23 + NToX.GetHashCode();
                hashCode = hashCode * -23 + NFromY.GetHashCode();
                hashCode = hashCode * -23 + NToY.GetHashCode();
                hashCode = hashCode * -23 + AlignmentX.GetHashCode();
                hashCode = hashCode * -23 + AlignmentY.GetHashCode();
                return hashCode;
            }
        }

        public override string Description => "Beam Taper";

        public BeamTaperAttributeModel()
        {
        }

        public BeamTaperAttributeModel(AttributeData value)
            : base(value)
        {
        }

        public BeamTaperAttributeModel(BeamTaperAttributeModel attr)
        {
            _value = new AttributeData()
            {
                NFromX = attr.Value.NFromX,
                NToX = attr.Value.NToX,
                NFromY = attr.Value.NFromY,
                NToY = attr.Value.NToY,
                AlignmentX = attr.Value.AlignmentX,
                AlignmentY = attr.Value.AlignmentY
            };
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "BeamTaperAttributeModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected BeamTaperAttributeModel(SerializationInfo info, StreamingContext context)
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
            _value = new AttributeData()
            {
                NFromX = info.GetDouble("NFromX"),
                NToX = info.GetDouble("NToX"),
                NFromY = info.GetDouble("NFromY"),
                NToY = info.GetDouble("NToY"),
                AlignmentX =  (AlignmentCodes)info.GetValue("AlignmentX", typeof(AlignmentCodes)),
                AlignmentY = (AlignmentCodes)info.GetValue("AlignmentY", typeof(AlignmentCodes))
            };
        }

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("NFromX", _value.NFromX);
            info.AddValue("NToX", _value.NToX);
            info.AddValue("NFromY", _value.NFromY);
            info.AddValue("NToY", _value.NToY);
            info.AddValue("AlignmentX", _value.AlignmentX, typeof(AlignmentCodes));
            info.AddValue("AlignmentY", _value.AlignmentY, typeof(AlignmentCodes));
        }

        #endregion

        public static bool operator ==(BeamTaperAttributeModel bt1, BeamTaperAttributeModel bt2)
        {
            if (ReferenceEquals(bt1, bt2))
                return true;
            if (bt1 is null)
                return false;
            return bt1.Equals(bt2);
        }

        public static bool operator !=(BeamTaperAttributeModel bt1, BeamTaperAttributeModel bt2)
        {
            return !(bt1 == bt2);
        }

        public bool Equals(BeamTaperAttributeModel other)
        {
            return !(other is null) && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            if (obj is BeamTaperAttributeModel bt)
                return Equals(bt);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 17;
            hashCode = hashCode * 23 + Value.GetHashCode();

            return hashCode;
        }

        public override void UpdateElement(ElementModel element)
        {
            element.CreateShapes();
        }

        public static AttributeData GetTaper(BeamModel beam)
        {
            if (beam.HasAttribute(typeof(BeamTaperAttributeModel)))
            {
                IEnumerable<AttributeModel> attr = beam.Attributes.Where(n => n.GetType() == typeof(BeamTaperAttributeModel));
                if (attr.Count() > 0)
                    return attr.Cast<BeamTaperAttributeModel>().First().Value;
            }
            return null;
        }        
    }
}
