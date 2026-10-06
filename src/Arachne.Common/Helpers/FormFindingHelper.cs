using Arachne.Common.Models;
using GPC.Utilities.Units;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.Geometry;

namespace Arachne.Common.Helpers
{
    public static class FormFindingHelper
    {
        public static Model AssemblyModel(ModelModel modelModel, Plane plane, int solverType, double gravity, double pressure, bool createMesh, out List<string> log)
        {
            log = new List<string>();

            Model ff_model = new Model();
            ModelHelper.GetUtilitiesConversionParams(modelModel.Units, out UnitsConvert.LengthUnits lengthUnits,
                out UnitsConvert.ForceUnits forceUnits, out UnitsConvert.MassUnits massUnits,
                out UnitsConvert.PressureUnits pressureUnits, out UnitsConvert.TemperatureUnits temperatureUnits);

            GPC.Geometry.Plane mmPlane;
            if (plane == Plane.Unset)
                mmPlane = new GPC.Geometry.Plane(GPC.Geometry.Point3d.Origin, GPC.Geometry.Vector3d.ZAxis);
            else
                mmPlane = new GPC.Geometry.Plane(new GPC.Geometry.Point3d(plane.Origin.X, plane.Origin.Y, plane.Origin.Z),
                    new GPC.Geometry.Vector3d(plane.Normal.X, plane.Normal.Y, plane.Normal.Z));

            Dictionary<(double, double, double), Node> pointNodeAssociation = new Dictionary<(double, double, double), Node>();

            // Nodes created for beam ends or plate vertices are registered too, so elements
            // sharing a point not defined as a model node share the same solver node.
            Node AddNodeAt(Point3d pt)
            {
                int newId = ff_model.AddNewNode(pt.X, pt.Y, pt.Z);
                Node newNode = ff_model.Nodes[newId];
                pointNodeAssociation.Add((pt.X, pt.Y, pt.Z), newNode);
                return newNode;
            }

            for (int i = 0; i < modelModel.Nodes.Count; i++)
            {
                NodeModel node = modelModel.Nodes.ElementAt(i);
                int id = ff_model.AddNewNode(node.Position.X, node.Position.Y, node.Position.Z);
                Node ff_node = ff_model.Nodes.Last();
                (double, double, double) nnn = (node.Position.X, node.Position.Y, node.Position.Z);
                if (!pointNodeAssociation.ContainsKey(nnn))
                    pointNodeAssociation.Add(nnn, ff_node);

                if (node.HasAttribute(typeof(NodeRestraintAttributeModel)))
                {
                    NodeRestraintAttributeModel.AttributeData restraint = NodeRestraintAttributeModel.GetRestraint(node);
                    ff_node.Tx = restraint.Tx;
                    ff_node.Ty = restraint.Ty;
                    ff_node.Tz = restraint.Tz;
                    ff_node.Rx = restraint.Rx;
                    ff_node.Ry = restraint.Ry;
                    ff_node.Rz = restraint.Rz;
                }
                IEnumerable<NodeForceLoadModel> forces = node.Loads.Where(l => l.GetType() == typeof(NodeForceLoadModel)).Cast<NodeForceLoadModel>();

                if (forces.Count() > 0)
                {
                    // Sequential sum: the parallel accumulation on the same vector lost loads.
                    Vector3d force = new Vector3d();
                    foreach (NodeForceLoadModel f in forces)
                        force += f.Value;
                    ff_node.SetForce(force.X, force.Y, force.Z);
                }
                if (node.HasAttribute(typeof(NodeWeightAttributeModel)))
                {
                    Vector3d weight = NodeWeightAttributeModel.GetWeight(node);
                    if (weight.X != 0)
                        ff_node.FixNLCoordX(weight.X);
                    if (weight.Y != 0)
                        ff_node.FixNLCoordY(weight.Y);
                    if (weight.Z != 0)
                        ff_node.FixNLCoordZ(weight.Z);
                    if (solverType == 0)
                        log.Add("Weights on nodes coordinates are ignored on the linear solver.");
                }
            }

            List<Curve> lineCurves = new List<Curve>(modelModel.Beams.Count);
            Dictionary<(int, int), Beam> nodesBeamsAss = new Dictionary<(int, int), Beam>();
            // The beam loads become forces on the end nodes, shared with the lever rule on the
            // input geometry. TensileLib shares them again at every iteration dividing the
            // input distance by the current length, so a beam that changes length moved its load.
            Dictionary<Node, Vector3d> beamLoadForces = new Dictionary<Node, Vector3d>();
            void AddBeamLoad(Node from, Node to, Vector3d force, double ratio)
            {
                beamLoadForces.TryGetValue(from, out Vector3d f1);
                beamLoadForces[from] = f1 + force * (1 - ratio);
                beamLoadForces.TryGetValue(to, out Vector3d f2);
                beamLoadForces[to] = f2 + force * ratio;
            }
            for (int j = 0; j < modelModel.Beams.Count; j++)
            {
                BeamModel beam = modelModel.Beams[j];
                Point3d ptFrom = beam.PointFrom;
                Point3d ptTo = beam.PointTo;
                lineCurves.Add(new LineCurve(ptFrom, ptTo));

                // Starting node
                Node node1 = null;
                if (!pointNodeAssociation.ContainsKey((ptFrom.X, ptFrom.Y, ptFrom.Z)))
                {
                    node1 = AddNodeAt(ptFrom);
                }
                else
                {
                    node1 = pointNodeAssociation[(ptFrom.X, ptFrom.Y, ptFrom.Z)];
                }

                // Ending node
                Node node2 = null;
                if (!pointNodeAssociation.ContainsKey((ptTo.X, ptTo.Y, ptTo.Z)))
                {
                    node2 = AddNodeAt(ptTo);
                }
                else
                {
                    node2 = pointNodeAssociation[(ptTo.X, ptTo.Y, ptTo.Z)];
                }

                if (node1 != null && node2 != null)
                {
                    int id = ff_model.AddNewBeam(node1.Id, node2.Id, mmPlane);
                    Beam bb = ff_model.Beams.Last();

                    if(!nodesBeamsAss.ContainsKey((node1.Id, node2.Id)))
                        nodesBeamsAss.Add((node1.Id, node2.Id), bb);
                    if (!nodesBeamsAss.ContainsKey((node2.Id, node1.Id)))
                        nodesBeamsAss.Add((node2.Id, node1.Id), bb);

                    var ff_beam = ff_model.Beams.Last();
                    ff_beam.Area = beam.BeamProperty.SectionArea;
                    ff_beam.Density = UnitsConvert.ConvertFromDefaultUnits(beam.BeamProperty.Material.Density, massUnits, 1, lengthUnits, -3);
                    //double fd = gh_beam.Value.ForceDensities.Sum(i => i.Value);
                    //double fd = gh_beam.Value.Loads.Where(l => l.GetType() == typeof(ForceDensityModel)).Sum(i => i.Value);

                    // Loads: each load is applied as its resultant force on the beam. The load
                    // components create only forces, uniform or keystone, in a global coordinate system.
                    for (int i = 0; i < beam.Loads.Count; i++)
                    {
                        LoadModel load = beam.Loads[i];
                        Vector3d v = beam.PointTo - beam.PointFrom;
                        if (v.Length == 0)
                            continue;
                        if (load is BeamPointLoadModel pl && pl.PointLoadType == BeamPointLoadModel.LoadType.Force)
                        {
                            Vector3d p = ToGlobal(pl.Value.CoordinateSystem, pl.Value.P);
                            AddBeamLoad(node1, node2, p, pl.Value.A);
                        }
                        else if (load is BeamDistributedLoadModel dl && dl.DistributedLoadType == BeamDistributedLoadModel.LoadType.Force)
                        {
                            double d = 0;
                            double q = 0;
                            double l = v.Length - v.Length * dl.Value.A - v.Length * dl.Value.B;
                            if (dl.Value.LoadSchema == BeamDistributedLoadModel.LoadSchema.Uniform)
                            {
                                d = v.Length * dl.Value.A + l / 2;
                                q = dl.Value.PA * l;
                            }
                            else if (dl.Value.LoadSchema == BeamDistributedLoadModel.LoadSchema.Keystone)
                            {
                                // Resultant at the centroid of the trapezoid, not at the middle of the span
                                double sum = dl.Value.PA + dl.Value.PB;
                                d = v.Length * dl.Value.A + (sum != 0 ? l * (dl.Value.PA + 2 * dl.Value.PB) / (3 * sum) : l / 2);
                                q = sum * l / 2;
                            }
                            else
                                continue;
                            Vector3d direction = Vector3d.Zero;
                            bool projected = false;
                            switch (dl.Value.LoadDirection)
                            {
                                case BeamDistributedLoadModel.LoadDirection.X_Projected:
                                    projected = true;
                                    goto case BeamDistributedLoadModel.LoadDirection.X;
                                case BeamDistributedLoadModel.LoadDirection.X:
                                    direction = ToGlobal(dl.Value.CoordinateSystem, Vector3d.XAxis);
                                    break;
                                case BeamDistributedLoadModel.LoadDirection.Y_Projected:
                                    projected = true;
                                    goto case BeamDistributedLoadModel.LoadDirection.Y;
                                case BeamDistributedLoadModel.LoadDirection.Y:
                                    direction = ToGlobal(dl.Value.CoordinateSystem, Vector3d.YAxis);
                                    break;
                                case BeamDistributedLoadModel.LoadDirection.Z_Projected:
                                    projected = true;
                                    goto case BeamDistributedLoadModel.LoadDirection.Z;
                                case BeamDistributedLoadModel.LoadDirection.Z:
                                    direction = ToGlobal(dl.Value.CoordinateSystem, Vector3d.ZAxis);
                                    break;
                                case BeamDistributedLoadModel.LoadDirection.Gravity_Projected:
                                    projected = true;
                                    goto case BeamDistributedLoadModel.LoadDirection.Gravity;
                                case BeamDistributedLoadModel.LoadDirection.Gravity:
                                    direction = -Vector3d.ZAxis;
                                    break;
                            }

                            // A projected load is given per unit length measured on the plane
                            // normal to the load direction.
                            if (projected)
                            {
                                double cos = (v / v.Length) * direction;
                                q *= Math.Sqrt(Math.Max(0, 1 - cos * cos));
                            }

                            AddBeamLoad(node1, node2, direction * q, d / v.Length);
                        }
                    }

                    IEnumerable<ForceDensityModel> fds = beam.Loads.Where(l => l.GetType() == typeof(ForceDensityModel)).Cast<ForceDensityModel>();

                    if (fds.Count() > 0)
                    {
                        ForceDensityModel fff = fds.FirstOrDefault();
                        double fd = fff.Value;
                        double ll = fff.CurvatureLengthValue;
                        double w = fff.Weight;

                        if (fff.ForceDensityTipe == ForceDensityModel.ForceDensityTypes.ForceDensity)
                            ff_beam.TargetForceDensity = fd;
                        else if (fff.ForceDensityTipe == ForceDensityModel.ForceDensityTypes.Cable)
                            ff_beam.TargetAxialForce = fd;
                        else if (fff.ForceDensityTipe == ForceDensityModel.ForceDensityTypes.Membrane)
                        {
                            ff_beam.TargetForceAreaDensity = fd;
                            ff_beam.MembraneType = (Beam.MembraneTypes)fff.MembraneType;
                        }
                        else if (fff.ForceDensityTipe == ForceDensityModel.ForceDensityTypes.Curvature)
                        {
                            ff_beam.TargetCurvatureRatio = fd;
                            ff_beam.CurveChord = ll;
                        }

                        ff_beam.Project = fff.Project;

                        if (w > 0)
                        {
                            ff_beam.FixNLForceDensity(w);
                            if (solverType == 0)
                                log.Add("Weights on force densities are ignored on the linear solver.");
                        }
                        if (fds.Count() > 1)
                            log.Add($"Beam {ff_beam} have multiple force density");
                    }

                    IEnumerable<FixedLengthModel> fls = beam.Loads.Where(l => l.GetType() == typeof(FixedLengthModel)).Cast<FixedLengthModel>();

                    if (fls.Count() > 0)
                    {
                        var fff = fls.First();
                        double fl = fff.Value;
                        double w = fff.Weight;
                        ff_beam.FixNLLength(fl, w);

                        if (solverType == 0)
                            log.Add("Fixed length is used only on the non-linear solver. In linear solver is ignored.");
                        if (fls.Count() > 1)
                            log.Add("More than one fixed length is set on a beam. It's considered only the first one.");
                    }

                    IEnumerable<FixedAxialForceModel> fafs = beam.Loads.Where(l => l.GetType() == typeof(FixedAxialForceModel)).Cast<FixedAxialForceModel>();

                    if (fafs.Count() > 0)
                    {
                        var ffa = fafs.First();
                        double af = ffa.Value;
                        double w = ffa.Weight;
                        ff_beam.TargetAxialForce = af;
                        ff_beam.FixNLAxialForce(w);
                        if (solverType == 0)
                            log.Add("Fixed axial force is used only on the non-linear solver. In linear solver is ignored.");
                        if (fafs.Count() > 1)
                            log.Add("More than one fixed axial force is set on a beam. It's considered only the first one.");
                    }
                }
                else
                {

                }
            }

            foreach (KeyValuePair<Node, Vector3d> share in beamLoadForces)
            {
                GPC.Geometry.Vector3d force = share.Key.NodeForce;
                share.Key.SetForce(force.X + share.Value.X, force.Y + share.Value.Y, force.Z + share.Value.Z);
            }

            if (createMesh)
            {
                for (int j = 0; j < modelModel.Plates.Count; j++)
                {
                    PlateModel plate = modelModel.Plates[j];

                    Point3d ptA = plate.Points[0];
                    Point3d ptB = plate.Points[1];
                    Point3d ptC = plate.Points[2];
                    Point3d ptD = Point3d.Unset;
                    if (plate.Points.Count == 4)
                        ptD = plate.Points[3];
                    if (plate.Points.Count > 4)
                        log.Add($"Plate n° {j} have more than vertices");

                    // Starting node
                    Node nodeA = null;
                    Node nodeB = null;
                    Node nodeC = null;
                    Node nodeD = null;

                    if (!pointNodeAssociation.ContainsKey((ptA.X, ptA.Y, ptA.Z)))
                    {
                        nodeA = AddNodeAt(ptA);
                    }
                    else
                    {
                        nodeA = pointNodeAssociation[(ptA.X, ptA.Y, ptA.Z)];
                    }

                    if (!pointNodeAssociation.ContainsKey((ptB.X, ptB.Y, ptB.Z)))
                    {
                        nodeB = AddNodeAt(ptB);
                    }
                    else
                    {
                        nodeB = pointNodeAssociation[(ptB.X, ptB.Y, ptB.Z)];
                    }

                    if (!pointNodeAssociation.ContainsKey((ptC.X, ptC.Y, ptC.Z)))
                    {
                        nodeC = AddNodeAt(ptC);
                    }
                    else
                    {
                        nodeC = pointNodeAssociation[(ptC.X, ptC.Y, ptC.Z)];
                    }

                    if (plate.Points.Count > 3)
                    {
                        if (!pointNodeAssociation.ContainsKey((ptD.X, ptD.Y, ptD.Z)))
                        {
                            nodeD = AddNodeAt(ptD);
                        }
                        else
                        {
                            nodeD = pointNodeAssociation[(ptD.X, ptD.Y, ptD.Z)];
                        }
                    }

                    if (plate.Points.Count == 3 && nodeA != null && nodeB != null && nodeC != null)
                    {
                        List<Beam> bb = new List<Beam>();
                        if (nodesBeamsAss.ContainsKey((nodeA.Id, nodeB.Id)))
                            bb.Add(nodesBeamsAss[(nodeA.Id, nodeB.Id)]);
                        if (nodesBeamsAss.ContainsKey((nodeB.Id, nodeC.Id)))
                            bb.Add(nodesBeamsAss[(nodeB.Id, nodeC.Id)]);
                        if (nodesBeamsAss.ContainsKey((nodeC.Id, nodeA.Id)))
                            bb.Add(nodesBeamsAss[(nodeC.Id, nodeA.Id)]);

                        Plate plt = new Plate(new Node[] { nodeA, nodeB, nodeC }, bb.ToArray(), mmPlane)
                        {
                            Id = ff_model.Plates.Count + 1,
                        };
                        ff_model.Plates.Add(plt);
                    }

                    if (plate.Points.Count > 3 && nodeA != null && nodeB != null && nodeC != null && nodeD != null)
                    {
                        List<Beam> bb = new List<Beam>();
                        if (nodesBeamsAss.ContainsKey((nodeA.Id, nodeB.Id)))
                            bb.Add(nodesBeamsAss[(nodeA.Id, nodeB.Id)]);
                        if (nodesBeamsAss.ContainsKey((nodeB.Id, nodeC.Id)))
                            bb.Add(nodesBeamsAss[(nodeB.Id, nodeC.Id)]);
                        if (nodesBeamsAss.ContainsKey((nodeC.Id, nodeD.Id)))
                            bb.Add(nodesBeamsAss[(nodeC.Id, nodeD.Id)]);
                        if (nodesBeamsAss.ContainsKey((nodeD.Id, nodeA.Id)))
                            bb.Add(nodesBeamsAss[(nodeD.Id, nodeA.Id)]);

                        Plate plt = new Plate(new Node[] { nodeA, nodeB, nodeC, nodeD }, bb.ToArray(), mmPlane)
                        {
                            Id = ff_model.Plates.Count + 1,
                        };
                        ff_model.Plates.Add(plt);
                    }
                }
            }

            ff_model.Pressure = pressure;
            ff_model.Gravity = gravity;
            ff_model.Plane = mmPlane;
            // TensileLib 2.1 has no CreateMesh switch: without plates the solver always
            // generates them from the beams, here done for every part of the net.
            if (ff_model.Plates.Count == 0)
            {
                GeneratePlatesForConnectedParts(ff_model);
                OrientPlates(ff_model, plane == Plane.Unset ? Vector3d.ZAxis : plane.Normal);
            }
            return ff_model;
        }

        /// <summary>
        /// TensileLib pushes the pressure along the winding of the plates, and the winding of the
        /// generated plates depends on the first beam of the walk. The plates of every part are
        /// turned so that a positive pressure acts along the normal of the generation plane.
        /// </summary>
        private static void OrientPlates(Model model, Vector3d normal)
        {
            List<Plate> plates = model.Plates.ToList();
            if (plates.Count == 0)
                return;

            // Parts: plates sharing a node
            Dictionary<int, int> parent = new Dictionary<int, int>();
            int Root(int id)
            {
                if (!parent.ContainsKey(id))
                    parent.Add(id, id);
                while (parent[id] != id)
                {
                    parent[id] = parent[parent[id]];
                    id = parent[id];
                }
                return id;
            }
            foreach (Plate plate in plates)
            {
                Node[] nodes = PlateNodes(plate);
                for (int i = 1; i < nodes.Length; i++)
                {
                    int a = Root(nodes[0].Id);
                    int b = Root(nodes[i].Id);
                    if (a != b)
                        parent[a] = b;
                }
            }

            Dictionary<int, double> flux = new Dictionary<int, double>();
            foreach (Plate plate in plates)
            {
                int root = Root(plate.Node1.Id);
                flux.TryGetValue(root, out double value);
                flux[root] = value + AreaVector(plate) * normal;
            }

            List<Plate> oriented = new List<Plate>(plates.Count);
            foreach (Plate plate in plates)
            {
                if (flux[Root(plate.Node1.Id)] >= 0)
                {
                    oriented.Add(plate);
                    continue;
                }
                // The first node is kept, so a quad is still split on the same diagonal
                Node[] nodes = PlateNodes(plate);
                Node[] reversed = new[] { nodes[0] }.Concat(nodes.Skip(1).Reverse()).ToArray();
                Beam[] beams = plate.Beams.Reverse().ToArray();
                oriented.Add(new Plate(reversed, beams, model.Plane) { Id = plate.Id });
            }
            model.Plates.Clear();
            model.Plates.AddRange(oriented);
        }

        private static Node[] PlateNodes(Plate plate)
        {
            return plate.PlateType == Plate.PlateTypes.Quad4
                ? new[] { plate.Node1, plate.Node2, plate.Node3, plate.Node4 }
                : new[] { plate.Node1, plate.Node2, plate.Node3 };
        }

        // Twice the area vector, with the split on the Node1-Node3 diagonal used for the pressure
        private static Vector3d AreaVector(Plate plate)
        {
            Point3d p1 = new Point3d(plate.Node1.X, plate.Node1.Y, plate.Node1.Z);
            Point3d p2 = new Point3d(plate.Node2.X, plate.Node2.Y, plate.Node2.Z);
            Point3d p3 = new Point3d(plate.Node3.X, plate.Node3.Y, plate.Node3.Z);
            Vector3d area = Vector3d.CrossProduct(p2 - p1, p3 - p1);
            if (plate.PlateType == Plate.PlateTypes.Quad4)
            {
                Point3d p4 = new Point3d(plate.Node4.X, plate.Node4.Y, plate.Node4.Z);
                area += Vector3d.CrossProduct(p3 - p1, p4 - p1);
            }
            return area;
        }

        /// <summary>
        /// TensileLib walks the faces from the first beam only: no plate is generated for the
        /// other disconnected nets, nor at all when the first beam is on no closed mesh (a mast,
        /// a backstay). The beams on no cycle are left out and every remaining part of the net
        /// is generated on its own.
        /// </summary>
        private static void GeneratePlatesForConnectedParts(Model model)
        {
            List<Node> nodes = model.Nodes.ToList();
            List<Beam> beams = model.Beams.Where(b => b.Node1.Id != b.Node2.Id).ToList();
            Dictionary<int, int> index = new Dictionary<int, int>(nodes.Count);
            for (int i = 0; i < nodes.Count; i++)
                index.Add(nodes[i].Id, i);
            List<(int a, int b)> edges = beams.Select(b => (index[b.Node1.Id], index[b.Node2.Id])).ToList();
            HashSet<int> bridges = Bridges(nodes.Count, edges);

            // Parts of the net once the bridges are removed
            List<int>[] adjacency = new List<int>[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
                adjacency[i] = new List<int>();
            for (int e = 0; e < edges.Count; e++)
            {
                if (bridges.Contains(e))
                    continue;
                adjacency[edges[e].a].Add(e);
                adjacency[edges[e].b].Add(e);
            }
            int[] part = Enumerable.Repeat(-1, nodes.Count).ToArray();
            int parts = 0;
            for (int start = 0; start < nodes.Count; start++)
            {
                if (part[start] >= 0 || adjacency[start].Count == 0)
                    continue;
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(start);
                part[start] = parts;
                while (queue.Count > 0)
                {
                    int n = queue.Dequeue();
                    foreach (int e in adjacency[n])
                    {
                        int next = edges[e].a == n ? edges[e].b : edges[e].a;
                        if (part[next] < 0)
                        {
                            part[next] = parts;
                            queue.Enqueue(next);
                        }
                    }
                }
                parts++;
            }

            if (parts == 1 && bridges.Count == 0)
            {
                model.GeneratePlatesFromBeams();
                return;
            }

            Dictionary<(int, int), Beam> beamsByNodes = new Dictionary<(int, int), Beam>();
            for (int e = 0; e < edges.Count; e++)
            {
                if (!beamsByNodes.ContainsKey((edges[e].a, edges[e].b))) beamsByNodes.Add((edges[e].a, edges[e].b), beams[e]);
                if (!beamsByNodes.ContainsKey((edges[e].b, edges[e].a))) beamsByNodes.Add((edges[e].b, edges[e].a), beams[e]);
            }
            for (int p = 0; p < parts; p++)
            {
                Model local = new Model { Plane = model.Plane };
                Dictionary<int, int> localIds = new Dictionary<int, int>();
                Dictionary<int, int> globalIndexes = new Dictionary<int, int>();
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (part[i] != p)
                        continue;
                    int localId = local.AddNewNode(nodes[i].X, nodes[i].Y, nodes[i].Z);
                    localIds.Add(i, localId);
                    globalIndexes.Add(localId, i);
                }
                for (int e = 0; e < edges.Count; e++)
                    if (!bridges.Contains(e) && part[edges[e].a] == p)
                        local.AddNewBeam(localIds[edges[e].a], localIds[edges[e].b], model.Plane);
                if (local.Beams.Count < 3)
                    continue;

                local.GeneratePlatesFromBeams();

                foreach (Plate plate in local.Plates)
                {
                    List<int> ids = PlateNodes(plate).Select(n => globalIndexes[n.Id]).ToList();
                    List<Beam> boundary = new List<Beam>();
                    for (int i = 0; i < ids.Count; i++)
                        if (beamsByNodes.TryGetValue((ids[i], ids[(i + 1) % ids.Count]), out Beam beam))
                            boundary.Add(beam);
                    model.Plates.Add(new Plate(ids.Select(i => nodes[i]).ToArray(), boundary.ToArray(), model.Plane)
                    {
                        Id = model.Plates.Count + 1,
                    });
                }
            }
        }

        /// <summary>
        /// The edges on no cycle (Tarjan), as indexes of <paramref name="edges"/>.
        /// </summary>
        private static HashSet<int> Bridges(int count, List<(int a, int b)> edges)
        {
            List<(int to, int edge)>[] adjacency = new List<(int, int)>[count];
            for (int i = 0; i < count; i++)
                adjacency[i] = new List<(int, int)>();
            for (int e = 0; e < edges.Count; e++)
            {
                adjacency[edges[e].a].Add((edges[e].b, e));
                adjacency[edges[e].b].Add((edges[e].a, e));
            }

            HashSet<int> bridges = new HashSet<int>();
            int[] discovery = new int[count];
            int[] low = new int[count];
            int time = 0;
            // Iterative depth first search: node, edge from the parent, next adjacency to visit
            Stack<(int node, int parentEdge, int next)> stack = new Stack<(int, int, int)>();
            for (int start = 0; start < count; start++)
            {
                if (discovery[start] != 0)
                    continue;
                discovery[start] = low[start] = ++time;
                stack.Push((start, -1, 0));
                while (stack.Count > 0)
                {
                    (int node, int parentEdge, int next) = stack.Pop();
                    if (next < adjacency[node].Count)
                    {
                        stack.Push((node, parentEdge, next + 1));
                        (int to, int edge) = adjacency[node][next];
                        if (edge == parentEdge)
                            continue;
                        if (discovery[to] == 0)
                        {
                            discovery[to] = low[to] = ++time;
                            stack.Push((to, edge, 0));
                        }
                        else
                            low[node] = Math.Min(low[node], discovery[to]);
                    }
                    else if (parentEdge >= 0)
                    {
                        int parentNode = edges[parentEdge].a == node ? edges[parentEdge].b : edges[parentEdge].a;
                        low[parentNode] = Math.Min(low[parentNode], low[node]);
                        if (low[node] > discovery[parentNode])
                            bridges.Add(parentEdge);
                    }
                }
            }
            return bridges;
        }

        /// <summary>
        /// TensileLib measures the convergence relative to the norm of the node coordinates, so
        /// far from the origin (site coordinates in mm) the iterations stop too early. The model
        /// is solved with the centre of its bounding box on the origin and then moved back.
        /// </summary>
        public sealed class OriginShift
        {
            private readonly List<(Node node, Point3d input)> _input;
            private readonly Vector3d _offset;

            /// <summary>
            /// Moves the model to the origin.
            /// </summary>
            public OriginShift(Model model)
            {
                _input = model.Nodes.Select(n => (n, new Point3d(n.X, n.Y, n.Z))).ToList();
                _offset = _input.Count > 0 ? new Vector3d(new BoundingBox(_input.Select(i => i.input)).Center) : Vector3d.Zero;
                foreach ((Node node, Point3d input) in _input)
                    node.Position = new GPC.Geometry.Point3d(input.X - _offset.X, input.Y - _offset.Y, input.Z - _offset.Z);
            }

            /// <summary>
            /// Moves the solved model back. The restrained coordinates get exactly their input value.
            /// </summary>
            public void Restore()
            {
                foreach ((Node node, Point3d input) in _input)
                    node.Position = new GPC.Geometry.Point3d(
                        node.Tx ? input.X : node.X + _offset.X,
                        node.Ty ? input.Y : node.Y + _offset.Y,
                        node.Tz ? input.Z : node.Z + _offset.Z);
            }
        }

        /// <summary>
        /// The case of the results computed by the form finding.
        /// </summary>
        public static LoadCaseModel FormFindingCase()
        {
            return new LoadCaseModel("FF", "Form Finding");
        }

        /// <summary>
        /// The forces that the supports exert on a solved model, in the order of
        /// <see cref="Model.Nodes"/>. At a restrained node the reaction balances the beam
        /// forces and the loads the solver applied to the node: node forces, beam self-weight,
        /// beam loads and pressure. Only the restrained directions are kept; the nodes
        /// without translational restraints get null.
        /// </summary>
        public static Vector3d?[] ComputeReactions(Model ff_model)
        {
            List<Node> nodes = ff_model.Nodes.ToList();
            Dictionary<int, int> indexes = new Dictionary<int, int>(nodes.Count);
            Vector3d[] sums = new Vector3d[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                indexes.Add(node.Id, i);
                sums[i] = ToRhino(node.NodeForce) + ToRhino(node.BeamSelfWeightForce) +
                    ToRhino(node.BeamLoadForce) + ToRhino(node.PressureForce);
            }

            foreach (Beam beam in ff_model.Beams)
            {
                Vector3d axis = new Vector3d(beam.Node2.X - beam.Node1.X, beam.Node2.Y - beam.Node1.Y, beam.Node2.Z - beam.Node1.Z);
                double length = axis.Length;
                if (length == 0)
                    continue;
                // A tension pulls each end towards the other one
                Vector3d pull = axis * (beam.AxialForce / length);
                sums[indexes[beam.Node1.Id]] += pull;
                sums[indexes[beam.Node2.Id]] -= pull;
            }

            Vector3d?[] reactions = new Vector3d?[nodes.Count];
            for (int i = 0; i < nodes.Count; i++)
            {
                Node node = nodes[i];
                if (!node.Tx && !node.Ty && !node.Tz)
                    continue;
                reactions[i] = new Vector3d(node.Tx ? -sums[i].X : 0, node.Ty ? -sums[i].Y : 0, node.Tz ? -sums[i].Z : 0);
            }
            return reactions;
        }

        private static Vector3d ToRhino(GPC.Geometry.Vector3d v)
        {
            return new Vector3d(v.X, v.Y, v.Z);
        }

        /// <summary>
        /// Converts a vector given in <paramref name="cs"/> into global coordinates.
        /// A null coordinate system is the global one.
        /// </summary>
        private static Vector3d ToGlobal(CoordinateSystemModel cs, Vector3d v)
        {
            if (cs == null)
                return v;
            Vector3d e1 = cs.Point1 - cs.Origin;
            Vector3d e3 = Vector3d.CrossProduct(e1, cs.Point2 - cs.Origin);
            Vector3d e2 = Vector3d.CrossProduct(e3, e1);
            if (!e1.Unitize() || !e2.Unitize() || !e3.Unitize())
                return v;
            return e1 * v.X + e2 * v.Y + e3 * v.Z;
        }

        public static Model AssemblyModel(List<Curve> curves, double tol, out List<string> log)
        {
            log = new List<string>();

            Model ff_model = new Model();

            RTree rTree = new RTree();

            for (int i = 0; i < curves.Count; i++)
            {
                Curve curve = curves[i];
                var ptStart = curve.PointAtStart;
                var ptEnd = curve.PointAtEnd;

                int idA = -1;
                int idB = -1;
                bool found = false;
                rTree.Search(new Sphere(ptStart, tol), new EventHandler<RTreeEventArgs>((sender, e) => { found = true; idA = e.Id; }));

                if (!found)
                {
                    idA = ff_model.AddNewNode(ptStart.X, ptStart.Y, ptStart.Z);
                    rTree.Insert(ptStart, idA);
                }

                found = false;
                rTree.Search(new Sphere(ptEnd, tol), new EventHandler<RTreeEventArgs>((sender, e) => { found = true; idB = e.Id; }));

                if (!found)
                {
                    idB = ff_model.AddNewNode(ptEnd.X, ptEnd.Y, ptEnd.Z);
                    rTree.Insert(ptEnd, idB);
                }

                ff_model.AddNewBeam(idA, idB);
            }

            return ff_model;
        }

        private struct Edge
        {
            public int A;
            public int B;
            public Edge(int a, int b)
            {
                A = a; B = b;
            }
        }

        private struct ScoreFace
        {
            public List<Edge> edges;
            public double score;

            public ScoreFace(List<Edge> e, double s = 0)
            {
                edges = e;
                score = s;
            }
        }

        private static string encodeFace(List<Edge> edges)
        {
            var vertices = new List<int>();
            for (int i = 0; i < edges.Count; ++i)
            {
                vertices.Add(edges[i].A);
            }
            return encodeFace(vertices);
        }

        private static string encodeFace(List<int> vertices)
        {
            vertices.Sort();
            var result = "";
            for (int i = 0; i < vertices.Count; ++i)
            {
                result += vertices[i];
                result += "_";
            }
            return result;
        }


        /// <summary>
        /// Find the shortest path for <paramref name="beam"/> with direction <paramref name="forward"/>. <paramref name="maxLen"/> is the path element limit 
        /// </summary>
        /// <param name="beam"></param>
        /// <param name="forward"></param>
        /// <param name="usedEdges"></param>
        /// <param name="maxLen"></param>
        /// <param name="skip"></param>
        /// <returns></returns>
        private static List<List<Edge>> FindPaths(Edge edge, Dictionary<int, List<Edge>> connections, HashSet<Edge> usedEdges, HashSet<string> usedFaces, int maxLen)
        {
            // The starting beam of the path
            var solution = new List<List<Edge>>();

            // All the paths starting from the beam. Init with one path containing only the starting beam.
            var paths = new List<List<Edge>>() { new List<Edge>() { edge } };

            int stopLength = maxLen;
            // While paths have not reached maximum length
            for (int i = 0; i < stopLength; ++i)
            {
                // Expand existing paths
                var newPaths = new List<List<Edge>>();

                for (int k = 0; k < paths.Count; k++)
                {
                    var path = paths[k];
                    var lastVert = path[path.Count - 1].B;
                    var connectedEdges = connections[lastVert];

                    // For each possible continuation of the path
                    for (int j = 0; j < connectedEdges.Count; j++)
                    {
                        var nextEdge = connectedEdges[j];
                        var tempPath = new List<Edge>();
                        var tempNodeSet = new HashSet<int>();
                        // Copy the path into temp
                        for (int p = 0; p < path.Count; ++p)
                        {
                            tempPath.Add(path[p]);
                            tempNodeSet.Add(path[p].B);
                        }

                        // Invalid path, we are reusing one beam
                        if (tempNodeSet.Contains(nextEdge.B) || usedEdges.Contains(nextEdge))
                            continue;

                        tempPath.Add(nextEdge);
                        tempNodeSet.Add(nextEdge.B);

                        // Check if the path is closed
                        if (nextEdge.B == tempPath[0].A)
                        {
                            // Invalid path
                            if (tempPath.Count < 3)
                            {
                                continue;
                            }

                            // Flipped existing face
                            if (usedFaces.Contains(encodeFace(tempPath)))
                            {
                                continue;
                            }

                            // Good face! Stop search at this depth
                            stopLength = tempPath.Count - 1;
                        }

                        // Add current path 
                        newPaths.Add(tempPath);
                    }
                }

                // Replace paths
                paths = newPaths;
            }

            for (int i = 0; i < paths.Count; ++i)
            {
                if (paths[i][0].A == paths[i][paths[i].Count - 1].B)
                {
                    solution.Add(paths[i]);
                }
            }

            // Return an empty solution
            return solution;
        }

        private static Vector3d FaceNormal(List<Edge> edges, List<Point3d> vertices)
        {
            var a = vertices[edges[0].A];
            var b = vertices[edges[0].B];
            var c = vertices[edges[1].B];
            var dirA = new Vector3d(a - b);
            var dirB = new Vector3d(c - b);
            var cross = Vector3d.CrossProduct(dirA, dirB);
            cross.Unitize();
            return cross;
        }

        private static double ComputeFaceScore(List<Edge> edges, List<Point3d> vertices, Vector3d normal)
        {
            double score = edges.Count - 3.0;
            var cross = FaceNormal(edges, vertices);
            var dot = Vector3d.Multiply(cross, normal);
            score += dot * (-5.0);
            return score;
        }

        private static bool IsFaceValid(List<Edge> edges, HashSet<Edge> usedEdges, HashSet<string> usedFaces)
        {
            for (int i = 0; i < edges.Count; ++i)
            {
                if (usedEdges.Contains(edges[i]))
                {
                    return false;
                }
                if (usedFaces.Contains(encodeFace(edges)))
                {
                    return false;
                }
            }
            return true;
        }

        public static Mesh GenerateMeshFromCurves(List<Curve> curves, int NUMBEROFTOTALPATHS = 6)
        {
            Mesh mesh = new Mesh();

            var vertexmap = new Dictionary<Point3d, int>();
            var vertices = new List<Point3d>();
            var edges = new List<Edge>();
            var faces = new List<int[]>();

            var connections = new Dictionary<int, List<Edge>>();

            var usedEdges = new HashSet<Edge>();
            var usedFaces = new HashSet<string>();
            var exploredPaths = new List<ScoreFace>();
            var exploredFaces = new HashSet<string>();

            // Get all connections
            for (int i = 0; i < curves.Count; i++)
            {
                if (!vertexmap.ContainsKey(curves[i].PointAtStart))
                {
                    vertexmap.Add(curves[i].PointAtStart, vertices.Count);
                    vertices.Add(curves[i].PointAtStart);
                }
                if (!vertexmap.ContainsKey(curves[i].PointAtEnd))
                {
                    vertexmap.Add(curves[i].PointAtEnd, vertices.Count);
                    vertices.Add(curves[i].PointAtEnd);
                }
                var a = vertexmap[curves[i].PointAtStart];
                var b = vertexmap[curves[i].PointAtEnd];
                edges.Add(new Edge(a, b));
                edges.Add(new Edge(b, a));

                if (!connections.ContainsKey(a))
                {
                    connections.Add(a, new List<Edge>());
                }
                if (!connections.ContainsKey(b))
                {
                    connections.Add(b, new List<Edge>());
                }

                connections[a].Add(new Edge(a, b));
                connections[b].Add(new Edge(b, a));
            }

            var currentNormal = new Vector3d(0, 0, 0);
            var acceptedScore = 0.0;
            var toExplore = new List<Edge>() { edges[0] };
            
            while (toExplore.Count > 0)
            {
                var edge = toExplore[0];
                toExplore.RemoveAt(0);

                var facePaths = FindPaths(edge, connections, usedEdges, usedFaces, NUMBEROFTOTALPATHS);
                var solutions = new List<ScoreFace>();
                for (int s = 0; s < facePaths.Count; s++)
                {
                    solutions.Add(new ScoreFace(facePaths[s], ComputeFaceScore(facePaths[s], vertices, currentNormal)));
                }
                solutions.Sort((solutionA, solutionB) => { return solutionA.score.CompareTo(solutionB.score); });

                if (solutions.Count == 0)
                {
                    continue;
                }


                if (solutions[0].score > acceptedScore)
                {
                    acceptedScore = solutions[0].score;
                    toExplore.Add(edge);
                    continue;
                }

                currentNormal = (currentNormal + FaceNormal(solutions[0].edges, vertices)) * 0.5;

                var solutionPath = solutions[0].edges;
                var face = new int[solutionPath.Count];

                for (int j = 0; j < solutionPath.Count; ++j)
                {
                    face[j] = solutionPath[j].A;
                    usedEdges.Add(solutionPath[j]);
                }
                usedFaces.Add(encodeFace(solutionPath));

                // Add face to mesh
                if (face.Length < 5)
                {
                    // 3-4 edges face
                    faces.Add(face);
                }
                else
                {
                    // Face triangulation
                    for (int i = 1; i < face.Length - 1; ++i)
                    {
                        faces.Add(new int[3] { face[0], face[i], face[i + 1] });
                    }
                }

                for (int j = 0; j < solutionPath.Count; ++j)
                {
                    var opposite = new Edge(solutionPath[j].B, solutionPath[j].A);
                    if (!usedEdges.Contains(opposite))
                    {
                        toExplore.Insert(0, opposite);
                    }
                }
            }

            // Create the mesh
            mesh.Vertices.AddVertices(vertices);
            for (int i = 0; i < faces.Count; ++i)
            {
                if (faces[i].Length == 3)
                {
                    mesh.Faces.AddFace(faces[i][0], faces[i][1], faces[i][2]);
                }
                else
                {
                    mesh.Faces.AddFace(faces[i][0], faces[i][1], faces[i][2], faces[i][3]);
                }
            }
            return mesh;
        }

    }
}
