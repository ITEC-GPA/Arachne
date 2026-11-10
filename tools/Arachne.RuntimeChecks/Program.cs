using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

// Runs the same net48 validation assembly in a modern CLR. No plug-in sources
// are recompiled for this probe: the binaries are the ones shipped to Rhino.
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: RuntimeChecks <ExampleWriter.exe> <Arachne root> <Rhino System> <Grasshopper dir> [ExampleWriter options]");
            return 2;
        }
        try
        {
            AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false);
            VerifySerializationReferences(args[0]);
            var runner = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            return (int)runner.EntryPoint.Invoke(null, new object[] { args.Skip(1).ToArray() });
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetBaseException());
            return 1;
        }
    }

    private static void VerifySerializationReferences(string runnerPath)
    {
        var tool = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(runnerPath)));
        string plugin = Path.Combine(tool.Parent.Parent.Parent.FullName, "Arachne.Grasshopper", tool.Parent.Name, "net48");
        foreach (string name in new[] { "Arachne.Grasshopper.gha", "Arachne.Common.dll", "Arachne.Arap.dll",
            "GPCUtilities.dll", "GPCGeometry.dll", "DelaunayMesh.dll", "GPCModel.dll", "GPC.Model.Persistence.dll", "GPC.Archive.dll" })
        {
            using var file = File.OpenRead(Path.Combine(plugin, name));
            using var pe = new PEReader(file);
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
        Console.WriteLine("Serialization metadata: no BinaryFormatter or NRBF reader references in Arachne and the GPC set.");
    }
}
