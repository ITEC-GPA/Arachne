using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Permissions;
using Rhino.Geometry;

namespace Arachne.Common.Models
{
    [Serializable]
    public class CoordinateSystemModel : ISerializable, IEquatable<CoordinateSystemModel>
    {
        protected string _id;
        protected string _name;
        protected Point3d _origin;
        protected Point3d _point1;
        protected Point3d _point2;

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

        public Point3d Origin
        {
            get => _origin;
            set => _origin = value;
        }

        public Point3d Point1
        {
            get => _point1;
            set => _point1 = value;
        }

        public Point3d Point2
        {
            get => _point2;
            set => _point2 = value;
        }

        public readonly static CoordinateSystemModel Global = new CoordinateSystemModel("1", "Global");

        public CoordinateSystemModel(string id, string name = "")
        {
            _id = id;
            if (name == "")
                _name = id;
            else
                _name = name;
            _origin = Point3d.Origin;
            _point1 = new Point3d(1, 0, 0);
            _point2 = new Point3d(0, 1, 0);
        }

        public CoordinateSystemModel(CoordinateSystemModel cs)
        {
            _id = cs._id;
            _name = cs._name;
            _origin = cs._origin;
            _point1 = cs._point1;
            _point2 = cs._point2;
        }

        /// <summary>
        /// The coordinate system of a plane: its origin, X and Y axes
        /// </summary>
        public static CoordinateSystemModel FromPlane(Plane plane, string name = "Plane")
        {
            return new CoordinateSystemModel(name)
            {
                _origin = plane.Origin,
                _point1 = plane.Origin + plane.XAxis,
                _point2 = plane.Origin + plane.YAxis
            };
        }

        /// <summary>
        /// The unit axes: X towards Point1, Z normal to the plane of the three points (right
        /// hand from X to Point2), Y = Z x X. False if the three points do not define a plane.
        /// </summary>
        public bool GetAxes(out Vector3d xAxis, out Vector3d yAxis, out Vector3d zAxis)
        {
            xAxis = _point1 - _origin;
            Vector3d second = _point2 - _origin;
            zAxis = Vector3d.CrossProduct(xAxis, second);
            yAxis = Vector3d.CrossProduct(zAxis, xAxis);
            if (!(zAxis.Length > 1e-9 * xAxis.Length * second.Length))
                return false;
            return xAxis.Unitize() && yAxis.Unitize() && zAxis.Unitize();
        }

        #region Serialization
        private string SERIALIZATION_RELEASE_NAME = "CoordinateSystemModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 1;

		protected CoordinateSystemModel(SerializationInfo info, StreamingContext context)
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
            _origin = (Point3d)info.GetValue("Origin", typeof(Point3d));
            _point1 = (Point3d)info.GetValue("Point1", typeof(Point3d));
            _point2 = (Point3d)info.GetValue("Point2", typeof(Point3d));
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Id", _id);
            info.AddValue("Name", _name);
            info.AddValue("Origin", _origin, typeof(Point3d));
            info.AddValue("Point1", _point1, typeof(Point3d));
            info.AddValue("Point2", _point2, typeof(Point3d));
        }

        #endregion

        public static bool operator ==(CoordinateSystemModel cs1, CoordinateSystemModel cs2)
        {
            if (ReferenceEquals(cs1, cs2))
                return true;
            return !(cs1 is null) && cs1.Equals(cs2);
        }

        public static bool operator !=(CoordinateSystemModel cs1, CoordinateSystemModel cs2)
        {
            return !(cs1 == cs2);
        }

        public bool Equals(CoordinateSystemModel other)
        {
            return !(other is null) && _id == other._id && _name == other._name && _origin.Equals(other._origin) &&
                _point1.Equals(other._point1) && _point2.Equals(other._point2);
        }

        public override bool Equals(object obj)
        {
            if (obj is CoordinateSystemModel cs)
                return Equals(cs);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = 98151905;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_id);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_name);
            hashCode = hashCode * -1521134295 + EqualityComparer<Point3d>.Default.GetHashCode(_origin);
            hashCode = hashCode * -1521134295 + EqualityComparer<Point3d>.Default.GetHashCode(_point1);
            hashCode = hashCode * -1521134295 + EqualityComparer<Point3d>.Default.GetHashCode(_point2);
            return hashCode;
        }

        public override string ToString()
        {
            return $"FeMM {_name} coordinate system";
        }
    }
}
