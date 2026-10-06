using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class UnweldMeshComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MeshRemap class.
        /// </summary>
        public UnweldMeshComponent()
          : base("Unweld Mesh", "UM", "Unweld mesh edges along curves", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to divide", GH_ParamAccess.item);
            pManager.AddCurveParameter("Curves", "C", "Curves over the edges to unweld", GH_ParamAccess.list);
            pManager.AddNumberParameter("Tolerance", "T", "Closest point tolerance", GH_ParamAccess.item, 0.01);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Result", "R", "The disjointed meshes", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = new GH_Mesh();
            List<Curve> curves = new List<Curve>();

            if (!DA.GetData(0, ref mesh))
            {
                return;
            }

            DA.GetDataList(1, curves);
            if (curves.Count <= 0)
            {
                return;
            }

            double tol = 0.01;
            DA.GetData(2, ref tol);

            Mesh unweld = mesh.Value.DuplicateMesh();

            DA.SetDataList(0, UnweldMesh(unweld, curves, tol));
        }


        /// <summary>
        /// Unweld mesh verices using a list of curves. Ideally the curves should follow a path of edges.
		/// If the end points of the unweld path are not on the border of the mesh, the vertices are not unwelded.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        public Mesh[] UnweldMesh(Mesh mesh, List<Curve> curves, double tol)
        {
            for (int i = 0; i < curves.Count; ++i)
            {
                Curve curve = curves[i];

                // Map original vertex with its unwelded sibling 
                Dictionary<int, int> map = new Dictionary<int, int>();
                // Compute the origial map of naked veritces (Before starting unwelding for this curve)
                var status = mesh.GetNakedEdgePointStatus();

                // List of vertex index and curve parameter
                List<(int, double)> vertices = new List<(int, double)>();
                double t = 0;
                for (int j = 0; j < mesh.Vertices.Count; ++j)
                {
                    if (curve.ClosestPoint(mesh.Vertices[j], out t, tol))
                    {
                        vertices.Add((j, t));
                    }
                }

                // If no vertices found there is nothing to unweld
                if (vertices.Count == 0)
                {
                    continue;
                }

                // Sort vertices following curve direction
                vertices.Sort((a, b) => { return a.Item2.CompareTo(b.Item2); });

                // Make edge sets that define a left or a right face (edges must be oriented!)
                var leftedges = new HashSet<(int, int)>();
                var rightedges = new HashSet<(int, int)>();

                // Following the edges to unweld, add them forward to left set and backward to right set
                for (int j = 0; j < vertices.Count - 1; ++j)
                {
                    leftedges.Add((vertices[j].Item1, vertices[j + 1].Item1));
                    rightedges.Add((vertices[j + 1].Item1, vertices[j].Item1));
                }

                // List of the faces that need unweld
                var facesToUnweld = new HashSet<int>();

                // If the first and last vertices are completely surrounded by faces they should not be unwelded
                var from = status[vertices[0].Item1] ? 0 : 1;
                var to = status[vertices[vertices.Count - 1].Item1] ? vertices.Count : vertices.Count - 1;

                for (int j = from; j < to; ++j)
                {
                    // Add a new vertices equal to this one
                    int v = vertices[j].Item1;
                    map[v] = mesh.Vertices.Count;
                    var point = mesh.Vertices[v];
                    mesh.Vertices.Add(point);

                    // Test all faces connected to vertex
                    var facesIndices = mesh.Vertices.GetVertexFaces(v);
                    for (int k = 0; k < facesIndices.Length; ++k)
                    {
                        var f = mesh.Faces[facesIndices[k]];

                        // List the edges of this face
                        var edges = new List<(int, int)>();
                        edges.Add((f.A, f.B));
                        edges.Add((f.B, f.C));
                        if (f.IsTriangle)
                        {
                            edges.Add((f.C, f.A));
                        }
                        else
                        {
                            edges.Add((f.C, f.D));
                            edges.Add((f.D, f.A));
                        }

                        var found = false;
                        var isRight = false;
                        // Try to find another edge on the curve
                        for (int n = 0; n < edges.Count; ++n)
                        {
                            if (leftedges.Contains(edges[n]))
                            {
                                found = true;
                                isRight = false;
                                break;
                            }
                            if (rightedges.Contains(edges[n]))
                            {
                                found = true;
                                isRight = true;
                                break;
                            }
                        }

                        // If unable to classify the face with edge direction, use a geometric strategy instead
                        if (!found)
                        {
                            // Get right direction
                            var curveDir = j == 0 ? mesh.Vertices[vertices[j + 1].Item1] - point : j == vertices.Count - 1 ? point - mesh.Vertices[vertices[j - 1].Item1] : mesh.Vertices[vertices[j + 1].Item1] - mesh.Vertices[vertices[j - 1].Item1];
                            var meshNorm = mesh.Normals[v];
                            var cross = Vector3d.CrossProduct(curveDir, meshNorm);

                            // Center of the face
                            Point3d center;
                            if (f.IsTriangle)
                            {
                                center = Point3d.Divide(mesh.Vertices[f.A] + mesh.Vertices[f.B] + mesh.Vertices[f.C], 3);
                            }
                            else
                            {
                                center = Point3d.Divide(mesh.Vertices[f.A] + mesh.Vertices[f.B] + mesh.Vertices[f.C] + mesh.Vertices[f.D], 4);
                            }

                            // Check if center is ar right side of the mesh
                            isRight = Vector3d.Multiply(cross, center - point) > 0;
                        }


                        // Add the counters edges to correct list
                        if (isRight)
                        {
                            // Also add the face to the list of the ones to unweld
                            facesToUnweld.Add(facesIndices[k]);
                            for (int e = 0; e < edges.Count; ++e)
                            {
                                var backward = (edges[e].Item2, edges[e].Item1);
                                if (!leftedges.Contains(backward))
                                {
                                    //rightedges.Add(backward); 
                                }
                            }
                        }
                        else
                        {
                            for (int e = 0; e < edges.Count; ++e)
                            {
                                var backward = (edges[e].Item2, edges[e].Item1);
                                if (!rightedges.Contains(backward))
                                {
                                    //leftedges.Add(backward);
                                }
                            }
                        }
                    }
                }

                // Unweld all the right faces
                for (int j = 0; j < facesToUnweld.Count; ++j)
                {
                    var fIdx = facesToUnweld.ElementAt(j);
                    var f = mesh.Faces[fIdx];

                    if (map.ContainsKey(f.A)) { f.A = map[f.A]; }
                    if (map.ContainsKey(f.B)) { f.B = map[f.B]; }
                    if (map.ContainsKey(f.C)) { f.C = map[f.C]; }
                    if (map.ContainsKey(f.D)) { f.D = map[f.D]; }
                    mesh.Faces[fIdx] = f;
                }

            }

            // Explode the mesh into sub meshes if a complete separation was made
            return mesh.ExplodeAtUnweldedEdges();
        }



        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("23032209-c252-5a53-a96d-3a958c387f15");
    }
}