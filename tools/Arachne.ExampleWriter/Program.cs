using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Grasshopper.Kernel;

internal static class Program
{
    private const string Usage = "Usage: ExampleWriter <Arachne root> <Rhino System> <Grasshopper dir> [--tests-only|--examples|--icons] [--femm <FeMM sources>]";

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine(Usage); return 2; }
        var root = Path.GetFullPath(args[0]);
        string mode = args.Skip(3).FirstOrDefault(a => a == "--tests-only" || a == "--examples" || a == "--icons");
        int femmIndex = Array.IndexOf(args, "--femm");
        string femm = femmIndex >= 0 && femmIndex + 1 < args.Length ? Path.GetFullPath(args[femmIndex + 1])
            : Path.GetFullPath(Path.Combine(root, "..", "FeMM 30-5-2024", "Test1", "FeMM"));
        // The tool checks the plug-in built with its own configuration (Debug or Release).
        string configuration = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory).Parent.Name;
        var pluginDir = Path.Combine(root, "bin", "Arachne.Grasshopper", configuration, "net48");
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
        {
            string name = new AssemblyName(request.Name).Name;
            foreach (string dir in new[] { pluginDir, args[1], args[2] })
                foreach (string extension in new[] { ".dll", ".gha" })
                {
                    var path = Path.Combine(dir, name + extension);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
            return null;
        };
        try
        {
            if (mode == "--icons") return Icons.Write(root, Path.Combine(pluginDir, "Arachne.Grasshopper.gha"), "NodeComponent");
            return StartRhino(root, pluginDir, mode, femm);
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhino(string root, string pluginDir, string mode, string femm)
    {
        using (var rhino = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
        {
            using (var modelDocument = Rhino.RhinoDoc.CreateHeadless(null))
            {
                modelDocument.ModelAbsoluteTolerance = 1e-6;
                Rhino.RhinoDoc.ActiveDoc = modelDocument;
                return Run(root, pluginDir, mode, femm);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string root, string pluginDir, string mode, string femm)
    {
        Console.WriteLine("ExampleWriter mode: " + (mode ?? "tests and examples") + "; plug-in: " + pluginDir);
        // Keep this headless validation process independent of installed third-party plugins.
        // This changes only this process, never the user's Grasshopper settings.
        var externalFilter = typeof(GH_ComponentServer).GetMethod("SetExternalGHAs", BindingFlags.Static | BindingFlags.NonPublic);
        if (externalFilter == null) throw new InvalidOperationException("This Grasshopper version cannot isolate validation plugins.");
        externalFilter.Invoke(null, new object[] { Array.Empty<string>() });
        string pluginPath = Path.Combine(pluginDir, "Arachne.Grasshopper.gha");
        // GH exposes this loader internally in the installed version. Only the validation tool uses reflection.
        var loader = typeof(GH_ComponentServer).GetMethod("LoadExternalFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(GH_ExternalFile), typeof(bool) }, null);
        if (loader == null || !(bool)loader.Invoke(Grasshopper.Instances.ComponentServer, new object[] { new GH_ExternalFile(pluginPath), false }))
            throw new InvalidOperationException("Could not register the Arachne plugin for archive verification.");
        GH_Document.EnableSolutions = true;

        if (mode != "--examples")
        {
            int failures = InventoryChecks.Run(root, femm) + FormFindingChecks.Run() + PatterningChecks.Run() + NetworkChecks.Run();
            if (failures > 0) { Console.WriteLine("FAILED checks: " + failures); return 1; }
            Console.WriteLine("All checks passed.");
            if (mode == "--tests-only") return 0;
        }
        FormFindingExample.Write(root);
        PatterningExample.Write(root);
        return 0;
    }
}
