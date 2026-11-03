using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using GH_IO.Serialization;
using GPC.Model.Core.Identity;
using GPC.Model.Persistence;

namespace Arachne.Grasshopper.Helpers
{
    /// <summary>GH document boundary for prepared GPC analyses. Numerical data stay in sidecars for
    /// file archives. Clipboard/undo archives have no location and carry a portable package instead.
    /// Inputs and results are loaded by the closed GPC codecs; reading does not execute a solver.</summary>
    internal sealed class AnalysisDocumentState
    {
        internal readonly List<ExternalModelReference> References = new List<ExternalModelReference>();
        private ExternalModelArchive archive = new ExternalModelArchive();
        private const string ChunkName = "GpcAnalyses";
        private const int MaximumRuns = 100000;
        private const int MaximumPortableBytes = 256 * 1024 * 1024;

        internal ExternalModelRun Open(int iteration)
        {
            if (iteration < 0 || iteration >= References.Count || References[iteration] == null)
                throw new InvalidOperationException("NoSavedFormFindingAnalysis");
            return archive.Open(References[iteration]);
        }

        internal void Prepare(int iteration, GPC.Model.Models.Model model, SolverIdentityMap map)
        {
            while (References.Count <= iteration) References.Add(null);
            References[iteration] = archive.Prepare(model, map);
        }

        // ArchiveLocation belongs to this write operation. GH_Document.FilePath can still be the old
        // location (or empty) during Save As and must not be used as the destination.
        internal void Write(GH_IWriter writer, Guid component)
        {
            if (!References.Any(r => r != null)) return;
            if (References.Count > MaximumRuns) throw new SerializationException("TooManyFormFindingRuns");
            string destination = LocalPath(writer.ArchiveLocation);
            var written = References.ToArray();
            if (destination != null)
            {
                string sidecar = Path.Combine(Path.GetDirectoryName(destination),
                    Path.GetFileNameWithoutExtension(destination) + "." + component.ToString("N") + ExternalModelArchive.Extension);
                var saved = archive.SaveSnapshot(sidecar, written.Where(r => r != null));
                int index = 0;
                for (int i = 0; i < written.Length; i++) if (written[i] != null) written[i] = saved[index++];
            }
            var root = writer.CreateChunk(ChunkName);
            root.SetInt32("Version", 1); root.SetInt32("Count", written.Length);
            long portableBytes = 0;
            for (int i = 0; i < written.Length; i++)
            {
                if (written[i] == null) continue;
                var chunk = root.CreateChunk("Run", i);
                using (var stream = new MemoryStream())
                {
                    written[i].Write(stream); chunk.SetByteArray("Reference", stream.ToArray());
                }
                if (destination == null)
                {
                    var run = Open(i);
                    using (var stream = new MemoryStream())
                    {
                        ModelArchive.Save(run.Model, stream); portableBytes += stream.Length;
                        if (portableBytes > MaximumPortableBytes) throw new SerializationException("PortableAnalysisArchiveTooLarge");
                        chunk.SetByteArray("PortableModel", stream.ToArray());
                    }
                    if (run.SolverMap != null)
                        using (var stream = new MemoryStream())
                        {
                            ModelValueArchive.Save(run.SolverMap, stream); portableBytes += stream.Length;
                            if (portableBytes > MaximumPortableBytes) throw new SerializationException("PortableAnalysisArchiveTooLarge");
                            chunk.SetByteArray("PortableMap", stream.ToArray());
                        }
                }
            }
            References.Clear(); References.AddRange(written);
        }

        internal void Read(GH_IReader reader)
        {
            var restored = new List<ExternalModelReference>();
            var store = new ExternalModelArchive();
            var root = reader.FindChunk(ChunkName);
            if (root != null)
            {
                if (root.GetInt32("Version") != 1) throw new SerializationException("UnsupportedAnalysisDocumentSchema");
                int count = root.GetInt32("Count");
                if (count < 0 || count > MaximumRuns) throw new SerializationException("InvalidFormFindingRunCount");
                string document = LocalPath(reader.ArchiveLocation), sidecar = null;
                long portableBytes = 0;
                for (int i = 0; i < count; i++)
                {
                    var chunk = root.FindChunk("Run", i);
                    if (chunk == null) { restored.Add(null); continue; }
                    ExternalModelReference reference;
                    using (var stream = new MemoryStream(chunk.GetByteArray("Reference"), false)) reference = ExternalModelReference.Read(stream);
                    if (chunk.ItemExists("PortableModel"))
                    {
                        byte[] data = chunk.GetByteArray("PortableModel"); portableBytes += data.Length;
                        if (portableBytes > MaximumPortableBytes) throw new SerializationException("PortableAnalysisArchiveTooLarge");
                        GPC.Model.Models.Model model;
                        using (var stream = new MemoryStream(data, false)) model = ModelArchive.Load(stream);
                        if (!reference.IsCurrent(model)) throw new SerializationException("PortableAnalysisFingerprintMismatch");
                        SolverIdentityMap map = null;
                        if (chunk.ItemExists("PortableMap"))
                        {
                            data = chunk.GetByteArray("PortableMap"); portableBytes += data.Length;
                            if (portableBytes > MaximumPortableBytes) throw new SerializationException("PortableAnalysisArchiveTooLarge");
                            using (var stream = new MemoryStream(data, false)) map = ModelValueArchive.Load<SolverIdentityMap>(stream);
                        }
                        if (sidecar != null) throw new SerializationException("MixedAnalysisStorageModes");
                        reference = store.Prepare(model, map);
                    }
                    else
                    {
                        if (document == null || portableBytes != 0) throw new SerializationException("MissingAnalysisDocumentLocation");
                        string path = reference.ResolvePath(document);
                        if (sidecar == null) { sidecar = path; store = new ExternalModelArchive(path); }
                        else if (!string.Equals(path, sidecar, StringComparison.OrdinalIgnoreCase)) throw new SerializationException("MixedAnalysisSidecars");
                    }
                    restored.Add(reference);
                }
            }
            // Do not read numerical blocks here. Missing/corrupt sidecars are diagnosed when opened
            // or saved, and never prevent the user from opening the GH definition and rerunning it.
            archive = store; References.Clear(); References.AddRange(restored);
        }

        private static string LocalPath(string location)
        {
            if (string.IsNullOrWhiteSpace(location)) return null;
            if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && uri.IsFile) return Path.GetFullPath(uri.LocalPath);
            if (Path.IsPathRooted(location)) return Path.GetFullPath(location);
            throw new SerializationException("UnsupportedAnalysisDocumentLocation");
        }
    }
}
