using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Display;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{

    public class DecompensationComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public DecompensationComponent()
          : base("Decompensation", "DC", "Decompensate the flatten patches", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The flatten panel to decompensate", GH_ParamAccess.list);
            pManager.AddGenericParameter("Descriptors", "D", "The decompensation descriptors for each curve", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The decompensated panel curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            var curves = new List<GH_Curve>();
            var objects = new List<object>();
            if (!DA.GetDataList(0, curves) || !DA.GetDataList(1, objects)) return;
            if (curves.Count == 0 || curves.Count != objects.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The number of curves and descriptors must match");
                return;
            }

            try
            {
                // Preserve the legacy common centre, computed from the original curve ends.
                var center = Point3d.Origin;
                foreach (var item in curves) center += item.Value.PointAtStart + item.Value.PointAtEnd;
                center /= 2 * curves.Count;
                var output = new List<PolylineCurve>();
                for (int i = 0; i < curves.Count; i++)
                {
                    var value = objects[i] is GH_ObjectWrapper wrapper ? wrapper.Value : objects[i];
                    var descriptor = value as DecompensationDescriptor;
                    if (descriptor == null || descriptor.Points.Count == 0)
                        throw new ArgumentException("Each curve requires a non-empty decompensation descriptor.");
                    if (!curves[i].Value.TryGetPolyline(out Polyline polyline))
                        throw new ArgumentException("Decompensation requires polylines or lines; convert other curves explicitly.");
                    double length = polyline.Length;
                    if (!IsFinite(length) || length <= 0) throw new ArgumentException("Curve length must be positive.");

                    // Convert into a private list. A descriptor can be shared by curves of different lengths.
                    var points = new List<DecompensationDescriptor.DecompensationPoint>();
                    foreach (var point in descriptor.Points)
                    {
                        double s = point.IsNormalized ? point.Length : point.Length / length;
                        if (s < 0) s += 1;
                        if (!IsFinite(s) || s < 0 || s > 1 || !IsFinite(point.Warp) || !IsFinite(point.Weft))
                            throw new ArgumentException("Descriptor positions must lie on the curve and scales must be finite.");
                        points.Add(new DecompensationDescriptor.DecompensationPoint(true, s, point.Warp, point.Weft));
                    }
                    points.Sort((a, b) => a.Length.CompareTo(b.Length));
                    for (int j = points.Count - 1; j > 0; j--)
                    {
                        if (points[j].Length != points[j - 1].Length) continue;
                        if (points[j].Warp != points[j - 1].Warp || points[j].Weft != points[j - 1].Weft)
                            throw new ArgumentException("Conflicting scales at the same descriptor position.");
                        points.RemoveAt(j);
                    }

                    // Sample both original corners and descriptor breakpoints before moving any vertex.
                    var samples = new SortedSet<double> { 0, 1 };
                    double accumulated = 0;
                    for (int j = 1; j < polyline.Count - 1; j++)
                    {
                        accumulated += polyline[j - 1].DistanceTo(polyline[j]);
                        samples.Add(accumulated / length);
                    }
                    foreach (var point in points) samples.Add(point.Length);
                    using (var original = new PolylineCurve(polyline))
                    {
                        var result = new List<Point3d>();
                        foreach (double s in samples)
                        {
                            var scale = Interpolate(s, points);
                            Point3d p = original.PointAtNormalizedLength(s);
                            p.X = center.X + (p.X - center.X) * scale.Item1;
                            p.Y = center.Y + (p.Y - center.Y) * scale.Item2;
                            if (!p.IsValid) throw new ArgumentException("Decompensation produced non-finite coordinates.");
                            result.Add(p);
                        }
                        output.Add(new PolylineCurve(result));
                    }
                }
                DA.SetDataList(0, output);
            }
            catch (ArgumentException error)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.Message);
            }
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static (double, double) Interpolate(double s, List<DecompensationDescriptor.DecompensationPoint> points)
        {
            if (s <= points[0].Length) return (points[0].Warp, points[0].Weft);
            for (int i = 1; i < points.Count; i++)
            {
                if (s > points[i].Length) continue;
                var a = points[i - 1];
                var b = points[i];
                double t = (s - a.Length) / (b.Length - a.Length);
                return (a.Warp + t * (b.Warp - a.Warp), a.Weft + t * (b.Weft - a.Weft));
            }
            var last = points[points.Count - 1];
            return (last.Warp, last.Weft);
        }
        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("CompensationIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("9bcc0da9-b5e3-5b52-9801-47c48ae166f1");
    }
}