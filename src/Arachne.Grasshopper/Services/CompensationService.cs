using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Rhino.Geometry;
using TensileLib.Geometry;
using TensileLib.FormFinding;
namespace Arachne.Grasshopper.Services
{
    internal static class CompensationService
    {
        /// <summary>
        /// Compensates a flat panel. The mesh is returned also when the solver stops at the
        /// iteration limit (<paramref name="code"/> tells it); solver failures throw.
        /// </summary>
        public static Mesh Apply(Mesh panel, Vector3d warp_dir, double waPerc, double wePerc, IReadOnlyList<BeamModel> fixedBeams, int maxIterNL, double tol, int maxIterSolver, double epsS, double geometryTolerance, out Solver.ResultCodes code)
        {
            if(panel==null || !panel.IsValid || panel.Faces.Count==0)throw new ArgumentException("A valid non-empty panel is required.");
            if(!warp_dir.IsValid || warp_dir.IsTiny())throw new ArgumentException("Warp direction must be non-zero and finite.");
            Validation.Finite(waPerc,"Warp percent");Validation.Finite(wePerc,"Weft percent");
            if(waPerc>=100 || wePerc>=100)throw new ArgumentException("Compensation must be less than 100 percent.");
            Validation.Positive(tol,"Relative tolerance");Validation.Positive(maxIterNL,"Iterations");
            Validation.NonNegative(maxIterSolver,"Algebraic iterations");Validation.NonNegative(epsS,"Algebraic tolerance");
            //This component use the flattening solver of tensile lib. The idea is to impose fixed lengths as compensated to the panel.
            panel = panel.DuplicateMesh();
            panel.Faces.ConvertQuadsToTriangles();
            panel.Normals.ComputeNormals();

            if (Math.Abs(warp_dir.Z) > geometryTolerance)
            {
                throw new NotSupportedException("Warp dir must be defined on plane xy");
            }

            //Testing planar mesh
            for (int i = 0; i < panel.Vertices.Count; i++)
            {
                if (Math.Abs(panel.Normals[i] * Vector3d.ZAxis) < 0.999)
                {
                    throw new NotSupportedException("Face " + (i + 1).ToString() + " not on plane xy");
                }
            }

            //x warp
            //y weft

            Vector2d dirX = new Vector2d(warp_dir.X, warp_dir.Y);
            dirX.Unitize();
            Vector2d dirY = new Vector2d(-dirX.Y, dirX.X);

            // Create the model for the flattening solver starting from the mesh.
            Model model = new Model();
            Dictionary<int, Point3d> modifiedNodes = new Dictionary<int, Point3d>();
            int index = 0;

            for (int i = 0; i < panel.Vertices.Count; i++)
            {
                Point3d pt = (Point3d)panel.Vertices.ElementAt(i);

                //original nodes
                model.AddNewNode(pt.X, pt.Y, pt.Z);

                //Modified position due to normal stretch
                double localX = pt.X * dirX.X + pt.Y * dirX.Y;
                double localY = pt.X * dirY.X + pt.Y * dirY.Y;
                //stretching of warp and weft deformations (>0=reduction)
                localX *= 1 - waPerc / 100;
                localY *= 1 - wePerc / 100;
                modifiedNodes.Add(index, new Point3d(localX, localY, pt.Z));
                index++;
            }

            for (int i = 0; i < panel.Faces.Count; i++)
            {
                MeshFace face = panel.Faces.ElementAt(i);
                if (!face.IsTriangle)
                {
                    throw new NotSupportedException();
                }

                for (int edgeId = 0; edgeId < 3; edgeId++)
                {
                    double fixedLength = double.MinValue;
                    double weigth = double.MinValue;
                    int firstIndex;
                    int secondIndex;

                    //A=0;B=1;C=2
                    if (edgeId == 0)
                    {
                        firstIndex = face.A;
                        secondIndex = face.B;
                    }
                    else if (edgeId == 1)
                    {
                        firstIndex = face.B;
                        secondIndex = face.C;
                    }
                    else
                    {
                        firstIndex = face.C;
                        secondIndex = face.A;
                    }

                    Node node1 = model.Nodes.ElementAt(firstIndex);
                    Node node2 = model.Nodes.ElementAt(secondIndex);
                    Beam existing = model.Beams.FirstOrDefault(b => b.Node1.Id == node1.Id && b.Node2.Id == node2.Id || b.Node1.Id == node2.Id && b.Node2.Id == node1.Id);
                    if (existing != null)
                        continue;

                    Point3d firstPOriginal = panel.Vertices[firstIndex];
                    Point3d secondPOriginal = panel.Vertices[secondIndex];

                    Point3d firstPMod = modifiedNodes[firstIndex];
                    Point3d secondPMod = modifiedNodes[secondIndex];

                    //serching fixed beam
                    for (int j = 0; j < fixedBeams.Count; j++)
                    {
                        BeamModel beam = fixedBeams[j];
                        if (beam.PointFrom.DistanceTo(firstPOriginal) < geometryTolerance &&
                            beam.PointTo.DistanceTo(secondPOriginal) < geometryTolerance ||
                            beam.PointTo.DistanceTo(firstPOriginal) < geometryTolerance &&
                             beam.PointFrom.DistanceTo(secondPOriginal) < geometryTolerance)
                        {
                            //found
                            //searching if beam have fixed length
                            for (int k = 0; k < beam.Loads.Count; k++)
                            {
                                LoadModel lm = beam.Loads[k];
                                FixedLengthModel flm = lm as FixedLengthModel;
                                if (flm != null)
                                {
                                    fixedLength = flm.Value;
                                    weigth = flm.Weight;
                                    break;
                                }
                            }
                            if (fixedLength > 0)
                                break;
                        }
                    }

                    if (fixedLength < 0)
                    {
                        fixedLength = firstPMod.DistanceTo(secondPMod);
                        weigth = 1;
                    }

                    int beamId = model.AddNewBeam(node1.Id, node2.Id);
                    model.Beams[beamId].FixNLLength(fixedLength, weigth);
                }

                model.AddNewPlate(model.Nodes.ElementAt(face.A).Id, model.Nodes.ElementAt(face.B).Id, model.Nodes.ElementAt(face.C).Id);
            }

            var solver = new PlanarFixedLengthsSolver(model, maxIterNL, tol, epsS, maxIterSolver);
            code = solver.Solve(out PlanarFixedLengthsSolver.PlanarFixedLengthsResult details);
            if(code != Solver.ResultCodes.Converged && code != Solver.ResultCodes.ConvergedUsingSlowMethodWithoutWeigths &&
               code != Solver.ResultCodes.NotconvergedAtMaxIterationNumber)
                throw new NotSupportedException("Unexpected solver result: " + code);
            // Make a new compensated mesh
            Mesh compensated = new Mesh();
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes.ElementAt(i);
                compensated.Vertices.Add(node.X, node.Y, node.Z);
            }

            for (int i = 0; i < model.Plates.Count; i++)
            {
                Plate plate = model.Plates.ElementAt(i);
                int i1 = model.Nodes.IndexOf(plate.Node1);
                int i2 = model.Nodes.IndexOf(plate.Node2);
                int i3 = model.Nodes.IndexOf(plate.Node3);
                int i4 = model.Nodes.IndexOf(plate.Node4);

                if (plate.PlateType == Plate.PlateTypes.Quad4)
                    compensated.Faces.AddFace(i1, i2, i3, i4);
                else if (plate.PlateType == Plate.PlateTypes.Tri3)
                    compensated.Faces.AddFace(i1, i2, i3);
            }

            compensated.Normals.ComputeNormals();
            compensated.Compact();

            return compensated;
        }
    }
}
