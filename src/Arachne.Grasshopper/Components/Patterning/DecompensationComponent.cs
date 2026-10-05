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
            List<GH_Curve> curves = new List<GH_Curve>();
            List<object> objects = new List<object>();
            List<DecompensationDescriptor> descriptors = new List<DecompensationDescriptor>();

            if (!DA.GetDataList(0, curves))
                return;
            if (!DA.GetDataList(1, objects))
                return;

            for (int i = 0; i < objects.Count; i++)
            {
                var descriptor = (objects[i] as GH_Goo<object>).Value as DecompensationDescriptor;
                if (descriptor != null)
                {
                    descriptors.Add(descriptor);
                }
            }

            if (curves.Count != descriptors.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The number of curves and descriptors must match");
                return;
            }

            List<PolylineCurve> outCurves = new List<PolylineCurve>();

            var center = new Point2d(0, 0);
            for (int i = 0; i < curves.Count; i++)
            {
                center.X = center.X + curves[i].Value.PointAtStart.X;
                center.Y = center.Y + curves[i].Value.PointAtStart.Y;
                center.X = center.X + curves[i].Value.PointAtEnd.X;
                center.Y = center.Y + curves[i].Value.PointAtEnd.Y;
            }

            center.X = center.X / (2 * curves.Count);
            center.Y = center.Y / (2 * curves.Count);

            for (int i = 0; i < curves.Count; i++)
            {
                var curve = new PolylineCurve(curves[i].Value as PolylineCurve);
                var descriptor = descriptors[i];

                var curveLength = curve.GetLength();

                for (int j = 0; j < descriptor.Points.Count; j++)
                {
                    var descriptorPoint = descriptor.Points[j];
                    if (!descriptorPoint.IsNormalized)
                    {
                        descriptorPoint.Length = descriptorPoint.Length / curveLength;
                        descriptorPoint.IsNormalized = true;
                    }

                    if (descriptorPoint.Length < 0)
                    {
                        descriptorPoint.Length = 1 + descriptorPoint.Length;
                    }

                    var param = 0.0;
                    curve.NormalizedLengthParameter(descriptorPoint.Length, out param);
                    var cc = curve.Split(param);
                    if (cc != null && cc.Length > 1)
                    {
                        curve = Curve.JoinCurves(cc)[0] as PolylineCurve;
                    }

                    // TODO: check that the curve is not flipped
                }

                outCurves.Add(curve);
                descriptors[i] = descriptor;
            }

            for (int i = 0; i < outCurves.Count; i++)
            {
                for (int j = 0; j < outCurves[i].PointCount; j++)
                {
                    var nl = GetPointNormalizedLength(outCurves[i], j);

                    var (warp, weft) = GetDecompensation(nl, descriptors[i]);

                    var pt = outCurves[i].Point(j);
                    pt.X = center.X + (pt.X - center.X) * warp;
                    pt.Y = center.Y + (pt.Y - center.Y) * weft;
                    outCurves[i].SetPoint(j, pt);
                }
            }

            DA.SetDataList(0, outCurves);
        }

        private double GetPointNormalizedLength(PolylineCurve curve, int index)
        {
            if (index == 0) { return 0.0; }
            if (index == curve.PointCount - 1) { return 1.0; }
            var param = 0.0;
            var norml = 0.0;
            curve.ClosestPoint(curve.Point(index), out param);
            norml = Math.Min(param / (curve.Domain.Max - curve.Domain.Min), 1.0);
            return norml;
        }

        private (double, double) GetDecompensation(double normalizedLength, DecompensationDescriptor descriptor)
        {
            var pre = 0;
            var post = descriptor.Points.Count - 1;

            for (int i = 0; i < post; i++)
            {
                if (descriptor.Points[i].Length > normalizedLength)
                {
                    break;
                }
                pre = i;
            }

            for (int i = post; i > pre; i--)
            {
                if (descriptor.Points[i].Length < normalizedLength)
                {
                    break;
                }
                post = i;
            }

            var length = descriptor.Points[post].Length - descriptor.Points[pre].Length;
            var multPre = (length - (normalizedLength - descriptor.Points[pre].Length)) / length;
            var multPost = (length - (descriptor.Points[post].Length - normalizedLength)) / length;

            var warp = descriptor.Points[pre].Warp * multPre + descriptor.Points[post].Warp * multPost;
            var weft = descriptor.Points[pre].Weft * multPre + descriptor.Points[post].Weft * multPost;

            return (warp, weft);
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