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
    private static void Near(double actual, double expected, double tolerance, string what)
        => Check(Math.Abs(actual - expected) <= tolerance, $"{what}: expected {expected}, got {actual}");

    // ---- Model construction with the real components ----

    internal sealed class Chain
    {
        public Canvas Canvas;
        public GH_Component LoadCase, Property;
    }

    // Arachne works in N and mm: the section diameter is in model units (10 mm by default)
    internal static Chain Start(Canvas c, string loadCase = "FF", double diameter = 10)
    {
        var lc = c.Add(new LoadCaseComponent()); Canvas.Set(lc, 0, new GH_String(loadCase));
        var section = c.Add(new BeamSectionSolidCircleComponent()); Canvas.Set(section, 0, new GH_Number(diameter));
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

    // Restraints along the axes of a plane (inputs: 0 Node, 1 Translational, 2 Coordinate System)
    internal static GH_Component Supports(Chain chain, IEnumerable<Point3d> points, bool tx, bool ty, bool tz, Plane? plane)
    {
        var restraints = chain.Canvas.Add(new NodeRestraintsComponent());
        Canvas.Link(restraints, 0, Nodes(chain, points));
        Canvas.Set<IGH_Goo>(restraints, 1, new GH_Boolean(tx), new GH_Boolean(ty), new GH_Boolean(tz));
        if (plane.HasValue)
            Canvas.Set(restraints, 2, new GH_Plane(plane.Value));
        return restraints;
    }

    internal static GH_Component NodeLoad(Chain chain, GH_Component nodes, Vector3d force)
    {
        var load = chain.Canvas.Add(new NodeLoadComponent());
        Canvas.Link(load, 0, nodes); Canvas.Link(load, 1, chain.LoadCase); Canvas.Set(load, 2, new GH_Vector(force));
        return load;
    }

    internal static GH_Component Build(Chain chain, params GH_Component[] elements) => Build(chain, ModelModel.ModelUnits.Nmm, elements);

    internal static GH_Component Build(Chain chain, ModelModel.ModelUnits units, params GH_Component[] elements)
    {
        var build = chain.Canvas.Add(new BuildModelComponent());
        foreach (var element in elements) Canvas.Link(build, 0, element);
        build.Params.Input[0].DataMapping = GH_DataMapping.Flatten; // one model from data coming with different paths
        Canvas.Set(build, 2, new GH_Integer((int)units));
        return build;
    }

    // Form Find with its Solver Options (inputs: 0 Gravity, 1 Pressure, 2 Pressure Load Case, 3 Load Cases,
    // 4 Max Iter, 5 Tolerance, 6 Max Iter,s, 7 ToleranceA,s, 8 ToleranceB,s, 9 Create mesh, 10 Plate Property)
    internal static GH_Component FormFind(Chain chain, GH_Component build, int solverType, int maxIterations = 20, double tolerance = 1e-4, double gravity = 0)
    {
        var options = chain.Canvas.Add(new SolverOptionsComponent());
        Canvas.Set(options, 0, new GH_Number(gravity));
        Canvas.Set(options, 4, new GH_Integer(maxIterations));
        Canvas.Set(options, 5, new GH_Number(tolerance));
        var ff = chain.Canvas.Add(new FormFindComponent());
        Canvas.Link(ff, 0, build);
        Canvas.Set(ff, 1, new GH_Integer(solverType));
        Canvas.Link(ff, 2, options);
        Canvas.Set(ff, 4, new GH_Boolean(true));
        return ff;
    }

    // The Solver Options of a Form Find made by FormFind
    internal static GH_Component Options(GH_Component ff) => (GH_Component)ff.Params.Input[2].Sources[0].Attributes.GetTopLevel.DocObject;

    internal static GH_Component Analysis(Chain chain, GH_Component component, GH_Component ff)
    {
        chain.Canvas.Add(component);
        Canvas.Link(component, 0, ff);
        return component;
    }

    private static List<T> Values<T>(GH_Component component, int output) where T : struct
        => Canvas.Out(component, output).Select(goo => goo.ScriptVariable()).Cast<T>().ToList();

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
                Canvas.Set(Options(ff), 1, new GH_Number(0.05)); Canvas.Link(Options(ff), 2, wind);
                c.Solve();
                var model = Solved(ff);
                var loads = model.Nodes.SelectMany(n => n.Loads.OfType<NodeForceLoadModel>()).ToList();
                Check(loads.Count > 0 && loads.All(l => l.LoadCase.Name == "Wind"), "Pressure loads " + loads.Count);
                Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count == 16, "Mesh faces");
            }
        });

        Test("Pressure without a load case: warning and no result (FeMM behaviour), not the default options", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(2, 4, 1);
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border)), 0);
                Canvas.Set(Options(ff), 1, new GH_Number(0.05));
                c.Solve();
                Check(Options(ff).RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0 && Canvas.Out(Options(ff), 0).Count == 0, "Solver Options without warning");
                // Without options from the connected component the Form Find stops instead of using the defaults
                Check(ff.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0 && Canvas.Out(ff, 0).Count == 0, "Missing warning");
            }
        });

        Test("Form Find without Solver Options uses the defaults: same result as the default options", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(2, 4, 1);
                var build = Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border));
                var withOptions = c.Add(new FormFindComponent()); Canvas.Link(withOptions, 0, build);
                var defaults = c.Add(new SolverOptionsComponent());
                Canvas.Link(withOptions, 2, defaults); Canvas.Set(withOptions, 4, new GH_Boolean(true));
                var without = c.Add(new FormFindComponent()); Canvas.Link(without, 0, build); Canvas.Set(without, 4, new GH_Boolean(true));
                c.Solve();
                var first = Solved(withOptions).Nodes.Select(n => n.Position).ToList();
                var second = Solved(without).Nodes.Select(n => n.Position).ToList();
                Check(first.Count == 9 && first.Zip(second, (p, q) => p.DistanceTo(q)).Max() == 0, "Different results");
                Check(Canvas.Single<GH_Mesh>(without, 1).Value.Faces.Count == 4, "No mesh with the default Create mesh");
                // FeMM defaults, except the tolerances of the algebraic solver: 1e-6, the ALGLIB default
                var options = Canvas.Single<GH_SolverOptions>(defaults).Value;
                Check(options.MaxIterations == 20 && options.Tolerance == 1e-4 && options.SolverMaxIterations == 0 && options.Gravity == 0 && options.CreateMesh, "FeMM defaults " + options);
                Check(options.SolverToleranceA == 1e-6 && options.SolverToleranceB == 1e-6 && options.LoadCases.Count == 0, "Tolerances " + options);
            }
        });

        Test("Load Cases of Solver Options: only the node loads of the selected cases (z = F / 2q)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var snow = c.Add(new LoadCaseComponent()); Canvas.Set(snow, 0, new GH_String("Snow"));
                var wind = c.Add(new LoadCaseComponent()); Canvas.Set(wind, 0, new GH_String("Wind"));
                var middle = Nodes(chain, new[] { m });
                GH_Component Load(GH_Component loadCase, double fz)
                {
                    var load = c.Add(new NodeLoadComponent());
                    Canvas.Link(load, 0, middle); Canvas.Link(load, 1, loadCase); Canvas.Set(load, 2, new GH_Vector(new Vector3d(0, 0, fz)));
                    return load;
                }
                var build = Build(chain, ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 2), Supports(chain, new[] { a, b2 }), Load(snow, -4), Load(wind, -2));
                var all = FormFind(chain, build, 0);
                var onlySnow = FormFind(chain, build, 0); Canvas.Link(Options(onlySnow), 3, snow);
                var both = FormFind(chain, build, 0); Canvas.Link(Options(both), 3, snow); Canvas.Link(Options(both), 3, wind);
                c.Solve();
                double Z(GH_Component ff) => Solved(ff).Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel))).Position.Z;
                Near(Z(all), -1.5, 1e-6, "All the loads");
                Near(Z(onlySnow), -1, 1e-6, "Snow only");
                Near(Z(both), -1.5, 1e-6, "Snow and wind");
            }
        });

        Test("Plate weight: membrane (g/m²) and steel (density x thickness) on the generated plates, balanced by the reactions", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(2, 4000, 0);
                var build = Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border));
                var membrane = c.Add(new MaterialMembraneComponent());
                Canvas.Set(membrane, 0, new GH_String("PVC/PES")); Canvas.Set(membrane, 1, new GH_Number(1050));
                Canvas.Set(membrane, 2, new GH_Number(4200)); Canvas.Set(membrane, 3, new GH_Number(4000));
                var steel = c.Add(new MaterialSteelComponent()); Canvas.Set(steel, 0, new GH_String("Steel")); Canvas.Set(steel, 1, new GH_Integer(0));
                GH_Component Find(GH_Component material)
                {
                    var ff = FormFind(chain, build, 0, 100, 1e-10, ModelModel.GRAVITY_mmSuS2);
                    if (material != null)
                    {
                        var property = c.Add(new PlatePropertyComponent());
                        Canvas.Set(property, 0, new GH_String("Plates")); Canvas.Set(property, 1, new GH_Number(1)); Canvas.Link(property, 2, material);
                        Canvas.Link(Options(ff), 10, property);
                    }
                    return ff;
                }
                var bare = Find(null); var fabric = Find(membrane); var plate = Find(steel);
                c.Solve();
                double Reaction(GH_Component ff) => Solved(ff).Nodes.SelectMany(n => n.Results.OfType<NodeReactResultModel>()).Sum(r => r.Force.Z);
                double Area(GH_Component ff) => AreaMassProperties.Compute(Canvas.Single<GH_Mesh>(ff, 1).Value).Area;
                double Sag(GH_Component ff) => Solved(ff).Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel))).Position.Z;
                // 1050 g/m² = 1.05e-9 t/mm² and steel 7.849e-9 t/mm³ x 1 mm, times g, on the final area
                double fabricWeight = 1.05e-9 * ModelModel.GRAVITY_mmSuS2 * Area(fabric);
                double steelWeight = 7.849e-9 * ModelModel.GRAVITY_mmSuS2 * Area(plate);
                Check(Math.Abs(Reaction(fabric) - Reaction(bare) - fabricWeight) < 1e-4 * fabricWeight, $"Membrane: reactions {Reaction(fabric) - Reaction(bare)} N, weight {fabricWeight} N");
                Check(Math.Abs(Reaction(plate) - Reaction(bare) - steelWeight) < 1e-4 * steelWeight, $"Steel: reactions {Reaction(plate) - Reaction(bare)} N, weight {steelWeight} N");
                Check(Sag(plate) < Sag(fabric) && Sag(fabric) < Sag(bare) && Sag(bare) < 0, $"Sags {Sag(bare)}, {Sag(fabric)}, {Sag(plate)}");
            }
        });

        Test("Membrane Material: data sheet units to N and mm; Plate Property saves material and thickness", () =>
        {
            using (var c = new Canvas())
            {
                var membrane = c.Add(new MaterialMembraneComponent());
                Canvas.Set(membrane, 0, new GH_String("PVC/PES")); Canvas.Set(membrane, 1, new GH_Number(1050));
                Canvas.Set(membrane, 2, new GH_Number(4200)); Canvas.Set(membrane, 3, new GH_Number(4000)); Canvas.Set(membrane, 4, new GH_Number(600));
                var property = c.Add(new PlatePropertyComponent());
                Canvas.Set(property, 0, new GH_String("Fabric")); Canvas.Set(property, 1, new GH_Number(0.8)); Canvas.Link(property, 2, membrane);
                c.Solve();
                var material = Canvas.Single<Arachne.Grasshopper.DataTypes.GH_Material>(membrane).Value;
                Check(material.Kind == MaterialModel.MaterialKind.Membrane && Math.Abs(material.AreaDensity - 1.05e-9) < 1e-18, "Weight " + material.AreaDensity);
                Check(material.StrengthWarp == 84 && material.StrengthWeft == 80 && material.StiffnessWarp == 600 && material.StiffnessWeft == 0, "Strengths and stiffnesses");
                // FeMM wrote the material of a plate property but read back only its name
                var chunk = new GH_LooseChunk("check");
                Check(Canvas.Single<GH_PlateProperty>(property).Write(chunk.CreateChunk("property")), "Write failed");
                var read = new GH_LooseChunk("check"); read.Deserialize_Binary(chunk.Serialize_Binary());
                var restored = new GH_PlateProperty();
                Check(restored.Read(read.FindChunk("property")), "Read failed");
                Check(restored.Value.Name == "Fabric" && restored.Value.Thickness == 0.8 && restored.Value.Material.StrengthWarp == 84, "Restored " + restored.Value);
            }
        });

        Test("Generic section: the beam preview is the circle of the same area", () =>
        {
            using (var c = new Canvas())
            {
                var section = c.Add(new BeamSectionGenericComponent()); Canvas.Set(section, 0, new GH_Number(100 * Math.PI));
                var material = c.Add(new MaterialSteelComponent()); Canvas.Set(material, 0, new GH_String("Steel")); Canvas.Set(material, 1, new GH_Integer(0));
                var property = c.Add(new BeamPropertyComponent()); Canvas.Set(property, 0, new GH_String("Cable")); Canvas.Set(property, 1, new GH_Integer(1));
                Canvas.Link(property, 2, section); Canvas.Link(property, 3, material);
                var beam = c.Add(new BeamComponent()); Canvas.Set(beam, 0, new GH_Curve(new LineCurve(Point3d.Origin, new Point3d(1000, 0, 0)))); Canvas.Link(beam, 1, property);
                c.Solve();
                var shapes = Canvas.Single<GH_Beam>(beam).Value.Shapes;
                Check(shapes != null && shapes.Length == 1, "No preview");
                var box = shapes[0].GetBoundingBox(true);
                var sizes = new[] { box.Max.X - box.Min.X, box.Max.Y - box.Min.Y, box.Max.Z - box.Min.Z }.OrderBy(v => v).ToArray();
                // FeMM scaled the radius by 0.001: a 20 mm cable was drawn 0.02 mm thick
                Check(Math.Abs(sizes[0] - 20) < 1e-6 && Math.Abs(sizes[1] - 20) < 1e-6 && Math.Abs(sizes[2] - 1000) < 1e-6, "Preview " + string.Join(" x ", sizes));
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
                Canvas.Set(keystone, 5, new GH_Number(1)); Canvas.Set(keystone, 6, new GH_Number(3));
                // Without coordinate system the load is global
                var projected = c.Add(new BeamDistributedLoadComponent());
                Canvas.Link(projected, 0, inclined); Canvas.Link(projected, 1, chain.LoadCase); Canvas.Set(projected, 2, new GH_Integer(0));
                Canvas.Set(projected, 4, new GH_Integer(7));
                Canvas.Set(projected, 5, new GH_Number(1));
                var build = Build(chain, keystone, projected);
                c.Solve();
                Check(!Canvas.HasErrors(keystone) && !Canvas.HasErrors(projected), Canvas.Messages(keystone) + Canvas.Messages(projected));
                var ff = FormFindingHelper.AssemblyModel(Canvas.Single<GH_Model>(build).Value, Plane.WorldXY, 0, new SolverOptionsModel(), out _);
                var k = ff.Beams.Single(x => Math.Abs(x.Length - 1) < 1e-9);
                var p = ff.Beams.Single(x => Math.Abs(x.Length - Math.Sqrt(2)) < 1e-9);
                // Keystone 1 -> 3 on 1 mm: resultant 2 at 7/12 of the span
                Check(Math.Abs(k.Load.Z + 2) < 1e-9 && Math.Abs(k.LoadPos - 7.0 / 12) < 1e-9, $"Keystone {k.Load.Z} at {k.LoadPos}");
                // Uniform 1 on the horizontal projection of a beam at 45 degrees: resultant 1 at the middle
                Check(Math.Abs(p.Load.Z + 1) < 1e-9 && Math.Abs(p.LoadPos - Math.Sqrt(2) / 2) < 1e-9, $"Projected {p.Load.Z} at {p.LoadPos}");
            }
        });

        Test("Beam point load: lever rule on the input geometry also when the beam stretches (z = -2.5)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var loaded = Beams(chain, new[] { new Line(a, m) });
                var point = c.Add(new BeamPointLoadComponent());
                Canvas.Link(point, 0, loaded); Canvas.Link(point, 1, chain.LoadCase); Canvas.Set(point, 5, new GH_Number(-1)); Canvas.Set(point, 6, new GH_Number(0.5));
                var cables = ForceDensity(chain, point, 0.1);
                var other = ForceDensity(chain, Beams(chain, new[] { new Line(m, b2) }), 0.1);
                var ff = FormFind(chain, Build(chain, cables, other, Supports(chain, new[] { a, b2 })), 0, 100, 1e-10);
                var reactions = Analysis(chain, new SupportReactionsComponent(), ff);
                c.Solve();
                Check(!Canvas.HasErrors(point), Canvas.Messages(point));
                var node = Solved(ff).Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel)));
                // Half of the load on the free node: 2 q z = -0.5, whatever the beam length becomes
                Check(Math.Abs(node.Position.Z + 2.5) < 1e-6, "Loaded node at " + node.Position);
                Check(Math.Abs(Canvas.Single<GH_Vector>(reactions, 3).Value.Z - 1) < 1e-9, "Vertical reaction " + Canvas.Single<GH_Vector>(reactions, 3).Value);
            }
        });

        Test("Pressure acts along the plane normal whatever the order of the beams", () =>
        {
            double Lift(bool reversed)
            {
                using (var c = new Canvas())
                {
                    var chain = Start(c); var (lines, border) = SaddleGrid(4, 10000, 2000);
                    if (reversed) { lines.Reverse(); lines = lines.Select(l => new Line(l.To, l.From)).ToList(); }
                    var wind = c.Add(new LoadCaseComponent()); Canvas.Set(wind, 0, new GH_String("Wind"));
                    var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border)), 0, 100, 1e-8);
                    Canvas.Set(Options(ff), 1, new GH_Number(1e-4)); Canvas.Link(Options(ff), 2, wind);
                    c.Solve();
                    var model = Solved(ff);
                    Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count == 16, "Mesh faces");
                    return model.Nodes.SelectMany(n => n.Loads.OfType<NodeForceLoadModel>()).Sum(l => l.Value.Z);
                }
            }
            double straight = Lift(false), reversed = Lift(true);
            Check(straight > 0 && Math.Abs(straight - reversed) < 1e-6 * straight, $"Pressure resultant {straight} N, with the beams reversed {reversed} N");
        });

        Test("Same shape far from the origin (site coordinates in mm)", () =>
        {
            List<Point3d> Shape(double offset)
            {
                using (var c = new Canvas())
                {
                    var chain = Start(c); var (lines, border) = SaddleGrid(4, 10000, 2000, offset);
                    var wind = c.Add(new LoadCaseComponent()); Canvas.Set(wind, 0, new GH_String("Wind"));
                    var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border)), 0);
                    Canvas.Set(Options(ff), 1, new GH_Number(1e-4)); Canvas.Link(Options(ff), 2, wind);
                    c.Solve();
                    return Solved(ff).Nodes.Select(n => n.Position - new Vector3d(offset, 0, 0)).ToList();
                }
            }
            var near = Shape(0); var far = Shape(1e6);
            double gap = near.Zip(far, (p, q) => p.DistanceTo(q)).Max();
            Check(near.Count == far.Count && gap < 1e-3, "Largest difference " + gap + " mm");
        });

        Test("Plates also when the first beam is a backstay, and for every net", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var g1 = SaddleGrid(2, 4000, 1000); var g2 = SaddleGrid(2, 4000, 1000, 10000);
                // A backstay from a corner first, then a cable joining the two nets
                var lines = new List<Line> { new Line(new Point3d(0, 0, 1000), new Point3d(-3000, 0, 0)), new Line(new Point3d(4000, 2000, 0), new Point3d(10000, 2000, 0)) };
                lines.AddRange(g1.lines); lines.AddRange(g2.lines);
                var supports = g1.border.Concat(g2.border).Concat(new[] { new Point3d(-3000, 0, 0) });
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, supports)), 0);
                c.Solve();
                Solved(ff);
                Check(Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count == 8, "Mesh faces " + Canvas.Single<GH_Mesh>(ff, 1).Value.Faces.Count);
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

        Test("Support reactions balance the node loads and the pressure (saddle in mm)", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(4, 10000, 2000);
                var wind = c.Add(new LoadCaseComponent()); Canvas.Set(wind, 0, new GH_String("Wind"));
                var top = NodeLoad(chain, Nodes(chain, new[] { new Point3d(5000, 5000, 0) }), new Vector3d(100, 0, -1000));
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border), top), 0, 100, 1e-10);
                Canvas.Set(Options(ff), 1, new GH_Number(1e-4)); Canvas.Link(Options(ff), 2, wind);
                var reactions = Analysis(chain, new SupportReactionsComponent(), ff);
                c.Solve();
                var model = Solved(ff);
                Check(Values<Vector3d>(reactions, 1).Count == border.Count && Canvas.Out(reactions, 2).Count == border.Count, "Reactions " + Canvas.Out(reactions, 1).Count);
                var loads = Vector3d.Zero;
                foreach (var node in model.Nodes)
                    foreach (var load in node.Loads.OfType<NodeForceLoadModel>())
                        loads += load.Value;
                var total = Canvas.Single<GH_Vector>(reactions, 3).Value;
                Check(loads.Length > 1000 && (total + loads).Length < 1e-6 * loads.Length, $"Reactions {total} do not balance the loads {loads}");
            }
        });

        Test("Self-weight: same sag and reactions in N/mm and in SI", () =>
        {
            // A 10 mm steel cable on two 1 m spans, q = 0.1 N/mm = 100 N/m
            (double sag, Vector3d total, double weight) Run(ModelModel.ModelUnits units, double metres)
            {
                using (var c = new Canvas())
                {
                    var chain = Start(c, "FF", 10 * metres);
                    var p0 = new Point3d(0, 0, 0); var p1 = new Point3d(1000 * metres, 0, 0); var p2 = new Point3d(2000 * metres, 0, 0);
                    var beams = ForceDensity(chain, Beams(chain, new[] { new Line(p0, p1), new Line(p1, p2) }), 0.1 / metres);
                    double gravity = ModelModel.GRAVITY_mmSuS2 * metres;
                    var ff = FormFind(chain, Build(chain, units, beams, Supports(chain, new[] { p0, p2 })), 0, 100, 1e-12, gravity);
                    var reactions = Analysis(chain, new SupportReactionsComponent(), ff);
                    c.Solve();
                    var model = Solved(ff);
                    double length = model.Beams.Sum(beam => beam.PointFrom.DistanceTo(beam.PointTo)) / metres;
                    var property = model.Beams[0].BeamProperty;
                    // Weight per mm: area in mm2, density in t/mm3 (Arachne default units), g in mm/s2
                    double weight = property.SectionArea / (metres * metres) * 7.849E-9 * ModelModel.GRAVITY_mmSuS2 * length;
                    double sag = model.Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel))).Position.Z / metres;
                    return (sag, Canvas.Single<GH_Vector>(reactions, 3).Value, weight);
                }
            }
            var nmm = Run(ModelModel.ModelUnits.Nmm, 1);
            var si = Run(ModelModel.ModelUnits.SI, 0.001);
            Check(nmm.sag < -25 && nmm.sag > -35, "Sag " + nmm.sag + " mm");
            Check(Math.Abs(nmm.sag - si.sag) < 1e-6 * Math.Abs(nmm.sag), $"Sag {nmm.sag} mm in N/mm, {si.sag} mm in SI");
            Check(Math.Abs(nmm.total.Z - nmm.weight) < 1e-6 * nmm.weight, $"Vertical reaction {nmm.total.Z} N, weight {nmm.weight} N");
            Check((nmm.total - si.total).Length < 1e-6 * nmm.weight, $"Reactions {nmm.total} N in N/mm, {si.total} N in SI");
        });

        Test("Beam Forces: axial forces, force densities and lengths of the solved model", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(4, 10000, 2000);
                var ff = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, lines), 2), Supports(chain, border)), 0);
                var forces = Analysis(chain, new BeamForcesComponent(), ff);
                c.Solve();
                Solved(ff);
                var n = Values<double>(forces, 1); var q = Values<double>(forces, 2); var l = Values<double>(forces, 3);
                Check(Canvas.Out(forces, 0).Count == 40 && n.Count == 40 && q.Count == 40 && l.Count == 40 && Canvas.Out(forces, 4).Count == 40, "Output counts");
                Check(q.All(v => Math.Abs(v - 2) < 1e-9), "Force densities " + string.Join(", ", q.Take(3)));
                Check(n.Zip(l, (force, length) => Math.Abs(force - 2 * length) < 1e-6).All(ok => ok), "N differs from q L");
            }
        });

        Test("Beam Forces and Support Reactions warn on a model not form found", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var build = Build(chain, ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 1), Supports(chain, new[] { a, b2 }));
                var forces = Analysis(chain, new BeamForcesComponent(), build);
                var reactions = Analysis(chain, new SupportReactionsComponent(), build);
                c.Solve();
                Check(forces.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0 && Canvas.Out(forces, 1).Count == 0, "Beam Forces without warning");
                Check(reactions.RuntimeMessages(GH_RuntimeMessageLevel.Warning).Count > 0 && Canvas.Out(reactions, 1).Count == 0, "Support Reactions without warning");
            }
        });

        Test("Pre-load also on the beams with a fixed axial force only, in its load case", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var cables = c.Add(new LoadCaseComponent()); Canvas.Set(cables, 0, new GH_String("Cables"));
                var axial = c.Add(new FixedAxialForceComponent());
                Canvas.Link(axial, 0, Beams(chain, new[] { new Line(a, m), new Line(m, b2) })); Canvas.Link(axial, 1, cables); Canvas.Set(axial, 2, new GH_Number(4));
                var ff = FormFind(chain, Build(chain, axial, Supports(chain, new[] { a, b2 }), NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0, 0, -4))), 2, 200, 1e-8);
                c.Solve();
                var model = Solved(ff);
                Check(model.Beams.All(beam => beam.Loads.OfType<BeamPreLoadModel>().Count() == 1 && beam.Loads.OfType<BeamPreLoadModel>().Single().LoadCase.Name == "Cables"),
                    "Pre-load missing or in another load case");
                Check(model.Beams.All(beam => Math.Abs(PreLoad(beam) - 4) < 1e-2), "Axial forces " + string.Join(", ", model.Beams.Select(PreLoad)));
            }
        });

        Test("A second form finding replaces the reactions of the first one", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var first = FormFind(chain, Build(chain, ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 2), Supports(chain, new[] { a, b2 }),
                    NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0, 0, -4))), 0);
                var second = chain.Canvas.Add(new FormFindComponent());
                Canvas.Link(second, 0, first); Canvas.Set(second, 4, new GH_Boolean(true));
                c.Solve();
                var model = Solved(second);
                Check(model.Nodes.All(n => n.Results.OfType<NodeReactResultModel>().Count() == (n.HasAttribute(typeof(NodeRestraintAttributeModel)) ? 1 : 0)), "Stale reactions");
                Check(model.Beams.All(beam => beam.Loads.OfType<BeamPreLoadModel>().Count() == 1), "Stale pre-loads");
            }
        });

        Test("Check Model: valid saddle, missing supports, unsupported part, beams without force", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(2, 4000, 1000); var other = SaddleGrid(2, 4000, 1000, 10000);
                GH_Component CheckOf(params GH_Component[] elements) => Analysis(chain, new CheckModelComponent(), Build(chain, elements));
                var valid = CheckOf(ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border));
                var noSupports = CheckOf(ForceDensity(chain, Beams(chain, lines), 1));
                var freePart = CheckOf(ForceDensity(chain, Beams(chain, lines.Concat(other.lines)), 1), Supports(chain, border));
                var noForce = CheckOf(ForceDensity(chain, Beams(chain, lines), 1), Beams(chain, new[] { new Line(new Point3d(0, 0, 5000), new Point3d(1000, 0, 5000)) }), Supports(chain, border));
                c.Solve();
                bool Valid(GH_Component check) => Canvas.Single<GH_Boolean>(check, 0).Value;
                int Parts(GH_Component check) => Canvas.Single<GH_Integer>(check, 3).Value;
                List<string> Texts(GH_Component check, int output) => Canvas.Out(check, output).Select(text => text.ToString()).ToList();
                string All(GH_Component check) => string.Join(" | ", Texts(check, 1).Concat(Texts(check, 2)));
                Check(Valid(valid) && Texts(valid, 1).Count == 0 && Texts(valid, 2).Count == 0 && Parts(valid) == 1, "Valid model: " + All(valid));
                Check(!Valid(noSupports) && Texts(noSupports, 1).Any(e => e.StartsWith("No node is restrained")), "No supports: " + All(noSupports));
                Check(!Valid(freePart) && Texts(freePart, 1).Any(e => e.EndsWith("has no restraint along X, Y, Z.")) && Parts(freePart) == 2, "Unsupported part: " + All(freePart));
                Check(!Valid(noForce) && Texts(noForce, 1).Any(e => e.StartsWith("2 nodes have no beam that carries a force")) &&
                    Texts(noForce, 2).Any(w => w.StartsWith("1 beams have no force density")), "Beam without force: " + All(noForce));
            }
        });

        // A node joined to the supports (0, 0, 0) with q = 1 and (4, 0, 2) with q = 3, loaded by
        // (0, 0, -2): free, it would go to (3, 0, 1). The plane through (2, 1, 0) with X along
        // (1, -1, 0) has the normal Z along (1, 1, 1).
        var p0 = new Point3d(2, 1, 0);
        var inclined = new Plane(p0, new Vector3d(1, -1, 0), new Vector3d(1, 1, -2));
        GH_Component Pulled(Chain chain, bool tx, bool ty, bool tz, Plane? plane)
        {
            var anchor1 = new Point3d(0, 0, 0); var anchor2 = new Point3d(4, 0, 2);
            var soft = ForceDensity(chain, Beams(chain, new[] { new Line(anchor1, p0) }), 1);
            var stiff = ForceDensity(chain, Beams(chain, new[] { new Line(anchor2, p0) }), 3);
            var load = NodeLoad(chain, Nodes(chain, new[] { p0 }), new Vector3d(0, 0, -2));
            return Build(chain, soft, stiff, load, Supports(chain, new[] { anchor1, anchor2 }), Supports(chain, new[] { p0 }, tx, ty, tz, plane));
        }

        Test("Restraints along the axes of a plane: node on an inclined plane and on an inclined line, reactions along the restraints", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                // Plane: restrained along its normal only, the point (3, 0, 1) projected on it and
                // the reaction -4/3 (1, 1, 1). Line along the X axis of the plane: (3, 0, 0), reaction (0, 0, -4).
                var planeModel = Pulled(chain, false, false, true, inclined);
                var onPlane = FormFind(chain, planeModel, 0, 100, 1e-10);
                var onLine = FormFind(chain, Pulled(chain, false, true, true, inclined), 0, 100, 1e-10);
                var nonLinear = FormFind(chain, Pulled(chain, false, false, true, inclined), 2, 100, 1e-10);
                var check = Analysis(chain, new CheckModelComponent(), planeModel);
                c.Solve();
                Check(Canvas.Single<GH_Boolean>(check, 0).Value, "Check Model: " + string.Join(" | ", Canvas.Out(check, 1)));
                foreach (var (ff, expected, reaction, what) in new[] {
                    (onPlane, new Point3d(8.0 / 3, -1.0 / 3, 2.0 / 3), new Vector3d(-4.0 / 3, -4.0 / 3, -4.0 / 3), "Plane"),
                    (onLine, new Point3d(3, 0, 0), new Vector3d(0, 0, -4), "Line") })
                {
                    var node = Solved(ff).Nodes.Single(n => n.Loads.OfType<NodeForceLoadModel>().Any());
                    Check(node.Position.DistanceTo(expected) < 1e-6, $"{what}: node at {node.Position}, expected {expected}");
                    var result = node.Results.OfType<NodeReactResultModel>().Single().Force;
                    Check((result - reaction).Length < 1e-6, $"{what}: reaction {result}, expected {reaction}");
                    var total = Solved(ff).Nodes.SelectMany(n => n.Results.OfType<NodeReactResultModel>()).Aggregate(Vector3d.Zero, (sum, r) => sum + r.Force);
                    Check((total - new Vector3d(0, 0, 2)).Length < 1e-6, $"{what}: total reaction {total}");
                }
                // Non-linear: on the plane, with the reaction along its normal
                var moved = Solved(nonLinear).Nodes.Single(n => n.Loads.OfType<NodeForceLoadModel>().Any());
                Check(Math.Abs((moved.Position - p0) * inclined.ZAxis) < 1e-9, "Non-linear: off the plane " + moved.Position);
                var force = moved.Results.OfType<NodeReactResultModel>().Single().Force;
                Check(Vector3d.CrossProduct(force, inclined.ZAxis).Length < 1e-6 * force.Length && force.Length > 0.1, "Non-linear: reaction " + force);
            }
        });

        Test("Restraints along a plane turned about Z: the same result as along Z without plane", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                // The middle node is held along Z only and moves in the plane XY
                GH_Component Find(Plane? plane)
                {
                    var beams = ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 2);
                    var load = NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0.5, 0.25, -4));
                    return FormFind(chain, Build(chain, beams, load, Supports(chain, new[] { a, b2 }), Supports(chain, new[] { m }, false, false, true, plane)), 0, 100, 1e-10);
                }
                var global = Find(null);
                var turned = Find(new Plane(m, new Vector3d(Math.Cos(0.5), Math.Sin(0.5), 0), new Vector3d(-Math.Sin(0.5), Math.Cos(0.5), 0)));
                c.Solve();
                var first = Solved(global).Nodes.Select(n => n.Position).ToList();
                var second = Solved(turned).Nodes.Select(n => n.Position).ToList();
                Check(first.Count == 3 && first.Zip(second, (p, q) => p == q).All(same => same), "Different positions");
                Check(first.Any(p => Math.Abs(p.Y) > 0.01), "The middle node did not move in the plane XY");
            }
        });

        Test("Check Model: a part restrained only along an inclined direction can move along the others", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c); var (lines, border) = SaddleGrid(2, 4000, 1000);
                // Normal of the plane along (1, 0, 1): the part can move along (1, 0, -1) and Y
                var plane = new Plane(Point3d.Origin, new Vector3d(1, 0, -1), Vector3d.YAxis);
                var check = Analysis(chain, new CheckModelComponent(), Build(chain, ForceDensity(chain, Beams(chain, lines), 1), Supports(chain, border, false, false, true, plane)));
                c.Solve();
                var errors = Canvas.Out(check, 1).Select(text => text.ToString()).ToList();
                Check(!Canvas.Single<GH_Boolean>(check, 0).Value && errors.Any(e => e.EndsWith("has no restraint along (0.707, 0, -0.707), Y.")), "Errors: " + string.Join(" | ", errors));
            }
        });

        Test("Restraints with a plane survive a file round trip", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var restraints = Supports(chain, new[] { p0 }, false, true, true, inclined);
                c.Solve();
                var node = Canvas.Single<GH_Node>(restraints);
                var chunk = new GH_LooseChunk("check");
                Check(node.Write(chunk.CreateChunk("node")), "Write failed");
                var read = new GH_LooseChunk("check"); read.Deserialize_Binary(chunk.Serialize_Binary());
                var restored = new GH_Node();
                Check(restored.Read(read.FindChunk("node")), "Read failed");
                var before = NodeRestraintAttributeModel.GetRestraint(node.Value);
                var after = NodeRestraintAttributeModel.GetRestraint(restored.Value);
                Check(after != null && after.Equals(before) && !after.Tx && after.Ty && after.Tz && after.CoordinateSystem != null, "Restored " + after);
                var axes = after.Axes();
                Check((axes[2] - inclined.ZAxis).Length < 1e-12 && (axes[0] - inclined.XAxis).Length < 1e-12, "Axes " + string.Join(", ", axes));
            }
        });

        Test("Element ID names the elements; Node and Beam Filter find them by ID and position", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var named = c.Add(new ElementIdComponent()); Canvas.Link(named, 0, Nodes(chain, new[] { m })); Canvas.Set(named, 1, new GH_String("Top"));
                var edge = c.Add(new ElementIdComponent()); Canvas.Link(edge, 0, Beams(chain, new[] { new Line(a, m) })); Canvas.Set(edge, 1, new GH_String("Edge"));
                var build = Build(chain, ForceDensity(chain, edge, 1), ForceDensity(chain, Beams(chain, new[] { new Line(m, b2) }), 1), named, Supports(chain, new[] { a, b2 }));
                var decompose = c.Add(new DecomposeModelComponent()); Canvas.Link(decompose, 0, build);
                var byId = c.Add(new NodeFilterComponent()); Canvas.Link(byId, 0, decompose, 0); Canvas.Set(byId, 1, new GH_String("Top"));
                var byPosition = c.Add(new NodeFilterComponent()); Canvas.Link(byPosition, 0, decompose, 0); Canvas.Set(byPosition, 2, new GH_Point(a));
                // The filters search the IDs given with Element ID, not the automatic numbers (FeMM)
                var beamById = c.Add(new BeamFilterComponent()); Canvas.Link(beamById, 0, decompose, 1); Canvas.Set(beamById, 1, new GH_String("Edge"));
                c.Solve();
                Check(Canvas.Single<GH_Model>(build).Value.Nodes.Any(n => n.NodeId == "Top" && n.Position == m), "Node ID not assigned");
                Check(Canvas.Single<GH_Model>(build).Value.Beams.Any(b => b.BeamId == "Edge"), "Beam ID not assigned");
                Check(Canvas.Out(byId, 0).Count == 1 && Canvas.Out(byId, 1).Count == 2, "Node Filter by ID: " + Canvas.Messages(byId));
                Check(Canvas.Out(byPosition, 0).Count == 1 && ((GH_Node)Canvas.Out(byPosition, 0)[0]).Value.Position == a, "Node Filter by position: " + Canvas.Messages(byPosition));
                Check(Canvas.Out(beamById, 0).Count == 1 && Canvas.Out(beamById, 1).Count == 1, "Beam Filter by ID: " + Canvas.Messages(beamById));
            }
        });

        Test("Form Find keeps one result per model", () =>
        {
            using (var c = new Canvas())
            {
                var chain = Start(c);
                var low = Build(chain, ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 1), Supports(chain, new[] { a, b2 }),
                    NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0, 0, -1)));
                var high = Build(chain, ForceDensity(chain, Beams(chain, new[] { new Line(a, m), new Line(m, b2) }), 1), Supports(chain, new[] { a, b2 }),
                    NodeLoad(chain, Nodes(chain, new[] { m }), new Vector3d(0, 0, -2)));
                var ff = FormFind(chain, low, 0);
                Canvas.Link(ff, 0, high);
                c.Solve();
                var models = Canvas.Out(ff, 0).Cast<GH_Model>().ToList();
                Check(models.Count == 2 && Canvas.Out(ff, 1).Count == 2, "Results " + models.Count);
                double Sag(GH_Model model) => model.Value.Nodes.Single(n => !n.HasAttribute(typeof(NodeRestraintAttributeModel))).Position.Z;
                Check(Math.Abs(Sag(models[0]) + 0.5) < 1e-6 && Math.Abs(Sag(models[1]) + 1) < 1e-6, $"Sags {Sag(models[0])}, {Sag(models[1])}");
            }
        });

        Console.WriteLine("Form finding checks: " + passed + " passed, " + failed + " failed.");
        return failed;
    }
}
