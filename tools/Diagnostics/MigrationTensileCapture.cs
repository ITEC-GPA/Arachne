using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using TensileLib.Geometry;

// Diagnostic-only reflection dump of the actual solver graph. Field names and object references
// are retained, without invoking properties or serializing Rhino native handles. All floating
// point fields are hexadecimal IEEE-754, including signed zero and NaN. This is not a file codec.
internal static class MigrationTensileCapture
{
    private static int sequence;
    internal static int Input(Model model, ModelModel.ModelUnits units, int solver, SolverOptionsModel options)
    {
        int id = Interlocked.Increment(ref sequence);
        Write(id, "input", new { units, solver, options, model });
        return id;
    }

    internal static void Result(int id, Model model, bool converged, string status)
        => Write(id, "result", new { converged, status, model,
            axialForces = model.Beams.Select(b => new { b.Id, b.AxialForce }).ToArray(),
            reactions = converged ? FormFindingHelper.ComputeReactions(model) : null });

    private static void Write(int id, string stage, object value)
    {
        string folder = Environment.GetEnvironmentVariable("ARACHNE_MIGRATION_CAPTURE");
        if (string.IsNullOrWhiteSpace(folder)) return;
        Directory.CreateDirectory(folder);
        using (var file = new FileStream(Path.Combine(folder, id.ToString("D5") + "-" + stage + ".xml.gz"), FileMode.CreateNew))
        using (var compressed = new GZipStream(file, CompressionLevel.Optimal))
        using (var xml = XmlWriter.Create(compressed, new XmlWriterSettings { Indent = false }))
        {
            xml.WriteStartElement("capture");
            xml.WriteAttributeString("case", Environment.GetEnvironmentVariable("ARACHNE_CAPTURE_CASE") ?? "unlabelled");
            xml.WriteAttributeString("solverAssembly", typeof(Model).Assembly.FullName);
            new Graph(xml).Value(value);
            xml.WriteEndElement();
        }
    }

    private sealed class Identity : IEqualityComparer<object>
    {
        public new bool Equals(object a, object b) => ReferenceEquals(a, b);
        public int GetHashCode(object value) => RuntimeHelpers.GetHashCode(value);
    }

    private sealed class Graph
    {
        private readonly XmlWriter xml;
        private readonly Dictionary<object, int> seen = new Dictionary<object, int>(new Identity());
        internal Graph(XmlWriter xml) { this.xml = xml; }
        internal void Value(object value)
        {
            xml.WriteStartElement("value");
            if (value == null) { xml.WriteAttributeString("null", "true"); xml.WriteEndElement(); return; }
            Type type = value.GetType();
            xml.WriteAttributeString("type", type.FullName);
            if (value is double d) xml.WriteAttributeString("bits", unchecked((ulong)BitConverter.DoubleToInt64Bits(d)).ToString("X16"));
            else if (value is float f) xml.WriteAttributeString("bits", BitConverter.ToUInt32(BitConverter.GetBytes(f), 0).ToString("X8"));
            else if (type.IsPrimitive || type.IsEnum || value is string || value is Guid || value is decimal)
                xml.WriteString(Convert.ToString(value, CultureInfo.InvariantCulture));
            else
            {
                if (value is Delegate || value is IntPtr || value is UIntPtr) throw new InvalidDataException("Unsupported capture field: " + type.FullName);
                if (!type.IsValueType)
                {
                    if (seen.TryGetValue(value, out int previous))
                    { xml.WriteAttributeString("ref", previous.ToString()); xml.WriteEndElement(); return; }
                    seen.Add(value, seen.Count + 1); xml.WriteAttributeString("id", seen.Count.ToString());
                }
                if (value is IEnumerable items && (type.IsArray || type.Namespace == "System.Collections.Generic"))
                    foreach (object item in items) Value(item);
                else
                    for (Type level = type; level != null && level != typeof(object); level = level.BaseType)
                        foreach (var field in level.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).OrderBy(fld => fld.Name, StringComparer.Ordinal))
                        {
                            xml.WriteStartElement("field"); xml.WriteAttributeString("name", level.FullName + "." + field.Name);
                            Value(field.GetValue(value)); xml.WriteEndElement();
                        }
            }
            xml.WriteEndElement();
        }
    }
}
