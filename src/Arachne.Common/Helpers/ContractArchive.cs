using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using GPC.Model.Persistence;
using GModel = GPC.Model.Models.Model;

namespace Arachne.Common.Helpers
{
    /// <summary>
    /// The bytes of the Grasshopper data (internalized parameters, copies, undo): GPC contracts only (review A2). After the header
    /// ARCGPC3, a version and a kind. Version 2 (written) holds the compact GPC archive (<see cref="ModelArchiveFormat.Compact"/>:
    /// <see cref="ModelValueArchive"/> version 2, <see cref="ModelArchive"/> version 6; binary contracts, deflated; 10 to 100 times
    /// smaller and faster than XML). Only this version is accepted. The root type is
    /// chosen by the reader, never by the data. The FeMM payloads of the
    /// previous versions (BinaryFormatter/NRBF up to Arachne 0.3, ARCGPC2 of the F6 development builds) are refused with an
    /// explicit message (review A3): no legacy reader is shipped.
    /// </summary>
    public static class ContractArchive
    {
        private static readonly byte[] Header = Encoding.ASCII.GetBytes("ARCGPC3");
        private const byte Version = 2;
        private const byte KindValue = 1, KindModel = 2, KindMaterial = 3, KindElasticPlate = 4, KindMembranePlate = 5;
        public const int MaximumBytes = 256 * 1024 * 1024;

        public const string LegacyMessage = "These Arachne data were saved by a version before the GPC objects (0.3 or an F6 development build) and are no longer read. " +
            "Connect the input again or recompute the component that produced them; internalized data must be internalized again.";

        public static byte[] Value<T>(T value) where T : class
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return Pack(KindValue, stream => ModelValueArchive.Save(value, stream, ModelArchiveFormat.Compact));
        }

        public static T ReadValue<T>(byte[] data) where T : class
            => Unpack(data, KindValue, stream => ModelValueArchive.Load<T>(stream));

        public static byte[] Model(GModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return Pack(KindModel, stream => ModelArchive.Save(model, stream, ModelArchiveFormat.Compact));
        }

        public static GModel ReadModel(byte[] data) => Unpack(data, KindModel, ModelArchive.Load);

        /// <summary>A value that the GH data carry by a base type (Material: isotropic material or membrane; Plate Property: elastic or
        /// membrane). The GPC archive needs a registered root type: the concrete type, from this closed list, is the kind byte of the
        /// archive and <see cref="ReadMember{T}"/> selects the root type from it.</summary>
        public static byte[] Member(GPC.Model.ModelObject value)
        {
            switch (value)
            {
                case null: throw new ArgumentNullException(nameof(value));
                case GPC.Model.Materials.Material material: return Pack(KindMaterial, stream => ModelValueArchive.Save(material, stream, ModelArchiveFormat.Compact));
                case GPC.Model.ElementProperties.ElasticPlateProperty plate: return Pack(KindElasticPlate, stream => ModelValueArchive.Save(plate, stream, ModelArchiveFormat.Compact));
                case GPC.Model.ElementProperties.MembranePlateProperty membrane: return Pack(KindMembranePlate, stream => ModelValueArchive.Save(membrane, stream, ModelArchiveFormat.Compact));
                default: throw new SerializationException("UnsupportedArachneValue: " + value.GetType().Name);
            }
        }

        public static T ReadMember<T>(byte[] data) where T : class
        {
            byte kind = data != null && data.Length >= Header.Length + 2 && data.Take(Header.Length).SequenceEqual(Header) ? data[Header.Length + 1] : KindValue;
            object value;
            switch (kind)
            {
                case KindMaterial: value = Unpack(data, kind, stream => ModelValueArchive.Load<GPC.Model.Materials.Material>(stream)); break;
                case KindElasticPlate: value = Unpack(data, kind, stream => ModelValueArchive.Load<GPC.Model.ElementProperties.ElasticPlateProperty>(stream)); break;
                case KindMembranePlate: value = Unpack(data, kind, stream => ModelValueArchive.Load<GPC.Model.ElementProperties.MembranePlateProperty>(stream)); break;
                // not an archive or an unknown kind: the messages of a plain value (legacy payload, unsupported archive, kind mismatch)
                default: value = Unpack(data, KindMaterial, stream => ModelValueArchive.Load<GPC.Model.Materials.Material>(stream)); break;
            }
            return value as T ?? throw new SerializationException("ArachneArchiveKindMismatch");
        }

        private static byte[] Pack(byte kind, Action<Stream> write)
        {
            using (var output = new MemoryStream())
            {
                output.Write(Header, 0, Header.Length); output.WriteByte(Version); output.WriteByte(kind);
                // The compact archive is deflated already; a graph its binary form does not reproduce is written (and read) as XML.
                write(output);
                if (output.Length > MaximumBytes) throw new SerializationException("ArachneArchiveTooLarge");
                return output.ToArray();
            }
        }

        private static T Unpack<T>(byte[] data, byte kind, Func<Stream, T> read)
        {
            if (data == null || data.Length > MaximumBytes) throw new SerializationException("InvalidArachneArchiveSize");
            if (data.Length < Header.Length + 2 || !data.Take(Header.Length).SequenceEqual(Header))
            {
                if (IsLegacy(data)) throw new SerializationException(LegacyMessage);
                throw new SerializationException("UnsupportedArachneArchive: the data are not an Arachne GPC archive.");
            }
            byte version = data[Header.Length];
            if (version != Version) throw new SerializationException("UnsupportedArachneArchiveVersion: " + version + " (update Arachne).");
            if (data[Header.Length + 1] != kind) throw new SerializationException("ArachneArchiveKindMismatch");
            using (var input = new MemoryStream(data, Header.Length + 2, data.Length - Header.Length - 2, false))
                return read(input) ?? throw new SerializationException("NullArachneArchiveValue");
        }

        /// <summary>True for a BinaryFormatter (NRBF) stream or an ARCGPC2 archive of a previous Arachne.</summary>
        public static bool IsLegacy(byte[] data)
        {
            if (data == null) return false;
            // NRBF: SerializationHeaderRecord (0), root id, header id -1, major version 1, minor version 0
            bool nrbf = data.Length >= 17 && data[0] == 0 && data[5] == 0xFF && data[6] == 0xFF && data[7] == 0xFF && data[8] == 0xFF
                && data[9] == 1 && data[10] == 0 && data[11] == 0 && data[12] == 0 && data[13] == 0 && data[14] == 0 && data[15] == 0 && data[16] == 0;
            var old = Encoding.ASCII.GetBytes("ARCGPC2");
            return nrbf || data.Length >= old.Length && data.Take(old.Length).SequenceEqual(old);
        }
    }
}
