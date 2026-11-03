// Test-only input builder captured from Arachne commit 5ddbe6f8a78de788a120c23cd6015c350cd2c320.
// This is an independent comparison path, never linked into the production plug-in.
using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using GPC.Utilities.Units;
using Rhino.Geometry;
using TensileLib.Geometry;
internal static class LegacyFormFindingBaseline
{
        public static Model AssemblyModel(ModelModel modelModel, Plane plane, int solverType, SolverOptionsModel options, out List<string> log)
        {
            log = new List<string>();
            double gravity = options.Gravity;
            double pressure = options.Pressure;
            bool createMesh = options.CreateMesh;

            Model ff_model = new Model();
            ModelHelper.GetUtilitiesConversionParams(modelModel.Units, out UnitsConvert.LengthUnits lengthUnits,
                out UnitsConvert.ForceUnits forceUnits, out UnitsConvert.MassUnits massUnits,
                out UnitsConvert.PressureUnits pressureUnits, out UnitsConvert.TemperatureUnits temperatureUnits);

            // Node and beam loads of the selected load cases only; all of them if none is selected
            List<LoadCaseModel> loadCases = options.LoadCases ?? new List<LoadCaseModel>();
            HashSet<LoadCaseModel> usedLoadCases = new HashSet<LoadCaseModel>();
            bool Applies(LoadModel load)
            {
                if (loadCases.Count == 0)
                    return true;
                if (!loadCases.Any(lc => lc == load.LoadCase))
                    return false;
                usedLoadCases.Add(load.LoadCase);
                return true;
            }

            // Weight of the plates: thickness x density; for a membrane material the weight per
            // area of its data sheet (the thickness then cancels out)
            void SetWeight(Plate plate, PlatePropertyModel property)
            {
                if (property?.Material == null || !(property.Thickness > 0))
                    return;
                plate.MemThick = property.Thickness;
                plate.Density = property.Material.Kind == MaterialModel.MaterialKind.Membrane
                    ? UnitsConvert.ConvertFromDefaultUnits(property.Material.AreaDensity, massUnits, 1, lengthUnits, -2) / property.Thickness
                    : UnitsConvert.ConvertFromDefaultUnits(property.Material.Density, massUnits, 1, lengthUnits, -3);
            }

            GPC.Geometry.Plane mmPlane;
            if (plane == Plane.Unset)
                mmPlane = new GPC.Geometry.Plane(GPC.Geometry.Point3d.Origin, GPC.Geometry.Vector3d.ZAxis);
            else
                mmPlane = new GPC.Geometry.Plane(new GPC.Geometry.Point3d(plane.Origin.X, plane.Origin.Y, plane.Origin.Z),
                    new GPC.Geometry.Vector3d(plane.Normal.X, plane.Normal.Y, plane.Normal.Z));

            Dictionary<(double, double, double), Node> pointNodeAssociation = new Dictionary<(double, double, double), Node>();
            bool previousPressure = false;

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
                    // Restraints along the axes of a plane (TensileLib 2.4)
                    if (restraint.CoordinateSystem != null)
                    {
                        if (restraint.CoordinateSystem.GetAxes(out Vector3d xAxis, out Vector3d yAxis, out _))
                            ff_node.SetRestraintAxes(ToGpc(xAxis), ToGpc(yAxis));
                        else
                            log.Add($"The coordinate system of the restraints of node {node.NodeId} has no axes: the global axes are used.");
                    }
                }
                // The forces of the pressure found by a previous form finding are results, not
                // loads: the pressure of the options acts on the new shape
                if (node.Loads.Any(l => l is NodePressureLoadModel))
                    previousPressure = true;
                IEnumerable<NodeForceLoadModel> forces = node.Loads.Where(l => l.GetType() == typeof(NodeForceLoadModel)).Cast<NodeForceLoadModel>().Where(Applies).ToList();

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

                    // Loads: each load is applied as its resultant force on the beam, at its distance
                    // from the first node (TensileLib keeps it as a fraction of the length). The load
                    // components create only forces, uniform or keystone, in a global coordinate system.
                    for (int i = 0; i < beam.Loads.Count; i++)
                    {
                        LoadModel load = beam.Loads[i];
                        if ((load is BeamPointLoadModel || load is BeamDistributedLoadModel) && !Applies(load))
                            continue;
                        Vector3d v = beam.PointTo - beam.PointFrom;
                        if (v.Length == 0)
                            continue;
                        if (load is BeamPointLoadModel pl && pl.PointLoadType == BeamPointLoadModel.LoadType.Force)
                        {
                            Vector3d p = ToGlobal(pl.Value.CoordinateSystem, pl.Value.P);
                            ff_beam.AddLoad(p.X, p.Y, p.Z, v.Length * pl.Value.A);
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

                            Vector3d p = direction * q;
                            ff_beam.AddLoad(p.X, p.Y, p.Z, d);
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
                        SetWeight(plt, plate.PlateProperty);
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
                        SetWeight(plt, plate.PlateProperty);
                        ff_model.Plates.Add(plt);
                    }
                }
            }

            ff_model.Pressure = pressure;
            ff_model.Gravity = gravity;
            ff_model.Plane = mmPlane;
            // TensileLib 2.2 has no CreateMesh switch: without plates the solver generates them
            // from the beams, for every part of the net, oriented along the normal of the plane.
            // They weigh as the Plate Property of the options.
            if (ff_model.Plates.Count == 0)
            {
                ff_model.GeneratePlatesFromBeams();
                foreach (Plate plate in ff_model.Plates)
                    SetWeight(plate, options.PlateProperty);
            }
            if (gravity == 0 && ff_model.Plates.Any(p => p.WeightMultiplier > 0))
                log.Add("The plates have a weight but Gravity is 0: the self weight is not applied.");
            foreach (LoadCaseModel lc in loadCases.Where(lc => !usedLoadCases.Contains(lc)))
                log.Add($"No node or beam load in the load case {lc.Name}.");
            if (previousPressure)
                log.Add("The pressure forces of a previous form finding are not applied: the Pressure of the Solver Options acts on the new shape.");
            return ff_model;
        }
        private static GPC.Geometry.Vector3d ToGpc(Vector3d v)
        {
            return new GPC.Geometry.Vector3d(v.X, v.Y, v.Z);
        }

        /// <summary>
        /// Converts a vector given in <paramref name="cs"/> into global coordinates.
        /// A null coordinate system is the global one.
        /// </summary>
        private static Vector3d ToGlobal(CoordinateSystemModel cs, Vector3d v)
        {
            if (cs == null || !cs.GetAxes(out Vector3d e1, out Vector3d e2, out Vector3d e3))
                return v;
            return e1 * v.X + e2 * v.Y + e3 * v.Z;
        }

}
