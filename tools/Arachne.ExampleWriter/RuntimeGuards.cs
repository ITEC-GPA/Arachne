using System;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.Loader;

// Preconditions of the .NET 8 validation host, formerly in tools/Arachne.RuntimeChecks: BinaryFormatter off, Rhino's
// Windows System.Management loaded before Rhino starts, and no serialization type referenced by the shipped binaries.
internal static class RuntimeGuards
{
    internal const string BinaryFormatterSwitch = "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization";

    /// <summary>Call first in Main, before any Rhino or Grasshopper type is touched.</summary>
    internal static void PrepareHost(string rhinoSystem)
    {
        // Also set in runtimeconfig.json (EnableUnsafeBinaryFormatterSerialization=false); the report records the effective value.
        AppContext.SetSwitch(BinaryFormatterSwitch, false);
        // Rhino 8 uses this Windows implementation. The netcore root DLL is a throwing stub; loading it changes
        // HostUtils.DeviceId and can invalidate the Cloud Zoo lease of the interactive Rhino session. Load it
        // before Rhino starts, as BridGHe.Smoke does; without it the checks do not start.
        string management = Path.Combine(rhinoSystem, "netcore", "runtimes", "win", "lib", "net7.0", "System.Management.dll");
        if (!File.Exists(management)) throw new FileNotFoundException("Rhino's Windows System.Management not found; checks not started.", management);
        AssemblyLoadContext.Default.LoadFromAssemblyPath(management);
    }

    /// <summary>The binaries of the plug-in folder reference neither BinaryFormatter nor an NRBF reader.</summary>
    internal static void VerifySerializationReferences(string pluginDir)
    {
        foreach (string name in new[] { "Arachne.Grasshopper.gha", "Arachne.Common.dll", "Arachne.Arap.dll",
            "GPCUtilities.dll", "GPCGeometry.dll", "DelaunayMesh.dll", "GPCModel.dll", "GPC.Model.Persistence.dll", "GPC.Archive.dll" })
        {
            using (var file = File.OpenRead(Path.Combine(pluginDir, name)))
            using (var pe = new PEReader(file))
            {
                var metadata = pe.GetMetadataReader();
                foreach (var handle in metadata.TypeReferences)
                {
                    var type = metadata.GetTypeReference(handle);
                    string ns = metadata.GetString(type.Namespace), typeName = metadata.GetString(type.Name);
                    if ((ns == "System.Runtime.Serialization.Formatters.Binary" && typeName == "BinaryFormatter")
                        || ns.StartsWith("System.Formats.Nrbf", StringComparison.Ordinal))
                        throw new InvalidOperationException(name + " references forbidden serialization type: " + ns + "." + typeName);
                }
            }
        }
        Console.WriteLine("Serialization metadata: no BinaryFormatter or NRBF reader references in Arachne and the GPC set.");
    }
}
