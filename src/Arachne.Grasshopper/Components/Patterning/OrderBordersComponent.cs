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
    public class OrderBordersComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public OrderBordersComponent()
          : base("OrderBorders", "OB", "Order the borders of a panel", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The border curves", GH_ParamAccess.list);
            pManager.AddBooleanParameter("ClockWise", "CW", "Order clockwise or counterclockwise", GH_ParamAccess.item, false);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Curves", "C", "The border curves", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<GH_Curve> ghcurves = new List<GH_Curve>();
            bool cw = false;

            if (!DA.GetDataList(0, ghcurves))
                return;
            if (!DA.GetData(1, ref cw))
                return;

            List<Curve> curves = new List<Curve>();
            for (int i = 0; i < ghcurves.Count; i++)
            {
                curves.Add(ghcurves[i].Value);
            }

            Point3d center = new Point3d(0, 0, 0);
            int bottomMost = 0;
            double minY = double.MaxValue;

            for (int i = 0; i < curves.Count; ++i)
            {
                var pt = curves[i].PointAtNormalizedLength(0.5);
                center += pt;
                if (pt.Y < minY)
                {
                    minY = pt.Y;
                    bottomMost = i;
                }
            }
            center = center / curves.Count;

            var refPt = curves[bottomMost].PointAtNormalizedLength(0.5) - center;
            var refAngle = Math.Atan2(refPt.Y, refPt.X);

            curves.Sort((a, b) =>
            {
                var pta = a.PointAtNormalizedLength(0.5) - center;
                var ptb = b.PointAtNormalizedLength(0.5) - center;
                var anglea = Math.Atan2(pta.Y, pta.X);
                var angleb = Math.Atan2(ptb.Y, ptb.X);
                anglea = anglea < refAngle ? anglea + Math.PI * 2 : anglea;
                angleb = angleb < refAngle ? angleb + Math.PI * 2 : angleb;
                return anglea.CompareTo(angleb);
            });

            if (cw)
            {
                var first = curves.ElementAt(0);
                curves.RemoveAt(0);
                curves.Reverse();
                curves.Insert(0, first);
            }


            for (var i = 0; i < curves.Count; i++)
            {
                var ptA = curves[i].PointAtStart;
                var ptB = curves[i].PointAtEnd;
                var vecA = center - ptA;
                var vecB = center - ptB;
                var cross = Vector3d.CrossProduct(vecA, vecB);

                var shouldFlip = cross.Z < 0;
                if (cw)
                {
                    shouldFlip = !shouldFlip;
                }

                if (shouldFlip)
                {
                    curves[i].Reverse();
                }
            }

            DA.SetDataList(0, curves);
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("085ff316-27ac-53fb-b09f-2f8039f82f68");
    }
}