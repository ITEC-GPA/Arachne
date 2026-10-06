using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using TensileLib.Geometry;
using TensileLib.FormFinding;
namespace Arachne.Grasshopper.Services
{
    internal static class CompensationService
    {
        public static Mesh Apply(Mesh panel, Vector3d warp_dir, double waPerc, double wePerc, IReadOnlyList<Arachne.Core.Cable> fixedBeams, int maxIterNL, double tol, int maxIterSolver, double epsS, double geometryTolerance)
        {
            if(panel==null || !panel.IsValid || panel.Faces.Count==0)throw new ArgumentException("A valid non-empty panel is required.");
            if(!warp_dir.IsValid || warp_dir.IsTiny())throw new ArgumentException("Warp direction must be non-zero and finite.");
            Arachne.Core.Validation.Finite(waPerc,"Warp percent");Arachne.Core.Validation.Finite(wePerc,"Weft percent");
            if(waPerc>=100 || wePerc>=100)throw new ArgumentException("Compensation must be less than 100 percent.");
            Arachne.Core.Validation.Positive(tol,"Relative tolerance");Arachne.Core.Validation.Positive(maxIterNL,"Iterations");
            Arachne.Core.Validation.NonNegative(maxIterSolver,"Algebraic iterations");Arachne.Core.Validation.NonNegative(epsS,"Algebraic tolerance");
            if(maxIterSolver==0)maxIterSolver=10000;
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

                    // Arachne Cable Constraints replaces the legacy FEM beam attributes.
                    foreach (var cable in fixedBeams)
                    {

                        if (cable == null) throw new ArgumentException("Fixed lengths expects Arachne cables from Cable Constraints.");
                        var from = new Point3d(cable.Start.X, cable.Start.Y, cable.Start.Z);
                        var to = new Point3d(cable.End.X, cable.End.Y, cable.End.Z);
                        double matchTolerance = geometryTolerance;
                        if ((from.DistanceTo(firstPOriginal) < matchTolerance && to.DistanceTo(secondPOriginal) < matchTolerance) ||
                            (to.DistanceTo(firstPOriginal) < matchTolerance && from.DistanceTo(secondPOriginal) < matchTolerance))
                        {
                            if (cable.FixedLength.HasValue) { fixedLength = cable.FixedLength.Value; weigth = cable.ConstraintWeight; break; }
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
            var code = solver.Solve(out PlanarFixedLengthsSolver.PlanarFixedLengthsResult details);
            if(code != Solver.ResultCodes.Converged && code != Solver.ResultCodes.ConvergedUsingSlowMethodWithoutWeigths)
                throw new InvalidOperationException("Not converged. No compensated mesh produced.");
            // If success make a new compensated mesh
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
