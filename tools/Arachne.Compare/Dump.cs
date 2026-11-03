using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// Text dump of the outputs: every double as its IEEE-754 bits, so that two runs compare bit by bit.
internal sealed class Dump : IDisposable
{
    private readonly StreamWriter writer;
    private string scenario;
    public Dump(string path) { writer = new StreamWriter(path, false, new System.Text.UTF8Encoding(false)); }
    public void Scenario(string name) { scenario = name; writer.WriteLine("S " + name); Console.WriteLine("SCENARIO " + name); }
    public void Number(string key, double value)
        => writer.WriteLine("V " + key + " " + BitConverter.DoubleToInt64Bits(value).ToString("X16") + " " + value.ToString("R", CultureInfo.InvariantCulture));
    public void Text(string key, string value) => writer.WriteLine("T " + key + " " + (value ?? "<null>").Replace("\r", " ").Replace("\n", " | "));
    public void Message(string key, string value) => writer.WriteLine("M " + key + " " + (value ?? "").Replace("\r", " ").Replace("\n", " | "));
    public void Point(string key, Point3d p) { Number(key + ".x", p.X); Number(key + ".y", p.Y); Number(key + ".z", p.Z); }
    public void Vector(string key, Vector3d v) { Number(key + ".x", v.X); Number(key + ".y", v.Y); Number(key + ".z", v.Z); }

    public void Messages(string key, GH_Component component)
    {
        foreach (var level in new[] { GH_RuntimeMessageLevel.Error, GH_RuntimeMessageLevel.Warning, GH_RuntimeMessageLevel.Remark })
            foreach (var message in component.RuntimeMessages(level))
                Message(key + "." + level, message);
    }

    public void Outputs(string key, GH_Component component, params int[] outputs)
    {
        Messages(key, component);
        foreach (int o in outputs.Length == 0 ? Enumerable.Range(0, component.Params.Output.Count).ToArray() : outputs)
        {
            var data = component.Params.Output[o].VolatileData.AllData(true).ToList();
            string prefix = key + "." + component.Params.Output[o].NickName + o;
            Text(prefix + ".count", data.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < data.Count; i++) Goo(prefix + "[" + i + "]", data[i]);
        }
    }

    public void Goo(string key, IGH_Goo goo)
    {
        switch (goo)
        {
            case null: Text(key, "<null>"); return;
            case GH_Number n: Number(key, n.Value); return;
            case GH_Integer i: Text(key, i.Value.ToString(CultureInfo.InvariantCulture)); return;
            case GH_Boolean b: Text(key, b.Value ? "true" : "false"); return;
            case GH_String s: Text(key, s.Value); return;
            case GH_Point p: Point(key, p.Value); return;
            case GH_Vector v: Vector(key, v.Value); return;
            case GH_Line l: Point(key + ".a", l.Value.From); Point(key + ".b", l.Value.To); return;
            case GH_Mesh m: MeshValue(key, m.Value); return;
            case GH_Curve c: Point(key + ".a", c.Value.PointAtStart); Point(key + ".b", c.Value.PointAtEnd); Number(key + ".length", c.Value.GetLength()); return;
        }
        // Plug-in data types: their standard geometry, as far as they cast to it.
        if (goo.CastTo(out GH_Point point)) { Point(key + ".point", point.Value); return; }
        if (goo.CastTo(out GH_Line line)) { Point(key + ".a", line.Value.From); Point(key + ".b", line.Value.To); return; }
        if (goo.CastTo(out GH_Curve curve) && curve.Value != null) { Point(key + ".a", curve.Value.PointAtStart); Point(key + ".b", curve.Value.PointAtEnd); return; }
        if (goo.CastTo(out GH_Mesh mesh) && mesh.Value != null) { MeshValue(key, mesh.Value); return; }
        Text(key + ".type", goo.TypeName);
    }

    private void MeshValue(string key, Mesh mesh)
    {
        Text(key + ".vertices", mesh.Vertices.Count.ToString(CultureInfo.InvariantCulture));
        Text(key + ".faces", mesh.Faces.Count.ToString(CultureInfo.InvariantCulture));
        var points = mesh.Vertices.ToPoint3dArray();
        for (int i = 0; i < points.Length; i++) Point(key + ".v" + i, points[i]);
        for (int i = 0; i < mesh.Faces.Count; i++)
        {
            var f = mesh.Faces[i];
            Text(key + ".f" + i, f.A + "," + f.B + "," + f.C + "," + f.D);
        }
    }

    public void Dispose() => writer.Dispose();
}

internal static class Diff
{
    private sealed class Entry { public string Kind, Key, Value; public long Bits; public double Number; }

    private static Dictionary<string, List<Entry>> Read(string path)
    {
        var result = new Dictionary<string, List<Entry>>(); List<Entry> current = null;
        foreach (var line in File.ReadAllLines(path))
        {
            if (line.StartsWith("S ")) { current = new List<Entry>(); result[line.Substring(2)] = current; continue; }
            if (current == null || line.Length < 3) continue;
            string kind = line.Substring(0, 1), rest = line.Substring(2);
            int space = rest.IndexOf(' ');
            string key = space < 0 ? rest : rest.Substring(0, space), value = space < 0 ? "" : rest.Substring(space + 1);
            var entry = new Entry { Kind = kind, Key = key, Value = value };
            if (kind == "V")
            {
                var parts = value.Split(' ');
                entry.Bits = long.Parse(parts[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                entry.Number = BitConverter.Int64BitsToDouble(entry.Bits);
            }
            current.Add(entry);
        }
        return result;
    }

    public static int Run(string referencePath, string candidatePath)
    {
        var reference = Read(referencePath); var candidate = Read(candidatePath);
        int identical = 0, different = 0, missing = 0;
        foreach (var scenario in reference)
        {
            if (!candidate.TryGetValue(scenario.Key, out var other)) { Console.WriteLine("MISSING scenario " + scenario.Key); missing++; continue; }
            var mine = scenario.Value.Where(e => e.Kind != "M").ToList(); var theirs = other.Where(e => e.Kind != "M").ToDictionary(e => e.Key, e => e);
            int bits = 0, numbers = 0, texts = 0; double worst = 0; string worstKey = null; var textDiffs = new List<string>(); var absent = new List<string>();
            foreach (var e in mine)
            {
                if (!theirs.TryGetValue(e.Key, out var c)) { absent.Add(e.Key); continue; }
                if (e.Kind == "V")
                {
                    numbers++;
                    if (e.Bits == c.Bits) { bits++; continue; }
                    double rel = Math.Abs(e.Number - c.Number) / Math.Max(1e-300, Math.Max(Math.Abs(e.Number), Math.Abs(c.Number)));
                    double abs = Math.Abs(e.Number - c.Number);
                    double measure = Math.Min(rel, abs);
                    if (double.IsNaN(measure)) measure = double.PositiveInfinity;
                    if (measure > worst) { worst = measure; worstKey = e.Key + " " + e.Number.ToString("R") + " vs " + c.Number.ToString("R"); }
                }
                else { texts++; if (e.Value != c.Value) textDiffs.Add(e.Key + ": '" + e.Value + "' vs '" + c.Value + "'"); }
            }
            var extra = theirs.Keys.Except(mine.Select(e => e.Key)).ToList();
            var refMessages = scenario.Value.Where(e => e.Kind == "M").Select(e => e.Key + " " + e.Value).ToList();
            var candMessages = other.Where(e => e.Kind == "M").Select(e => e.Key + " " + e.Value).ToList();
            bool same = bits == numbers && textDiffs.Count == 0 && absent.Count == 0 && extra.Count == 0;
            if (same) identical++; else different++;
            Console.WriteLine((same ? "IDENTICAL " : "DIFFERENT ") + scenario.Key + $": {bits}/{numbers} numbers bit-identical, {texts - textDiffs.Count}/{texts} texts"
                + (worstKey != null ? $"; worst min(abs,rel) difference {worst:E3} at {worstKey}" : ""));
            foreach (var t in textDiffs.Take(10)) Console.WriteLine("   text " + t);
            foreach (var a in absent.Take(10)) Console.WriteLine("   missing " + a);
            foreach (var x in extra.Take(10)) Console.WriteLine("   extra " + x);
            foreach (var m in refMessages.Except(candMessages)) Console.WriteLine("   message only in reference: " + m);
            foreach (var m in candMessages.Except(refMessages)) Console.WriteLine("   message only in candidate: " + m);
        }
        foreach (var name in candidate.Keys.Except(reference.Keys)) Console.WriteLine("EXTRA scenario " + name);
        Console.WriteLine($"Scenarios: {identical} identical, {different} different, {missing} missing.");
        return different + missing == 0 ? 0 : 1;
    }
}
