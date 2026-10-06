using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class SubdivideMeshComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MeshRemap class.
        /// </summary>
        public SubdivideMeshComponent()
          : base("Subdivide Mesh", "MC", "Subdivide mesh following the ideal surface", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh to clean", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Subdivisions", "S", "Number of subdivisions", GH_ParamAccess.item, 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGeometryParameter("Result", "R", "The subdivided mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = new GH_Mesh();

            if (!DA.GetData(0, ref mesh))
            {
                return;
            }

            int repeat = 1;
            DA.GetData(1, ref repeat);

            Mesh subdivided = mesh.Value;

            // Repeat subdivision operation n times
            for (int i = 0; i < repeat; ++i)
            {
                subdivided = SubdivideMesh(subdivided);
            }

            DA.SetData(0, new GH_Mesh(subdivided));
        }


        // Subdivide the mesh, 4 faces fore each face
        public Mesh SubdivideMesh(Mesh mesh)
        {
            // Make a copy of the mesh without faces
            Mesh subdivided = mesh.DuplicateMesh();
            subdivided.Faces.Clear();

            for (int i = 0; i < mesh.Faces.Count; ++i)
            {
                // Build a bezier surface for each face 
                var face = mesh.Faces[i];
                BezierSurface surface = BuildQuadraticSurface(mesh, i);

                // Different strategies if quad or triangle
                if (face.IsQuad)
                {
                    // Get the current vertices
                    var a = face.A;
                    var b = face.B;
                    var c = face.C;
                    var d = face.D;

                    // Set 4 new vertices on edges and one in the center
                    var ab = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleQuadPoint(0, 0.5, surface));

                    var da = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleQuadPoint(0.5, 0, surface));

                    var center = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleQuadPoint(0.5, 0.5, surface));

                    var bc = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleQuadPoint(0.5, 1, surface));

                    var cd = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleQuadPoint(1, 0.5, surface));

                    // Add 4 new faces
                    subdivided.Faces.AddFace(a, ab, center, da);
                    subdivided.Faces.AddFace(ab, b, bc, center);
                    subdivided.Faces.AddFace(center, bc, c, cd);
                    subdivided.Faces.AddFace(da, center, cd, d);
                }
                else if (face.IsTriangle)
                {
                    // Get the current vertices
                    var a = face.A;
                    var b = face.B;
                    var c = face.C;

                    // Set 3 new vertices on edges
                    var ab = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleTrianglePoint(0.5, 0.5, 0, surface));

                    var bc = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleTrianglePoint(0, 0.5, 0.5, surface));

                    var ca = subdivided.Vertices.Count;
                    subdivided.Vertices.Add(SampleTrianglePoint(0.5, 0, 0.5, surface));

                    // Add 4 new faces
                    subdivided.Faces.AddFace(a, ab, ca);
                    subdivided.Faces.AddFace(ab, b, bc);
                    subdivided.Faces.AddFace(bc, c, ca);
                    subdivided.Faces.AddFace(ca, ab, bc);
                }

            }
            // Combine identical vertices.
            // NOTE: vertices should be identical because the math to obtain the same vertex from different faces is the same
            subdivided.Vertices.CombineIdentical(false, true);

            // Compute the normals for the new vertices
            subdivided.RebuildNormals();
            subdivided.UnifyNormals();

            return subdivided;
        }

        // Create a Quadratic Bezier surface from a mesh face
        public BezierSurface BuildQuadraticSurface(Mesh mesh, int faceIndex)
        {
            BezierSurface surface;
            MeshFace face = mesh.Faces[faceIndex];

            // Different strategy if face is quad or triangle
            if (face.IsQuad)
            {
                surface.Points = new Point3d[9];
                surface.Normals = new Vector3d[9];

                // init with the 4 original vertices of the face
                surface.Points[0] = mesh.Vertices[face.A];
                surface.Points[2] = mesh.Vertices[face.B];
                surface.Points[6] = mesh.Vertices[face.D];
                surface.Points[8] = mesh.Vertices[face.C];

                surface.Normals[0] = mesh.Normals[face.A];
                surface.Normals[2] = mesh.Normals[face.B];
                surface.Normals[6] = mesh.Normals[face.D];
                surface.Normals[8] = mesh.Normals[face.C];

                // Sample the new point of each edge
                (surface.Points[1], surface.Normals[1]) = QuadraticEdge(surface.Points[0], surface.Points[2], surface.Normals[0], surface.Normals[2]);
                (surface.Points[3], surface.Normals[3]) = QuadraticEdge(surface.Points[6], surface.Points[0], surface.Normals[6], surface.Normals[0]);
                (surface.Points[5], surface.Normals[5]) = QuadraticEdge(surface.Points[2], surface.Points[8], surface.Normals[2], surface.Normals[8]);
                (surface.Points[7], surface.Normals[7]) = QuadraticEdge(surface.Points[8], surface.Points[6], surface.Normals[8], surface.Normals[6]);

                // To sample the 
                var (centerA, centerNormA) = QuadraticEdge(surface.Points[1], surface.Points[7], surface.Normals[1], surface.Normals[7]);
                var (centerB, centerNormB) = QuadraticEdge(surface.Points[3], surface.Points[5], surface.Normals[3], surface.Normals[5]);

                surface.Points[4] = (centerA + centerB) * 0.5;
                surface.Normals[4] = (centerNormA + centerNormB) * 0.5;
            }
            else
            {
                surface.Points = new Point3d[6];
                surface.Normals = new Vector3d[6];

                surface.Points[0] = mesh.Vertices[face.A];
                surface.Points[2] = mesh.Vertices[face.B];
                surface.Points[5] = mesh.Vertices[face.C];

                surface.Normals[0] = mesh.Normals[face.A];
                surface.Normals[2] = mesh.Normals[face.B];
                surface.Normals[5] = mesh.Normals[face.C];

                (surface.Points[1], surface.Normals[1]) = QuadraticEdge(surface.Points[0], surface.Points[2], surface.Normals[0], surface.Normals[2]);
                (surface.Points[3], surface.Normals[3]) = QuadraticEdge(surface.Points[5], surface.Points[0], surface.Normals[5], surface.Normals[0]);
                (surface.Points[4], surface.Normals[4]) = QuadraticEdge(surface.Points[2], surface.Points[5], surface.Normals[2], surface.Normals[5]);
            }

            return surface;
        }

        public (Point3d, Vector3d) QuadraticEdge(Point3d ptA, Point3d ptB, Vector3d norA, Vector3d norB)
        {
            Vector3d AB = ptA - ptB;

            Vector3d vecA = norA;
            Vector3d vecB = norB;

            var crossA = Vector3d.CrossProduct(norA, AB);
            var crossB = Vector3d.CrossProduct(norB, AB);

            vecA.Rotate(-Math.PI * 0.5, crossA);
            vecB.Rotate(Math.PI * 0.5, crossB);

            vecA.Unitize();
            vecB.Unitize();

            var a = ptA + Vector3d.Multiply(vecA, AB.Length * 0.5);
            var b = ptB + Vector3d.Multiply(vecB, AB.Length * 0.5);

            var outPt = (a + b) * 0.5;

            BezierCurve curve = new BezierCurve();
            curve.Points = new Point3d[3] { ptA, outPt, ptB };
            var outVec = BezierCurveDerivative(curve, 0.5);
            outVec.Rotate(Math.PI * 0.5, (crossA + crossB) * 0.5);
            outVec.Unitize();

            return (outPt, outVec);
        }


        public Point3d SampleQuadPoint(double u, double v, BezierSurface surface)
        {
            Point3d pt = new Point3d(0.0, 0.0, 0.0);

            if (surface.Points.Length == 9)
            {
                for (int i = 0; i < 3; ++i)
                {
                    for (int j = 0; j < 3; ++j)
                    {
                        var idx = i * 3 + j;
                        var b = bernstein(2, i, u) * bernstein(2, j, v);
                        pt.X += b * surface.Points[idx].X;
                        pt.Y += b * surface.Points[idx].Y;
                        pt.Z += b * surface.Points[idx].Z;
                    }
                }
            }

            return pt;
        }


        public Point3d SampleTrianglePoint(double u, double v, double w, BezierSurface surface)
        {
            Point3d pt = new Point3d(0.0, 0.0, 0.0);


            if (surface.Points.Length == 6)
            {
                for (int i = 0; i < 3; ++i)
                {
                    for (int j = 0; j < 3; ++j)
                    {
                        for (int k = 0; k < 3; ++k)
                        {
                            if (i + j + k != 2) { continue; }

                            var idx = i == 2 ? 0 : j == 2 ? 2 : k == 2 ? 5 : i + j == 2 ? 1 : i + k == 2 ? 3 : 4;

                            var b = 2 / (factorial(i) * factorial(j) * factorial(k)) * Math.Pow(u, i) * Math.Pow(v, j) * Math.Pow(w, k);

                            pt.X += b * surface.Points[idx].X;
                            pt.Y += b * surface.Points[idx].Y;
                            pt.Z += b * surface.Points[idx].Z;

                        }
                    }
                }
            }

            return pt;
        }



        public int factorial(int n)
        {
            int res = 1;

            while (n > 1)
            {
                res *= n;
                n--;
            }

            return res;
        }

        public int binomial(int n, int i)
        {
            return factorial(n) / (factorial(i) * factorial(n - i));
        }

        public double bernstein(int n, int i, double u)
        {
            return binomial(n, i) * Math.Pow(u, i) * Math.Pow(1 - u, n - i);
        }



        public Point3d PointOnBezierCurve(BezierCurve curve, double t)
        {
            Point3d pt = new Point3d(0.0, 0.0, 0.0);

            if (curve.Points.Length == 3)
            {
                var a = (1 - t) * (1 - t);
                var b = 2 * (1 - t) * t;
                var c = t * t;

                pt.X = a * curve.Points[0].X + b * curve.Points[1].X + c * curve.Points[2].X;
                pt.Y = a * curve.Points[0].Y + b * curve.Points[1].Y + c * curve.Points[2].Y;
                pt.Z = a * curve.Points[0].Z + b * curve.Points[1].Z + c * curve.Points[2].Z;
            }

            return pt;
        }

        public Vector3d BezierCurveDerivative(BezierCurve curve, double t)
        {
            Vector3d vec = new Vector3d(0.0, 0.0, 0.0);

            if (curve.Points.Length == 3)
            {
                var a = 2 * (1 - t);
                var b = 2 * t;

                vec.X = a * (curve.Points[1].X - curve.Points[0].X) + b * (curve.Points[2].X - curve.Points[1].X);
                vec.Y = a * (curve.Points[1].Y - curve.Points[0].Y) + b * (curve.Points[2].Y - curve.Points[1].Y);
                vec.Z = a * (curve.Points[1].Z - curve.Points[0].Z) + b * (curve.Points[2].Z - curve.Points[1].Z);
            }

            return vec;
        }



        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("a9271e1c-81d2-58d2-843c-bee8a3e0aa8d");


        public struct BezierCurve
        {
            public Point3d[] Points;
        }

        public struct BezierSurface
        {
            public Point3d[] Points;
            public Vector3d[] Normals;
        }

    }
}