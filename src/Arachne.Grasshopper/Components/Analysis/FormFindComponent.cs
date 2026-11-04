using Arachne.Common.Analysis;
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

namespace Arachne.Grasshopper.Components.Analysis
{
    /// <summary>
    /// Form Find on GPC objects: the analysis graph of the run (<see cref="FormFindingInputBuilder"/>), its domain preflight and the
    /// TensileLib solver (<see cref="TensileModelAdapter"/>), the results attached to the graph and kept in the sidecar of the document
    /// (<see cref="AnalysisDocumentState"/>), and the outputs projected from them (<see cref="FormFindingOutputProjection"/>), as the
    /// former Form Find. A saved run restores its outputs without the solver while its inputs are unchanged.
    /// </summary>
    public class FormFindComponent : GH_Component
    {
        private readonly AnalysisDocumentState _analysisState = new AnalysisDocumentState();

        /// <summary>The references of the runs in the archive (waits for their preparation).</summary>
        public IReadOnlyList<GPC.Model.Persistence.ExternalModelReference> AnalysisReferences => _analysisState.References;
        public GPC.Model.Persistence.ExternalModelRun ReadAnalysis(int iteration) => _analysisState.Open(iteration);
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

        public override bool Write(GH_IO.Serialization.GH_IWriter writer)
        {
            if (!base.Write(writer)) return false;
            try { _analysisState.Write(writer, InstanceGuid); return true; }
            catch (Exception error)
            {
                writer.AddMessage("Cannot save form-finding analysis: " + error.Message, GH_IO.Serialization.GH_Message_Type.error);
                return false;
            }
        }

        public override bool Read(GH_IO.Serialization.GH_IReader reader)
        {
            if (!base.Read(reader)) return false;
            try { _analysisState.Read(reader); _models.Clear(); _meshes.Clear(); return true; }
            catch (Exception error)
            {
                reader.AddMessage("Cannot read form-finding analysis: " + error.Message, GH_IO.Serialization.GH_Message_Type.error);
                return false;
            }
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
            FormFindingOptions options = gh_options?.Value ?? new FormFindingOptions();
            DA.GetData(3, ref plane);
            if (!DA.GetData("Auto Run", ref autoRun))
                return;

            // If the user gives the pressures it must give also the load case
            if (options.Pressure != 0 && options.PressureLoadCase == null)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Give the Load Case to assign at the pressure forces");
                return;
            }

            bool execute = _run || autoRun;
            if (execute)
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

            if (!execute && _analysisState.Has(DA.Iteration))
                RestoreAnalysis(DA.Iteration, model, solverType, options, plane);

            if (DA.Iteration < _models.Count && _models[DA.Iteration] != null)
            {
                DA.SetData(0, _models[DA.Iteration]);
                DA.SetData(1, _meshes[DA.Iteration]);
            }
        }

        private void RestoreAnalysis(int iteration, GH_Model model, int solverType, FormFindingOptions options, Plane plane)
        {
            var run = _analysisState.Keep(iteration);
            try
            {
                var current = FormFindingInputBuilder.Build(model.Value, model.Units, solverType, options, plane);
                var exported = TensileModelAdapter.Export(current.Model, current.Units, current);
                if (!_analysisState.IsCurrent(iteration, current.Model.Guid, exported.Map.AnalysisFingerprint))
                {
                    if (iteration < _models.Count) { _models[iteration] = null; _meshes[iteration] = null; }
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Saved form-finding results belong to different inputs. Run the analysis again.");
                    Message = "Inputs changed"; return;
                }
                var view = FormFindingOutputProjection.Project(model.Value, _analysisState.Analysis(iteration), options, model.Units);
                SetResult(iteration, new GH_Model(view.Model, model.Units), view.Mesh == null ? null : new GH_Mesh(view.Mesh));
                _analysisState.Put(iteration, run);
                Message = "Restored";
            }
            catch (Exception error)
            {
                if (iteration < _models.Count) { _models[iteration] = null; _meshes[iteration] = null; }
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Cannot restore saved form finding: " + error.Message);
            }
        }

        // The inputs that the selected method ignores are said by the remarks of the former assembly too: no warning for them.
        private static readonly HashSet<string> IgnoredInputCodes = new HashSet<string> { "FormFindingFixedLengthIgnored", "FormFindingFixedAxialForceIgnored", "FormFindingCoordinateWeightsIgnored" };

        private void Solve(int iteration, GH_Model model, int solverType, FormFindingOptions options, Plane plane)
        {
            // A failed run does not leave the previous result on the outputs
            SetResult(iteration, null, null);
            try
            {
                var input = FormFindingInputBuilder.Build(model.Value, model.Units, solverType, options, plane);
                foreach (string remark in input.Remarks) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, remark);
                var exported = TensileModelAdapter.Export(input.Model, input.Units, input);
                foreach (var diagnostic in exported.Diagnostics)
                    AddRuntimeMessage(diagnostic.Severity == GPC.Model.Core.Diagnostics.DiagnosticSeverity.Warning && !IgnoredInputCodes.Contains(diagnostic.Code)
                        ? GH_RuntimeMessageLevel.Warning : GH_RuntimeMessageLevel.Remark, diagnostic.Code + ": " + diagnostic.Message);
                foreach (string remark in exported.Remarks.Concat(input.FinalRemarks)) AddRuntimeMessage(GH_RuntimeMessageLevel.Remark, remark);
#if ARACHNE_MIGRATION_CAPTURE
                int captureId = MigrationTensileCapture.Input(exported.Solver, model.Units, solverType, options);
#endif
                var outcome = TensileModelAdapter.Solve(exported.Solver, input.Model.FormFinding);
#if ARACHNE_MIGRATION_CAPTURE
                MigrationTensileCapture.Result(captureId, exported.Solver, outcome.Converged, "solver-returned");
#endif
                if (outcome.Error != null)
                {
                    // Linear step by step: no result when its outer loop does not converge
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, outcome.Error);
                    Message = "Error";
                    return;
                }
                // The reactions are stored only for an equilibrium configuration; the shape is published also at the iteration limit
                if (!outcome.Converged) AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Not converged. The maximum number of iteration has been reached.");
                TensileModelAdapter.Attach(input.Model, exported, outcome, input);
                // The sidecar of the run is prepared in the background from here, while the outputs are projected: the projection
                // only reads the analysis graph and its results (lane L9), so a save right after the run waits less, or not at all.
                _analysisState.Prepare(iteration, input.Model, exported.Map);
                var run = _analysisState.Keep(iteration);
                FormFindingOutputProjection.Output view;
                try { view = FormFindingOutputProjection.Project(model.Value, input.Model, options, model.Units); }
                catch { _analysisState.Clear(iteration); throw; }
                SetResult(iteration, new GH_Model(view.Model, model.Units), view.Mesh == null ? null : new GH_Mesh(view.Mesh));
                _analysisState.Put(iteration, run);
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
            _analysisState.Clear(iteration);
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
