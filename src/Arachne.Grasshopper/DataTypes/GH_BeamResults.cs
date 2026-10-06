using System;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
using System.IO;
using Grasshopper.Kernel.Types;
using GH_IO.Serialization;
using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_BeamResults : GH_Goo<List<ResultModel>>
    {
        #region Properties

        public override bool IsValid => Value != null && Value.Count > 0;

        public override string IsValidWhyNot => "Invalid or empty beam results";

        public override string TypeName => "Beam Result";

        public override string TypeDescription => "The beam analysis results";

        #endregion

        #region Constructors

        public GH_BeamResults()
        {
        }

        public GH_BeamResults(GH_Beam beam)
            : this()
        {
            Value = new List<ResultModel>(beam.Value.Results);
        }

        #endregion

        #region IGH_Goo implementation functions

        public override IGH_Goo Duplicate()
        {
            return new GH_BeamResults
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
