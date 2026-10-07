using Arachne.Common.Models;
using Arachne.Grasshopper.ComponentAttributes;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.FormFinding;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Analysis
{
    public class FormFindComponent : GH_Component
    {
        private bool _run;
        // Last result of every data tree iteration, kept on the outputs until the next run
        private readonly List<GH_Model> _models;
        private readonly List<GH_Mesh> _meshes;
        private int _runCount;

        /// <summary>
        /// Initializes a new instance of the FormFindComponent class.
        /// </summary>
        public FormFindComponent()
          : base("Form Find", "FF", "Calculate the net cable form", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_ANALYSIS)
        {
            _run = false;
            _models = new List<GH_Model>();
            _meshes = new List<GH_Mesh>();
            _runCount = 0;
        }

        public override void CreateAttributes()
        {
            var runBtn = new ComponentAttributes.ComponentNButtonsAttributes.ButtonBase("Run");
            var btns = new List<ComponentNButtonsAttributes.ButtonBase>() { runBtn };

            var attr = new ComponentNButtonsAttributes(this, btns);
            attr.ButtonPressed += (i) =>
            {
                if (i == 0)
                {
                    _run = true;
                    ExpireSolution(true);
                }
            };
            m_attributes = attr;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        // The options of FeMM (gravity, pressure, iterations, tolerances, mesh) are given by the
        // Solver Options component
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "The FEM model", GH_ParamAccess.item);
            int i = pManager.AddIntegerParameter("Solver Type", "ST", "The type of solver to be used", GH_ParamAccess.item, 0);
            Param_Integer mtParam = pManager[i] as Param_Integer;
            mtParam.AddNamedValue("Linear", 0);
            mtParam.AddNamedValue("Linear step by step", 1);
            mtParam.AddNamedValue("Non-Linear", 2);
            i = pManager.AddGenericParameter("Options", "O", "The solver options (Solver Options component). If not connected, the defaults", GH_ParamAccess.item);
            pManager[i].Optional = true;
            pManager.AddPlaneParameter("Plane", "P", "Plane of mesh generation", GH_ParamAccess.item, Plane.WorldXY);
            pManager.AddBooleanParameter("Auto Run", "AR", "Activate or disable the live update", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Model", "M", "The FEM model", GH_ParamAccess.item);
            pManager.AddMeshParameter("Mesh", "M", "The resulting mesh usefull to geometric operations", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Message = "";
            GH_Model model = null;
            int solverType = 0;
            GH_SolverOptions gh_options = null;
            bool autoRun = false;
            Plane plane = Plane.Unset;

            if (!DA.GetData("Model", ref model))
                return;
            DA.GetData(1, ref solverType);
            if (!DA.GetData(2, ref gh_options) && Params.Input[2].SourceCount > 0)
            {
                // Connected but empty: the options were refused upstream, the defaults would hide it
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter Options failed to collect data");
                return;
            }
            SolverOptionsModel options = gh_options?.Value ?? new SolverOptionsModel();
            DA.GetData(3, ref plane);
            if (!DA.GetData("Auto Run", ref autoRun))
                return;

            // If the user gives the pressures it must give also the load case
            if (options.Pressure != 0 && options.PressureLoadCase == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Give the Load Case to assign at the pressure forces");
                return;
            }

            if (_run || autoRun)
            {
                try
                {
                    Solve(DA.Iteration, model, solverType, options, plane);
                }
                finally
                {
                    // Also after an error, otherwise the Run state remains set
                    _runCount--;
                    if (_runCount <= 0)
                        _run = false;
                }
            }

            if (DA.Iteration < _models.Count && _models[DA.Iteration] != null)
            {
                DA.SetData(0, _models[DA.Iteration]);
                DA.SetData(1, _meshes[DA.Iteration]);
            }
        }

        private void Solve(int iteration, GH_Model model, int solverType, SolverOptionsModel options, Plane plane)
        {
            // A failed run does not leave the previous result on the outputs
            SetResult(iteration, null, null);
            try
            {
                int maxIterNumber = options.MaxIterations;
                double tolerance = options.Tolerance;
                int maxIterNumberSolver = options.SolverMaxIterations;
                double toleranceSolverA = options.SolverToleranceA;
                double toleranceSolverB = options.SolverToleranceB;
                bool makeMesh = options.CreateMesh;

                if (solverType < 0 || solverType > 2)
                    throw new ArgumentException("Unknown solver type");
                if (maxIterNumber < 1)
                    throw new ArgumentException("Max Iter must be at least 1");

                Model ff_model = Common.Helpers.FormFindingHelper.AssemblyModel(model.Value, plane, solverType, options, out List<string> warnings);
                foreach (string warning in warnings)
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, warning);

                // The reactions are stored only for an equilibrium configuration
                bool converged = true;
                if (solverType == 1)
                {
                    List<Beam> fixedAxialForceBeam = ff_model.Beams.Where(j => j.HasNLFixedAxialForce).ToList();
                    List<Beam> fixedLengthBeam = ff_model.Beams.Where(j => j.HasNLFixedLength).ToList();

                    // The relative errors below are divided by these targets
                    if (fixedLengthBeam.Any(b => !(b.NLFixedLength > 0)))
                        throw new ArgumentException("The fixed lengths must be greater than 0");
                    if (fixedAxialForceBeam.Any(b => b.TargetAxialForce == 0))
                        throw new ArgumentException("The fixed axial forces must be different from 0");

                    bool exit = true;
                    int count = 0;
                    Solver.ResultCodes ret;

                    ForceDensitySolver solver = new LinearForceDensitySolver(ff_model, maxIterNumber, tolerance, toleranceSolverA, toleranceSolverB, maxIterNumberSolver);

                    do
                    {
                        exit = true;
                        count++;

                        ret = solver.SolveForceDensity();

                        // The next steps go on from the force densities just found, as TensileLib did
                        // before 2.2: now the solver keeps them apart from the targets
                        foreach (Beam beam in ff_model.Beams)
                            beam.TargetForceDensity = beam.ForceDensity;

                        for (int i = 0; i < fixedLengthBeam.Count; i++)
                        {
                            Beam bb = fixedLengthBeam[i];

                            if (Math.Abs((bb.Length - bb.NLFixedLength) / bb.NLFixedLength) > tolerance)
                            {
                                exit = false;
                                bb.TargetForceDensity = bb.TargetForceDensity * bb.Length / bb.NLFixedLength;
                            }
                        }
                        for (int i = 0; i < fixedAxialForceBeam.Count; i++)
                        {
                            Beam bb = fixedAxialForceBeam[i];

                            if (Math.Abs((bb.AxialForce - bb.TargetAxialForce) / bb.TargetAxialForce) > tolerance)
                            {
                                if (bb.AxialForce == 0)
                                    throw new InvalidOperationException("A beam with fixed axial force has no force: its force density cannot be updated");
                                exit = false;
                                bb.TargetForceDensity = bb.TargetForceDensity * bb.TargetAxialForce / bb.AxialForce;
                            }
                        }

                        if (!exit && count >= maxIterNumber)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Not converged. The maximum number of iteration has been reached.");
                            Message = "Error";
                            return;
                        }

                    } while (!exit);

                    if (ret == Solver.ResultCodes.NotconvergedAtMaxIterationNumber)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged. The maximum number of iteration has been reached.");
                        converged = false;
                    }
                }
                else
                {
                    ForceDensitySolver solver;
                    if (solverType == 0)
                        solver = new LinearForceDensitySolver(ff_model, maxIterNumber, tolerance, toleranceSolverA, toleranceSolverB, maxIterNumberSolver);
                    else
                        solver = new NonLinearForceDensitySolver(ff_model, maxIterNumber, tolerance, toleranceSolverA, toleranceSolverB, maxIterNumberSolver);

                    Solver.ResultCodes ret = solver.SolveForceDensity();

                    if (ret == Solver.ResultCodes.NotconvergedAtMaxIterationNumber)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged. The maximum number of iteration has been reached.");
                        converged = false;
                    }
                }

                // Create a deep copy of the model where to add the results
                GH_Model result = (GH_Model)model.Duplicate();

                // Update the model with the FF results
                NodeModel[] nodeArray = result.Value.Nodes.ToArray();
                for (int i = 0; i < nodeArray.Length; i++)
                {
                    NodeModel node = nodeArray[i];
                    Node ff_node = ff_model.Nodes.ElementAt(i);
                    node.Position = new Point3d(ff_node.X, ff_node.Y, ff_node.Z);

                    Vector3d p = new Vector3d(ff_node.PressureX, ff_node.PressureY, ff_node.PressureZ);
                    if (p.Length != 0)
                        node.Loads.Add(new NodeForceLoadModel(options.PressureLoadCase, p));
                }

                BeamModel[] beamsModel = result.Value.Beams.ToArray();
                for (int i = 0; i < beamsModel.Length; i++)
                {
                    BeamModel beam = beamsModel[i];
                    Beam ff_beam = ff_model.Beams.ElementAt(i);
                    beam.PointFrom = new Point3d(ff_beam.Node1.X, ff_beam.Node1.Y, ff_beam.Node1.Z);
                    beam.PointTo = new Point3d(ff_beam.Node2.X, ff_beam.Node2.Y, ff_beam.Node2.Z);
                    // The pre-load uses the load case of the force density or, on the beams governed
                    // only by a fixed axial force or a fixed length, the load case of that load.
                    // Beams without any of them carry no force and get no pre-load.
                    LoadModel source = beam.Loads.FirstOrDefault(l => l.GetType() == typeof(ForceDensityModel))
                        ?? beam.Loads.FirstOrDefault(l => l.GetType() == typeof(FixedAxialForceModel))
                        ?? beam.Loads.FirstOrDefault(l => l.GetType() == typeof(FixedLengthModel));
                    if (source != null)
                    {
                        LoadCaseModel lc = source.LoadCase;
                        for (int k = 0; k < beam.Loads.Count; k++)
                        {
                            if (beam.Loads[k].GetType() == typeof(BeamPreLoadModel))
                            {
                                beam.Loads.Remove(beam.Loads[k]);
                                k--;
                            }
                        }
                        beam.Loads.Add(new BeamPreLoadModel(lc, ff_beam.AxialForce, BeamPreLoadModel.LoadType.Tension));
                    }
                }

                // Support reactions, read by the Support Reactions component. A previous form
                // finding of the same model leaves reactions that are no longer valid.
                foreach (NodeModel node in nodeArray)
                {
                    for (int k = node.Results.Count - 1; k >= 0; k--)
                        if (node.Results[k] is NodeReactResultModel)
                            node.Results.RemoveAt(k);
                }
                if (converged)
                {
                    Vector3d?[] reactions = Common.Helpers.FormFindingHelper.ComputeReactions(ff_model);
                    LoadCaseModel reactionCase = Common.Helpers.FormFindingHelper.FormFindingCase();
                    for (int i = 0; i < nodeArray.Length && i < reactions.Length; i++)
                    {
                        if (reactions[i].HasValue)
                            nodeArray[i].Results.Add(new NodeReactResultModel(reactionCase) { Force = reactions[i].Value });
                    }
                }

                GH_Mesh resultMesh = null;
                if (makeMesh) // Create the mesh if required
                {
                    // Generate the plates
                    try
                    {
                        if (ff_model.Plates == null || ff_model.Plates.Count == 0)
                            ff_model.GeneratePlatesFromBeams();
                    }
                    catch (Exception)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Fail to generate the mesh");
                    }

                    Mesh mesh = new Mesh();
                    Dictionary<int, int> idRhinoIndexMap = new Dictionary<int, int>();

                    for (int i = 0; i < ff_model.Nodes.Count; i++)
                    {
                        Node ff_node = ff_model.Nodes.ElementAt(i);
                        int tag = mesh.Vertices.Add(ff_node.X, ff_node.Y, ff_node.Z);
                        idRhinoIndexMap.Add(ff_node.Id, tag);
                    }

                    for (int i = 0; i < ff_model.Plates.Count; i++)
                    {
                        Plate ff_plate = ff_model.Plates.ElementAt(i);
                        int i1 = idRhinoIndexMap[ff_plate.Node1.Id];
                        int i2 = idRhinoIndexMap[ff_plate.Node2.Id];
                        int i3 = idRhinoIndexMap[ff_plate.Node3.Id];
                        int i4 = -1;

                        if (ff_plate.PlateType == Plate.PlateTypes.Quad4)
                            i4 = idRhinoIndexMap[ff_plate.Node4.Id];

                        if (ff_plate.PlateType == Plate.PlateTypes.Quad4)
                            mesh.Faces.AddFace(i1, i2, i3, i4);
                        else if (ff_plate.PlateType == Plate.PlateTypes.Tri3)
                            mesh.Faces.AddFace(i1, i2, i3);
                    }
                    mesh.Compact();
                    mesh.RebuildNormals();

                    resultMesh = new GH_Mesh(mesh);

                    if (result.Value.Plates.Count > 0)
                    {
                        for (int i = 0; i < ff_model.Plates.Count && i < result.Value.Plates.Count; i++)
                        {
                            Plate ff_plate = ff_model.Plates.ElementAt(i);
                            PlateModel femmPlate = result.Value.Plates.ElementAt(i);
                            if (int.TryParse(femmPlate.PlateId, out int femmPlateId))
                            {
                                if (femmPlateId == ff_plate.Id)
                                {
                                    Point3d[] points = null;

                                    if (ff_plate.PlateType == Plate.PlateTypes.Quad4)
                                    {
                                        points = new[]
                                        {
                                            new Point3d(ff_plate.Node1.X, ff_plate.Node1.Y, ff_plate.Node1.Z),
                                            new Point3d(ff_plate.Node2.X, ff_plate.Node2.Y, ff_plate.Node2.Z),
                                            new Point3d(ff_plate.Node3.X, ff_plate.Node3.Y, ff_plate.Node3.Z),
                                            new Point3d(ff_plate.Node4.X, ff_plate.Node4.Y, ff_plate.Node4.Z)
                                        };
                                    }
                                    else
                                    {
                                        points = new[]
                                        {
                                            new Point3d(ff_plate.Node1.X, ff_plate.Node1.Y, ff_plate.Node1.Z),
                                            new Point3d(ff_plate.Node2.X, ff_plate.Node2.Y, ff_plate.Node2.Z),
                                            new Point3d(ff_plate.Node3.X, ff_plate.Node3.Y, ff_plate.Node3.Z)
                                        };
                                    }

                                    femmPlate.Points = new System.Collections.ObjectModel.ObservableCollection<Point3d>(points);
                                    femmPlate.CreateShapes();
                                    GH_Plate gH_Plate = new GH_Plate(femmPlate);

                                    result.Guids.Remove(femmPlate);
                                    result.AddGuid(femmPlate, gH_Plate.Guid);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (options.PlateProperty != null) // Add the plates to the model if it's required
                        {
                            PlateModel[] plates = new PlateModel[ff_model.Plates.Count];

                            for (int i = 0; i < ff_model.Plates.Count; i++)
                            {
                                Plate ff_plate = ff_model.Plates.ElementAt(i);
                                Point3d[] points = null;

                                if (ff_plate.PlateType == Plate.PlateTypes.Quad4)
                                {
                                    points = new[]
                                    {
                                        new Point3d(ff_plate.Node1.X, ff_plate.Node1.Y, ff_plate.Node1.Z),
                                        new Point3d(ff_plate.Node2.X, ff_plate.Node2.Y, ff_plate.Node2.Z),
                                        new Point3d(ff_plate.Node3.X, ff_plate.Node3.Y, ff_plate.Node3.Z),
                                        new Point3d(ff_plate.Node4.X, ff_plate.Node4.Y, ff_plate.Node4.Z)
                                    };
                                }
                                else
                                {
                                    points = new[]
                                    {
                                        new Point3d(ff_plate.Node1.X, ff_plate.Node1.Y, ff_plate.Node1.Z),
                                        new Point3d(ff_plate.Node2.X, ff_plate.Node2.Y, ff_plate.Node2.Z),
                                        new Point3d(ff_plate.Node3.X, ff_plate.Node3.Y, ff_plate.Node3.Z)
                                    };
                                }
                                PlateModel plate = new PlateModel(points, options.PlateProperty)
                                {
                                    PlateId = ff_plate.Id.ToString()
                                };
                                plates[i] = plate;

                                GH_Plate gH_Plate = new GH_Plate(plate);
                                result.AddGuid(plate, gH_Plate.Guid);
                            }
                            result.Value.AddElements(plates);
                        }
                    }
                }

                SetResult(iteration, result, resultMesh);
                Message = "Done";
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                Message = "Error";
            }
        }

        private void SetResult(int iteration, GH_Model model, GH_Mesh mesh)
        {
            while (_models.Count <= iteration)
            {
                _models.Add(null);
                _meshes.Add(null);
            }
            _models[iteration] = model;
            _meshes[iteration] = mesh;
        }

        protected override void BeforeSolveInstance()
        {
            _runCount = Params.Input[0].VolatileDataCount;
            base.BeforeSolveInstance();
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// New GUID: the options moved to the Solver Options component.
        /// </summary>
        public override Guid ComponentGuid => new Guid("f4c3d571-98d3-4bc9-926e-b3315a1444f3");

        public override GH_Exposure Exposure => GH_Exposure.primary;
    }
}
