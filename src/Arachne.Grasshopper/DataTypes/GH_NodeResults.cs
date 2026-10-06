using System;
using System.Collections.Generic;
using Grasshopper.Kernel.Types;
using Arachne.Common.Models;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_NodeResults : GH_Goo<List<ResultModel>>
    {
        #region Properties

        public override bool IsValid => Value != null && Value.Count > 0;

        public override string IsValidWhyNot => "Invalid or empty node results";

        public override string TypeName => "Node Result";

        public override string TypeDescription => "The node analysis results";

        #endregion

        #region Constructors

        public GH_NodeResults()
        {
        }

        public GH_NodeResults(GH_Node node)
            : this()
        {
            Value = new List<ResultModel>(node.Value.Results);
        }

        #endregion

        #region GH_Goo implementation functions

        public override IGH_Goo Duplicate()
        {
            return new GH_NodeResults
            {
                Value = new List<ResultModel>(Value)
            };
        }

        /*public bool CastFrom(object source)
        {
            return false; ;
        }

        public bool CastTo<T>(out T target)
        {
            target = default(T);
            return false;
        }*/

        public override string ToString()
        {
            return Value.ToString();
        }

        #endregion
    }
}
