using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.Components.Analysis;
using Arachne.Grasshopper.Components.Attributes;
using Arachne.Grasshopper.Components.Definitions;
using Arachne.Grasshopper.Components.ElementProperties;
using Arachne.Grasshopper.Components.Loads;
using Arachne.Grasshopper.Components.Materials;
using Arachne.Grasshopper.Components.Models;
using Arachne.Grasshopper.Components.Sections;
using Arachne.Grasshopper.Components.Tools;
using Arachne.Grasshopper.DataTypes;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// End-to-end form finding with the FeMM components, compared with analytical results.
internal static class FormFindingChecks
{
    private static int passed, failed;
    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("FORMFIND PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("FORMFIND FAIL " + name + ": " + e.GetBaseException().Message); }
    }
    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    // ---- Model construction with the real components ----

    internal sealed class Chain
    {
        public Canvas Canvas;
        public GH_Component LoadCase, Property;
    }

    internal static Chain Start(Canvas c, string loadCase = "FF")
    {
        var lc = c.Add(new LoadCaseComponent()); Canvas.Set(lc, 0, new GH_String(loadCase));
        var section = c.Add(new BeamSectionSolidCircleComponent()); Canvas.Set(section, 0, new GH_Number(0.01));
        var material = c.Add(new MaterialSteelComponent()); Canvas.Set(material, 0, new GH_String("Steel")); Canvas.Set(material, 1, new GH_Integer(0));
        var property = c.Add(new BeamPropertyComponent()); Canvas.Set(property, 0, new GH_String("Cable")); Canvas.Set(property, 1, new GH_Integer(1));
        Canvas.Link(property, 2, section); Canvas.Link(property, 3, material);
        return new Chain { Canvas = c, LoadCase = lc, Property = property };
    }

    internal static GH_Component Beams(Chain chain, IEnumerable<Line> lines)
    {
        var beam = chain.Canvas.Add(new BeamComponent());
        Canvas.Set(beam, 0, lines.Select(l => new GH_Curve(new LineCurve(l))).ToArray());
        Canvas.Link(beam, 1, chain.Property);
        return beam;
    }

    internal static GH_Component ForceDensity(Chain chain, GH_Component beams, double value, int type = 0, double weight = 1)
    {
        var fd = chain.Canvas.Add(new ForceDensityComponent());
        Canvas.Link(fd, 0, beams); Canvas.Set(fd, 1, new GH_Integer(type)); Canvas.Link(fd, 2, chain.LoadCase);
        Canvas.Set(fd, 3, new GH_Number(value)); Canvas.Set(fd, 4, new GH_Boolean(false)); Canvas.Set(fd, 5, new GH_Number(weight));
        return fd;
    }

    internal static GH_Component Nodes(Chain chain, IEnumerable<Point3d> points)
    {
        var node = chain.Canvas.Add(new NodeComponent());
        Canvas.Set(node, 0, points.Select(p => new GH_Point(p)).ToArray());
        return node;
    }

    internal static GH_Component Supports(Chain chain, IEnumerable<Point3d> points)
    {
        var restraints = chain.Canvas.Add(new NodeRestraintsComponent());
        Canvas.Link(restraints, 0, Nodes(chain, points));
        Canvas.Set<IGH_Goo>(restraints, 1, new GH_Boolean(true), new GH_Boolean(true), new GH_Boolean(true));
        return restraints;
    }

    internal static GH_Component NodeLoad(Chain chain, GH_Component nodes, Vector3d force)
    {
        var load = chain.Canvas.Add(new NodeLoadComponent());
        Canvas.Link(load, 0, nodes); Canvas.Link(load, 1, chain.LoadCase); Canvas.Set(load, 2, new GH_Vector(force));
        return load;
    }

    internal static GH_Component Build(Chain chain, params GH_Component[] elements)
    {
        var build = chain.Canvas.Add(new BuildModelComponent());
        foreach (var element in elements) Canvas.Link(build, 0, element);
        build.Params.Input[0].DataMapping = GH_DataMapping.Flatten; // one model from data coming with different paths
        Canvas.Set(build, 2, new GH_Integer((int)ModelModel.ModelUnits.SI));
        return build;
    }

    internal static GH_Component FormFind(Chain chain, GH_Component build, int solverType, int maxIterations = 20, double tolerance = 1e-4)
    {
        var ff = chain.Canvas.Add(new FormFindComponent());
        Canvas.Link(ff, 0, build);
        Canvas.Set(ff, 4, new GH_Integer(solverType));
        Canvas.Set(ff, 5, new GH_Integer(maxIterations));
        Canvas.Set(ff, 6, new GH_Number(tolerance));
        Canvas.Set(ff, 13, new GH_Boolean(true));
        return ff;
    }

    internal static (List<Line> lines, List<Point3d> border) SaddleGrid(int n, double span, double rise, double dx = 0)
    {
        var p = new Point3d[n + 1, n + 1];
        var lines = new List<Line>(); var border = new List<Point3d>();
        for (int y = 0; y <= n; y++)
            for (int x = 0; x <= n; x++)
            {
                bool edge = x == 0 || x == n || y == 0 || y == n;
                p[x, y] = new Point3d(dx + span * x / n, span * y / n, edge ? rise * (2.0 * x / n - 1) * (2.0 * y / n - 1) : 0);
                if (edge) border.Add(p[x, y]);
                if (x > 0) lines.Add(new Line(p[x - 1, y], p[x, y]));
                if (y > 0) lines.Add(new Line(p[x, y - 1], p[x, y]));
            }
        return (lines, border);
    }

    private static ModelModel Solved(GH_Component ff)
    {
        Check(!Canvas.HasErrors(ff), "Form Find: " + Canvas.Messages(ff));
        return Canvas.Single<GH_Model>(ff).Value;
    }

    private static double PreLoad(BeamModel beam) => beam.Loads.OfType<BeamPreLoadModel>().Single().Value;

    internal static int Run()
    {
        passed = failed = 0;

        Test("Linear: interior nodes at the average of the neighbours, N = q L (saddle 4 x 4)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(4, 10, 2);
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border)), 0);
                c.Solve();
                var model = Solved(ff);
                Check(model.Nodes.Count == 25 && model.Beams.Count == 40, $"Model {model.Nodes.Count} nodes / {model.Beams.Count} beams");
                foreach (var node in model.Nodes.Where(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel))))
                {
                    var neighbours = model.Beams.Where(b => b.PointFrom.DistanceTo(node.Position) < 1e-9 || b.PointTo.DistanceTo(node.Position) < 1e-9)
                        .Select(b => b.PointFrom.DistanceTo(node.Position) < 1e-9 ? b.PointTo : b.PointFrom).ToList();
                    Check(neighbours.Count == 4, "Interior node with " + neighbours.Count + " beams");
                    var average = new Point3d(neighbours.Average(q => q.X), neighbours.Average(q => q.Y), neighbours.Average(q => q.Z));
                    Check(average.DistanceTo(node.Position) < 1e-6, "Not in equilibrium: " + node.Position);
                }
                foreach (var beam in model.Beams)
                    Check(Math.Abs(PreLoad(beam) - beam.PointFrom.DistanceTo(beam.PointTo)) < 1e-6, "Pre-load differs from q L");
                Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count == 16, "Mesh faces");
            }
        });

        var a = new Point3d(0, 0, 0); var m = new Point3d(1, 0, 0); var b2 = new Point3d(2, 0, 0);

        Test("Linear: node load between two cables (z = -F / 2q)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var beams = ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 2);
                var ff = FormFind(chain, Build(chain, beams, Supports(chain, new[] { a, b2 }), NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0, 0, -4))), 0);
                c.Solve();
                var model = Solved(ff);
                var node = model.Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel)));
                Check(node.Position.DistanceTo(new Point3d(1, 0, -1)) < 1e-6, "Loaded node at " + node.Position);
                Check(model.Beams.All(beam => Math.Abs(PreLoad(beam) - 2 * Math.Sqrt(2)) < 1e-6), "Axial force differs from q L");
            }
        });

        Test("Linear step by step: the fixed length is reached", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var first = ForceDensity(chain, Beams(chain, new[] { new Line(a, m) }), 1);
                var fixedLength = c.Add(new FixedLengthComponent());
                Canvas.Link(fixedLength, 0, first); Canvas.Link(fixedLength, 1, chain.LoadCase); Canvas.Set(fixedLength, 2, new GH_Number(0.6));
                var second = ForceDensity(chain, Beams(chain, new[] { new Line(m, b2) }), 1);
                var ff = FormFind(chain, Build(chain, fixedLength, second, Supports(chain, new[] { a, b2 })), 1, 100, 1e-6);
                c.Solve();
                var node = Solved(ff).Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel)));
                Check(Math.Abs(node.Position.X - 0.6) < 1e-5 && Math.Abs(node.Position.Z) < 1e-9, "Node at " + node.Position);
            }
        });

        Test("Linear step by step: an impossible fixed length stops with an error and no result", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var beams = ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 1);
                var fixedLength = c.Add(new FixedLengthComponent());
                Canvas.Link(fixedLength, 0, beams); Canvas.Link(fixedLength, 1, chain.LoadCase); Canvas.Set(fixedLength, 2, new GH_Number(0.5));
                var ff = FormFind(chain, Build(chain, fixedLength, Supports(chain, new[] { a, b2 })), 1, 10, 1e-6);
                c.Solve();
                Check(Canvas.HasErrors(ff) && Canvas.Out(ff, 0).Count == 0, "Expected an error without outputs");
            }
        });

        Test("Non-linear: the fixed axial force is reached (N = 4, z = -tan 30)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                // Without a weight on the force density the only target is the axial force
                var beams = ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 1, 0, 0);
                var axial = c.Add(new FixedAxialForceComponent());
                Canvas.Link(axial, 0, beams); Canvas.Link(axial, 1, chain.LoadCase); Canvas.Set(axial, 2, new GH_Number(4));
                var ff = FormFind(chain, Build(chain, axial, Supports(chain, new[] { a, b2 }), NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0, 0, -4))), 2, 200, 1e-8);
                c.Solve();
                var model = Solved(ff);
                var node = model.Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel)));
                Check(Math.Abs(node.Position.Z + Math.Tan(Math.PI / 6)) < 1e-3, "Node at " + node.Position);
                Check(model.Beams.All(beam => Math.Abs(PreLoad(beam) - 4) < 1e-2), "Axial forces " + string.Join(", ", model.Beams.Select(PreLoad)));
            }
        });

        Test("Pressure: nodal loads in the given load case and solved mesh", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(4, 10, 2);
                var wind = c.Add(new LoadCaseComponent()); Canvas.Set(wind, 0, new GH_String("Wind"));
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border)), 0);
                Canvas.Set(ff, 2, new GH_Number(0.05)); Canvas.Link(ff, 3, wind);
                c.Solve();
                var model = Solved(ff);
                var loads = model.Nodes.SelectMany(n => n.Loads.OfType<NodeForceLoadModel>()).ToList();
                Check(loads.Count > 0 && loads.All(l => l.LoadCase.Name == "Wind"), "Pressure loads " + loads.Count);
                Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count == 16, "Mesh faces");
            }
        });

        Test("Pressure without a load case: warning and no result (FeMM behaviour)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(2, 4, 1);
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border)), 0);
                Canvas.Set(ff, 2, new GH_Number(0.05));
                c.Solve();
                Check(ff.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0 && Canvas.Out(ff, 0).Count == 0, "Missing warning");
            }
        });

        Test("Build Model merges two nodes defined at the same point", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var beams = ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 1);
                var build = Build(chain, beams, Supports(chain, new[] { a, b2 }), NodeLoad(chain, Nodes(chain, new[] { a }), new Vector3d(1, 0, 0)));
                c.Solve();
                var node = Canvas.Single<GH_Model>(build).Value.Nodes.Single(n => n.Position.DistanceTo(a) < 1e-9);
                Check(node.HasAttribute(typeof(NodeRestraintAttributeModel)) && node.Loads.OfType<NodeForceLoadModel>().Count() == 1, "Restraint or load lost");
            }
        });

        Test("Parallel attribute branches keep the beam identity", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var beams = Beams(chain, new[] { new Line(a, m) });
                var fd = ForceDensity(chain, beams, 1);
                var fixedLength = c.Add(new FixedLengthComponent());
                Canvas.Link(fixedLength, 0, beams); Canvas.Link(fixedLength, 1, chain.LoadCase); Canvas.Set(fixedLength, 2, new GH_Number(0.9));
                var keep = c.Add(new KeepPositionComponent());
                var node = Nodes(chain, new[] { m });
                Canvas.Link(keep, 0, node);
                var load = NodeLoad(chain, node, new Vector3d(0, 0, -1));
                var build = Build(chain, fd, fixedLength, keep, load, Supports(chain, new[] { a }));
                c.Solve();
                var model = Canvas.Single<GH_Model>(build).Value;
                Check(model.Beams.Count == 1, "Beams: " + model.Beams.Count);
                Check(model.Beams[0].Loads.OfType<ForceDensityModel>().Count() == 1 && model.Beams[0].Loads.OfType<FixedLengthModel>().Count() == 1, "Beam attributes not merged");
                var merged = model.Nodes.Single(n => n.Position.DistanceTo(m) < 1e-9);
                Check(merged.HasAttribute(typeof(NodeWeightAttributeModel)) && merged.Loads.Count == 1, "Node attributes not merged");
            }
        });

        Test("Beam loads: keystone resultant at the centroid, projected load on the projection", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var flat = Beams(chain, new[] { new Line(a, m) });
                var inclined = Beams(chain, new[] { new Line(b2, new Point3d(3, 0, 1)) });
                var keystone = c.Add(new BeamDistributedLoadComponent());
                Canvas.Link(keystone, 0, flat); Canvas.Link(keystone, 1, chain.LoadCase); Canvas.Set(keystone, 2, new GH_Integer(1));
                Canvas.Set(keystone, 3, new GH_CoordinateSystem(CoordinateSystemModel.Global)); Canvas.Set(keystone, 4, new GH_Integer(6));
                Canvas.Set(keystone, 6, new GH_Number(1)); Canvas.Set(keystone, 7, new GH_Number(3));
                var projected = c.Add(new BeamDistributedLoadComponent());
                Canvas.Link(projected, 0, inclined); Canvas.Link(projected, 1, chain.LoadCase); Canvas.Set(projected, 2, new GH_Integer(0));
                Canvas.Set(projected, 3, new GH_CoordinateSystem(CoordinateSystemModel.Global)); Canvas.Set(projected, 4, new GH_Integer(7));
                Canvas.Set(projected, 6, new GH_Number(1));
                var build = Build(chain, keystone, projected);
                c.Solve();
                Check(!Canvas.HasErrors(keystone) && !Canvas.HasErrors(projected), Canvas.Messages(keystone) + Canvas.Messages(projected));
                var ff = FormFindingHelper.AssemblyModel(Canvas.Single<GH_Model>(build).Value, Plane.WorldXY, 0, 0, 0, true, out _);
                var k = ff.Beams.Single(x => Math.Abs(x.Length - 1) < 1e-9);
                var p = ff.Beams.Single(x => Math.Abs(x.Length - Math.Sqrt(2)) < 1e-9);
                Check(Math.Abs(k.Load.Z + 2) < 1e-9 && Math.Abs(k.LoadPos - 7.0 / 12) < 1e-9, $"Keystone {k.Load.Z} at {k.LoadPos}");
                Check(Math.Abs(p.Load.Z + 1) < 1e-9 && Math.Abs(p.LoadPos - Math.Sqrt(2) / 2) < 1e-9, $"Projected {p.Load.Z} at {p.LoadPos}");
            }
        });

        Test("Disconnected nets: plates generated for every part", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var g1 = SaddleGrid(2, 4, 1); var g2 = SaddleGrid(2, 4, 1, 10);
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, g1.lines.Concat(g2.lines)), 1), Supports(chain, g1.border.Concat(g2.border))), 0);
                c.Solve();
                Solved(ff);
                Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count == 8, "Mesh faces " + Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count);
            }
        });

        Test("Model, node and beam survive a file round trip", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var beams = ForceDensity(chain, Beams(chain, new[] { new Line(a, m) }), 1);
                var supports = Supports(chain, new[] { a });
                var build = Build(chain, beams, supports);
                c.Solve();
                var model = Canvas.Single<GH_Model>(build);
                var beam = Canvas.Single<GH_Beam>(beams);
                var node = Canvas.Single<GH_Node>(supports);
                foreach (object value in new object[] { model.Value, beam.Value, node.Value })
                    try { SerializationHelper.FromBytes<object>(SerializationHelper.ToBytes(value)); } catch (Exception e) { throw new Exception(value.GetType().Name + ": " + e.GetBaseException().Message); }
                var chunk = new GH_LooseChunk("check");
                Check(model.Write(chunk.CreateChunk("model")) && beam.Write(chunk.CreateChunk("beam")) && node.Write(chunk.CreateChunk("node")), "Write failed");
                var data = chunk.Serialize_Binary();
                var read = new GH_LooseChunk("check"); read.Deserialize_Binary(data);
                var model2 = new GH_Model(); var beam2 = new GH_Beam(); var node2 = new GH_Node();
                Check(model2.Read(read.FindChunk("model")) && beam2.Read(read.FindChunk("beam")) && node2.Read(read.FindChunk("node")), "Read failed");
                Check(model2.Value.Beams.Count == 1 && model2.Value.Nodes.Count == 2, "Model content");
                Check(beam2.Value.Loads.OfType<ForceDensityModel>().Single().Value == 1 && beam2.Guid == beam.Guid, "Beam content");
                Check(node2.Value.HasAttribute(typeof(NodeRestraintAttributeModel)) && node2.Value.Position == a, "Node content");
                var copy = (GH_Model)model2.Duplicate();
                Check(copy.Value.Beams.Count == 1, "Duplicate of a read model");
            }
        });

        Test("Cable Net on a saddle perimeter: linear form finding with mesh", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var perimeter = new PolylineCurve(new[] { new Point3d(0, 0, 1.5), new Point3d(12, 0, -1.5), new Point3d(12, 8, 1.5), new Point3d(0, 8, -1.5), new Point3d(0, 0, 1.5) });
                var border = perimeter.DuplicateSegments().Select(s => new GH_Curve(s)).ToArray();
                var net = c.Add(new Arachne.Grasshopper.Components.Patterning.CableNetComponent());
                Canvas.Set(net, 0, border); Canvas.Set(net, 2, border); Canvas.Set(net, 3, new GH_Number(1)); Canvas.Set(net, 4, new GH_Number(1));
                var warp = c.Add(new BeamComponent()); Canvas.Link(warp, 0, net, 2); Canvas.Link(warp, 1, chain.Property);
                var weft = c.Add(new BeamComponent()); Canvas.Link(weft, 0, net, 3); Canvas.Link(weft, 1, chain.Property);
                var edges = c.Add(new BeamComponent()); Canvas.Link(edges, 0, net, 0); Canvas.Link(edges, 1, chain.Property);
                edges.Params.Input[0].DataMapping = GH_DataMapping.Flatten; warp.Params.Input[0].DataMapping = GH_DataMapping.Flatten; weft.Params.Input[0].DataMapping = GH_DataMapping.Flatten;
                var node = c.Add(new NodeComponent()); Canvas.Link(node, 0, net, 4);
                var restraints = c.Add(new NodeRestraintsComponent()); Canvas.Link(restraints, 0, node);
                Canvas.Set<IGH_Goo>(restraints, 1, new GH_Boolean(true), new GH_Boolean(true), new GH_Boolean(true));
                var build = Build(chain, ForceDensity(chain, warp, 2), ForceDensity(chain, weft, 1), ForceDensity(chain, edges, 1), restraints);
                var ff = FormFind(chain, build, 0);
                c.Solve();
                var model = Solved(ff);
                var free = model.Nodes.Where(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel))).ToList();
                Check(free.Count > 0 && free.All(n => n.Position.IsValid), "No free nodes");
                Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count > 0, "No mesh faces");
            }
        });

        Test("Curve2Mesh: faces of a curve network", () =>
        {
            using (var c = new Canvas())
            {
                var (lines, _) = SaddleGrid(3, 6, 0);
                var c2m = c.Add(new CurveToMeshComponent());
                Canvas.Set<IGH_Goo>(c2m, 0, lines.Select(l => (IGH_Goo)new GH_Curve(new LineCurve(l))).ToArray());
                c.Solve();
                Check(!Canvas.HasErrors(c2m), Canvas.Messages(c2m));
                Check(Canvas.Single<GH_Mesh>(c2m).Value.Faces.Count == 9, "Faces " + Canvas.Single<GH_Mesh>(c2m).Value.Faces.Count);
            }
        });

        Console.WriteLine("Form finding checks: " + passed + " passed, " + failed + " failed.");
        return failed;
    }
}
