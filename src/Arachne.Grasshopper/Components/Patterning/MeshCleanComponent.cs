using Arachne.Grasshopper.Helpers;
using System;
using System.Collections.Generic;
using System.Drawing.Text;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class MeshCleanComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MeshRemap class.
        /// </summary>
        public MeshCleanComponent()
          : base("Mesh Clean", "MC", "Clean a mesh, removing small triangles", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to clean", GH_ParamAccess.item);
            pManager.AddNumberParameter("Length", "L", "Minimum allowed length for an edge", GH_ParamAccess.item, 1);
            pManager.AddNumberParameter("Area", "A", "Minimum allowed area for a face", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("Angle", "R", "Angle in radians to consider a vertex as a corner", GH_ParamAccess.item, 2.5);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Cleaned", "C", "The cleaned mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = new GH_Mesh();
            double len = 0;
            double area = 0;
            double angle = 0;

            if (!DA.GetData(0, ref mesh))
            {
                return;
            }
            DA.GetData(1, ref len);
            DA.GetData(2, ref area);
            DA.GetData(3, ref angle);

            var cleaned = CleanMesh(mesh.Value, len, area, angle);

            DA.SetData(0, new GH_Mesh(cleaned));
        }
        public static Mesh CleanMesh(Mesh mesh, double length, double area, double cornerTol = 2.35619)
        {
            // TODO: Is this a proper cleaning operation?
            mesh.Weld(0.5);

            mesh.RebuildNormals();
            mesh.UnifyNormals();
            mesh.Vertices.CombineIdentical(true, true);
            mesh.Vertices.CullUnused();

            // WeldMap: one set for each vertex containing the vertices that will be merged in one point.
            //          init with the vertex itself
            var weldMap = new HashSet<int>[mesh.Vertices.Count];
            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                weldMap[i] = new HashSet<int> { i };
            }

            // Search vertices clusters
            RTree tree = new RTree();
            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                // Result of the search
                var closest = -1;
                var distance = double.MaxValue;

                // Search operation to find the closest vertex
                tree.Search(new Sphere(mesh.Vertices[i], length), new EventHandler<RTreeEventArgs>((sender, e) =>
                {
                    // NOTE: If we have found a cluster, do not exit unless it is a closer cluster
                    if (closest < 0 || weldMap[closest].Count == 1 || weldMap[e.Id].Count > 1)
                    {
                        var dist = mesh.Vertices[i].DistanceTo(mesh.Vertices[e.Id]);
                        if (dist < distance)
                        {
                            distance = dist;
                            closest = e.Id;
                        }
                    }
                }));

                // No other vertex close enough found in tree. Insert this vertex.
                if (closest == -1)
                {
                    tree.Insert(mesh.Vertices[i], i);
                    continue;
                }

                // Otherwise add the vertex to the weldmap
                //weldMap[i].Union(weldMap[closest]);
                foreach (var w in weldMap[closest])
                {
                    weldMap[i].Add(w);
                }
                weldMap[closest] = weldMap[i];
            }

            // Result cleaned mesh
            Mesh clean = new Mesh();

            // NakedStatus: one bool for each vertex that tells if the vertex is on border
            var nakedStatus = mesh.GetNakedEdgePointStatus();
            // vmap: map from old mesh vertex index, to new mesh vertex index 
            Dictionary<int, int> vmap = new Dictionary<int, int>();

            for (int i = 0; i < mesh.Vertices.Count; ++i)
            {
                // Vertex already added (so it must be part of a cluster)
                if (vmap.ContainsKey(i))
                {
                    continue;
                }

                // Find corner points and border points
                var cornerList = new List<int>();
                var nakedList = new List<int>();

                foreach (var w in weldMap[i])
                {
                    // Not a border point (and not a corner)
                    if (!nakedStatus[w])
                    {
                        continue;
                    }

                    // It is a border point
                    nakedList.Add(w);

                    // Check if it is a corner point

                    // find all connections of this vertex
                    var connPoints = new List<Point3d>();
                    var connIndices = mesh.Vertices.GetConnectedVertices(w);

                    // Keep only connection with other border points
                    for (int c = 0; c < connIndices.Length; ++c)
                    {
                        var connIdx = connIndices[c];
                        if (connIdx != w && nakedStatus[connIdx])
                        {
                            connPoints.Add(mesh.Vertices[connIdx]);
                        }
                    }

                    // A corner must have more than one connection
                    if (connPoints.Count < 2)
                    {
                        continue;
                    }


                    var vertPt = mesh.Vertices[w];
                    double angle = 0;

                    // TODO: should be better if using naked edges

                    // We don't know wich edge is the border:
                    // Compare angles from each pair of connected points and choose the largest one
                    for (int k = 0; k < connPoints.Count - 1; ++k)
                    {
                        for (int kk = k + 1; kk < connPoints.Count; ++kk)
                        {
                            var vecA = new Vector3d(connPoints[k] - vertPt);
                            var vecB = new Vector3d(connPoints[kk] - vertPt);
                            var newAngle = Vector3d.VectorAngle(vecA, vecB);
                            if (newAngle == RhinoMath.UnsetValue)
                            {
                                // TODO: It is probably a duplicated point, can be better handled
                                newAngle = 0;
                                continue;
                            }
                            if (newAngle > angle)
                            {
                                angle = newAngle;
                            }
                        }
                    }

                    // If the angle is set and lesser than tol it is a corner vertex
                    if (angle > 0 && angle < cornerTol)
                    {
                        cornerList.Add(w);
                    }

                }

                // Set positions of new points
                Point3d pt = new Point3d(0, 0, 0);

                if (cornerList.Count > 0)
                {
                    // If we have one or more corners, use corners position
                    foreach (var n in cornerList)
                    {
                        pt += mesh.Vertices[n];
                    }
                    pt = Point3d.Divide(pt, cornerList.Count);
                }
                else if (nakedList.Count > 0)
                {
                    // If we have one or more borders, use borders position
                    foreach (var n in nakedList)
                    {
                        pt += mesh.Vertices[n];
                    }
                    pt = Point3d.Divide(pt, nakedList.Count);
                }
                else
                {
                    // Otherwise average all the points
                    foreach (var n in weldMap[i])
                    {
                        pt += mesh.Vertices[n];
                    }
                    pt = Point3d.Divide(pt, weldMap[i].Count);
                }

                // Add the vertex to new mesh
                var newIndex = clean.Vertices.Count;
                clean.Vertices.Add(pt);

                // Map the new vertex
                foreach (var n in weldMap[i])
                {
                    vmap.Add(n, newIndex);
                }
            }

            // Add the faces to new mesh
            for (int i = 0; i < mesh.Faces.Count; ++i)
            {
                var face = mesh.Faces[i];

                // List of the old face indices
                var vertices = new int[4] { vmap[face.A], vmap[face.B], vmap[face.C], vmap[face.D] };
                HashSet<int> uniques = new HashSet<int>();
                var uniquesLoop = new List<int>();

                // Find the uniquely mapped indices, keep indices order
                for (int j = 0; j < vertices.Length; ++j)
                {
                    if (!uniques.Contains(vertices[j]))
                    {
                        uniques.Add(vertices[j]);
                        uniquesLoop.Add(vertices[j]);
                    }
                }

                // Add new face to the mesh
                if (uniquesLoop.Count == 3)
                {
                    clean.Faces.AddFace(uniquesLoop[0], uniquesLoop[1], uniquesLoop[2]);
                }
                if (uniquesLoop.Count == 4)
                {
                    clean.Faces.AddFace(uniquesLoop[0], uniquesLoop[1], uniquesLoop[2], uniquesLoop[3]);
                }
            }

            // Clean areas
            if (area > 0)
            {
                clean.CollapseFacesByArea(area, 0);
            }

            clean.RebuildNormals();
            clean.UnifyNormals();

            return clean;
        }

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("CleanMeshIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("ca2b11c4-55c9-5d47-8db1-af6ecf7efa07");
    }
}