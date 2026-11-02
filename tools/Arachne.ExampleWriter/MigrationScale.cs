using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Rhino.Geometry;

// Baseline of the existing plugin, not the proposed domain. Archive formats differ by plugin.
internal static class MigrationScale
{
    private static StreamWriter output;
    private static int nodeCount, plateCount;
    private static readonly Func<long> Allocated = (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", Type.EmptyTypes));
    internal static int Run()
    {
        string path = Environment.GetEnvironmentVariable("MIGRATION_SCALE_OUTPUT");
        if (string.IsNullOrEmpty(path)) throw new ArgumentException("Set MIGRATION_SCALE_OUTPUT to a new CSV file.");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        using (output = new StreamWriter(new FileStream(path, FileMode.CreateNew)))
        {
            output.WriteLine("runtime,nodes,plates,operation,repetition,seconds,threadAllocatedBytes,heapDeltaBytes");
            new ModelModel(Build(3));
            foreach (int side in new[] { 10, 30, 100 })
            {
                nodeCount = side * side; plateCount = 2 * (side - 1) * (side - 1);
                ModelModel model = null, copy = null; byte[] archive = null;
                Measure("build-and-assemble", 0, () => model = Build(side));
                Measure("copy-first", 0, () => copy = new ModelModel(model));
                for (int i = 1; i <= 5; i++)
                {
                    Measure("copy-warm", i, () => copy = new ModelModel(model));
                    if (copy.Nodes.Count != model.Nodes.Count || copy.Plates.Count != model.Plates.Count) throw new Exception("Entity loss in copy");
                    var point = copy.Nodes[0].Position;
                    copy.Nodes[0].Position = new Point3d(point.X + 17, point.Y, point.Z);
                    if (model.Nodes[0].Position.X != point.X) throw new Exception("Node mutation leaked into source");
                    copy = null;
                }
                Measure("save-memory", 0, () => archive = SerializationHelper.ToBytes(model));
                Measure("load-memory", 0, () => { using (var stream = new MemoryStream(archive, false)) copy = ModelModel.Deserialize(new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter(), stream); });
                if (copy.Nodes.Count != nodeCount || copy.Plates.Count != plateCount) throw new Exception("Archive entity loss");
                byte[] hash = null;
                Measure("archive-sha256", 0, () => { using (var sha = SHA256.Create()) hash = sha.ComputeHash(archive); });
                Console.WriteLine("PASS " + nodeCount + "/" + plateCount + ", archive bytes " + archive.Length + ", SHA256 " + BitConverter.ToString(hash).Replace("-", ""));
            }
            Chains();
        }
        return 0;
    }

    private static void Measure(string name, int repetition, Action action)
    {
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        long before = Allocated(), heap = GC.GetTotalMemory(false);
        var watch = Stopwatch.StartNew(); action(); watch.Stop();
        output.WriteLine(string.Join(",", RuntimeInformation.FrameworkDescription, nodeCount, plateCount, name, repetition,
            watch.Elapsed.TotalSeconds.ToString("R", CultureInfo.InvariantCulture), Allocated() - before, GC.GetTotalMemory(false) - heap));
        output.Flush();
        Console.WriteLine(nodeCount + "/" + plateCount + " " + name + ": " + watch.Elapsed.TotalSeconds.ToString("F4", CultureInfo.InvariantCulture) + " s");
    }

    private static ModelModel Build(int side)
    {
        var model = new ModelModel(ModelModel.ModelUnits.Nmm);
        var property = new PlatePropertyModel("P") { Name = "Synthetic 100 mm", Thickness = 100, Material = new MaterialModel("S") { Modulus = 210000 } };
        var elements = new List<ElementModel>();
        var points = new Point3d[side, side];
        for (int j = 0; j < side; j++) for (int i = 0; i < side; i++)
        {
            points[i,j] = new Point3d(1000.0*i,1000.0*j,0);
            elements.Add(new NodeModel((1+j*side+i).ToString(CultureInfo.InvariantCulture), points[i,j]));
        }
        int id = 0;
        for (int j = 0; j < side-1; j++) for (int i = 0; i < side-1; i++)
        {
            elements.Add(new PlateModel((++id).ToString(CultureInfo.InvariantCulture), new[] { points[i,j], points[i+1,j], points[i+1,j+1] }, property));
            elements.Add(new PlateModel((++id).ToString(CultureInfo.InvariantCulture), new[] { points[i,j], points[i+1,j+1], points[i,j+1] }, property));
        }
        model.AddElements(elements);
        return model;
    }
    
    private static void Chains()
    {
        foreach (int divisions in new[] { 4, 12, 32 })
        {
            using (var canvas = new Canvas())
            {
                var chain = FormFindingChecks.Start(canvas);
                var grid = FormFindingChecks.SaddleGrid(divisions, 10000, 1000);
                var beams = FormFindingChecks.ForceDensity(chain, FormFindingChecks.Beams(chain, grid.lines), 20);
                var model = FormFindingChecks.Build(chain, beams, FormFindingChecks.Supports(chain, grid.border));
                var solved = FormFindingChecks.FormFind(chain, model, 0);
                nodeCount = (divisions + 1) * (divisions + 1); plateCount = divisions * divisions;
                for (int repetition = 0; repetition < 3; repetition++)
                {
                    Measure("gh-build-formfind-chain", repetition, () => canvas.Document.NewSolution(true));
                    var result = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Model>(solved).Value;
                    if (Canvas.HasErrors(solved) || result.Nodes.Count != nodeCount) throw new Exception("Incomplete GH chain");
                }
            }
        }
    }

}

