using System;
using System.Linq;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Arachne.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public sealed class SolverSettingsComponent : ComponentBase
    {
        public SolverSettingsComponent() : base("Solver Settings","Settings","Configure form finding in SI units.","2 Form Finding") { }
        public override Guid ComponentGuid => new Guid("7e0e1c73-06ab-465d-862f-383f415cbe73");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            int i=p.AddIntegerParameter("Solver","S","0 Linear, 1 Linear iterative, 2 Nonlinear",GH_ParamAccess.item,0);
            foreach(SolverKind kind in Enum.GetValues(typeof(SolverKind))) ((Param_Integer)p[i]).AddNamedValue(kind.ToString(),(int)kind);
            p.AddIntegerParameter("Max iterations","I","Maximum iterations; also caps the outer iterative loop",GH_ParamAccess.item,100);
            p.AddNumberParameter("Tolerance","T","Relative convergence tolerance",GH_ParamAccess.item,1e-4);
            p.AddNumberParameter("Gravity","G","Gravity acceleration in m/s2; 0 disables self-weight",GH_ParamAccess.item,0);
            p.AddNumberParameter("Pressure","P","Normal pressure in N/m2",GH_ParamAccess.item,0);
            p.AddBooleanParameter("Create mesh","M","Generate output mesh",GH_ParamAccess.item,true);
            p.AddPlaneParameter("Plane","PL","Reference plane for projection and mesh generation",GH_ParamAccess.item,Plane.WorldXY);
            p.AddIntegerParameter("Algebraic iterations","AI","0 uses TensileLib default",GH_ParamAccess.item,0);
            p.AddNumberParameter("Algebraic tolerance A","TA","0 uses TensileLib default",GH_ParamAccess.item,0);
            p.AddNumberParameter("Algebraic tolerance B","TB","Reserved for compatibility; keep 0. This TensileLib version supports only tolerance A.",GH_ParamAccess.item,0);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) => p.AddGenericParameter("Settings","S","Solver settings",GH_ParamAccess.item);
        protected override void Solve(IGH_DataAccess data)
        {
            int kind=0, iterations=100, algebraic=0; double tolerance=1e-4,gravity=0,pressure=0,ta=0,tb=0; bool mesh=true; var plane=Plane.WorldXY;
            data.GetData(0,ref kind);data.GetData(1,ref iterations);data.GetData(2,ref tolerance);data.GetData(3,ref gravity);data.GetData(4,ref pressure);
            data.GetData(5,ref mesh);data.GetData(6,ref plane);data.GetData(7,ref algebraic);data.GetData(8,ref ta);data.GetData(9,ref tb);
            if(!plane.IsValid) throw new ArgumentException("Invalid reference plane.");
            var settings=new SolverSettings{Kind=(SolverKind)kind,MaxIterations=iterations,Tolerance=tolerance,Gravity=gravity,Pressure=pressure,
                CreateMesh=mesh,PlaneOrigin=ToCore(plane.Origin),PlaneNormal=ToCore(plane.Normal),AlgebraicMaxIterations=algebraic,AlgebraicToleranceA=ta,AlgebraicToleranceB=tb};
            Validation.Settings(settings);data.SetData(0,new GH_ObjectWrapper(settings));
        }
    }

    public sealed class FormFindComponent : ComponentBase
    {
        private CancellationTokenSource cancellation;
        private Task<FormResult> pending;
        private string pendingKey;
        private int generation;
        private string progressText;
        public FormFindComponent() : base("Form Find","FF","Compute equilibrium with TensileLib. Outputs geometry only after convergence.","2 Form Finding") { }
        public override Guid ComponentGuid => new Guid("c78cb6a5-0d3d-41d8-b381-8a1b2b20ba08");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("Model","M","Arachne model",GH_ParamAccess.item);
            int i=p.AddGenericParameter("Settings","S","Optional settings; defaults to Linear",GH_ParamAccess.item);p[i].Optional=true;
            p.AddBooleanParameter("Run","R","True runs after every input change; False clears results",GH_ParamAccess.item,false);
            p.AddBooleanParameter("Background","BG","Run without blocking the canvas; current result is cleared when inputs change",GH_ParamAccess.item,false);
            p.AddBooleanParameter("Cancel","C","Cancel a running background solve at the next solver checkpoint. Set Run=False to reset.",GH_ParamAccess.item,false);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddPointParameter("Nodes","N","Equilibrium nodes in metres",GH_ParamAccess.list);
            p.AddLineParameter("Cables","C","Equilibrium cable axes",GH_ParamAccess.list);
            p.AddMeshParameter("Mesh","M","Equilibrium mesh",GH_ParamAccess.item);
            p.AddNumberParameter("Axial forces","F","Axial force for each cable, N",GH_ParamAccess.list);
            p.AddNumberParameter("Force densities","Q","Axial force divided by real length, N/m",GH_ParamAccess.list);
            p.AddVectorParameter("Pressure forces","P","Equivalent nodal pressure loads in N",GH_ParamAccess.list);
            p.AddTextParameter("Status","S","Converged, NotConverged, Failed or DependencyMissing",GH_ParamAccess.item);
            p.AddTextParameter("Log","L","Warnings and diagnostics",GH_ParamAccess.list);
            p.AddIntegerParameter("Outer iterations","I","Number of calls to the TensileLib solver",GH_ParamAccess.item);
            p.AddGenericParameter("Result","R","Complete calculation, initial model, solved element records and history",GH_ParamAccess.item);
            p.AddGenericParameter("Solved model","SM","Immutable solved nodes, cables and plates; connect to result query components",GH_ParamAccess.item);
        }
        protected override void Solve(IGH_DataAccess data)
        {
            var model=Read<FormModel>(data,0);if(model==null)return;
            var settings=Read<SolverSettings>(data,1)??new SolverSettings();bool run=false;data.GetData(2,ref run);
            bool background=false,cancel=false;data.GetData(3,ref background);data.GetData(4,ref cancel);
            if(!run){ResetJob();Message="Ready";data.SetData(6,"Ready");return;}
            FormResult result;
            if(background)
            {
                if(Params.Input[0].VolatileDataCount!=1 || Params.Input[1].VolatileDataCount>1)
                    throw new ArgumentException("Background mode requires one model and one settings item. Use synchronous mode for data trees.");
                Validation.Settings(settings);
                string key=model.Id+"|"+string.Join("|",new object[]{settings.Kind,settings.MaxIterations,settings.Tolerance,settings.AlgebraicMaxIterations,settings.AlgebraicToleranceA,settings.AlgebraicToleranceB,settings.Gravity,settings.Pressure,settings.CreateMesh,settings.PlaneOrigin.X,settings.PlaneOrigin.Y,settings.PlaneOrigin.Z,settings.PlaneNormal.X,settings.PlaneNormal.Y,settings.PlaneNormal.Z}.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture)));
                if(pendingKey!=key){ResetJob();if(cancel){data.SetData(6,"Cancelled");return;}StartJob(model,settings,key);}
                if(cancel)cancellation?.Cancel();
                if(!pending.IsCompleted){Message=cancel?"Cancelling":progressText??"Running";data.SetData(6,Message);return;}
                result=pending.GetAwaiter().GetResult();
            }
            else
            {
                ResetJob();if(cancel){data.SetData(6,"Cancelled");Message="Cancelled";return;}
                result=new TensileFormFindingSolver().Solve(model,settings);
            }
            data.SetData(9,new GH_ObjectWrapper(result));
            if(result.Solved!=null)data.SetData(10,new GH_ObjectWrapper(result.Solved));
            Message=result.Status.ToString(); data.SetData(6,Message); data.SetDataList(7,result.Messages); data.SetData(8,result.OuterIterations);
            foreach(string message in result.Messages) AddRuntimeMessage(result.Status==SolveStatus.Cancelled?GH_RuntimeMessageLevel.Remark:result.Status==SolveStatus.Converged?GH_RuntimeMessageLevel.Warning:GH_RuntimeMessageLevel.Error,message);
            if(result.Status!=SolveStatus.Converged)return;
            var points=result.Positions.Select(ToRhino).ToList();
            data.SetDataList(0,points);data.SetDataList(1,model.Edges.Select(e=>new Line(points[e.Start],points[e.End])));
            if(settings.CreateMesh && result.Faces.Count>0)
            {
                var mesh=new Mesh();mesh.Vertices.AddVertices(points);
                foreach(var face in result.Faces)
                    if(face.Length==3)mesh.Faces.AddFace(face[0],face[1],face[2]);else mesh.Faces.AddFace(face[0],face[1],face[2],face[3]);
                mesh.Normals.ComputeNormals();mesh.Compact();data.SetData(2,mesh);
            }
            data.SetDataList(3,result.AxialForces);data.SetDataList(4,result.ForceDensities);
            data.SetDataList(5,result.PressureForces.Select(v=>new Vector3d(v.X,v.Y,v.Z)));
        }
        private void StartJob(FormModel model,SolverSettings settings,string key)
        {
            var document=OnPingDocument();int job=generation;var tokenSource=new CancellationTokenSource();cancellation=tokenSource;
            var snapshot=model.Snapshot();var parameters=settings.Copy();pendingKey=key;progressText="Running";double lastUpdate=-1000;
            void Update(){Rhino.RhinoApp.InvokeOnUiThread((Action)(()=>{if(generation==job && document!=null && OnPingDocument()==document)document.ScheduleSolution(10,_=>{if(generation==job)ExpireSolution(false);});}));}
            pending=Task.Run(()=>new TensileFormFindingSolver().Solve(snapshot,parameters,tokenSource.Token,step=>{
                if(step.ElapsedMilliseconds-lastUpdate<150)return;lastUpdate=step.ElapsedMilliseconds;progressText=$"Step {step.Outer}.{step.Inner}";Update();
            }));
            pending.ContinueWith(_=>Update(),TaskScheduler.Default);
        }
        private void ResetJob()
        {
            generation++;var old=cancellation;var task=pending;cancellation=null;pending=null;pendingKey=null;progressText=null;
            if(old!=null){old.Cancel();if(task!=null&&!task.IsCompleted)task.ContinueWith(_=>old.Dispose(),TaskScheduler.Default);else old.Dispose();}
        }
        public override void RemovedFromDocument(GH_Document document){ResetJob();base.RemovedFromDocument(document);}
    }
}
