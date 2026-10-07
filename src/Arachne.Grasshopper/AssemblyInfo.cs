using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper
{
    public sealed class AssemblyInfo : GH_AssemblyInfo
    {
        public override string Name => "Arachne";
        public override Bitmap Icon => Helpers.ComponentIcons.Get("Arachne");
        public override string Description => "Form finding e patterning di tele e tensostrutture: componenti FeMM.";
        public override Guid Id => new Guid("3f5451ce-fb9d-4b79-bbc3-0d78995556ce");
        public override string AuthorName => "GPC";
        public override string AuthorContact => "";
    }
}
