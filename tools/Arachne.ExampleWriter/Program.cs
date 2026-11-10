using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Grasshopper.Kernel;

internal static class Program
{
    private const string Usage = "Usage: ExampleWriter <Arachne root> <Rhino System> <Grasshopper dir> [--tests-only|--examples|--icons] [--femm <FeMM sources>] [--require-femm] [--report <json>]";
    private static readonly CheckReport Report = new CheckReport();

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length < 3) { Console.Error.WriteLine(Usage); return 2; }
        foreach (string option in new[] { "--femm", "--report" })
        {
            int index = Array.IndexOf(args, option);
            if (index >= 0 && (index + 1 == args.Length || args[index + 1].StartsWith("--")))
            { Console.Error.WriteLine("Missing value for " + option); return 2; }
        }
        var root = Path.GetFullPath(args[0]);
        string mode = args.Skip(3).FirstOrDefault(a => a == "--tests-only" || a == "--examples" || a == "--icons" || a == "--migration-scale" || a == "--f6-contracts");
        int femmIndex = Array.IndexOf(args, "--femm");
        string femm = femmIndex >= 0 && femmIndex + 1 < args.Length ? Path.GetFullPath(args[femmIndex + 1])
            : Path.GetFullPath(Path.Combine(root, "..", "FeMM 30-5-2024", "Test1", "FeMM"));
        Report.FemmSources = femm;
        Report.RequireFemm = args.Contains("--require-femm");
        int reportIndex = Array.IndexOf(args, "--report");
        string reportPath = reportIndex < 0 ? null : args[reportIndex + 1];
        // The tool checks the plug-in built next to it, with the same configuration:
        // <bin>\Arachne.ExampleWriter\<configuration>\net48 -> <bin>\Arachne.Grasshopper\<configuration>\net48
        var toolDir = new DirectoryInfo(Path.GetDirectoryName(typeof(Program).Assembly.Location));
        string configuration = toolDir.Parent.Name;
        var pluginDir = Path.Combine(toolDir.Parent.Parent.Parent.FullName, "Arachne.Grasshopper", configuration, "net48");
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
            if (Report.RequireFemm && !Directory.Exists(Path.Combine(femm, "FeMM.Grasshopper")))
            { Report.FemmComparison = "Failed"; throw new DirectoryNotFoundException("Required FeMM reference sources missing: " + femm); }
            if (mode == "--icons") return Icons.Write(root, Path.Combine(pluginDir, "Arachne.Grasshopper.gha"), "NodeComponent");
            int result = StartRhino(root, pluginDir, mode, femm);
            Report.Status = result == 0 ? (Report.Suites.Any(s => s.Skipped > 0) ? "PassedWithSkips" : "Passed") : "Failed";
            return result;
        }
        catch (Exception error) { Report.Error = error.ToString(); Console.Error.WriteLine(error); return 1; }
        finally { if (reportPath != null) Report.Save(reportPath); }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhino(string root, string pluginDir, string mode, string femm)
    {
        using (var rhino = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
        {
            WaitForRhinoLicense();
            using (var modelDocument = Rhino.RhinoDoc.CreateHeadless(null))
            {
                modelDocument.ModelAbsoluteTolerance = 1e-6;
                Rhino.RhinoDoc.ActiveDoc = modelDocument;
                return Run(root, pluginDir, mode, femm);
            }
        }
    }

    // The core can return before its asynchronous license initialization has
    // completed. Retry only this startup probe, never a failed numerical test.
    private static void WaitForRhinoLicense()
    {
        var elapsed = Stopwatch.StartNew();
        bool announced = false;
        while (true)
        {
            try
            {
                using (var line = new Rhino.Geometry.LineCurve(Rhino.Geometry.Point3d.Origin, new Rhino.Geometry.Point3d(1, 0, 0)))
                    if (Math.Abs(line.GetLength() - 1) > 1e-12) throw new InvalidOperationException("Rhino geometry startup probe failed.");
                return;
            }
            catch (Rhino.Runtime.NotLicensedException)
            {
                if (elapsed.Elapsed.TotalSeconds >= 30)
                    throw new InvalidOperationException("Rhino license unavailable after 30 seconds; numerical checks were not started.");
                if (!announced) { Console.WriteLine("Waiting for Rhino license initialization (maximum 30 seconds)."); announced = true; }
                Rhino.RhinoApp.Wait();
                Thread.Sleep(100);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Run(string root, string pluginDir, string mode, string femm)
    {
        Report.BinaryFormatterSwitch = AppContext.TryGetSwitch("System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization", out bool enabled) ? (bool?)enabled : null;
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
        if (mode == "--migration-scale") return MigrationScale.Run();
        if (mode == "--f6-contracts") return F6ContractsChecks.Run();

        if (mode != "--examples")
        {
            int failures = InventoryChecks.Run(root, femm, Report.RequireFemm);
            Report.Suites.Add(InventoryChecks.Summary);
            Report.FemmComparison = InventoryChecks.FemmStatus;
            failures += F6ContractsChecks.Run(); Report.Suites.Add(F6ContractsChecks.Summary);
            failures += FormFindingChecks.Run(); Report.Suites.Add(FormFindingChecks.Summary);
            failures += PatterningChecks.Run(); Report.Suites.Add(PatterningChecks.Summary);
            failures += NetworkChecks.Run(); Report.Suites.Add(NetworkChecks.Summary);
            var archives = ExampleArchiveChecks.Run(root); Report.Suites.Add(archives); failures += archives.Failed;
            if (failures > 0) { Console.WriteLine("FAILED checks: " + failures); return 1; }
            Console.WriteLine(Report.Suites.Any(s => s.Skipped > 0) ? "Executed checks passed; reference comparisons were skipped (see report)." : "All checks passed.");
            if (mode == "--tests-only") return 0;
        }
        FormFindingExample.Write(root);
        PatterningExample.Write(root);
        return 0;
    }
}
