// Ported from FeMM 30-5-2024; new Arachne component identity. See docs/PORTING.md.
using Arachne.Grasshopper.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Components.Networks
{
    public class CableNetRadialComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamComponent class.
        /// </summary>
        public CableNetRadialComponent()
          : base("Radial Cable Net", "RCN", "Create a radial cable net", "Arachne", "0 Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            int idR = pManager.AddCurveParameter("Restraint edges", "RB", "The restraint boundary curve", GH_ParamAccess.list);
            pManager[idR].Optional = true;
            int idF = pManager.AddCurveParameter("Free edges", "CB", "The free boundary curve", GH_ParamAccess.list);
            pManager[idF].Optional = true;
            int idPR = pManager.AddCurveParameter("Perimeter", "PR", "The perimeter of the cable net", GH_ParamAccess.list);
            pManager[idPR].Optional = true;
            pManager.AddNumberParameter("Radial step", "Warp", "The warp distance step", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Radial subdivision", "Weft", "The weft distance step", GH_ParamAccess.item);
            int idPL = pManager.AddPlaneParameter("Plane", "PL", "The plane of the cable net", GH_ParamAccess.item, Plane.WorldXY);
            pManager[idPL].Optional = true;
            int idO = pManager.AddPointParameter("Origin", "O", "The origin point of the cable net", GH_ParamAccess.item);
            pManager[idO].Optional = true;
            int idA = pManager.AddNumberParameter("Angle", "A", "The angle of rotation of the cable net", GH_ParamAccess.item, 0);
            pManager[idA].Optional = true;
            int idT = pManager.AddNumberParameter("Tolerance", "T", "The intersection tolerance", GH_ParamAccess.item, 0.001);
            pManager[idT].Optional = true;
            int idM = pManager.AddNumberParameter("Match", "M", "The matching tolerance", GH_ParamAccess.item, 0);
            pManager[idM].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Restraint edges", "RB", "The border lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Free edges", "CB", "The border lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Warp", "Warp", "The warp lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Weft", "Weft", "The weft lines", GH_ParamAccess.tree);
            pManager.AddPointParameter("Restraint points", "RP", "The boundary points", GH_ParamAccess.list);
            pManager.AddPointParameter("NotRestraint points", "NRP", "The boundary points", GH_ParamAccess.list);
            pManager.AddPointParameter("Internal points", "IP", "The internal points", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> freeCurveInput = new List<Curve>();
            List<Curve> fixCurveInput = new List<Curve>();
            List<Curve> perimeterInput = new List<Curve>();
            int radialSubd = 0;
            double radialStep = 0;
            double angle = 0;
            Plane plane = Plane.WorldXY;
            Point3d origin = Point3d.Origin;
            double tolerance = 0.01;
            double matching = 0;

            DA.GetDataList("Restraint edges", fixCurveInput);
            DA.GetDataList("Free edges", freeCurveInput);
            DA.GetDataList("Perimeter", perimeterInput);
            if (!DA.GetData("Radial subdivision", ref radialSubd))
                return;
            if (!DA.GetData("Radial step", ref radialStep))
                return;
            DA.GetData("Plane", ref plane);
            DA.GetData("Origin", ref origin);
            DA.GetData("Angle", ref angle);
            DA.GetData("Tolerance", ref tolerance);
            DA.GetData("Match", ref matching);


            List<List<LineCurve>> outWarp = new List<List<LineCurve>>();
            List<List<LineCurve>> outWeft = new List<List<LineCurve>>();
            List<List<Curve>> outFreeBorder = new List<List<Curve>>();
            List<List<Curve>> outFixBorder = new List<List<Curve>>();
            List<Point3d> restrain = new List<Point3d>();
            List<Point3d> notRestrain = new List<Point3d>();
            List<Point3d> internalPoint = new List<Point3d>();

            if (freeCurveInput.Count == 0 && fixCurveInput.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input curves cannot be empty");
                return;
            }

            if (radialSubd < 3 || radialStep <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Steps must be positive; radial divisions must be at least 3");
                return;
            }

            if (freeCurveInput.Any(i => i == null))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Null items in free edges list");
                return;
            }
            if (fixCurveInput.Any(i => i == null))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Null items in restraint edges list");
                return;
            }

            try
            {
                var refPlane = CableNetHelper.MakePlane(plane, origin, angle / 180 * Math.PI);
                if (!CableNetHelper.CreateRadialCableNet2(fixCurveInput, freeCurveInput, perimeterInput, radialSubd, radialStep, refPlane, out outWarp,
                        out outWeft, out outFreeBorder, out outFixBorder, out restrain, out notRestrain, out internalPoint, tolerance, matching))
                    return;
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Fail to create the cable net: " + e.Message); return;
            }

            int outCount = 0;
            DA.SetDataTree(outCount++, ListListToTree(outFixBorder));
            DA.SetDataTree(outCount++, ListListToTree(outFreeBorder));
            DA.SetDataTree(outCount++, ListListToTree(outWarp));
            DA.SetDataTree(outCount++, ListListToTree(outWeft));
            DA.SetDataList(outCount++, restrain);
            DA.SetDataList(outCount++, notRestrain);
            DA.SetDataList(outCount++, internalPoint);
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => null;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("f5b16b9e-f4e6-45c1-9430-7595f8b26cca");

        private IGH_DataTree ListListToTree(List<List<Curve>> curves)
        {
            DataTree<Curve> tree = new DataTree<Curve>();

            for (int i = 0; i < curves.Count; i++)
            {
                tree.AddRange(curves[i], new GH_Path(i));
            }

            return tree;
        }

        private IGH_DataTree ListListToTree(List<List<LineCurve>> curves)
        {
            DataTree<LineCurve> tree = new DataTree<LineCurve>();

            for (int i = 0; i < curves.Count; i++)
            {
                tree.AddRange(curves[i], new GH_Path(i));
            }

            return tree;
        }
    }
}

