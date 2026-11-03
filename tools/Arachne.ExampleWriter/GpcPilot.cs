#if GPC_PILOT
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.Components.Attributes;
using Arachne.Grasshopper.DataTypes;
using GPC.Model.Analysis;
using GPC.Model.Core;
using GPC.Model.Models;
using GPC.Model.Persistence;
using ModelArchive = GPC.Model.Persistence.ModelArchive;
using GPC.Persistence;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using TensileLib.FormFinding;

internal static class GpcPilot
{
    private static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    private static void Near(double a, double b, string name) => Check(!double.IsNaN(a) && Math.Abs(a-b) <= 1e-8 * Math.Max(1,Math.Abs(b)), name);
    internal static int Run(string root)
    {
        string reopen=Environment.GetEnvironmentVariable("GPC_PILOT_REOPEN"); if(!string.IsNullOrEmpty(reopen)) return ReopenFolder(reopen);
        GpcF5Objectives.Run();
        string folder = Path.Combine(root, "artifacts", "f4", "membrane-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")); Directory.CreateDirectory(folder);
        for (int test = 0; test < 4; test++) RunCase(folder, test);
        Console.WriteLine("F4 MEMBRANE PASS " + folder); return 0;
    }
    private static int ReopenFolder(string folder)
    {
        int count=0;
        foreach(string document in Directory.GetFiles(folder,"*.ghx"))
        {
            var reader=new GH_Archive(); Check(reader.ReadFromFile(document),"fresh process GH read");
            using(var doc=new GH_Document())
            {
                Check(reader.ExtractObject(doc,"Definition"),"fresh process GH reopen");
                string text=doc.Objects.OfType<GH_Panel>().Single().UserText;
                ExternalModelReference reference;
                using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(text))) reference=ExternalModelReference.Read(stream);
                var run=ExternalModelArchive.Open(document,reference);
                Check(Numeric(run.Model)==File.ReadAllText(document+".numeric.sha256"),"fresh process numeric bytes");
                Check(run.Model.AnalysisFingerprint()==File.ReadAllText(document+".input.sha256"),"fresh process input fingerprint");
                Check(ModelValues.Fingerprint(new object[]{run.SolverMap})==File.ReadAllText(document+".map.sha256"),"fresh process solver identities");
                count++;
            }
        }
        Check(count>0,"saved pilot documents exist"); Console.WriteLine("F4 REOPEN PASS "+count); return 0;
    }
    private static string Numeric(Model model)
    {
        using(var stream=new MemoryStream()) { ModelArchive.Save(model,stream); stream.Position=0;
            using(var zip=new ZipArchive(stream,ZipArchiveMode.Read)) using(var input=zip.GetEntry("results.bin").Open()) using(var bytes=new MemoryStream()) { input.CopyTo(bytes); return ArchiveBlock.Digest(bytes.ToArray()); } }
    }
    private static void RunCase(string folder, int test)
    {
        using (var c = new Canvas())
        {
            var chain = FormFindingChecks.Start(c);
            var grid = FormFindingChecks.SaddleGrid(4, 1000, 200);
            GH_Component Edges(bool warp)
            {
                var lines = grid.lines.Where(l => (Math.Abs(l.Direction.X) > 0) == warp);
                var edges = FormFindingChecks.Beams(chain, lines);
                var fd = c.Add(new ForceDensityMembraneComponent()); Canvas.Link(fd,0,edges); Canvas.Link(fd,1,chain.LoadCase);
                Canvas.Set(fd,2,new GH_Number(warp ? 2 : 3)); Canvas.Set(fd,3,new GH_Integer(warp ? 0 : 1)); return fd;
            }
            var supports = FormFindingChecks.Supports(chain, grid.border);
            var center = FormFindingChecks.NodeLoad(chain, FormFindingChecks.Nodes(chain,new[]{new Point3d(500,500,0)}),new Vector3d(0,0,-10));
            var rotated = FormFindingChecks.Supports(chain, new[]{new Point3d(500,500,0)}, true, false, false, new Plane(Point3d.Origin,new Vector3d(1,1,0),new Vector3d(-1,1,0)));
            var build = test == 2 ? FormFindingChecks.Build(chain, Edges(true), Edges(false), supports, center, rotated) : FormFindingChecks.Build(chain, Edges(true), Edges(false), supports, center);
            var fabric=c.Add(new Arachne.Grasshopper.Components.Materials.MaterialMembraneComponent());
            Canvas.Set(fabric,0,new GH_String("PVC/PES")); Canvas.Set(fabric,1,new GH_Number(1050)); Canvas.Set(fabric,2,new GH_Number(4200)); Canvas.Set(fabric,3,new GH_Number(4000));
            var property=c.Add(new Arachne.Grasshopper.Components.ElementProperties.PlatePropertyComponent());
            Canvas.Set(property,0,new GH_String("Fabric")); Canvas.Set(property,1,new GH_Number(1)); Canvas.Link(property,2,fabric);
            c.Solve(); Check(!Canvas.HasErrors(build), "GH build " + test);
            var input = Canvas.Single<GH_Model>(build).Value;
            var options = new SolverOptionsModel { MaxIterations = 200, Tolerance = 1e-9, SolverToleranceA = 1e-12, SolverToleranceB = 1e-12, Pressure = test == 1 ? .0001 : 0 };
            if(test==3) { options.Gravity=ModelModel.GRAVITY_mmSuS2; options.PlateProperty=Canvas.Single<GH_PlateProperty>(property).Value; }
            var baseline = LegacyFormFindingBaseline.AssemblyModel(input, Plane.WorldXY, test == 2 ? 1 : 0, options, out var warnings);
            var settings = new FormFindingDefinition { Method = test == 2 ? FormFindingMethod.NonlinearForceDensity : FormFindingMethod.LinearForceDensity, MaxIterations = options.MaxIterations, RelativeTolerance = options.Tolerance, SolverToleranceA = options.SolverToleranceA, SolverToleranceB = options.SolverToleranceB };
            Guid owner = GpcPilotAdapter.Identity(new Guid("96341d4b-14c9-477a-aab4-c9149c079318"), "branch:" + test);
            var watch = Stopwatch.StartNew(); var model = GpcPilotAdapter.Import(baseline,owner,settings);
            string fingerprint = model.AnalysisFingerprint(); var branch = ModelValues.CopyValue(model);
            branch.NodesElements.Values.First().Position.X += 1;
            Check(model.AnalysisFingerprint() == fingerprint && branch.AnalysisFingerprint() != fingerprint,"detached GH branch " + test);
            var unsupported=ModelValues.CopyValue(model); unsupported.NodesElements.Values.First().Assignments.Active=false;
            bool rejected=false; try { GpcPilotAdapter.Export(unsupported); } catch(NotSupportedException) { rejected=true; }
            Check(rejected,"unsupported assignment rejected");
            var exported = GpcPilotAdapter.Export(model);
            var oldCode = GpcPilotAdapter.Solve(baseline, settings); var code = GpcPilotAdapter.Solve(exported.Solver, settings);
            Check(code == oldCode && code != Solver.ResultCodes.NotconvergedAtMaxIterationNumber,"convergence " + test);
            foreach (var n in baseline.Nodes)
            {
                var actual = exported.Solver.Nodes[n.Id]; Near(actual.DX,n.DX,"DX " + n.Id); Near(actual.DY,n.DY,"DY " + n.Id); Near(actual.DZ,n.DZ,"DZ " + n.Id);
            }
            foreach (var b in baseline.Beams) Near(exported.Solver.Beams[b.Id].AxialForce,b.AxialForce,"N " + b.Id);
            var reactions = FormFindingHelper.ComputeReactions(baseline); var actualReactions = FormFindingHelper.ComputeReactions(exported.Solver);
            for(int i=0;i<reactions.Length;i++) if(reactions[i].HasValue) { Near(actualReactions[i].Value.X,reactions[i].Value.X,"Rx " + i); Near(actualReactions[i].Value.Y,reactions[i].Value.Y,"Ry " + i); Near(actualReactions[i].Value.Z,reactions[i].Value.Z,"Rz " + i); }
            Check(model.AnalysisFingerprint() == fingerprint,"solver leaves inputs unchanged"); GpcPilotAdapter.Attach(model,exported,code); string resultDigest=Numeric(model);
            var archive = new ExternalModelArchive(); var reference = archive.Prepare(model,exported.Map);
            var panel = c.Add(new GH_Panel(),900,0); string document=Path.Combine(folder,"membrane-"+test+".ghx");
            archive.Save(ExternalModelArchive.PathFor(document),new[]{reference}, refs =>
            {
                using(var s=new MemoryStream()) { refs[0].Write(s); panel.UserText=Encoding.UTF8.GetString(s.ToArray()); }
                var gh = new GH_Archive(); gh.AppendObject(c.Document,"Definition");
                BlockArchive.WriteFile(document,s=> { byte[] bytes=Encoding.UTF8.GetBytes(gh.Serialize_Xml()); s.Write(bytes,0,bytes.Length); });
            });
            File.WriteAllText(document+".numeric.sha256",resultDigest);
            File.WriteAllText(document+".input.sha256",fingerprint);
            File.WriteAllText(document+".map.sha256",ModelValues.Fingerprint(new object[]{exported.Map}));
            var read = new GH_Archive(); Check(read.ReadFromFile(document),"read GHX");
            using(var reopenedDocument=new GH_Document())
            {
                Check(read.ExtractObject(reopenedDocument,"Definition"),"reopen GH document");
                var text=reopenedDocument.Objects.OfType<GH_Panel>().Single().UserText;
                ExternalModelReference saved; using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(text))) saved=ExternalModelReference.Read(stream);
                var run=ExternalModelArchive.Open(document,saved);
                Check(run.Model.AnalysisFingerprint()==fingerprint,"reopen inputs");
                Check(Numeric(run.Model)==resultDigest,"reopen exact result bytes");
                Check(run.Model.FormFinding.Goals.Count==settings.Goals.Count,"reopen goals and fabric directions");
                var repeated=GpcPilotAdapter.Export(run.Model); var repeatedCode=GpcPilotAdapter.Solve(repeated.Solver,run.Model.FormFinding);
                Check(repeatedCode==code,"reopened solver status");
                foreach(var b in baseline.Beams) Near(repeated.Solver.Beams[b.Id].AxialForce,b.AxialForce,"reopened N "+b.Id);
                Check(run.Model.NodesElements.Values.Sum(n=>n.Results.Count)>0,"reopened archived results");
            }
            watch.Stop(); File.WriteAllText(Path.Combine(folder,"case-"+test+".txt"),"PASS\ninput="+fingerprint+"\nseconds="+watch.Elapsed.TotalSeconds.ToString("R",System.Globalization.CultureInfo.InvariantCulture)+"\nsolver="+code+"\nwarnings="+string.Join(";",warnings));
        }
    }
}
#endif
