using System;
using System.IO;
using System.Linq;
using System.Reflection;

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
            if (args.Contains("--disable-binary-formatter"))
            {
                AppContext.SetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", false);
                args = args.Where(a => a != "--disable-binary-formatter").ToArray();
            }
            var runner = Assembly.LoadFrom(Path.GetFullPath(args[0]));
            return (int)runner.EntryPoint.Invoke(null, new object[] { args.Skip(1).ToArray() });
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error.GetBaseException());
            return 1;
        }
    }
}
