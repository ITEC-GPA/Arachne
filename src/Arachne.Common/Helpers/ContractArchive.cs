using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using GPC.Model.Persistence;
using GModel = GPC.Model.Models.Model;

namespace Arachne.Common.Helpers
{
    /// <summary>
    /// The bytes of the Grasshopper data (internalized parameters, copies, undo): GPC contracts only (review A2). A value is written
    /// by <see cref="ModelValueArchive"/>, a model by <see cref="ModelArchive"/> (XML document or package with compact results),
    /// deflated, after the header ARCGPC3. The root type is chosen by the reader, never by the data. The FeMM payloads of the
    /// previous versions (BinaryFormatter/NRBF up to Arachne 0.3, ARCGPC2 of the F6 development builds) are refused with an
    /// explicit message (review A3): no legacy reader is shipped.
    /// </summary>
    public static class ContractArchive
    {
        private static readonly byte[] Header = Encoding.ASCII.GetBytes("ARCGPC3");
        private const byte Version = 1;
        private const byte KindValue = 1, KindModel = 2;
        public const int MaximumBytes = 256 * 1024 * 1024;

        public const string LegacyMessage = "These Arachne data were saved by a version before the GPC objects (0.3 or an F6 development build) and are no longer read. " +
            "Connect the input again or recompute the component that produced them; internalized data must be internalized again.";

        public static byte[] Value<T>(T value) where T : class
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            return Pack(KindValue, stream => ModelValueArchive.Save(value, stream));
        }

        public static T ReadValue<T>(byte[] data) where T : class
            => Unpack(data, KindValue, stream => ModelValueArchive.Load<T>(stream));

        public static byte[] Model(GModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            return Pack(KindModel, stream => ModelArchive.Save(model, stream));
        }

        public static GModel ReadModel(byte[] data) => Unpack(data, KindModel, ModelArchive.Load);

        private static byte[] Pack(byte kind, Action<Stream> write)
        {
            using (var output = new MemoryStream())
            {
                output.Write(Header, 0, Header.Length); output.WriteByte(Version); output.WriteByte(kind);
                using (var deflate = new DeflateStream(output, CompressionLevel.Fastest, true)) write(deflate);
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
            if (data[Header.Length] != Version) throw new SerializationException("UnsupportedArachneArchiveVersion: " + data[Header.Length] + " (update Arachne).");
            if (data[Header.Length + 1] != kind) throw new SerializationException("ArachneArchiveKindMismatch");
            using (var input = new MemoryStream(data, Header.Length + 2, data.Length - Header.Length - 2, false))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            using (var buffer = new MemoryStream())
            {
                var block = new byte[81920]; int n;
                while ((n = deflate.Read(block, 0, block.Length)) > 0)
                {
                    if (buffer.Length + n > MaximumBytes) throw new SerializationException("ArachneArchiveTooLarge");
                    buffer.Write(block, 0, n);
                }
                buffer.Position = 0;
                return read(buffer) ?? throw new SerializationException("NullArachneArchiveValue");
            }
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
