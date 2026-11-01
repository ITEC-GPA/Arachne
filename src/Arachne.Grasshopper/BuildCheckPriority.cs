using Grasshopper.Kernel;

namespace Arachne.Grasshopper
{
    /// <summary>
    /// Starts the load-time check shared by the gPC plug-ins: a bin built from another commit than the checkout, or GPC and
    /// TensileLib libraries loaded by another plug-in with other content than the copies next to Arachne, are reported.
    /// </summary>
    public sealed class BuildCheckPriority : GH_AssemblyPriority
    {
        public override GH_LoadingInstruction PriorityLoad()
        {
            Gpc.PluginChecks.PluginBuildCheck.Start(typeof(BuildCheckPriority).Assembly, "Arachne");
            return GH_LoadingInstruction.Proceed;
        }
    }
}
