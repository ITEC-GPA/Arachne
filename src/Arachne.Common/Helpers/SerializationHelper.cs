using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Xml;

namespace Arachne.Common.Helpers
{
    /// <summary>Versioned compatibility-wrapper archive. No document may select an assembly to load.</summary>
    public static class SerializationHelper
    {
        private static readonly byte[] Header = Encoding.ASCII.GetBytes("ARCGPC2");
        private const int MaximumBytes = 256 * 1024 * 1024;
        public static T ReadOptional<T>(SerializationInfo info, string name, T fallback)
        {
            foreach (SerializationEntry entry in info) if (entry.Name == name) return (T)entry.Value;
            return fallback;
        }
        public static int ReadVersion(SerializationInfo info, string name, int current)
        {
            int version = ReadOptional(info, name, 0);
            if (version < 0 || version > current) throw new SerializationException("UnsupportedArachneSchema: " + name + "=" + version);
            return version;
        }
        public static double? ReadNullableDouble(SerializationInfo info, string name) => (double?)info.GetValue(name, typeof(double?));
        public static void WriteNullableDouble(SerializationInfo info, string name, double? value) => info.AddValue(name, value, typeof(double?));
        private static DataContractSerializer Serializer() => new DataContractSerializer(typeof(object), new DataContractSerializerSettings
        {
            PreserveObjectReferences = true, MaxItemsInObjectGraph = 10000000, DataContractResolver = new WrapperResolver()
        });
        public static byte[] ToBytes(object value)
        {
            using (var stream = new MemoryStream())
            {
                stream.Write(Header, 0, Header.Length);
                using (var writer = XmlDictionaryWriter.CreateBinaryWriter(stream, null, null, false)) GPC.Model.Core.ContractXml.Write(Serializer(), writer, value);
                if (stream.Length > MaximumBytes) throw new SerializationException("ArachneArchiveTooLarge");
                return stream.ToArray();
            }
        }
        public static T FromBytes<T>(byte[] data)
        {
            if (data == null || data.Length > MaximumBytes) throw new SerializationException("InvalidArachneArchiveSize");
            if (data.Length < Header.Length || !data.Take(Header.Length).SequenceEqual(Header))
            {
                if (LegacyNrbfArchive.HasHeader(data))
                {
                    var legacy = LegacyNrbfArchive.Read<T>(data);
                    if (legacy is Models.ModelModel legacyModel)
                        using (var sha = System.Security.Cryptography.SHA256.Create())
                        {
                            legacyModel.InputOwner = new Guid(sha.ComputeHash(data).Take(16).ToArray());
                            for (int i = 0; i < legacyModel.Nodes.Count; i++)
                                legacyModel.Nodes[i].BindInputIdentity(Analysis.FormFindingInputBuilder.Identity(legacyModel.InputOwner, "node:" + i));
                        }
                    return legacy;
                }
                throw new SerializationException("UnsupportedArachneArchive: expected ARCGPC2 or the closed legacy NRBF vocabulary.");
            }
            using (var reader = XmlDictionaryReader.CreateBinaryReader(data, Header.Length, data.Length - Header.Length,
                new XmlDictionaryReaderQuotas { MaxArrayLength = 10000000, MaxStringContentLength = 16000000, MaxDepth = 512, MaxBytesPerRead = 65536, MaxNameTableCharCount = 16000000 }))
            {
                object value = GPC.Model.Core.ContractXml.Read(Serializer(), reader);
                if (reader.MoveToContent() != XmlNodeType.None) throw new SerializationException("TrailingArachneArchiveContent");
                if (value != null && !(value is T)) throw new SerializationException("ArachneArchiveRootTypeMismatch");
                if (value is Models.ModelModel model) model.RebuildIndexes();
                return (T)value;
            }
        }
        public static T Clone<T>(T value) => FromBytes<T>(ToBytes(value));
        private sealed class WrapperResolver : DataContractResolver
        {
            private const string Namespace = "urn:gpc:arachne:wrapper:2";
            private static readonly HashSet<Type> Collections = new HashSet<Type> { typeof(List<>), typeof(Dictionary<,>), typeof(KeyValuePair<,>), typeof(ObservableCollection<>), typeof(Nullable<>) };
            private static readonly HashSet<Type> Types = new HashSet<Type>(typeof(Models.ModelModel).Assembly.GetTypes()
                .Where(t => t.Namespace == "Arachne.Common.Models" && !t.ContainsGenericParameters && (t.IsSerializable || typeof(ISerializable).IsAssignableFrom(t)))
                .Concat(GPC.Model.Core.ModelValues.DataContracts)
                .Concat(new[] { typeof(object), typeof(string), typeof(decimal), typeof(Guid), typeof(DateTime), typeof(TimeSpan), typeof(System.Drawing.Color),
                    typeof(Rhino.Geometry.Point2d), typeof(Rhino.Geometry.Point3d), typeof(Rhino.Geometry.Vector2d), typeof(Rhino.Geometry.Vector3d),
                    typeof(Rhino.Geometry.Plane), typeof(Rhino.Geometry.Line), typeof(Rhino.Geometry.Interval), typeof(Rhino.Geometry.Transform), typeof(Rhino.Geometry.BoundingBox),
                    typeof(Models.NodeRestraintAttributeModel.AttributeData), typeof(Geometry.Shapes), typeof(GPC.Geometry.Shape), typeof(GPC.Geometry.Shape2d), typeof(GPC.Geometry.Polygon2d), typeof(GPC.Geometry.Polygon3d), typeof(GPC.Geometry.Point2d), typeof(GPC.Geometry.Point3d) }));
            private static readonly Dictionary<string, Assembly> Assemblies = Types.Select(t => t.Assembly).Concat(Collections.Select(t => t.Assembly))
                .Distinct().ToDictionary(a => a.GetName().Name, StringComparer.Ordinal);
            private static bool Allowed(Type type) => type != null && (Types.Contains(type) || type.IsPrimitive
                || type.IsArray && Allowed(type.GetElementType())
                || type.IsGenericType && Collections.Contains(type.GetGenericTypeDefinition()) && type.GetGenericArguments().All(Allowed));
            public override bool TryResolveType(Type type, Type declaredType, DataContractResolver known, out XmlDictionaryString name, out XmlDictionaryString ns)
            {
                if (!Allowed(type)) throw new SerializationException("UnregisteredArachneWrapperType: " + type.FullName);
                var dictionary = new XmlDictionary();
                name = dictionary.Add("T" + BitConverter.ToString(Encoding.UTF8.GetBytes(type.AssemblyQualifiedName)).Replace("-", ""));
                ns = dictionary.Add(Namespace); return true;
            }
            public override Type ResolveName(string name, string ns, Type declaredType, DataContractResolver known)
            {
                Type type;
                if (ns != Namespace) type = known.ResolveName(name, ns, declaredType, null);
                else
                {
                    if (name.Length < 3 || name[0] != 'T' || name.Length % 2 != 1 || name.Length > 65537) throw new SerializationException("InvalidArachneTypeToken");
                    var bytes = new byte[(name.Length - 1) / 2];
                    for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(name.Substring(1 + 2 * i, 2), 16);
                    type = Type.GetType(Encoding.UTF8.GetString(bytes), a => Assemblies.TryGetValue(a.Name, out var assembly) ? assembly : throw new SerializationException("UnregisteredArachneAssembly: " + a.Name), null, true);
                }
                if (!Allowed(type)) throw new SerializationException("UnregisteredArachneWrapperType: " + type?.FullName);
                return type;
            }
        }
    }
}
