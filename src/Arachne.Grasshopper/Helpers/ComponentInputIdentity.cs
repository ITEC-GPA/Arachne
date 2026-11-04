using System;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Helpers
{
    internal static class ComponentInputIdentity
    {
        // Component InstanceGuid survives document reopen; output tree address survives
        // recomputation and coordinate edits. Coincident points do not define identity.
        internal static Guid For(GH_Component component, IGH_DataAccess access, int output = 0)
            => Common.Analysis.InputIdentity.Of(component.InstanceGuid,
                "output:" + output + ":" + access.ParameterTargetPath(output) + ":" + access.ParameterTargetIndex(output));
    }
}
