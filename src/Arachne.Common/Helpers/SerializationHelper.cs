using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Arachne.Common.Helpers
{
    public static class SerializationHelper
    {

        public static double? ReadNullableDouble(SerializationInfo info,
                                                 string name)
        {
            return (double?)info.GetValue(name, typeof(double?));
        }

        public static void WriteNullableDouble(SerializationInfo info,
                                               string name,
                                               double? value)
        {
            info.AddValue(name, value, typeof(double?));
        }

        /// <summary>
        /// Binary snapshot of a serializable model, used to store the model objects in the Grasshopper files.
        /// </summary>
        public static byte[] ToBytes(object value)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                new BinaryFormatter().Serialize(ms, value);
                return ms.ToArray();
            }
        }

        public static T FromBytes<T>(byte[] data)
        {
            using (MemoryStream ms = new MemoryStream(data))
                return (T)new BinaryFormatter().Deserialize(ms);
        }
    }
}
