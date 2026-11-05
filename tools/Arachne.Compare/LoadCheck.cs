using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Grasshopper.Kernel;

// The load-time check of the plug-in (Gpc.PluginChecks.PluginBuildCheck, shared by the gPC plug-ins) with other copies of the
// shared libraries loaded first, as another plug-in of the same Rhino would (release check F8: a candidate GPC set).
// Plug-in independent: the check is found by its type name in the loaded plug-in.
internal static class LoadCheck
{
    // Rhino binds a plug-in's reference to the copy of that assembly already loaded, whatever its version or folder
    internal static Assembly LoadedByName(string name)
        => AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic && string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

    internal static void Preload(string[] files)
    {
        foreach (string file in files)
        {
            var assembly = Assembly.LoadFrom(Path.GetFullPath(file));
            Console.WriteLine("PRELOADED " + assembly.GetName().Name + " " + assembly.GetName().Version + " " + assembly.Location);
        }
    }

    internal static void Report(string gha, string reportPath)
    {
        var plugin = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => !a.IsDynamic && a.GetName().Name == "Arachne.Grasshopper")
            ?? throw new InvalidOperationException("Plug-in not loaded");
        var check = plugin.GetType("Gpc.PluginChecks.PluginBuildCheck") ?? throw new InvalidOperationException("No PluginBuildCheck in " + gha);
        // Grasshopper runs the GH_AssemblyPriority of a plug-in it loads; Start is idempotent, so it is called again to be sure
        foreach (var type in plugin.GetTypes().Where(t => typeof(GH_AssemblyPriority).IsAssignableFrom(t) && !t.IsAbstract))
            ((GH_AssemblyPriority)Activator.CreateInstance(type)).PriorityLoad();
        string warning = (string)check.GetProperty("Warning", BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
        using (var report = new StreamWriter(reportPath, false, new System.Text.UTF8Encoding(false)))
        {
            report.WriteLine("plugin " + gha + " " + System.Diagnostics.FileVersionInfo.GetVersionInfo(gha).ProductVersion);
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).OrderBy(a => a.GetName().Name, StringComparer.Ordinal))
            {
                string name = a.GetName().Name;
                if (!(name.StartsWith("GPC", StringComparison.Ordinal) || name == "DelaunayMesh" || name == "TensileLib")) continue;
                string location = a.Location;
                report.WriteLine("loaded " + name + " " + a.GetName().Version + " " + Sha(location).Substring(0, 16) + " " + location);
            }
            report.WriteLine("warning " + (warning ?? "<none>"));
        }
        Console.WriteLine("WARNING " + (warning ?? "<none>"));
    }

    private static string Sha(string path)
    {
        using (var sha = SHA256.Create())
        using (var stream = File.OpenRead(path))
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
    }
}
