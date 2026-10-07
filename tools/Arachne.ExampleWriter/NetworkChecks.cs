using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Grasshopper.Components.Patterning;
using Arachne.Grasshopper.Geometry;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

// Cable nets generated on known perimeters: counts of cables, perimeter pieces and points,
// with and without the matching tolerance.
internal static class NetworkChecks
{
    private static int passed, failed;

    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("NETWORK PASS " + name); }
        catch (Exception e) { failed++; Console.WriteLine("NETWORK FAIL " + name + ": " + e.GetBaseException().Message); }
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }

    // Saddle perimeter 8 x 6 with corners at +/-0.8
    private static List<Curve> Rectangle() => new PolylineCurve(new[]
        { new Point3d(0, 0, 0.8), new Point3d(8, 0, -0.8), new Point3d(8, 6, 0.8), new Point3d(0, 6, -0.8), new Point3d(0, 0, 0.8) })
        .DuplicateSegments().ToList();

    // Circle of radius 4 drawn with 16 sides
    private static List<Curve> Circle() => new PolylineCurve(Enumerable.Range(0, 17)
        .Select(i => new Point3d(4 * Math.Cos(2 * Math.PI * i / 16), 4 * Math.Sin(2 * Math.PI * i / 16), 0)))
        .DuplicateSegments().ToList();

    internal static int Run()
    {
        passed = failed = 0;
        var none = new List<Curve>();

        foreach (double matching in new[] { 0, 1e-5 })
            Test("Rectangular net, spacing 2: every cable reaches the perimeter, matching " + matching, () =>
            {
                var border = Rectangle();
                CableNetHelper.CreateCableNet2(border, none, border, 2, 2, Plane.WorldXY,
                    out var warp, out var weft, out _, out var fixedEdges, out _, out _, out var interior, 1e-6, matching);
                // 3 warp lines in 3 pieces, 2 weft lines in 4 pieces
                Check(warp.Sum(b => b.Count) == 9 && weft.Sum(b => b.Count) == 8, $"Cable pieces: warp {warp.Sum(b => b.Count)}, weft {weft.Sum(b => b.Count)}");
                // Perimeter split at the 10 cable ends plus the 4 corners; 3 x 2 inner crossings
                Check(fixedEdges.Sum(b => b.Count) == 14 && interior.Count == 6, $"Perimeter pieces {fixedEdges.Sum(b => b.Count)}, inner points {interior.Count}");
                foreach (var p in new[] { new Point3d(2, 0, 0.4), new Point3d(2, 6, -0.4), new Point3d(0, 2, 0.8 / 3), new Point3d(8, 2, -0.8 / 3) })
                    Check(warp.Concat(weft).SelectMany(b => b).Any(c => c.PointAtStart.DistanceTo(p) < 1e-6 || c.PointAtEnd.DistanceTo(p) < 1e-6), "No cable reaches the perimeter at " + p);
            });

        foreach (double matching in new[] { 0, 1e-5 })
            Test("Radial net, 16 spokes and 3 rings: one centre point, matching " + matching, () =>
            {
                var border = Circle();
                CableNetHelper.CreateRadialCableNet2(border, none, border, 16, 1, Plane.WorldXY,
                    out var warp, out var weft, out _, out var fixedEdges, out _, out _, out var interior, 1e-6, matching);
                Check(warp.Sum(b => b.Count) == 64, "Spoke pieces " + warp.Sum(b => b.Count) + " instead of 16 x 4");
                Check(weft.Sum(b => b.Count) == 48, "Ring pieces " + weft.Sum(b => b.Count) + " instead of 3 x 16, without chords across the border");
                Check(fixedEdges.Sum(b => b.Count) == 16, "Perimeter pieces " + fixedEdges.Sum(b => b.Count));
                // 3 rings x 16 + the centre once: a duplicated centre would multiply a load placed there
                Check(interior.Count == 49, "Inner points " + interior.Count);
            });

        foreach (double matching in new[] { 0, 1e-5 })
            Test("Compound net: the perimeter is split only at the cable ends, matching " + matching, () =>
            {
                var border = Rectangle().Select(b => new GH_Curve(b)).ToArray();
                var c = new CompoundNetComponent();
                Canvas.Set(c, 0, border); Canvas.Set(c, 2, border);
                Canvas.Set(c, 3, new GH_Number(2)); Canvas.Set(c, 4, new GH_Number(2)); Canvas.Set(c, 5, new GH_Number(0));
                Canvas.Set(c, 6, new GH_Point(Point3d.Origin)); Canvas.Set(c, 8, new GH_Number(1e-6)); Canvas.Set(c, 9, new GH_Number(matching));
                using (var canvas = new Canvas())
                {
                    canvas.Add(c);
                    canvas.Solve();
                    Check(!Canvas.HasErrors(c), Canvas.Messages(c));
                    Check(c.Params.Output[0].VolatileDataCount == 14, "Perimeter pieces " + c.Params.Output[0].VolatileDataCount);
                }
            });

        Console.WriteLine("Network checks: " + passed + " passed, " + failed + " failed.");
        return failed;
    }
}
