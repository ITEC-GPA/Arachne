using Arachne.Grasshopper.Helpers;
// Ported from FeMM 30-5-2024; new Arachne component identity. See docs/PORTING.md.
using Arachne.Grasshopper.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class CompoundNetComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamComponent class.
        /// </summary>
        public CompoundNetComponent()
          : base("Compound Net", "CompNet", "Create a compound cable net", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PATTERNING)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            int idR = pManager.AddCurveParameter("Restraint edges", "R", "The restraint boundary curve", GH_ParamAccess.tree);
            pManager[idR].Optional = true;
            int idF = pManager.AddCurveParameter("Free edges", "F", "The free boundary curve", GH_ParamAccess.tree);
            pManager[idF].Optional = true;
            int idP = pManager.AddCurveParameter("Perimeter", "PE", "The perimeter of the grid", GH_ParamAccess.tree);
            pManager[idP].Optional = true;
            pManager.AddNumberParameter("Warp step", "Warp", "The warp distance step", GH_ParamAccess.list);
            pManager.AddNumberParameter("Weft step", "Weft", "The weft distance step", GH_ParamAccess.list);
            int idK = pManager.AddNumberParameter("Kind", "K", "Kind of the net: 0 = Grid; 1 = Radial;", GH_ParamAccess.list, 0);
            pManager[idK].Optional = true;
            int idO = pManager.AddPointParameter("Origin", "O", "The origin point of the cable net", GH_ParamAccess.list, Point3d.Origin);
            pManager[idO].Optional = true;
            int idA = pManager.AddNumberParameter("Angle", "A", "The angle of rotation of the cable net", GH_ParamAccess.list, 0);
            pManager[idA].Optional = true;
            int idT = pManager.AddNumberParameter("Tolerance", "T", "The intersection tolerance", GH_ParamAccess.item, 0.001);
            pManager[idT].Optional = true;
            int idM = pManager.AddNumberParameter("Match", "M", "The matching tolerance", GH_ParamAccess.item, 0);
            pManager[idM].Optional = true;
            int idD = pManager.AddNumberParameter("Discretization", "D", "The discretization of the surface (higher means slower)", GH_ParamAccess.item, 25);
            pManager[idD].Optional = true;
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
            GH_Structure<GH_Curve> fixCurveInputs;
            GH_Structure<GH_Curve> freeCurveInputs;
            GH_Structure<GH_Curve> perimeterInputs;

            var warpSteps = new List<double>();
            var weftSteps = new List<double>();
            var netKinds = new List<double>(); // If int it does not take inputs
            var origins = new List<Point3d>();
            var angles = new List<double>();

            double tolerance = 0.1;
            double matching = 0;

            double discretization = 25.0;

            DA.GetDataTree("Restraint edges", out fixCurveInputs);
            DA.GetDataTree("Free edges", out freeCurveInputs);
            DA.GetDataTree("Perimeter", out perimeterInputs);
            if (!DA.GetDataList("Warp step", warpSteps))
                return;
            if (!DA.GetDataList("Weft step", weftSteps))
                return;
            DA.GetDataList("Kind", netKinds);
            DA.GetDataList("Origin", origins);
            DA.GetDataList("Angle", angles);
            DA.GetData("Tolerance", ref tolerance);
            DA.GetData("Match", ref matching);
            DA.GetData("Discretization", ref discretization);

            fixCurveInputs = fixCurveInputs ?? new GH_Structure<GH_Curve>();
            freeCurveInputs = freeCurveInputs ?? new GH_Structure<GH_Curve>();
            perimeterInputs = perimeterInputs ?? new GH_Structure<GH_Curve>();
            if (perimeterInputs.DataCount == 0 ||
                new[] { fixCurveInputs, freeCurveInputs, perimeterInputs }.Any(tree =>
                    tree.AllData(false).Cast<GH_Curve>().Any(curve => curve == null || curve.Value == null || !curve.Value.IsValid)))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Valid curves and a closed perimeter are required.");
                return;
            }
            if (perimeterInputs.Paths.Any(path => path.Indices.Length != 1 || path.Indices[0] < 0))
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Use one-level perimeter paths {0}, {1}, ...");
                return;
            }

            if (freeCurveInputs.DataCount == 0 && fixCurveInputs.DataCount == 0)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Input curves cannot be empty");
                return;
            }

            if (warpSteps.Count == 0 || warpSteps.Count != weftSteps.Count)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Warp and weft steps lists must have the same length");
                return;
            }

            for (int i = 0; i < warpSteps.Count; i++)
            {
                if (!Arachne.Common.Helpers.Validation.IsFinite(warpSteps[i]) || !Arachne.Common.Helpers.Validation.IsFinite(weftSteps[i]) || warpSteps[i] <= 0 || weftSteps[i] <= 0)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Steps must be positive; radial divisions must be at least 3");
                    return;
                }
            }

            if (discretization < 2 || double.IsNaN(discretization) || double.IsInfinity(discretization)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Discretization must be >= 2."); return; }
            var netNumber = 0;
            for (int i = 0; i < perimeterInputs.PathCount; ++i)
            {
                var path = perimeterInputs.Paths[i];
                netNumber = Math.Max(path.Indices[0] + 1, netNumber);
            }

            while (warpSteps.Count < netNumber)
            {
                warpSteps.Add(warpSteps[warpSteps.Count - 1]);
            }
            while (weftSteps.Count < netNumber)
            {
                weftSteps.Add(weftSteps[weftSteps.Count - 1]);
            }
            while (netKinds.Count < netNumber)
            {
                netKinds.Add(0);
            }
            while (origins.Count < netNumber)
            {
                origins.Add(new Point3d(0, 0, 0));
            }
            while (angles.Count < netNumber)
            {
                angles.Add(0);
            }
            for (int i = 0; i < netNumber; i++)
            {
                if (!perimeterInputs.Paths.Contains(new GH_Path(i)) ||
                    (netKinds[i] != 0 && netKinds[i] != 1) ||
                    (netKinds[i] == 1 && (warpSteps[i] < 3 || warpSteps[i] != Math.Floor(warpSteps[i]))))
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Use consecutive perimeter paths; Kind must be 0 or 1, with integer radial divisions >= 3.");
                    return;
                }
            }

            var inFixCables = new List<List<Curve>>();
            var inFreeCables = new List<List<Curve>>();
            var inPerimeters = new List<List<Curve>>();

            var uniqueFixSet = new HashSet<Guid>();
            var uniqueFreeSet = new HashSet<Guid>();
            var uniqueFix = new List<Curve>();
            var uniqueFree = new List<Curve>();

            for (int i = 0; i < netNumber; i++)
            {
                var ghfixcurves = fixCurveInputs.Paths.Contains(new GH_Path(i)) ? (List<GH_Curve>)fixCurveInputs.get_Branch(new GH_Path(i)) : new List<GH_Curve>();
                var ghfreecurves = freeCurveInputs.Paths.Contains(new GH_Path(i)) ? (List<GH_Curve>)freeCurveInputs.get_Branch(new GH_Path(i)) : new List<GH_Curve>();
                var ghperimeters = perimeterInputs.Paths.Contains(new GH_Path(i)) ? (List<GH_Curve>)perimeterInputs.get_Branch(new GH_Path(i)) : new List<GH_Curve>();

                inFixCables.Add(new List<Curve>());
                inFreeCables.Add(new List<Curve>());
                inPerimeters.Add(new List<Curve>());

                for (int j = 0; j < ghfixcurves.Count; j++)
                {
                    inFixCables[i].Add(ghfixcurves[j].Value);
                    if (ghfixcurves[j].ReferenceID == Guid.Empty || !uniqueFixSet.Contains(ghfixcurves[j].ReferenceID))
                    {
                        uniqueFixSet.Add(ghfixcurves[j].ReferenceID);
                        uniqueFix.Add(ghfixcurves[j].Value);
                    }
                }
                for (int j = 0; j < ghfreecurves.Count; j++)
                {
                    inFreeCables[i].Add(ghfreecurves[j].Value);
                    if (ghfreecurves[j].ReferenceID == Guid.Empty || !uniqueFreeSet.Contains(ghfreecurves[j].ReferenceID))
                    {
                        uniqueFreeSet.Add(ghfreecurves[j].ReferenceID);
                        uniqueFree.Add(ghfreecurves[j].Value);
                    }
                }
                for (int j = 0; j < ghperimeters.Count; j++)
                {
                    inPerimeters[i].Add(ghperimeters[j].Value);
                }
            }



            var allOutWarp = new List<List<LineCurve>>();
            var allOutWeft = new List<List<LineCurve>>();
            var allOutFreeBorder = new List<List<Curve>>();
            var allOutFixBorder = new List<List<Curve>>();
            var allRestrain = new List<Point3d>();
            var allNotRestrain = new List<Point3d>();
            var allInternalPoint = new List<Point3d>();

            // TODO: Check for nulls
            //if (freeCurveInput.Any(i => i == null))
            //{
            //	AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Null items in free edges list");
            //	return;
            //}
            //if (fixCurveInput.Any(i => i == null))
            //{
            //	AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Null items in restraint edges list");
            //	return;
            //}

            for (int i = 0; i < netNumber; ++i)
            {
                var outWarp = new List<List<LineCurve>>();
                var outWeft = new List<List<LineCurve>>();
                var outFreeBorder = new List<List<Curve>>();
                var outFixBorder = new List<List<Curve>>();
                var restrain = new List<Point3d>();
                var notRestrain = new List<Point3d>();
                var internalPoint = new List<Point3d>();


                var surfacePoints = new List<Point3d>();
                for (int j = 0; j < inFixCables[i].Count; ++j)
                {
                    foreach (var param in inFixCables[i][j].DivideByCount((int)discretization, true))
                    {
                        surfacePoints.Add(inFixCables[i][j].PointAt(param));
                    }
                }
                for (int j = 0; j < inFreeCables[i].Count; ++j)
                {
                    foreach (var param in inFreeCables[i][j].DivideByCount((int)discretization, false))
                    {
                        surfacePoints.Add(inFreeCables[i][j].PointAt(param));
                    }
                }

                Plane plane;
                Plane.FitPlaneToPoints(surfacePoints, out plane);
                if (!plane.IsValid)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Cannot fit a plane to the boundary points.");
                    return;
                }

                // MAKE THE NET
                try
                {
                    var refPlane = CableNetHelper.MakePlane(plane, origins[i], angles[i] / 180 * Math.PI);
                    if (netKinds[i] == 0)
                    {
                        if (!CableNetHelper.CreateCableNet2(inFixCables[i], inFreeCables[i], inPerimeters[i], warpSteps[i], weftSteps[i], refPlane,
                            out outWarp, out outWeft, out outFreeBorder, out outFixBorder, out restrain, out notRestrain, out internalPoint, tolerance, matching))
                            return;
                    }
                    else if (netKinds[i] == 1)
                    {
                        if (!CableNetHelper.CreateRadialCableNet2(inFixCables[i], inFreeCables[i], inPerimeters[i], (int)warpSteps[i], weftSteps[i], refPlane,
                            out outWarp, out outWeft, out outFreeBorder, out outFixBorder, out restrain, out notRestrain, out internalPoint, tolerance, matching))
                            return;
                    }
                }
                catch (Exception e)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Fail to create the cable net: " + e.Message); return;
                }

                // PROJECT TO PATCH
                var patch = Brep.CreatePatch(new List<GeometryBase>() { new PointCloud(surfacePoints) }, (int)discretization, (int)discretization, tolerance);
                if (patch == null)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Cannot construct the surface patch.");
                    return;
                }

                var projectedPoints = Intersection.ProjectPointsToBreps(new List<Brep>() { patch }, internalPoint, plane.Normal, tolerance).ToList();

                var dict = new Dictionary<Point3d, int>();
                for (int k = 0; k < internalPoint.Count; k++)
                {
                    dict.Add(internalPoint[k], k);
                }

                // ADJUST WARP POITS WITH PROJECTION
                for (int k = 0; k < outWarp.Count; k++)
                {
                    for (int n = 0; n < outWarp[k].Count; n++)
                    {
                        if (dict.ContainsKey(outWarp[k][n].PointAtStart))
                        {
                            outWarp[k][n].SetStartPoint(projectedPoints[dict[outWarp[k][n].PointAtStart]]);
                        }
                        if (dict.ContainsKey(outWarp[k][n].PointAtEnd))
                        {
                            outWarp[k][n].SetEndPoint(projectedPoints[dict[outWarp[k][n].PointAtEnd]]);
                        }
                    }
                }
                // ADJUST WEFT POITS WITH PROJECTION
                for (int k = 0; k < outWeft.Count; k++)
                {
                    for (int n = 0; n < outWeft[k].Count; n++)
                    {
                        if (dict.ContainsKey(outWeft[k][n].PointAtStart))
                        {
                            outWeft[k][n].SetStartPoint(projectedPoints[dict[outWeft[k][n].PointAtStart]]);
                        }
                        if (dict.ContainsKey(outWeft[k][n].PointAtEnd))
                        {
                            outWeft[k][n].SetEndPoint(projectedPoints[dict[outWeft[k][n].PointAtEnd]]);
                        }
                    }
                }

                // ADD TO THE WHOLE LISTS
                internalPoint = projectedPoints;

                allOutWarp.AddRange(outWarp);
                allOutWeft.AddRange(outWeft);
                allOutFreeBorder.AddRange(outFreeBorder);
                allOutFixBorder.AddRange(outFixBorder);
                allRestrain.AddRange(restrain);
                allNotRestrain.AddRange(notRestrain);
                allInternalPoint.AddRange(internalPoint);
            }

            // MATCH THE CABLES OF DIFFERENT NETS
            var selfFixPoints = new HashSet<Point3d>();
            var selfFreePoints = new HashSet<Point3d>();
            for (int k = 0; k < allOutFixBorder.Count; k++)
            {
                for (int n = 0; n < allOutFixBorder[k].Count; n++)
                {
                    selfFixPoints.Add(allOutFixBorder[k][n].PointAtStart);
                    selfFixPoints.Add(allOutFixBorder[k][n].PointAtEnd);
                }
            }
            for (int k = 0; k < allOutFreeBorder.Count; k++)
            {
                for (int n = 0; n < allOutFreeBorder[k].Count; n++)
                {
                    selfFreePoints.Add(allOutFreeBorder[k][n].PointAtStart);
                    selfFreePoints.Add(allOutFreeBorder[k][n].PointAtEnd);
                }
            }

            var collapsedRestrain = CollapsePoints(allRestrain, selfFixPoints.ToList(), matching);
            var collapsedNotRestrain = CollapsePoints(allNotRestrain, selfFreePoints.ToList(), matching);

            // GET THE UNIQUES POINTS ON CABLES
            var uniqueRestrainSet = new HashSet<Point3d>();
            var uniqueNotRestrainSet = new HashSet<Point3d>();

            foreach (var entry in collapsedRestrain)
            {
                if (uniqueRestrainSet.Contains(entry.Value)) continue;
                uniqueRestrainSet.Add(entry.Value);
            }
            foreach (var entry in collapsedNotRestrain)
            {
                if (uniqueNotRestrainSet.Contains(entry.Value)) continue;
                uniqueNotRestrainSet.Add(entry.Value);
            }

            allRestrain = uniqueRestrainSet.ToList();
            allNotRestrain = uniqueNotRestrainSet.ToList();

            allOutFixBorder.Clear();
            allOutFreeBorder.Clear();

            // SPLIT THE CABLES AT UNIQUE POINTS
            for (int i = 0; i < uniqueFix.Count; i++)
            {
                var splits = new List<double>();
                double t = 0;
                for (int j = 0; j < allRestrain.Count; j++)
                {
                    // Rhino treats maximumDistance=0 as unlimited, not as exact matching.
                    if (uniqueFix[i].ClosestPoint(allRestrain[j], out t, Math.Max(tolerance, matching)))
                    {
                        splits.Add(t);
                    }
                }
                var result = uniqueFix[i].Split(splits).ToList();
                if (result.Count > 0)
                {
                    allOutFixBorder.Add(result);
                }
                else
                {
                    allOutFixBorder.Add(new List<Curve>() { uniqueFix[i] });
                }
            }

            for (int i = 0; i < uniqueFree.Count; i++)
            {
                var splits = new List<double>();
                double t = 0;
                for (int j = 0; j < allNotRestrain.Count; j++)
                {
                    if (uniqueFree[i].ClosestPoint(allNotRestrain[j], out t, Math.Max(tolerance, matching)))
                    {
                        splits.Add(t);
                    }
                }
                var result = uniqueFree[i].Split(splits).ToList();
                if (result.Count > 0)
                {
                    allOutFreeBorder.Add(result);
                }
                else
                {
                    allOutFreeBorder.Add(new List<Curve>() { uniqueFree[i] });
                }
            }

            // UPDATE WARP AND WEFT POINTS IN THE SAME POSITION OF THE SPLITS
            for (int i = 0; i < allOutWarp.Count; i++)
            {
                for (int j = 0; j < allOutWarp[i].Count; j++)
                {
                    if (collapsedRestrain.ContainsKey(allOutWarp[i][j].PointAtStart))
                    {
                        allOutWarp[i][j].SetStartPoint(collapsedRestrain[allOutWarp[i][j].PointAtStart]);
                    }
                    if (collapsedRestrain.ContainsKey(allOutWarp[i][j].PointAtEnd))
                    {
                        allOutWarp[i][j].SetEndPoint(collapsedRestrain[allOutWarp[i][j].PointAtEnd]);
                    }
                    if (collapsedNotRestrain.ContainsKey(allOutWarp[i][j].PointAtStart))
                    {
                        allOutWarp[i][j].SetStartPoint(collapsedNotRestrain[allOutWarp[i][j].PointAtStart]);
                    }
                    if (collapsedNotRestrain.ContainsKey(allOutWarp[i][j].PointAtEnd))
                    {
                        allOutWarp[i][j].SetEndPoint(collapsedNotRestrain[allOutWarp[i][j].PointAtEnd]);
                    }
                }
            }

            for (int i = 0; i < allOutWeft.Count; i++)
            {
                for (int j = 0; j < allOutWeft[i].Count; j++)
                {
                    if (collapsedRestrain.ContainsKey(allOutWeft[i][j].PointAtStart))
                    {
                        allOutWeft[i][j].SetStartPoint(collapsedRestrain[allOutWeft[i][j].PointAtStart]);
                    }
                    if (collapsedRestrain.ContainsKey(allOutWeft[i][j].PointAtEnd))
                    {
                        allOutWeft[i][j].SetEndPoint(collapsedRestrain[allOutWeft[i][j].PointAtEnd]);
                    }
                    if (collapsedNotRestrain.ContainsKey(allOutWeft[i][j].PointAtStart))
                    {
                        allOutWeft[i][j].SetStartPoint(collapsedNotRestrain[allOutWeft[i][j].PointAtStart]);
                    }
                    if (collapsedNotRestrain.ContainsKey(allOutWeft[i][j].PointAtEnd))
                    {
                        allOutWeft[i][j].SetEndPoint(collapsedNotRestrain[allOutWeft[i][j].PointAtEnd]);
                    }
                }
            }

            // OUTPUT RESULTS
            int outCount = 0;
            DA.SetDataTree(outCount++, ListListToTree(allOutFixBorder));
            DA.SetDataTree(outCount++, ListListToTree(allOutFreeBorder));
            DA.SetDataTree(outCount++, ListListToTree(allOutWarp));
            DA.SetDataTree(outCount++, ListListToTree(allOutWeft));
            DA.SetDataList(outCount++, allRestrain);
            DA.SetDataList(outCount++, allNotRestrain);
            DA.SetDataList(outCount++, allInternalPoint);
        }

        private Dictionary<Point3d, Point3d> CollapsePoints(List<Point3d> points, List<Point3d> notablePoints, double matching)
        {
            var notableTree = new RTree();
            for (int i = 0; i < notablePoints.Count; ++i)
            {
                notableTree.Insert(notablePoints[i], i);
            }

            var map = new Dictionary<Point3d, Point3d>();
            var searchTree = new RTree();
            for (var i = 0; i < points.Count; i++)
            {
                if (map.ContainsKey(points[i])) continue;

                map.Add(points[i], points[i]);

                int notableId = -1;
                notableTree.Search(new Sphere(points[i], matching), new EventHandler<RTreeEventArgs>((sender, e) =>
                {
                    if (notableId == -1 || notablePoints[notableId].DistanceTo(points[i]) > notablePoints[e.Id].DistanceTo(points[i]))
                    {
                        notableId = e.Id;
                    }
                }));

                if (notableId >= 0)
                {
                    map[points[i]] = notablePoints[notableId];
                    continue;
                }

                var neighbours = new HashSet<int>();
                searchTree.Search(new Sphere(points[i], matching), new EventHandler<RTreeEventArgs>((sender, e) => { neighbours.Add(e.Id); }));

                foreach (var n in neighbours)
                {
                    map[points[i]] += map[points[n]];
                }
                map[points[i]] /= neighbours.Count + 1;
                foreach (var n in neighbours)
                {
                    searchTree.Remove(map[points[n]], n);
                    map[points[n]] = map[points[i]];
                    searchTree.Insert(map[points[n]], n);
                }

                searchTree.Insert(map[points[i]], i);
            }
            return map;
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("ca17f231-a85b-42dd-b48f-91c81e0916d7");

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

