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
    public class CableNetComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamComponent class.
        /// </summary>
        public CableNetComponent()
          : base("Cable Net", "CN", "Create a cable net", "Arachne", "0 Geometry")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            int idR = pManager.AddCurveParameter("Restraint edges", "R", "The restraint boundary curve", GH_ParamAccess.list);
            pManager[idR].Optional = true;
            int idF = pManager.AddCurveParameter("Free edges", "F", "The free boundary curve", GH_ParamAccess.list);
            pManager[idF].Optional = true;
            int idP = pManager.AddCurveParameter("Perimeter", "PE", "The perimeter of the grid", GH_ParamAccess.list);
            pManager[idP].Optional = true;
            pManager.AddNumberParameter("Warp step", "Warp", "The warp distance step", GH_ParamAccess.item);
            pManager.AddNumberParameter("Weft step", "Weft", "The weft distance step", GH_ParamAccess.item);
            int idL = pManager.AddPlaneParameter("Plane", "PL", "The plane of the grid", GH_ParamAccess.item, Plane.WorldXY);
            pManager[idL].Optional = true;
            int idO = pManager.AddPointParameter("Origin", "O", "The origin point of the cable net", GH_ParamAccess.item, Point3d.Origin);
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
            pManager.AddCurveParameter("Free edges", "FB", "The border lines", GH_ParamAccess.tree);
            pManager.AddCurveParameter("Warp", "Warp", "The warp lines", GH_ParamAccess.list);
            pManager.AddCurveParameter("Weft", "Weft", "The weft lines", GH_ParamAccess.list);
            pManager.AddPointParameter("Restraint points", "RP", "The boundary points", GH_ParamAccess.list);
            pManager.AddPointParameter("NotRestraint points", "NRP", "The boundary points", GH_ParamAccess.list);
            pManager.AddPointParameter("Internal points", "IP", "The internal points", GH_ParamAccess.list);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Curve> freeCurveInput = new List<Curve>();
            List<Curve> fixCurveInput = new List<Curve>();
            List<Curve> perimeterInput = new List<Curve>();
            double warpStep = 0;
            double weftStep = 0;
            Plane plane = Plane.WorldXY;
            double angle = 0;
            Point3d origin = Point3d.Origin;
            double tolerance = 0.1;
            double matching = 0;

            DA.GetDataList("Restraint edges", fixCurveInput);
            DA.GetDataList("Free edges", freeCurveInput);
            DA.GetDataList("Perimeter", perimeterInput);
            if (!DA.GetData("Warp step", ref warpStep))
                return;
            if (!DA.GetData("Weft step", ref weftStep))
                return;
            DA.GetData("Plane", ref plane);
            DA.GetData("Origin", ref origin);
            DA.GetData("Angle", ref angle);
            DA.GetData("Tolerance", ref tolerance);
            DA.GetData("Match", ref matching);


            if (freeCurveInput.Count == 0 && fixCurveInput.Count == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input curves cannot be empty");
                return;
            }

            if (warpStep <= 0 || weftStep <= 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Steps must be positive; radial divisions must be at least 3");
                return;
            }

            List<List<LineCurve>> outWarp = new List<List<LineCurve>>();
            List<List<LineCurve>> outWeft = new List<List<LineCurve>>();
            List<List<Curve>> outFreeBorder = new List<List<Curve>>();
            List<List<Curve>> outFixBorder = new List<List<Curve>>();
            List<Point3d> restrain = new List<Point3d>();
            List<Point3d> notRestrain = new List<Point3d>();
            List<Point3d> internalPoint = new List<Point3d>();

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
                if (!CableNetHelper.CreateCableNet2(fixCurveInput, freeCurveInput, perimeterInput, warpStep, weftStep, refPlane, out outWarp,
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
        public override Guid ComponentGuid => new Guid("cb2b48b5-63b8-4af9-8f54-75ff5d506e5f");

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

