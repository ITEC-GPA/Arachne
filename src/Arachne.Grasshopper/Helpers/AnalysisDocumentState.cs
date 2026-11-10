using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using GH_IO.Serialization;
using GPC.Model.Core.Identity;
using GPC.Model.Persistence;

namespace Arachne.Grasshopper.Helpers
{
    /// <summary>GH document boundary for prepared GPC analyses. Numerical data stay in sidecars for
    /// file archives. Clipboard/undo archives have no location and carry a portable package instead.
    /// Inputs and results are loaded by the closed GPC codecs; reading does not execute a solver.</summary>
    /// <remarks>The sidecar of a run is prepared in the background when the run ends (review A2: the GPC graph of 10,000 nodes takes
    /// tens of seconds to encode): the outputs are published at once, and saving, copying or opening a run waits for its preparation.
    /// The preparations and every other use of the archive happen one at a time, in order. The graph of a run of this session stays in
    /// memory, so restoring the outputs for the same inputs does not read the archive; nothing changes it after the run (the outputs
    /// are a separate copy).</remarks>
    internal sealed class AnalysisDocumentState
    {
        private sealed class Run
        {
            internal Task<ExternalModelReference> Reference;
            // A run of this session: the analysis graph, its solver map and the binding of its results
            internal GPC.Model.Models.Model Model;
            internal SolverIdentityMap Map;
            internal Guid ModelGuid;
            internal string Fingerprint;
        }

        private readonly List<Run> runs = new List<Run>();
        private ExternalModelArchive archive = new ExternalModelArchive();
        private readonly object gate = new object();
        private Task last = Task.CompletedTask;
        private const string ChunkName = "GpcAnalyses";
        private const int MaximumRuns = 100000;
        private const int MaximumPortableBytes = 256 * 1024 * 1024;

        internal bool Has(int iteration) => iteration >= 0 && iteration < runs.Count && runs[iteration] != null;

        /// <summary>The references in the archive, after their preparation (an error of a preparation is thrown here).</summary>
        internal IReadOnlyList<ExternalModelReference> References => runs.Select(r => r == null ? null : Wait(r)).ToList().AsReadOnly();

        /// <summary>The run of an iteration, to put it back with <see cref="Put"/> after the outputs were reset.</summary>
        internal object Keep(int iteration) => Has(iteration) ? runs[iteration] : null;

        internal void Put(int iteration, object run)
        {
            while (runs.Count <= iteration) runs.Add(null);
            runs[iteration] = (Run)run;
        }

        internal void Clear(int iteration) { if (Has(iteration)) runs[iteration] = null; }

        /// <summary>True if the run belongs to these inputs: same model and fingerprint of the exported graph.</summary>
        internal bool IsCurrent(int iteration, Guid modelGuid, string fingerprint)
        {
            var run = runs[iteration];
            if (run.Model != null) return run.ModelGuid == modelGuid && run.Fingerprint == fingerprint;
            var reference = Wait(run);
            return reference.ModelGuid == modelGuid && reference.AnalysisFingerprint == fingerprint;
        }

        /// <summary>The analysis graph of a run: the one in memory for a run of this session, otherwise read from the archive.</summary>
        internal GPC.Model.Models.Model Analysis(int iteration)
        {
            if (!Has(iteration)) throw new InvalidOperationException("NoSavedFormFindingAnalysis");
            return runs[iteration].Model ?? Open(iteration).Model;
        }

        internal ExternalModelRun Open(int iteration)
        {
            if (!Has(iteration)) throw new InvalidOperationException("NoSavedFormFindingAnalysis");
            var reference = Wait(runs[iteration]);
            WaitArchive();
            return archive.Open(reference);
        }

        internal void Prepare(int iteration, GPC.Model.Models.Model model, SolverIdentityMap map)
        {
            if (model == null || map == null) throw new ArgumentNullException();
            var run = new Run { Model = model, Map = map, ModelGuid = model.Guid, Fingerprint = map.AnalysisFingerprint };
            var store = archive;
            lock (gate)
            {
                // After the previous preparations, whatever their outcome; the error of this one stays in its task
                run.Reference = last.ContinueWith(_ => store.Prepare(model, map), CancellationToken.None, TaskContinuationOptions.DenyChildAttach, TaskScheduler.Default);
                last = run.Reference;
            }
            Put(iteration, run);
        }

        private static ExternalModelReference Wait(Run run)
        {
            try { return run.Reference.GetAwaiter().GetResult(); }
            catch (Exception e) when (!(e is SerializationException)) { throw new SerializationException("The form-finding results could not be prepared for saving: " + e.Message, e); }
        }

        // The archive is not shared between threads: the GH thread uses it after the preparations.
        private void WaitArchive()
        {
            Task pending; lock (gate) pending = last;
            try { pending.Wait(); } catch (AggregateException) { } // the error of a preparation belongs to its run
        }

        // ArchiveLocation belongs to this write operation. GH_Document.FilePath can still be the old
        // location (or empty) during Save As and must not be used as the destination.
        internal void Write(GH_IWriter writer, Guid component)
        {
            if (!runs.Any(r => r != null)) return;
            if (runs.Count > MaximumRuns) throw new SerializationException("TooManyFormFindingRuns");
            string destination = LocalPath(writer.ArchiveLocation);
            // A run whose preparation failed (e.g. a graph beyond the size of a GPC archive block) is not saved: the document is,
            // and the run is computed again after opening it.
            var written = new ExternalModelReference[runs.Count];
            for (int i = 0; i < runs.Count; i++)
            {
                if (runs[i] == null) continue;
                try { written[i] = Wait(runs[i]); }
                catch (SerializationException e)
                {
                    writer.AddMessage("Form-finding results of run " + i + " not saved, run the analysis again after opening: " + (e.InnerException ?? e).Message, GH_Message_Type.warning);
                }
            }
            if (!written.Any(r => r != null)) return;
            WaitArchive();
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
                    var model = runs[i].Model; var map = runs[i].Map;
                    if (model == null) { var opened = archive.Open(written[i]); model = opened.Model; map = opened.SolverMap; }
                    using (var stream = new MemoryStream())
                    {
                        ModelArchive.Save(model, stream, ModelArchiveFormat.Compact); portableBytes += stream.Length;
                        if (portableBytes > MaximumPortableBytes) throw new SerializationException("PortableAnalysisArchiveTooLarge");
                        chunk.SetByteArray("PortableModel", stream.ToArray());
                    }
                    if (map != null)
                        using (var stream = new MemoryStream())
                        {
                            ModelValueArchive.Save(map, stream, ModelArchiveFormat.Compact); portableBytes += stream.Length;
                            if (portableBytes > MaximumPortableBytes) throw new SerializationException("PortableAnalysisArchiveTooLarge");
                            chunk.SetByteArray("PortableMap", stream.ToArray());
                        }
                }
            }
            // The references of the saved document (relative to it); the graphs of this session stay in memory
            for (int i = 0; i < written.Length; i++) if (written[i] != null) runs[i].Reference = Task.FromResult(written[i]);
        }

        internal void Read(GH_IReader reader)
        {
            var restored = new List<Run>();
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
                    restored.Add(new Run { Reference = Task.FromResult(reference) });
                }
            }
            // Do not read numerical blocks here. Missing/corrupt sidecars are diagnosed when opened
            // or saved, and never prevent the user from opening the GH definition and rerunning it.
            WaitArchive();
            archive = store; runs.Clear(); runs.AddRange(restored);
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
