using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

// Written even on failure; BUILD.json is written by the script only on success.
public sealed class CheckReport
{
    public string Status { get; set; } = "Failed";
    public string Runtime { get; set; } = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;
    public string FemmSources { get; set; }
    public bool RequireFemm { get; set; }
    public string FemmComparison { get; set; } = "NotRun";
    public string Error { get; set; }
    public List<SuiteReport> Suites { get; set; } = new List<SuiteReport>();

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using (var stream = File.Create(path))
            new DataContractJsonSerializer(typeof(CheckReport)).WriteObject(stream, this);
    }
}

public sealed class SuiteReport
{
    public string Name { get; set; }
    public int Passed { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
}
