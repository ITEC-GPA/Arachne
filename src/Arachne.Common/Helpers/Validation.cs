using System;

namespace Arachne.Common.Helpers
{
    /// <summary>
    /// Argument checks shared by the patterning services and the cable net generators.
    /// </summary>
    public static class Validation
    {
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static void Finite(double value, string name)
        {
            if (!IsFinite(value))
                throw new ArgumentException(name + " must be finite.");
        }

        public static void Positive(double value, string name)
        {
            Finite(value, name);
            if (value <= 0)
                throw new ArgumentException(name + " must be > 0.");
        }

        public static void NonNegative(double value, string name)
        {
            Finite(value, name);
            if (value < 0)
                throw new ArgumentException(name + " must be >= 0.");
        }
    }
}
