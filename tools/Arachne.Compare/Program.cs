using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Grasshopper.Kernel;

// Arachne.Compare run <plug-in folder> <Rhino System> <Grasshopper dir> <dump file> [--io <work folder> <io file>]
// Arachne.Compare diff <reference dump> <candidate dump>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 3 && args[0] == "diff") return Diff.Run(args[1], args[2]);
        if (args.Length < 5 || args[0] != "run")
        {
            Console.Error.WriteLine("Usage: Arachne.Compare run <plug-in folder> <Rhino System> <Grasshopper dir> <dump> [--io <work folder> <io file>] | diff <reference> <candidate>");
            return 2;
        }
        string plugin = Path.GetFullPath(args[1]);
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
        {
            string name = new AssemblyName(request.Name).Name;
            foreach (string dir in new[] { plugin, args[2], args[3] })
                foreach (string extension in new[] { ".dll", ".gha" })
                {
                    var path = Path.Combine(dir, name + extension);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
            return null;
        };
        try { return StartRhino(plugin, args); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhino(string plugin, string[] args)
    {
        using (var rhino = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
        using (var modelDocument = Rhino.RhinoDoc.CreateHeadless(null))
        {
            modelDocument.ModelAbsoluteTolerance = 1e-6;
            Rhino.RhinoDoc.ActiveDoc = modelDocument;
            return Run(plugin, args);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string plugin, string[] args)
    {
        var externalFilter = typeof(GH_ComponentServer).GetMethod("SetExternalGHAs", BindingFlags.Static | BindingFlags.NonPublic);
        if (externalFilter == null) throw new InvalidOperationException("This Grasshopper version cannot isolate validation plugins.");
        externalFilter.Invoke(null, new object[] { Array.Empty<string>() });
        string gha = Path.Combine(plugin, "Arachne.Grasshopper.gha");
        var loader = typeof(GH_ComponentServer).GetMethod("LoadExternalFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(GH_ExternalFile), typeof(bool) }, null);
        if (loader == null || !(bool)loader.Invoke(Grasshopper.Instances.ComponentServer, new object[] { new GH_ExternalFile(gha), false }))
            throw new InvalidOperationException("Could not register the plug-in " + gha);
        GH_Document.EnableSolutions = true;
        Console.WriteLine("Arachne.Compare: " + gha + " " + System.Diagnostics.FileVersionInfo.GetVersionInfo(gha).FileVersion);
        using (var dump = new Dump(args[4]))
            Scenarios.Run(dump);
        int io = Array.IndexOf(args, "--io");
        if (io >= 0 && io + 2 < args.Length) Persistence.Run(Path.GetFullPath(args[io + 1]), Path.GetFullPath(args[io + 2]));
        return 0;
    }
}
