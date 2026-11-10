using GPC.Geometry;
using Grasshopper.Kernel.Types;
using R = Rhino.Geometry;

namespace Arachne.Grasshopper.DataTypes
{
    /// <summary>A coordinate system: a GPC <see cref="CoordinateSystem"/> (origin in the document units), from a Rhino plane.</summary>
    public class GH_CoordinateSystem : GpcGoo<CoordinateSystem>
    {
        public override string TypeName => "CoordinateSystem";
        public override string TypeDescription => "The coordinate system definition";
        public GH_CoordinateSystem() { }
        public GH_CoordinateSystem(CoordinateSystem cs) : base(Copy(cs)) { }
        public static GH_CoordinateSystem FromPlane(R.Plane plane)
            => new GH_CoordinateSystem { Value = new CoordinateSystem(new Point3d(plane.OriginX, plane.OriginY, plane.OriginZ),
                new Vector3d(plane.XAxis.X, plane.XAxis.Y, plane.XAxis.Z), new Vector3d(plane.YAxis.X, plane.YAxis.Y, plane.YAxis.Z), "Plane") };
        public static GH_CoordinateSystem Global => new GH_CoordinateSystem(CoordinateSystem.Global);
        public override IGH_Goo Duplicate() => new GH_CoordinateSystem(Value);
        public override bool CastFrom(object source)
        {
            if (source is CoordinateSystem cs) { Value = Copy(cs); return true; }
            if (source is GH_Plane p && p.Value.IsValid) { Value = FromPlane(p.Value).Value; return true; }
            if (source is R.Plane plane && plane.IsValid) { Value = FromPlane(plane).Value; return true; }
            return false;
        }
        public override bool CastTo<Q>(ref Q target)
        {
            if (Value != null && typeof(Q).IsAssignableFrom(typeof(GH_Plane)))
            {
                var plane = new R.Plane(new R.Point3d(Value.Origin.X, Value.Origin.Y, Value.Origin.Z), new R.Vector3d(Value.V1.X, Value.V1.Y, Value.V1.Z), new R.Vector3d(Value.V2.X, Value.V2.Y, Value.V2.Z));
                target = (Q)(object)new GH_Plane(plane); return true;
            }
            return false;
        }
        public override string ToString() => Value == null ? "Null CoordinateSystem" : $"Coordinate system {Value.Name}: origin {Value.Origin}, X {Value.V1}, Y {Value.V2}";
    }
}
