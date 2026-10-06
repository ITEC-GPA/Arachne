using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Text;
using Arachne.Examples;
using Grasshopper.Kernel;

// Component icons: drawn by IconArtwork, embedded by the plug-in from Resources/Icons.
internal static class Icons
{
    /// <summary>
    /// Renders the missing icons, plus <paramref name="redraw"/>, of the plug-in objects; no Rhino runtime needed.
    /// Existing icons are left untouched.
    /// </summary>
    internal static int Write(string root, string pluginPath, params string[] redraw)
    {
        // Outside Rhino, loading RhinoCommon redirects Console: write to the real standard output.
        var log = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        var assembly = Assembly.LoadFrom(pluginPath);
        var names = DocumentObjectTypes(assembly).Select(t => t.Name).Concat(new[] { "Arachne" }).Distinct().OrderBy(n => n).ToList();
        string folder = Path.Combine(root, "src", "Arachne.Grasshopper", "Resources", "Icons");
        int written = 0, missing = 0;
        foreach (string name in names)
        {
            string path = Path.Combine(folder, name + ".png");
            if (File.Exists(path) && !redraw.Contains(name)) continue;
            if (!IconArtwork.Has(name)) { log.WriteLine("ICON MISSING artwork for " + name); missing++; continue; }
            using (var icon = IconArtwork.Get(name))
                icon.Save(path, ImageFormat.Png);
            written++;
            log.WriteLine("ICON written " + name);
        }
        foreach (string file in Directory.GetFiles(folder, "*.png"))
            if (!names.Contains(Path.GetFileNameWithoutExtension(file), StringComparer.OrdinalIgnoreCase))
                log.WriteLine("ICON UNUSED " + Path.GetFileName(file));
        log.WriteLine($"Icons: {names.Count} objects, {written} written, {missing} without artwork.");
        return missing == 0 ? 0 : 1;
    }

    internal static IEnumerable<Type> DocumentObjectTypes(Assembly assembly)
        => assembly.GetTypes().Where(t => t.IsPublic && !t.IsAbstract && typeof(IGH_DocumentObject).IsAssignableFrom(t) && t.GetConstructor(Type.EmptyTypes) != null);

    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string E(string text) => SecurityElement.Escape(text);
    private static void Text(StringBuilder svg, double x, double y, string text, int size = 16) => svg.Append($"<text x='{F(x)}' y='{F(y)}' font-size='{size}'>{E(text)}</text>");

    /// <summary>Icon overview of all components (docs/ICONS.svg), as shown in the Grasshopper panels.</summary>
    internal static void WriteOverview(string root, IReadOnlyList<IGH_DocumentObject> objects)
    {
        var sorted = objects.OrderBy(c => c.SubCategory).ThenBy(c => c.Name).ToArray();
        int rows = (sorted.Length + 3) / 4;
        var svg = new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='1200' height='{100 + rows * 108}' viewBox='0 0 1200 {100 + rows * 108}'><rect width='100%' height='100%' fill='white'/><g font-family='Segoe UI, sans-serif' fill='#28394a'>");
        Text(svg, 25, 42, $"ARACHNE / {sorted.Length} componenti", 26);
        for (int i = 0; i < sorted.Length; i++)
        {
            var c = sorted[i];
            byte[] bytes;
            using (var buffer = new MemoryStream()) { c.Icon_24x24.Save(buffer, ImageFormat.Png); bytes = buffer.ToArray(); }
            int x = 25 + (i % 4) * 300, y = 75 + (i / 4) * 108;
            svg.Append($"<image x='{x}' y='{y}' width='48' height='48' href='data:image/png;base64,{Convert.ToBase64String(bytes)}'/>");
            Text(svg, x, y + 69, c.Name, 14); Text(svg, x, y + 89, c.SubCategory, 12);
        }
        svg.Append("</g></svg>");
        File.WriteAllText(Path.Combine(root, "docs", "ICONS.svg"), svg.ToString(), Encoding.UTF8);
        Console.WriteLine("Icon overview: docs/ICONS.svg, " + sorted.Length + " objects.");
    }
}
