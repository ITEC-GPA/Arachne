namespace Arachne.Grasshopper.Helpers
{
    public static class ModelHelper
    {
        /// <summary>The distance under which Build Model merges two nodes, and the filters match a position (document units).</summary>
        public static double GetTolerance() => Settings.Instance.Tolerance;

        public static double GetSquaredTolerance() => GPC.Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(Settings.Instance.Tolerance);
    }
}
