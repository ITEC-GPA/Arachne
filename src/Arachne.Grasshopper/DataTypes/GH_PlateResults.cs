using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Types;
using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_PlateResults : GH_Goo<List<ResultModel>>
    {
        #region Properties

        public override bool IsValid => Value != null && Value.Count > 0;

        public override string IsValidWhyNot => "Invalid or empty plate results";

        public override string TypeName => "Plate Result";

        public override string TypeDescription => "The plate" +
            " analysis results";

        #endregion

        #region Constructors

        public GH_PlateResults()
        {
        }

        public GH_PlateResults(GH_Plate plate)
            : this()
        {
            Value = new List<ResultModel>(plate.Value.Results);
        }

        #endregion

        #region IGH_Goo implementation functions

        public override IGH_Goo Duplicate()
        {
            return new GH_PlateResults
            {
                Value = new List<ResultModel>(Value)
            };
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        #endregion
    }
}
