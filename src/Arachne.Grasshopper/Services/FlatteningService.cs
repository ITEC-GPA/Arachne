using System;
using System.Collections.Generic;
using System.Linq;
using Rhino.Geometry;
using TensileLib.Geometry;
using TensileLib.FormFinding;
namespace Arachne.Grasshopper.Services
{
    internal static class FlatteningService
    {
        internal static void GetFlattenedMesh(Mesh mesh, double borderWeigth, int max_iter, double tol, double epsS, int maxIterSolver, out Solver.ResultCodes ret, out Mesh flat)
        {
            if(mesh==null || !mesh.IsValid || mesh.Faces.Count==0)throw new ArgumentException("A valid non-empty mesh is required.");
            Arachne.Common.Helpers.Validation.Positive(borderWeigth,"Border weight");Arachne.Common.Helpers.Validation.Positive(max_iter,"Iterations");
            Arachne.Common.Helpers.Validation.Positive(tol,"Tolerance");Arachne.Common.Helpers.Validation.NonNegative(maxIterSolver,"Algebraic iterations");Arachne.Common.Helpers.Validation.NonNegative(epsS,"Algebraic tolerance");
            // The input mesh is not modified
            mesh = mesh.DuplicateMesh();
            mesh.Faces.ConvertQuadsToTriangles();

            //Moving vertices to a fitted plane througth the 3d mesh
            Plane startingSdr = Plane.WorldXY;
            Vector3d normal = Vector3d.Zero;

            mesh.RebuildNormals();
            for (int i = 0; i < mesh.FaceNormals.Count; i++)
            {
                normal += mesh.FaceNormals[i];
            }

            normal /= mesh.FaceNormals.Count;
            Point3d origin = Point3d.Origin;

            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Point3d v = (Point3d)mesh.Vertices[i];
                origin += v;
            }
            origin /= mesh.Vertices.Count;

            if (normal * Vector3d.ZAxis < 0)
            {
                normal *= -1;
            }

            Vector3d xDir, yDir;
            if (Math.Abs(normal * Vector3d.YAxis) > 0.9999)
            {
                yDir = Vector3d.CrossProduct(normal, Vector3d.XAxis);
                xDir = Vector3d.CrossProduct(yDir, normal);
            }
            else
            {
                xDir = Vector3d.CrossProduct(Vector3d.YAxis, normal);
                yDir = Vector3d.CrossProduct(normal, xDir);
            }

            Plane fitSdr = new Plane(origin, origin + xDir, origin + yDir);
            Transform trans = Transform.ChangeBasis(startingSdr, fitSdr);

            // Create the model for the flattening solver starting from the mesh.
            Model model = new Model();

            for (int i = 0; i < mesh.Vertices.Count; i++)
            {
                Point3d pt = (Point3d)mesh.Vertices[i];
                //Inserting the local points
                Point3d fitPoint = trans * pt;
                model.AddNewNode(fitPoint.X, fitPoint.Y, fitPoint.Z);
            }

            Polyline[] ne = mesh.GetNakedEdges();

            for (int k = 0; k < mesh.Faces.Count; k++)
            {
                MeshFace face = mesh.Faces[k];
                bool is_e1_naked = false;
                bool is_e2_naked = false;
                bool is_e3_naked = false;
                bool is_e4_naked = false;

                if (ne != null)
                {
                    for (int j = 0; j < ne.Length; j++)
                    {
                        Polyline p = ne[j];
                        for (int i = 0; i < p.SegmentCount; i++)
                        {
                            if (p.SegmentAt(i).From == mesh.Vertices[face.A] && p.SegmentAt(i).To == mesh.Vertices[face.B] ||
                                p.SegmentAt(i).From == mesh.Vertices[face.B] && p.SegmentAt(i).To == mesh.Vertices[face.A])
                                is_e1_naked = true;
                            if (p.SegmentAt(i).From == mesh.Vertices[face.B] && p.SegmentAt(i).To == mesh.Vertices[face.C] ||
                                p.SegmentAt(i).From == mesh.Vertices[face.C] && p.SegmentAt(i).To == mesh.Vertices[face.B])
                                is_e2_naked = true;
                            if (face.IsQuad)
                            {
                                if (p.SegmentAt(i).From == mesh.Vertices[face.C] && p.SegmentAt(i).To == mesh.Vertices[face.D] ||
                                    p.SegmentAt(i).From == mesh.Vertices[face.D] && p.SegmentAt(i).To == mesh.Vertices[face.C])
                                    is_e3_naked = true;
                                if (p.SegmentAt(i).From == mesh.Vertices[face.D] && p.SegmentAt(i).To == mesh.Vertices[face.A] ||
                                    p.SegmentAt(i).From == mesh.Vertices[face.A] && p.SegmentAt(i).To == mesh.Vertices[face.D])
                                    is_e4_naked = true;
                            }
                            else
                            {
                                if (p.SegmentAt(i).From == mesh.Vertices[face.A] && p.SegmentAt(i).To == mesh.Vertices[face.C] ||
                                    p.SegmentAt(i).From == mesh.Vertices[face.C] && p.SegmentAt(i).To == mesh.Vertices[face.A])
                                    is_e3_naked = true;
                            }
                        }
                    }
                }

                Node id1 = null;
                Node id2 = null;
                Node id3 = null;
                Node id4 = null;

                AddBeamToModel(model, face.A, face.B, is_e1_naked, borderWeigth, ref id1, ref id2);
                AddBeamToModel(model, face.B, face.C, is_e2_naked, borderWeigth, ref id2, ref id3);
                if (face.IsQuad)
                {
                    AddBeamToModel(model, face.C, face.D, is_e3_naked, borderWeigth, ref id3, ref id4);
                    AddBeamToModel(model, face.D, face.A, is_e4_naked, borderWeigth, ref id4, ref id1);
                }
                else
                {
                    AddBeamToModel(model, face.C, face.A, is_e3_naked, borderWeigth, ref id3, ref id1);
                }

                model.AddNewPlate(id1.Id, id2.Id, id3.Id, id4 == null ? -1 : id4.Id);
            }

            // Solve 
            PlanarFixedLengthsSolver solver = new PlanarFixedLengthsSolver(model, max_iter, tol, epsS, maxIterSolver);
            ret = solver.Solve(out PlanarFixedLengthsSolver.PlanarFixedLengthsResult planarFixedLengthsResult);

            // Also at the iteration limit, as in FeMM: the component reports it with a warning
            flat = new Mesh();

            //Changeing coords
            Point3d newOrigin = new Point3d(fitSdr.Origin.X, fitSdr.Origin.Y, 0);
            Transform transPlane = Transform.ChangeBasis(new Plane(newOrigin, Vector3d.ZAxis), startingSdr);
            for (int i = 0; i < model.Nodes.Count; i++)
            {
                Node node = model.Nodes.ElementAt(i);
                Point3d planePt = transPlane * new Point3d(node.X, node.Y, node.Z);
                flat.Vertices.Add(planePt);
            }

            for (int i = 0; i < model.Plates.Count; i++)
            {
                Plate plate = model.Plates.ElementAt(i);
                int i1 = model.Nodes.IndexOf(plate.Node1);
                int i2 = model.Nodes.IndexOf(plate.Node2);
                int i3 = model.Nodes.IndexOf(plate.Node3);
                int i4 = model.Nodes.IndexOf(plate.Node4);

                if (plate.PlateType == Plate.PlateTypes.Quad4)
                    flat.Faces.AddFace(i1, i2, i3, i4);
                else if (plate.PlateType == Plate.PlateTypes.Tri3)
                    flat.Faces.AddFace(i1, i2, i3);
            }

            flat.Normals.ComputeNormals();
            flat.Compact();
        }

        private static bool AddBeamToModel(Model model, int i1, int i2, bool isNaked, double borderWeigth, ref Node id1, ref Node id2)
        {
            Node node1 = model.Nodes.ElementAt(i1);
            Node node2 = model.Nodes.ElementAt(i2);
            double len = new Point3d(node1.X, node1.Y, node1.Z).DistanceTo(new Point3d(node2.X, node2.Y, node2.Z));

            if (node1 != null && node2 != null)
            {
                id1 = node1;
                id2 = node2;

                IEnumerable<Beam> beams = model.Beams.Where(b => b.Node1.Id == node1.Id && b.Node2.Id == node2.Id ||
                    b.Node1.Id == node2.Id && b.Node2.Id == node1.Id);
                if (beams.Count() > 0)
                    return false;

                int id = model.AddNewBeam(id1.Id, id2.Id);
                if (isNaked)
                    model.Beams[id].FixNLLength(len, borderWeigth);
                else
                    model.Beams[id].FixNLLength(len, 1);
                return true;
            }

            return false;
        }

    }
}
