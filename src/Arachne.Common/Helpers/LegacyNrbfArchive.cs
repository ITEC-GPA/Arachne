// The 9.x API is pinned deliberately; only this compatibility reader uses its experimental surface.
#pragma warning disable SYSLIB5005
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Formats.Nrbf;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace Arachne.Common.Helpers
{
    /// <summary>Read-only migration of the closed Arachne legacy graph. The decoder reads records;
    /// the mapper only constructs types fixed by Vocabulary.cs. Unknown types, cycles and future releases fail explicitly.</summary>
    internal static partial class LegacyNrbfArchive
    {
        internal static bool HasHeader(byte[] data)
        { using (var stream = new MemoryStream(data, false)) return NrbfDecoder.StartsWithPayloadHeader(stream); }
        internal static T Read<T>(byte[] data)
        {
            using (var stream = new MemoryStream(data, false))
            {
                var record = NrbfDecoder.Decode(stream, leaveOpen: true);
                if (stream.Position != stream.Length) throw new SerializationException("TrailingLegacyArachneContent");
                var mapper = new Mapper();
                object result = mapper.Read(record, 0);
                if (!(result is T)) throw new SerializationException("LegacyArachneRootMismatch");
                mapper.Complete();
                if (result is Models.ModelModel model) model.RebuildIndexes();
                return (T)result;
            }
        }
        private static Type[] Vocabulary()
        {
            var leaves = LeafTypes.Concat(new[] { typeof(object), typeof(string), typeof(bool), typeof(byte), typeof(sbyte), typeof(char), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal), typeof(DateTime), typeof(TimeSpan) }).Distinct().ToArray();
            return leaves.Concat(leaves.Select(t => t.MakeArrayType()))
                .Concat(leaves.Select(t => typeof(List<>).MakeGenericType(t)))
                .Concat(leaves.Select(t => typeof(ObservableCollection<>).MakeGenericType(t)))
                .ToArray();
        }
        private static readonly Type[] Types;
        static LegacyNrbfArchive() { Types = Vocabulary(); }
        private sealed class Mapper
        {
            private readonly Dictionary<SerializationRecordId,object> values = new Dictionary<SerializationRecordId,object>();
            private readonly HashSet<SerializationRecordId> visiting = new HashSet<SerializationRecordId>();
            private readonly Dictionary<string,Type> typeCache = new Dictionary<string,Type>(StringComparer.Ordinal);
            private readonly List<object> callbacks = new List<object>();
            private int count;
            private Type Resolve(SerializationRecord record)
            {
                string name = record.TypeName.FullName;
                if (typeCache.TryGetValue(name, out var cached)) return cached;
                Type type = Types.FirstOrDefault(record.TypeNameMatches);
                if (type == null) throw new SerializationException("UnregisteredLegacyArachneType: " + name);
                typeCache.Add(name,type); return type;
            }
            internal object Read(object raw, int depth)
            {
                if (depth > 128 || ++count > 5000000) throw new SerializationException("LegacyArachneGraphLimit");
                if (!(raw is SerializationRecord record)) return raw;
                if (record is PrimitiveTypeRecord primitive) return primitive.Value;
                if (values.TryGetValue(record.Id,out object previous)) return previous;
                if (!visiting.Add(record.Id)) throw new SerializationException("CyclicLegacyArachneGraph");
                Type type = Resolve(record);
                object value;
                if (record is ArrayRecord array)
                {
                    if (array.Rank != 1 || array.Lengths[0] > 1000000) throw new SerializationException("LegacyArachneArrayLimit");
                    var items = array.GetArray(type);
                    var result = Array.CreateInstance(type.GetElementType(),items.Length);
                    for (int i=0; i<items.Length; i++) result.SetValue(Read(items.GetValue(i),depth+1),i);
                    value=result;
                }
                else if (record is ClassRecord data)
                {
                    if (type.IsEnum) value = Enum.ToObject(type,Read(data.GetRawValue("value__"),depth+1));
                    else if (type.IsGenericType && type.GetGenericTypeDefinition()==typeof(List<>))
                    {
                        int length=data.GetInt32("_size");
                        var items=(Array)Read(data.GetRawValue("_items"),depth+1);
                        if(length<0||length>items.Length)throw new SerializationException("InvalidLegacyListSize");
                        var list=(IList)Activator.CreateInstance(type);
                        for(int i=0;i<length;i++)list.Add(items.GetValue(i));
                        value=list;
                    }
                    else if (type.IsGenericType && type.GetGenericTypeDefinition()==typeof(ObservableCollection<>))
                    {
                        string key=data.MemberNames.Single(n=>n=="items"||n.EndsWith("+items",StringComparison.Ordinal));
                        var list=(IList)Activator.CreateInstance(type);
                        foreach(object item in (IEnumerable)Read(data.GetRawValue(key),depth+1))list.Add(item);
                        value=list;
                    }
                    else if (typeof(ISerializable).IsAssignableFrom(type))
                    {
                        var info=new SerializationInfo(type,new FormatterConverter());
                        foreach(string name in data.MemberNames)
                        {
                            object member=Read(data.GetRawValue(name),depth+1);
                            if(Releases.TryGetValue(name,out int max)&&Convert.ToInt32(member)>max)throw new SerializationException("FutureLegacyArachneRelease: "+name);
                            info.AddValue(name,member);
                        }
                        var ctor=type.GetConstructor(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new[]{typeof(SerializationInfo),typeof(StreamingContext)},null);
                        if(ctor==null||type.IsAbstract)throw new SerializationException("MissingLegacyConstructor: "+type.FullName);
                        value=ctor.Invoke(new object[]{info,new StreamingContext(StreamingContextStates.Persistence)});
                        callbacks.Add(value);
                    }
                    else
                    {
                        value=Activator.CreateInstance(type);
                        foreach(string name in data.MemberNames)
                        {
                            object member=Read(data.GetRawValue(name),depth+1);
                            var field=type.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                            if(field!=null){field.SetValue(value,member);continue;}
                            string property=name.StartsWith("<",StringComparison.Ordinal)&&name.EndsWith(">k__BackingField",StringComparison.Ordinal)?name.Substring(1,name.IndexOf('>')-1):null;
                            var setter=property==null?null:type.GetProperty(property)?.SetMethod;
                            if(setter==null)throw new SerializationException("UnknownLegacyMember: "+type.FullName+"."+name);
                            setter.Invoke(value,new[]{member});
                        }
                        callbacks.Add(value);
                    }
                }
                else throw new SerializationException("UnsupportedLegacyArachneRecord");
                visiting.Remove(record.Id); values.Add(record.Id,value); return value;
            }
            internal void Complete()
            {
                foreach(object value in callbacks)
                {
                    for(Type type=value.GetType();type!=null;type=type.BaseType)
                        foreach(var method in type.GetMethods(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly)
                            .Where(m=>m.IsDefined(typeof(OnDeserializedAttribute),false)))
                            method.Invoke(value,new object[]{new StreamingContext(StreamingContextStates.Persistence)});
                    if(value is IDeserializationCallback callback)callback.OnDeserialization(null);
                }
            }
        }
    }
}
