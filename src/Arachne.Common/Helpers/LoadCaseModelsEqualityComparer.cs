using System;
using System.Collections.Generic;
using Arachne.Common.Models;

namespace Arachne.Common.Helpers
{
    /// <summary>
    /// Compare two LoadCaseModel objects
    /// </summary>
    public class LoadCaseModelsEqualityComparer : IEqualityComparer<LoadCaseModel>
    {
        public bool Equals(LoadCaseModel x, LoadCaseModel y)
        {
            // Check whether the compared objects reference the same data.
            if (ReferenceEquals(x, y))
                return true;

            // Check whether any of the compared objects is null.
            if (x == null || y == null)
                return false;

            // Compare groups for equality
            return x.Name == y.Name;
        }

        public int GetHashCode(LoadCaseModel lcase)
        {
            // Check whether the object is null
            if (lcase == null)
                return 0;

            // Get hash code for the Name field if it is not null.
            return lcase.Name == null ? 0 : lcase.Name.GetHashCode();
        }
    }
}
