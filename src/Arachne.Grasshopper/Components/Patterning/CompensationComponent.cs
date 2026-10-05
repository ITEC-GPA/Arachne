using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

using Rhino;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Patterning
{
    public class CompensationComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CompensationComponent class.
        /// </summary>
        public CompensationComponent()
          : base("Compensation", "C", "Compensate the flatten patches", "Arachne", "3 Patterning")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Panel", "P", "The flatten panel to compensate", GH_ParamAccess.item);
            pManager.AddVectorParameter("Warp direction", "W", "The vector of the warp firection of the panel", GH_ParamAccess.item);
            pManager.AddNumberParameter("Wa Scale (%)", "Wa%", "The deformation in the warp direction in %(greater than 0 = reduction)", GH_ParamAccess.item);
            pManager.AddNumberParameter("We Scale (%)", "We%", "The deformation in the weft direction in %(greater than 0 = reduction)", GH_ParamAccess.item);
            pManager.AddGenericParameter("Fixed lengths", "FLs", "The fixed lengths of edges, for example borders, defined with the Fixed compensation component", GH_ParamAccess.list);
            pManager[pManager.ParamCount - 1].Optional = true;
            pManager.AddIntegerParameter("Max Iter NL", "MI,NL", "Max number of iterations in non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 20);//reduced at default
            pManager.AddNumberParameter("Tolerance NL", "T NL", "Tolerance on relative error durign non linear solution (used in non linear solver or updating pressures direction)", GH_ParamAccess.item, 0.0001);
            pManager.AddIntegerParameter("Max Iter,s", "MI,s", "Max number of iterations for the solver", GH_ParamAccess.item, 10000);
            pManager.AddNumberParameter("Tolerance,s", "T,s", "Tolerance for the solver", GH_ParamAccess.item, 0.0001);
            //pManager.AddNumberParameter("Top Scale (%)", "T", "The scale factor in the relative X axis of the top side", GH_ParamAccess.item);
            //pManager.AddNumberParameter("Bottom Scale (%)", "B", "The scale factor in the relative X axis of the top side", GH_ParamAccess.item);
            //pManager.AddNumberParameter("Transaction Length", "L", "The transaction length beetweel the vertical and horizontal sides", GH_ParamAccess.item);
            //pManager.AddIntegerParameter("Junction type", "J", "Set the junction type between the top, bottom, left and right side", GH_ParamAccess.item, 0);
            //Param_Integer jParam = pManager[7] as Param_Integer;
            //jParam.AddNamedValue("Arc", 0);
            //jParam.AddNamedValue("Line", 1);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Panel", "P", "The compensated panel", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Mesh panel = new GH_Mesh();
            Vector3d warp_dir = Vector3d.Unset;
            double waPerc = 0;
            double wePerc = 0;
            int maxIterNL = 0;
            double tol = 0;
            int maxIterSolver = 0;
            double epsS = 0;
            List<GH_ObjectWrapper> fixedBeams = new List<GH_ObjectWrapper>();

            if (!DA.GetData(0, ref panel))
                return;
            if (!DA.GetData(1, ref warp_dir))
                return;
            if (!DA.GetData(2, ref waPerc))
                return;
            if (!DA.GetData(3, ref wePerc))
                return;
            DA.GetDataList(4, fixedBeams);
            if (!DA.GetData(5, ref maxIterNL))
                return;
            if (!DA.GetData(6, ref tol))
                return;
            if (!DA.GetData(7, ref maxIterSolver))
                return;
            if (!DA.GetData(8, ref epsS))
                return;

            //This component use the flattening solver of tensile lib. The idea is to impose fixed lengths as compensated to the panel.
            panel = new GH_Mesh(panel.Value.DuplicateMesh());
            panel.Value.Faces.ConvertQuadsToTriangles();
            panel.Value.Normals.ComputeNormals();

            if (Math.Abs(warp_dir.Z) > RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
            {
                throw new NotSupportedException("Warp dir must be defined on plane xy");
            }

            //Testing planar mesh
            for (int i = 0; i < panel.Value.Vertices.Count; i++)
            {
                if (Math.Abs(panel.Value.Normals[i] * Vector3d.ZAxis) < 0.999)
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

            for (int i = 0; i < panel.Value.Vertices.Count; i++)
            {
                Point3d pt = (Point3d)panel.Value.Vertices.ElementAt(i);

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

            for (int i = 0; i < panel.Value.Faces.Count; i++)
            {
                MeshFace face = panel.Value.Faces.ElementAt(i);
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

                    Point3d firstPOriginal = panel.Value.Vertices[firstIndex];
                    Point3d secondPOriginal = panel.Value.Vertices[secondIndex];

                    Point3d firstPMod = modifiedNodes[firstIndex];
                    Point3d secondPMod = modifiedNodes[secondIndex];

                    // Arachne Cable Constraints replaces the legacy FEM beam attributes.
                    foreach (var wrapper in fixedBeams)
                    {
                        var cable = wrapper.Value as Arachne.Core.Cable;
                        if (cable == null) throw new ArgumentException("Fixed lengths expects Arachne cables from Cable Constraints.");
                        var from = new Point3d(cable.Start.X, cable.Start.Y, cable.Start.Z);
                        var to = new Point3d(cable.End.X, cable.End.Y, cable.End.Z);
                        double matchTolerance = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
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

            try
            {
                // Solve 
                PlanarFixedLengthsSolver solver = new PlanarFixedLengthsSolver(model, maxIterNL, tol, epsS, maxIterSolver);
                Solver.ResultCodes ret = solver.Solve(out PlanarFixedLengthsSolver.PlanarFixedLengthsResult planarFixedLengthsResult);

                switch (ret)
                {
                    case Solver.ResultCodes.Converged:
                    case Solver.ResultCodes.ConvergedUsingSlowMethodWithoutWeigths:
                        {
                            break;
                        }
                    case Solver.ResultCodes.NotconvergedAtMaxIterationNumber:
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged");
                            break;
                        }
                    default:
                        throw new NotSupportedException();
                }
            }
            catch (Exception)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Fail to solve");
            }

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

            DA.SetData(0, compensated);
        }

        public override GH_Exposure Exposure => GH_Exposure.quarternary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => PatterningIcons.Get("CompensationIcon");

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("51dce643-1c37-56ac-9eb1-9641e7bcdf1f");
    }
}