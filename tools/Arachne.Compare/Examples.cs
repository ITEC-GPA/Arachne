using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

// The distributed examples as a user meets them, on any Arachne build (release check F8):
// 1. open: every .gh/.ghx of the folder is opened as saved (by any Arachne version), its objects, wires and internalized data are
//    dumped, the definition is solved and every component output is dumped, then the form-found models are probed with
//    Beam Forces, Support Reactions and Decompose Model (added only in memory, removed again before saving);
// 2. resaved: the solved document is saved by the plug-in under test (as with Ctrl+S, sidecars included) into the work folder,
//    reopened in a new document and solved again, with the same dump;
// 3. restored: the resaved document reopened with every Auto Run toggle off, so that Form Find restores the saved run instead
//    of solving it again.
// The three dumps have the same scenario names (the file names), so that `diff` compares them with each other and with the
// dumps of another build. Plug-in independent: components are known by their GUIDs only.
internal static class Examples
{
    internal static void Run(string folder, string work, string dumpPath)
    {
        Directory.CreateDirectory(work);
        var files = Directory.GetFiles(folder).Where(f => f.EndsWith(".gh", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".ghx", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => Path.GetFileName(f), StringComparer.Ordinal).ToList();
        var resavedPaths = new Dictionary<string, string>();
        using (var open = new Dump(dumpPath))
            foreach (string file in files)
            {
                string name = Path.GetFileName(file);
                open.Scenario(name);
                try
                {
                    // One folder per file (the .gh and the .ghx of an example share their sidecar names); never overwritten
                    string own = Path.Combine(work, name.Replace('.', '_'));
                    if (Directory.Exists(own) && Directory.EnumerateFileSystemEntries(own).Any()) throw new IOException("Work folder not empty: " + own);
                    Directory.CreateDirectory(own);
                    string target = Path.Combine(own, name);
                    Visit(open, file, false, document =>
                    {
                        if (!new GH_DocumentIO(document).SaveQuiet(target)) throw new IOException("Cannot save " + target);
                        resavedPaths[name] = target;
                        foreach (string written in Directory.GetFiles(own).OrderBy(f => f, StringComparer.Ordinal))
                        {
                            // a message: master writes no sidecar, F6 one per Form Find run
                            open.Message("saved." + (written == target ? "document" : "sidecar"), Path.GetFileName(written));
                            Console.WriteLine("  SAVED " + Path.GetFileName(written) + " " + new FileInfo(written).Length + " B");
                        }
                    });
                }
                catch (Exception e) { open.Text("exception", e.GetType().Name + ": " + e.GetBaseException().Message); Console.WriteLine("  EXCEPTION " + e); }
            }
        foreach (var pass in new[] { new { Suffix = ".resaved", AutoRunOff = false }, new { Suffix = ".restored", AutoRunOff = true } })
            using (var dump = new Dump(dumpPath + pass.Suffix))
                foreach (string file in files)
                {
                    string name = Path.GetFileName(file);
                    dump.Scenario(name);
                    try
                    {
                        if (!resavedPaths.TryGetValue(name, out string resaved)) throw new IOException("Not resaved: " + name);
                        Visit(dump, resaved, pass.AutoRunOff, null);
                    }
                    catch (Exception e) { dump.Text("exception", e.GetType().Name + ": " + e.GetBaseException().Message); Console.WriteLine("  EXCEPTION " + e); }
                }
    }

    // Opens, dumps the definition, solves, dumps the outputs and the probes, then hands the document (without the probes) on.
    private static void Visit(Dump dump, string path, bool autoRunOff, Action<GH_Document> then)
    {
        ReadMessages(dump, path);
        var io = new GH_DocumentIO();
        if (!io.Open(path) || io.Document == null) throw new IOException("Cannot open " + path);
        var document = io.Document;
        try
        {
            Definition(dump, document);
            if (autoRunOff)
                foreach (var toggle in document.Objects.OfType<GH_BooleanToggle>().Where(t => t.NickName == "Auto Run")) toggle.Value = false;
            document.Enabled = true;
            document.NewSolution(false);
            var components = document.Objects.OfType<GH_Component>().OrderBy(c => c.InstanceGuid).ToList();
            foreach (var component in components)
            {
                dump.Message("c." + Key(component) + ".message", component.Message ?? "");
                dump.Outputs("c." + Key(component), component);
            }
            // Probes on every Form Find, solved alone (nothing else is expired), then removed
            var probes = new List<GH_Component>();
            foreach (var ff in components.Where(c => c.ComponentGuid == new Guid(Id.FormFind)))
                foreach (var probe in new[] { Id.BeamForces, Id.SupportReactions, Id.Decompose })
                {
                    var p = Grasshopper.Instances.ComponentServer.EmitObject(new Guid(probe)) as GH_Component ?? throw new InvalidOperationException("Missing component " + probe);
                    p.CreateAttributes(); document.AddObject(p, false); p.Params.Input[0].AddSource(ff.Params.Output[0]);
                    probes.Add(p);
                    dump.Text("p." + Key(ff) + "." + p.Name + ".added", "true");
                }
            if (probes.Count > 0)
            {
                document.NewSolution(false);
                foreach (var p in probes) dump.Outputs("p." + Key(p.Params.Input[0].Sources[0].Attributes.GetTopLevel.DocObject) + "." + p.Name, p);
                foreach (var p in probes) { p.Params.Input[0].RemoveAllSources(); document.RemoveObject(p, false); }
            }
            then?.Invoke(document);
        }
        finally { document.Enabled = false; document.Dispose(); }
    }

    // The messages of reading the archive (a refused legacy payload is an error here) and its object count.
    private static void ReadMessages(Dump dump, string path)
    {
        var archive = new GH_Archive();
        if (!archive.ReadFromFile(path)) throw new IOException("Cannot read " + path);
        using (var scratch = new GH_Document())
        {
            bool extracted = archive.ExtractObject(scratch, "Definition");
            dump.Text("read.extracted", extracted ? "true" : "false");
            dump.Text("read.objects", scratch.ObjectCount.ToString(CultureInfo.InvariantCulture));
        }
        var objects = archive.GetRootNode.FindChunk("Definition")?.FindChunk("DefinitionObjects");
        int declared = -1;
        if (objects != null) objects.TryGetInt32("ObjectCount", ref declared);
        dump.Text("read.declared", declared.ToString(CultureInfo.InvariantCulture));
        int errors = 0, warnings = 0;
        foreach (var message in archive.Messages)
        {
            if (message.Type == GH_Message_Type.error) errors++;
            if (message.Type == GH_Message_Type.warning) warnings++;
            dump.Message("read." + message.Type, message.Message);
            Console.WriteLine("  READ " + message.Type + ": " + message.Message);
        }
        dump.Text("read.errors", errors.ToString(CultureInfo.InvariantCulture));
        dump.Text("read.warnings", warnings.ToString(CultureInfo.InvariantCulture));
    }

    // What survives the opening: objects, wires and internalized data of every parameter.
    private static void Definition(Dump dump, GH_Document document)
    {
        var objects = document.Objects.OrderBy(o => o.InstanceGuid).ToList();
        dump.Text("objects", objects.Count.ToString(CultureInfo.InvariantCulture));
        int wires = 0;
        foreach (var obj in objects)
        {
            string key = "o." + Key(obj);
            dump.Text(key + ".type", obj.ComponentGuid + " " + obj.Name);
            switch (obj)
            {
                case GH_Component component:
                    for (int i = 0; i < component.Params.Input.Count; i++) wires += Parameter(dump, key + ".in" + i, component.Params.Input[i]);
                    dump.Text(key + ".outputs", component.Params.Output.Count.ToString(CultureInfo.InvariantCulture));
                    break;
                case GH_BooleanToggle toggle:
                    dump.Text(key + ".value", toggle.Value ? "true" : "false");
                    break;
                case GH_Panel panel:
                    wires += Parameter(dump, key, panel);
                    dump.Text(key + ".text", panel.UserText);
                    break;
                case IGH_Param param:
                    wires += Parameter(dump, key, param);
                    break;
            }
        }
        dump.Text("wires", wires.ToString(CultureInfo.InvariantCulture));
    }

    private static int Parameter(Dump dump, string key, IGH_Param param)
    {
        dump.Text(key + ".name", param.NickName + " " + param.TypeName);
        dump.Text(key + ".sources", string.Join(",", param.Sources.Select(s => Key(s.Attributes.GetTopLevel.DocObject) + "/" + s.NickName)));
        dump.Text(key + ".mapping", param.DataMapping.ToString());
        // PersistentData of GH_PersistentParam<T>, whatever T
        if (param.GetType().GetProperty("PersistentData")?.GetValue(param, null) is IGH_Structure data)
        {
            var items = data.AllData(false).ToList();
            dump.Text(key + ".data.paths", string.Join(";", data.Paths.Select(p => p.ToString())));
            dump.Text(key + ".data.count", items.Count.ToString(CultureInfo.InvariantCulture));
            for (int k = 0; k < items.Count; k++)
            {
                dump.Text(key + ".data[" + k + "].kind", items[k]?.TypeName ?? "<null>");
                dump.Goo(key + ".data[" + k + "]", items[k]);
            }
        }
        return param.SourceCount;
    }

    private static string Key(IGH_DocumentObject obj) => obj.InstanceGuid.ToString("N").Substring(0, 8);
}
