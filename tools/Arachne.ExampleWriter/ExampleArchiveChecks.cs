using System;
using System.IO;
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
            string temporary = Path.Combine(Path.GetTempPath(), "arachne-example-" + Guid.NewGuid().ToString("N") + extension);
            try
            {
                using (var original = Read(Path.Combine(root, "examples", name + extension)))
                {
                    Verify(original, name);
                    var saved = new GH_Archive();
                    if (!saved.AppendObject(original, "Definition") || !saved.WriteToFile(temporary, true, false))
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
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
            document.NewSolution(false);
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void Verify(GH_Document document, string name)
    {
        if (name == "02_Patterning") PatterningExample.Verify(document);
        else FormFindingExample.Verify(document, name, name == "01_SaddleNet" ? 25 : 0, name == "01_SaddleNet" ? 40 : 0);
    }
}
