using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class FlatteningComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the FlatteningComponent class.
        /// </summary>
        public FlatteningComponent()
          : base("Flattening", "F", "Cut the mesh on the geodesics lines and creates the flatten the panels", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            //pManager.AddMeshParameter("Mesh", "M", "The mesh", GH_ParamAccess.item);
            //pManager.AddCurveParameter("Geodesics", "G", "The geodesics lines for cutting the mesh", GH_ParamAccess.list);
            pManager.AddMeshParameter("Mesh", "M", "The mesh to flatten", GH_ParamAccess.item);
            pManager.AddNumberParameter("Border Weight", "BW", "The weight to assign to the border during the flattening operation", GH_ParamAccess.item, 1);
            pManager.AddIntegerParameter("Max Iter NL", "MI,NL", "Max number of iterations in non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 20);//reduced at default
            pManager.AddNumberParameter("Tolerance NL", "T NL", "Tolerance on relative error durign non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 0.01);
            pManager.AddIntegerParameter("Max Iter,s", "MI,s", "Max number of iterations for the solver", GH_ParamAccess.item, 10000);
            pManager.AddNumberParameter("Tolerance,s", "T,s", "Tolerance for the solver", GH_ParamAccess.item, 0);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            //pManager.AddMeshParameter("Panels 3D", "3D", "The 3D panels", GH_ParamAccess.list);
            //pManager.AddMeshParameter("Panels 2D", "2D", "The flatten panels", GH_ParamAccess.list);
            //pManager.AddVectorParameter("Warp directions", "W", "The warp direction of each panel", GH_ParamAccess.list);
            pManager.AddMeshParameter("Flatten", "F", "The flatten mesh", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh mesh = null;
            double weigth = 0;
            int max_iter = 0;
            double tol = 0;
            int maxIterSolver = 0;
            double epsS = 0;
            if (!DA.GetData(0, ref mesh))
                return;
            if (!DA.GetData(1, ref weigth))
                return;
            if (!DA.GetData(2, ref max_iter))
                return;
            if (!DA.GetData(3, ref tol))
                return;
            if (!DA.GetData(4, ref maxIterSolver))
                return;
            if (!DA.GetData(5, ref epsS))
                return;

            Mesh flat;

            try
            {
                GetFlattenedMesh(mesh.Value, weigth, max_iter, tol, epsS, maxIterSolver, out Solver.ResultCodes ret, out flat);
                switch (ret)
                {
                    case Solver.ResultCodes.Converged:
                    case Solver.ResultCodes.ConvergedUsingSlowMethodWithoutWeigths:
                        {
                            break;
                        }
                    case Solver.ResultCodes.NotconvergedAtMaxIterationNumber:
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged at max iteration number. Test internal lengths and area.");
                            break;
                        }
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (Exception ex)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, ex.Message);
                return;
            }

            DA.SetData(0, flat);
        }

        internal static void GetFlattenedMesh(Mesh mesh, double borderWeigth, int max_iter, double tol, double epsS, int maxIterSolver, out Solver.ResultCodes ret, out Mesh flat)
        {
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

            // If success make a new flatten mesh
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

        protected static bool AddBeamToModel(Model model, int i1, int i2, bool isNaked, double borderWeigth, ref Node id1, ref Node id2)
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

        public override GH_Exposure Exposure => GH_Exposure.tertiary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("FlatteningIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b8fa603b-1674-573c-9380-450724df4b24");
    }
}