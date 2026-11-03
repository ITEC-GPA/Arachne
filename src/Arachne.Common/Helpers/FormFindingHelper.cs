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
        /// <summary>Compatibility entry point; all physical solver input now passes through GPC.</summary>
        public static Model AssemblyModel(ModelModel model, Plane plane, int solverType, SolverOptionsModel options, out List<string> log)
        {
            var input = Analysis.FormFindingInputBuilder.Build(model, plane, solverType, options, model.InputOwner, model.InputIdentities);
            log = input.Diagnostics;
            return Analysis.TensileModelAdapter.Export(input.Model, input.Units).Solver;
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
        /// forces and the loads the solver applied to the node: node forces, self-weight of
        /// beams and plates, beam loads and pressure. Only the part along the restrained
        /// directions is kept (the axes of the restraints, global or of a plane); the nodes
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
                sums[i] = ToRhino(node.NodeForce) + ToRhino(node.BeamSelfWeightForce) + ToRhino(node.PlateSelfWeightForce) +
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
                reactions[i] = ToRhino(node.RestrainedPart(ToGpc(-sums[i])));
            }
            return reactions;
        }

        private static Vector3d ToRhino(GPC.Geometry.Vector3d v)
        {
            return new Vector3d(v.X, v.Y, v.Z);
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
