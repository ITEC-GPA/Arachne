using System;
using System.Threading;

namespace Arachne.Core
{
    public static class IterationControl
    {
        // Returns true only after a successful convergence check. The cap counts actual solves.
        public static bool Run(int maximum, Func<int, bool> step, CancellationToken cancellation, out int iterations)
        {
            if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (step == null) throw new ArgumentNullException(nameof(step));
            iterations = 0;
            for (int index = 1; index <= maximum; index++)
            {
                cancellation.ThrowIfCancellationRequested();
                iterations = index;
                if (step(index)) return true;
            }
            return false;
        }

        public static double RelativeError(double actual, double target)
        {
            Validation.Finite(actual, "Actual value"); Validation.Positive(target, "Target");
            return Math.Abs(actual - target) / target;
        }
        public static double RescaleDensity(double density, double numerator, double denominator)
        {
            Validation.Positive(density, "Density"); Validation.Positive(numerator, "Numerator");
            Validation.Positive(denominator, "Denominator");
            double value = density * numerator / denominator;
            Validation.Positive(value, "Updated density");
            return value;
        }
    }
}
