using System.ComponentModel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Helpers
{
    /// <summary>The choices of the beam load components (same values as FeMM) and the global vector of a load given in a coordinate
    /// system, with the arithmetic of FeMM's coordinate system (CoordinateSystemModel.GetAxes and ToGlobal).</summary>
    internal static class BeamLoadKinds
    {
        public enum LoadSchema
        {
            Undefined = -1, Uniform, Keystone, Triangular,
            [Description("Triangular at end 1 + Keystone")] TriangularEnd1_Keystone,
            [Description("Keystone + Triangular at end 2")] Keystone_TriangularEnd2,
            [Description("Triangular at ends + Keystone")] TriangularEnds_Keystone
        }
        public enum LoadDirection { Undefined = -1, X, Y, Z, X_Projected, Y_Projected, Z_Projected, Gravity, Gravity_Projected }

        /// <summary>A vector given in <paramref name="cs"/> in global coordinates; a null coordinate system is the global one.</summary>
        public static Vector3d ToGlobal(GPC.Geometry.CoordinateSystem cs, Vector3d v)
        {
            if (cs == null) return v;
            var origin = new Point3d(cs.Origin.X, cs.Origin.Y, cs.Origin.Z);
            Vector3d x = (origin + new Vector3d(cs.V1.X, cs.V1.Y, cs.V1.Z)) - origin, second = (origin + new Vector3d(cs.V2.X, cs.V2.Y, cs.V2.Z)) - origin;
            Vector3d z = Vector3d.CrossProduct(x, second), y = Vector3d.CrossProduct(z, x);
            if (!(z.Length > 1e-9 * x.Length * second.Length) || !(x.Unitize() && y.Unitize() && z.Unitize())) return v;
            return x * v.X + y * v.Y + z * v.Z;
        }
    }
}
