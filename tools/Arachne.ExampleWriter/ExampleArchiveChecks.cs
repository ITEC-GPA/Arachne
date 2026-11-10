using System;
using System.IO;
using System.Linq;
using Arachne.Grasshopper.Components.Analysis;
using Arachne.Grasshopper.Components.Parameters;
using Grasshopper.Kernel.Special;
using GH_IO.Serialization;
using Grasshopper.Kernel;

// Open the distributed definitions before generating anything, then round-trip
// temporary copies. The repository examples remain untouched.
internal static class ExampleArchiveChecks
{
    internal static SuiteReport Run(string root)
    {
        var report = new SuiteReport { Name = "ExampleArchives" };
        foreach (string name in new[] { "01_SaddleNet", "02_Patterning", "03_CableNet" })
        foreach (string extension in new[] { ".gh", ".ghx" })
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), "arachne-example-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporaryDirectory);
            string temporary = Path.Combine(temporaryDirectory, name + extension);
            try
            {
                using (var original = Read(Path.Combine(root, "examples", name + extension)))
                {
                    Verify(original, name);
                    var saved = new GH_DocumentIO { Document = original };
                    if (!saved.SaveQuiet(temporary))
                        throw new IOException("Cannot write example copy");
                    using (var restored = Read(temporary))
                    {
                        if (restored.ObjectCount != original.ObjectCount) throw new IOException("Object count changed");
                        Verify(restored, name);
                    }
                }
                report.Passed++; Console.WriteLine("ARCHIVE PASS " + name + extension);
            }
            catch (Exception error) { report.Failed++; Console.WriteLine("ARCHIVE FAIL " + name + extension + ": " + error.GetBaseException().Message); }
            finally { Directory.Delete(temporaryDirectory, true); }
        }
        return report;
    }

    private static GH_Document Read(string path)
    {
        var archive = new GH_Archive();
        var document = new GH_Document { Enabled = true };
        try
        {
            if (!archive.ReadFromFile(path) || !archive.ExtractObject(document, "Definition"))
                throw new IOException("Cannot reopen " + path);
            document.Enabled = true;
            document.NewSolution(false);
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void Verify(GH_Document document, string name)
    {
        if (name == "02_Patterning") PatterningExample.Verify(document);
        else
        {
            FormFindingExample.Verify(document, name, name == "01_SaddleNet" ? 25 : 0, name == "01_SaddleNet" ? 40 : 0);
            if (document.Objects.OfType<GH_BooleanToggle>().Any(t => t.NickName == "Auto Run" && t.Value)) throw new InvalidOperationException("Example must have Auto Run disabled");
            if (document.Objects.OfType<FormFindComponent>().Single().Message != "Restored") throw new InvalidOperationException("Example did not restore saved results");
            var frame = document.Objects.OfType<CoordinateSystemParam>().Single().PersistentData.AllData(true).Single();
            var chunk = new GH_LooseChunk("frame");
            if (!frame.Write(chunk)) throw new InvalidOperationException("Cannot archive internalized GPC data");
            var bytes = chunk.GetByteArray("Data");
            if (System.Text.Encoding.ASCII.GetString(bytes, 0, 7) != "ARCGPC3" || bytes[7] != 2) throw new InvalidOperationException("Internalized example data are not current GPC archives");
        }
    }
}
