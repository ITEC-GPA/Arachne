using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Arachne.Grasshopper.Components;
using Arachne.Core;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 3 && !(args.Length == 4 && args[3] == "--tests-only")) { Console.Error.WriteLine("Usage: ExampleWriter <Arachne root> <Rhino System> <Grasshopper dir> [--tests-only]"); return 2; }
        var root = Path.GetFullPath(args[0]);
        var pluginDir = Path.Combine(root, "src", "Arachne.Grasshopper", "bin", "Release", "net48");
        AppDomain.CurrentDomain.AssemblyResolve += (sender, request) =>
        {
            string name = new AssemblyName(request.Name).Name;
            foreach (string dir in new[] { pluginDir, args[1], args[2] })
                foreach (string extension in new[] { ".dll", ".gha" })
                {
                    var path = Path.Combine(dir, name + extension);
                    if (File.Exists(path)) return Assembly.LoadFrom(path);
                }
            return null;
        };
        try { return StartRhino(root, args.Length == 4); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int StartRhino(string root, bool testsOnly)
    {
        using (var rhino = new Rhino.Runtime.InProcess.RhinoCore(new[] { "/nosplash", "/notemplate" }, Rhino.Runtime.InProcess.WindowStyle.NoWindow))
        {
            using (var modelDocument = Rhino.RhinoDoc.CreateHeadless(null))
            {
                modelDocument.ModelAbsoluteTolerance = 1e-6;
                Rhino.RhinoDoc.ActiveDoc = modelDocument;
                return Write(root, testsOnly);
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int Write(string root, bool testsOnly)
    {
        VerifyNetworkGeometry();
        string pluginPath = Path.Combine(root, "src", "Arachne.Grasshopper", "bin", "Release", "net48", "Arachne.Grasshopper.gha");
        // GH exposes this loader internally in the installed version. Only the validation tool uses reflection.
        var loader = typeof(GH_ComponentServer).GetMethod("LoadExternalFile", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(GH_ExternalFile), typeof(bool) }, null);
        if (loader == null || !(bool)loader.Invoke(Grasshopper.Instances.ComponentServer, new object[] { new GH_ExternalFile(pluginPath), false }))
            throw new InvalidOperationException("Could not register the Arachne plugin for archive verification.");
        PatterningChecks.Run();
        ReleaseChecks.Run();
        if (testsOnly) return 0;
        var document = new GH_Document { Enabled = true };
        var demo = new DemoComponent(); var cable = new CableComponent(); var node = new NodeComponent();
        var model = new ModelComponent(); var settings = new SolverSettingsComponent(); var solver = new FormFindComponent();
        var run = new GH_BooleanToggle { Value = false };
        var status = new GH_Panel(); var log = new GH_Panel();
        void Add(IGH_DocumentObject obj, int x, int y)
        {
            obj.CreateAttributes(); obj.Attributes.Pivot = new PointF(x, y);
            document.AddObject(obj, false);
        }
        Add(demo, 80, 170); Add(cable, 310, 110); Add(node, 310, 360);
        Add(model, 570, 210); Add(settings, 570, 510); Add(run, 610, 690); Add(solver, 850, 280);
        Add(status, 1100, 280); Add(log, 1100, 440);
        cable.Params.Input[0].AddSource(demo.Params.Output[0]);
        node.Params.Input[0].AddSource(demo.Params.Output[1]);
        model.Params.Input[0].AddSource(cable.Params.Output[0]);
        model.Params.Input[1].AddSource(node.Params.Output[0]);
        model.Params.Input[2].AddSource(demo.Params.Output[2]);
        solver.Params.Input[0].AddSource(model.Params.Output[0]);
        solver.Params.Input[1].AddSource(settings.Params.Output[0]);
        solver.Params.Input[2].AddSource(run);
        status.AddSource(solver.Params.Output[6]); log.AddSource(solver.Params.Output[7]);
        GH_Document.EnableSolutions = true;
        document.NewSolution(false);
        Console.WriteLine("Document: " + document.SolutionState + "; enabled=" + document.Enabled);
        foreach (var component in new GH_Component[] { demo, cable, node, model, settings, solver })
            Console.WriteLine(component.Name + ": " + component.Phase + "; output counts=" + string.Join(",", component.Params.Output.Select(p => p.VolatileDataCount)));
        foreach (var component in new GH_Component[] { demo, cable, node, model, settings, solver })
            if (component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0)
                throw new InvalidOperationException(component.Name + ": " + string.Join("; ", component.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
        var assembled = ((GH_ObjectWrapper)model.Params.Output[0].VolatileData.AllData(true).Single()).Value as FormModel;
        if (assembled == null || assembled.Nodes.Count != 25 || assembled.Edges.Count != 40 || assembled.Faces.Count != 16)
            throw new InvalidOperationException("Demo model does not match the expected 25 nodes, 40 cables, 16 faces.");
        Console.WriteLine("Rhino/Grasshopper model check: 25 nodes, 40 cables, 16 faces.");
        if (TensileFormFindingSolver.IsAvailable)
        {
            run.Value = true; run.ExpireSolution(false); document.NewSolution(false);
            string state = solver.Params.Output[6].VolatileData.AllData(true).Single().ToString();
            if (state != "Converged" || solver.Params.Output[0].VolatileDataCount != 25 || solver.Params.Output[1].VolatileDataCount != 40)
                throw new InvalidOperationException("Equilibrium check failed: " + state + "; " + string.Join("; ",solver.Params.Output[7].VolatileData.AllData(true)));
            var mesh = ((GH_Mesh)solver.Params.Output[2].VolatileData.AllData(true).Single()).Value;
            if (mesh.Faces.Count != 16) throw new InvalidOperationException("Solved mesh lost faces.");
            Console.WriteLine("Rhino/Grasshopper equilibrium: Converged, 25 nodes, 40 cables, 16 faces.");
            run.Value = false; run.ExpireSolution(false); document.NewSolution(false);
        }
        else
        {
            run.Value = true; run.ExpireSolution(false); document.NewSolution(false);
            string state = solver.Params.Output[6].VolatileData.AllData(true).Single().ToString();
            if (state != "DependencyMissing" || solver.Params.Output[0].VolatileDataCount != 0)
                throw new InvalidOperationException("Missing solver check failed: " + state);
            Console.WriteLine("Rhino/Grasshopper missing dependency check: no calculated geometry.");
            run.Value = false; run.ExpireSolution(false); document.NewSolution(false);
        }
        var archive = new GH_Archive();
        if (!archive.AppendObject(document, "Definition")) throw new InvalidOperationException("Could not serialize the definition.");
        var path = Path.Combine(root, "examples", "01_SaddleNet.ghx");
        if (!archive.WriteToFile(path, true, false)) throw new IOException("Could not write the example.");
        var check = new GH_Archive();
        if (!check.ReadFromFile(path)) throw new IOException("Could not read the generated GHX archive.");
        var reopened = new GH_Document { Enabled = true };
        if (!check.ExtractObject(reopened, "Definition") || reopened.Objects.Count != 9)
            throw new IOException("Could not restore all 9 objects from the GHX archive.");
        reopened.NewSolution(false);
        var reopenedModel = reopened.Objects.OfType<ModelComponent>().Single();
        var restored = ((GH_ObjectWrapper)reopenedModel.Params.Output[0].VolatileData.AllData(true).Single()).Value as FormModel;
        if (restored == null || restored.Edges.Count != 40) throw new IOException("Restored definition did not rebuild the model.");
        var binary = new GH_Archive();
        if (!binary.AppendObject(document, "Definition") || !binary.WriteToFile(Path.ChangeExtension(path, ".gh"), true, false))
            throw new IOException("Could not write the binary GH example.");
        Console.WriteLine("Generated and parsed: " + path);
        Console.WriteLine("9 objects, 10 connections; saved with Run=False. Solver checked when available.");
        PatterningExample.Write(root);
        return 0;
    }

    private static void VerifyNetworkGeometry()
    {
        var square = new Rhino.Geometry.PolylineCurve(new[] {
            new Rhino.Geometry.Point3d(0,0,0), new Rhino.Geometry.Point3d(10,0,0),
            new Rhino.Geometry.Point3d(10,10,0), new Rhino.Geometry.Point3d(0,10,0), new Rhino.Geometry.Point3d(0,0,0) });
        var boundary = new List<Rhino.Geometry.Curve> { square };
        var empty = new List<Rhino.Geometry.Curve>();
        if (!Arachne.Grasshopper.Geometry.CableNetHelper.CreateCableNet2(boundary, empty, boundary, 2.5, 2.5, Rhino.Geometry.Plane.WorldXY,
            out var warp, out var weft, out _, out _, out var anchors, out _, out _, 1e-6, 0) ||
            warp.Sum(c => c.Count) == 0 || weft.Sum(c => c.Count) == 0 || anchors.Count == 0)
            throw new InvalidOperationException("Rectangular network geometry check failed.");
        var circle = new Rhino.Geometry.Circle(Rhino.Geometry.Plane.WorldXY, 5).ToNurbsCurve();
        var radialBoundary = new List<Rhino.Geometry.Curve> { circle };
        if (!Arachne.Grasshopper.Geometry.CableNetHelper.CreateRadialCableNet2(radialBoundary, empty, radialBoundary, 8, 1, Rhino.Geometry.Plane.WorldXY,
            out var radial, out var rings, out _, out _, out var radialAnchors, out _, out _, 1e-6, 0) ||
            radial.Sum(c => c.Count) == 0 || rings.Sum(c => c.Count) == 0 || radialAnchors.Count == 0)
            throw new InvalidOperationException("Radial network geometry check failed.");
        Console.WriteLine("Rhino geometry checks: rectangular and radial networks generated.");
    }
}
