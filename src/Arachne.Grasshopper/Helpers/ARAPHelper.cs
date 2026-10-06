using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Arap;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Helpers
{
    // Rhino/GPC conversion only. All ARAP calculation lives in Arachne.Arap.
    internal static class ARAPHelper
    {
        internal static ArapMesh ToArap(Mesh mesh)
        {
            if (mesh == null || !mesh.IsValid || mesh.Faces.Count == 0 || mesh.Faces.QuadCount != 0)
                throw new ArgumentException("ARAP requires a valid triangulated mesh.");
            return new ArapMesh(mesh.Vertices.ToPoint3dArray().Select(p => new GPC.Geometry.Point3d(p.X, p.Y, p.Z)),
                mesh.Faces.SelectMany(f => new[] { f.A, f.B, f.C }));
        }
        private static ArapResult Apply(Mesh output, ArapResult result)
        {
            if (!result.Success) return result;
            var vertices = result.GetVertices();
            if (output.Vertices.Count != vertices.Length) throw new ArgumentException("ARAP output vertex count differs from the input.");
            output.Vertices.UseDoublePrecisionVertices = true;
            for (int i = 0; i < vertices.Length; i++) output.Vertices.SetVertex(i, vertices[i].X, vertices[i].Y, vertices[i].Z);
            return result;
        }
        internal static ArapResult Flattening(Mesh input, Mesh output, ArapOptions options)
            => Apply(output, ArapSolver.Flatten(ToArap(input), options));
        internal static ArapResult Deform(Mesh input, Mesh output, List<int> indices, List<Point3d> positions, bool unwrap, ArapOptions options)
        {
            if (indices.Count != positions.Count) throw new ArgumentException("Each pin requires a position.");
            var pins = indices.Select((id, i) => new ArapPin(id, new GPC.Geometry.Point3d(positions[i].X, positions[i].Y, unwrap ? 0 : positions[i].Z))).ToArray();
            var mesh = ToArap(input);
            return Apply(output, unwrap ? ArapSolver.Unwrap(mesh, pins, options) : ArapSolver.Deform(mesh, pins, options));
        }
        internal static void SetDiagnostics(IGH_DataAccess data, ArapResult result, bool perimeter)
        {
            data.SetData(1, result.Status.ToString());
            data.SetDataList(2, new[] { result.Message, "ARAP C# iterations: " + result.History.Count,
                "Perimeter passes: " + result.PerimeterIterations,
                "Maximum normalized linear residual: " + (result.History.Count == 0 ? 0 : result.History.Max(h => h.LinearResidual)).ToString("G6") });
            data.SetDataList(3, result.History.Select(h => h.Energy));
            if (result.Success) data.SetData(4, result.MaxRelativeEdgeError);
            if (perimeter && (result.Success || result.Status == ArapStatus.PerimeterLimit)) data.SetData(5, result.MaxPerimeterError);
        }
        internal static GH_RuntimeMessageLevel FailureLevel(ArapResult result)
            => result.Status == ArapStatus.IterationLimit || result.Status == ArapStatus.PerimeterLimit || result.Status == ArapStatus.Cancelled
                ? GH_RuntimeMessageLevel.Warning : GH_RuntimeMessageLevel.Error;
    }
}
