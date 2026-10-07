using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Arachne.Grasshopper.Components.Analysis;
using Grasshopper.Kernel;

// Component inventory: identity, icons, and inputs/outputs compared with the FeMM sources.
internal static class InventoryChecks
{
    private static int passed, failed;
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("INVENTORY PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("INVENTORY FAIL " + name + ": " + e.GetBaseException().Message); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    internal static List<IGH_DocumentObject> Objects()
        => Icons.DocumentObjectTypes(typeof(FormFindComponent).Assembly).Select(t => (IGH_DocumentObject)Activator.CreateInstance(t)).ToList();

    internal static int Run(string root, string femm)
    {
        passed = failed = 0;
        var objects = Objects();
        bool femmAvailable = Directory.Exists(femm);
        var femmFiles = femmAvailable
            ? Directory.GetFiles(Path.Combine(femm, "FeMM.Grasshopper"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\")).ToList()
            : new List<string>();

        Test($"{objects.Count} components and parameters with distinct GUIDs", () =>
        {
            Check(objects.Select(o => o.ComponentGuid).Distinct().Count() == objects.Count, "Duplicate GUIDs");
            Check(objects.All(o => o.Category == "Arachne"), "Objects outside the Arachne tab");
        });

        if (femmAvailable)
            Test("No GUID is shared with FeMM", () =>
            {
                var femmGuids = new HashSet<Guid>(femmFiles.SelectMany(f => Regex.Matches(File.ReadAllText(f), @"ComponentGuid\s*=>\s*new\s+Guid\(""([0-9A-Fa-f\-]{36})""\)").Cast<Match>())
                    .Select(m => Guid.Parse(m.Groups[1].Value)));
                var shared = objects.Where(o => femmGuids.Contains(o.ComponentGuid)).Select(o => o.Name).ToList();
                Check(shared.Count == 0, "FeMM GUIDs: " + string.Join(", ", shared));
            });

        Test("Every object has its own 24 px icon", () =>
        {
            var hashes = new HashSet<string>();
            foreach (var o in objects)
            {
                var icon = o.Icon_24x24;
                Check(icon != null && icon.Width == 24 && icon.Height == 24, "Invalid icon: " + o.Name);
                using (var buffer = new MemoryStream())
                using (var sha = SHA256.Create())
                {
                    icon.Save(buffer, System.Drawing.Imaging.ImageFormat.Png);
                    Check(hashes.Add(Convert.ToBase64String(sha.ComputeHash(buffer.ToArray()))), "Duplicate icon: " + o.Name);
                }
            }
        });

        if (femmAvailable)
        {
            foreach (var component in objects.OfType<GH_Component>().OrderBy(c => c.GetType().Name))
            {
                string source = femmFiles.FirstOrDefault(f => Path.GetFileNameWithoutExtension(f) == component.GetType().Name);
                if (source == null) { Console.WriteLine("INVENTORY NOTE no FeMM source for " + component.GetType().Name); continue; }
                Test("Same inputs and outputs as FeMM: " + component.Name, () => CompareParameters(component, File.ReadAllText(source)));
            }
        }
        else
            Console.WriteLine("INVENTORY NOTE FeMM sources not found, input/output comparison skipped: " + femm);

        Console.WriteLine("Inventory checks: " + passed + " passed, " + failed + " failed.");
        return failed;
    }

    private sealed class Declared
    {
        public string Kind, Name, NickName, Access;
        public override string ToString() => $"{Kind} '{Name}' ({NickName}, {Access})";
    }

    // FeMM inputs removed on purpose: the SAP model definitions (load combinations, named sets,
    // staged constructions, the gravity on the non-structural mass) and the distributed loads
    // that the form finding does not handle.
    private static readonly Dictionary<string, string[]> RemovedInputs = new Dictionary<string, string[]>
    {
        { "BuildModelComponent", new[] { "Load Combinations", "Named sets", "Staged constructions" } },
        { "BeamDistributedLoadComponent", new[] { "P1", "P2" } },
        { "LoadCaseComponent", new[] { "Gravity NSM" } },
    };

    // FeMM components whose inputs were reworked on purpose: the inputs are the ones listed here,
    // the outputs are still compared with FeMM. Plate Property has one thickness and no SAP shell
    // type; the options of the Form Find are given by the Solver Options component.
    private static readonly Dictionary<string, string[]> ReworkedInputs = new Dictionary<string, string[]>
    {
        { "PlatePropertyComponent", new[] { "Name", "Thickness", "Material" } },
        { "FormFindComponent", new[] { "Model", "Solver Type", "Options", "Plane", "Auto Run" } },
    };

    private static void CompareParameters(GH_Component component, string source)
    {
        source = Regex.Replace(source, @"/\*.*?\*/", "", RegexOptions.Singleline);
        source = Regex.Replace(source, @"//[^\r\n]*", "");
        var inputs = Parse(Body(source, "RegisterInputParams"));
        var outputs = Parse(Body(source, "RegisterOutputParams"));
        if (RemovedInputs.TryGetValue(component.GetType().Name, out string[] removed))
        {
            Check(removed.All(name => inputs.Any(d => d.Name == name)), "A removed input is not in the FeMM source");
            inputs = inputs.Where(d => !removed.Contains(d.Name)).ToList();
        }
        if (ReworkedInputs.TryGetValue(component.GetType().Name, out string[] reworked))
            Check(component.Params.Input.Select(p => p.Name).SequenceEqual(reworked), "inputs " + string.Join(", ", component.Params.Input.Select(p => p.Name)));
        else
            Compare("input", inputs, component.Params.Input);
        Compare("output", outputs, component.Params.Output);
    }

    private static void Compare(string side, List<Declared> declared, List<IGH_Param> actual)
    {
        Check(declared.Count == actual.Count, $"{side} count {actual.Count} instead of {declared.Count}: " + string.Join(", ", actual.Select(p => p.Name)));
        for (int i = 0; i < declared.Count; i++)
        {
            var d = declared[i]; var p = actual[i];
            if (d.Name != null) Check(d.Name == p.Name, $"{side} {i}: name '{p.Name}' instead of '{d.Name}'");
            if (d.NickName != null) Check(d.NickName == p.NickName, $"{side} {i}: nickname '{p.NickName}' instead of '{d.NickName}'");
            if (d.Access != null) Check(d.Access == p.Access.ToString(), $"{side} {i}: access {p.Access} instead of {d.Access}");
            if (d.Kind != null) Check(ParamType(d.Kind) == p.GetType().Name, $"{side} {i}: type {p.GetType().Name} instead of {ParamType(d.Kind)}");
        }
    }

    private static string ParamType(string kind)
    {
        switch (kind)
        {
            case "Generic": return "Param_GenericObject";
            case "Text": return "Param_String";
            case "Angle": return "Param_Number";
            default: return "Param_" + kind;
        }
    }

    private static string Body(string source, string method)
    {
        int start = source.IndexOf("void " + method + "(", StringComparison.Ordinal);
        if (start < 0) throw new Exception(method + " not found in the FeMM source");
        int open = source.IndexOf('{', start), depth = 0;
        for (int i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
        }
        throw new Exception(method + " body not closed");
    }

    private static List<Declared> Parse(string body)
    {
        var result = new List<Declared>();
        foreach (Match m in Regex.Matches(body, @"pManager\.Add(\w*)Parameter\s*\("))
        {
            string args = Arguments(body, m.Index + m.Length);
            var parts = Split(args);
            var d = new Declared();
            int nameIndex = 0;
            if (m.Groups[1].Value == "") { d.Kind = null; nameIndex = 1; } // AddParameter(customParam, name, nick, ...)
            else d.Kind = m.Groups[1].Value;
            d.Name = Literal(parts[nameIndex]);
            d.NickName = Literal(parts[nameIndex + 1]);
            var access = Regex.Match(args, @"GH_ParamAccess\.(\w+)");
            d.Access = access.Success ? access.Groups[1].Value : null;
            result.Add(d);
        }
        return result;
    }

    private static string Arguments(string text, int start)
    {
        int depth = 1;
        for (int i = start; i < text.Length; i++)
        {
            if (text[i] == '(') depth++;
            else if (text[i] == ')' && --depth == 0) return text.Substring(start, i - start);
            else if (text[i] == '"') i = text.IndexOf('"', i + 1);
        }
        throw new Exception("Unbalanced parameter declaration");
    }

    private static List<string> Split(string args)
    {
        var parts = new List<string>(); int depth = 0, last = 0;
        for (int i = 0; i < args.Length; i++)
        {
            char c = args[i];
            if (c == '"') { i = args.IndexOf('"', i + 1); continue; }
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0) { parts.Add(args.Substring(last, i - last).Trim()); last = i + 1; }
        }
        parts.Add(args.Substring(last).Trim());
        return parts;
    }

    // Literal strings only; expressions (e.g. char.ConvertFromUtf32) are not compared.
    private static string Literal(string part)
        => part.Length >= 2 && part[0] == '"' && part[part.Length - 1] == '"' ? Regex.Unescape(part.Substring(1, part.Length - 2)) : null;
}
