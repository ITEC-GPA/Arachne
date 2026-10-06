using System;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using Arachne.Arap;
using Arachne.Core;
using GPC.Utilities.Units;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public sealed class UnitSettingsComponent:ComponentBase
    {
        public UnitSettingsComponent():base("Unit Settings","Units","Explicit external units. Arachne models and solvers always use m, N, kg. Does not change Rhino document units.","1 Model"){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85309");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            int i=p.AddIntegerParameter("Length","L","GPC length unit",GH_ParamAccess.item,(int)UnitsConvert.LengthUnits.m);foreach(UnitsConvert.LengthUnits u in Enum.GetValues(typeof(UnitsConvert.LengthUnits)))((Param_Integer)p[i]).AddNamedValue(u.ToString(),(int)u);
            i=p.AddIntegerParameter("Force","F","GPC force unit",GH_ParamAccess.item,0);foreach(UnitsConvert.ForceUnits u in Enum.GetValues(typeof(UnitsConvert.ForceUnits)))((Param_Integer)p[i]).AddNamedValue(u.ToString(),(int)u);
            i=p.AddIntegerParameter("Mass","M","GPC mass unit",GH_ParamAccess.item,0);foreach(UnitsConvert.MassUnits u in Enum.GetValues(typeof(UnitsConvert.MassUnits)))((Param_Integer)p[i]).AddNamedValue(u.ToString(),(int)u);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddGenericParameter("Units","U","Explicit conversion settings",GH_ParamAccess.item);p.AddNumberParameter("Length factor","L","Multiply geometry coordinates by this factor to obtain metres",GH_ParamAccess.item);p.AddNumberParameter("Force factor","F","Multiply forces to obtain N",GH_ParamAccess.item);p.AddNumberParameter("Pressure factor","P","Multiply force/length2 values to obtain Pa",GH_ParamAccess.item);}
        protected override void Solve(IGH_DataAccess d){int l=2,f=0,m=0;d.GetData(0,ref l);d.GetData(1,ref f);d.GetData(2,ref m);var u=new UnitSettings((UnitsConvert.LengthUnits)l,(UnitsConvert.ForceUnits)f,(UnitsConvert.MassUnits)m);d.SetData(0,new GH_ObjectWrapper(u));d.SetData(1,u.Factor(Quantity.Length));d.SetData(2,u.Factor(Quantity.Force));d.SetData(3,u.Factor(Quantity.Pressure));}
    }
    public sealed class ConvertUnitsComponent:ComponentBase
    {
        public ConvertUnitsComponent():base("Convert Units","SI","Convert numeric quantities explicitly using GPCUtilities. Pressure uses the selected force divided by length squared.","1 Model"){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85310");
        protected override void RegisterInputParams(GH_InputParamManager p){p.AddNumberParameter("Values","V","Numbers to convert",GH_ParamAccess.list);p.AddGenericParameter("Units","U","Unit Settings",GH_ParamAccess.item);int i=p.AddIntegerParameter("Quantity","Q","Dimensional quantity",GH_ParamAccess.item,0);foreach(Quantity q in Enum.GetValues(typeof(Quantity)))((Param_Integer)p[i]).AddNamedValue(q.ToString(),(int)q);p.AddBooleanParameter("To SI","SI","True: external to SI; False: SI to external",GH_ParamAccess.item,true);}
        protected override void RegisterOutputParams(GH_OutputParamManager p)=>p.AddNumberParameter("Values","V","Converted values",GH_ParamAccess.list);
        protected override void Solve(IGH_DataAccess d){var values=new List<double>();d.GetDataList(0,values);var u=Read<UnitSettings>(d,1);if(u==null)return;int q=0;bool si=true;d.GetData(2,ref q);d.GetData(3,ref si);d.SetDataList(0,values.Select(v=>u.Convert(v,(Quantity)q,si)).ToArray());}
    }
    public sealed class PanelDistortionComponent:ComponentBase
    {
        public PanelDistortionComponent():base("Panel Distortion","Dist","Compare corresponding original and developed mesh edges/areas. Thresholds concern distortion, not material capacity.","3 Patterning"){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85311");
        protected override void RegisterInputParams(GH_InputParamManager p){p.AddMeshParameter("Original","A","Original mesh",GH_ParamAccess.item);p.AddMeshParameter("Developed","B","Same vertex and face ordering; triangulate both equally if needed",GH_ParamAccess.item);p.AddNumberParameter("Edge tolerance","ET","Absolute fractional change; 0.02=2%",GH_ParamAccess.item,0.02);p.AddNumberParameter("Area tolerance","AT","Absolute fractional change; 0.04=4%",GH_ParamAccess.item,0.04);}
        protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddBooleanParameter("Accepted","OK","Distortion within chosen limits",GH_ParamAccess.item);p.AddLineParameter("Edges","E","Developed edges corresponding to changes",GH_ParamAccess.list);p.AddNumberParameter("Length changes","DL","Signed fractions, not percent",GH_ParamAccess.list);p.AddNumberParameter("Area changes","DA","Signed fractions per face",GH_ParamAccess.list);p.AddNumberParameter("Max length change","ML","Maximum absolute fraction",GH_ParamAccess.item);p.AddNumberParameter("Max area change","MA","Maximum absolute fraction",GH_ParamAccess.item);}
        protected override void Solve(IGH_DataAccess d)
        {
            Mesh a=null,b=null;if(!d.GetData(0,ref a)||!d.GetData(1,ref b))return;double et=0.02,at=0.04;d.GetData(2,ref et);d.GetData(3,ref at);
            if(!a.IsValid||!b.IsValid||a.Vertices.Count!=b.Vertices.Count||a.Faces.Count!=b.Faces.Count)throw new ArgumentException("Valid meshes with matching topology required.");
            var faces=new List<int[]>();for(int i=0;i<a.Faces.Count;i++){var f=a.Faces[i];var g=b.Faces[i];if(f.A!=g.A||f.B!=g.B||f.C!=g.C||f.D!=g.D)throw new ArgumentException("Mesh face ordering differs.");faces.Add(f.IsTriangle?new[]{f.A,f.B,f.C}:new[]{f.A,f.B,f.C,f.D});}
            var r=PanelDistortion.Evaluate(a.Vertices.Select(v=>ToCore((Point3d)v)).ToArray(),b.Vertices.Select(v=>ToCore((Point3d)v)).ToArray(),faces,et,at);
            d.SetData(0,r.Accepted);d.SetDataList(1,r.EdgeVertices.Select(e=>new Line(b.Vertices[e[0]],b.Vertices[e[1]])));d.SetDataList(2,r.EdgeChanges);d.SetDataList(3,r.AreaChanges);d.SetData(4,r.MaxEdgeChange);d.SetData(5,r.MaxAreaChange);
        }
    }
    public sealed class ResultPreviewComponent:ResultComponentBase
    {
        private Line[] lines=Array.Empty<Line>();private Color[] colors=Array.Empty<Color>();
        public ResultPreviewComponent():base("Result Preview","Preview","Color solved cables by force, force density or adjacent-node residual. SI values and explicit legend limits."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85312");
        protected override void BeforeSolveInstance(){lines=Array.Empty<Line>();colors=Array.Empty<Color>();base.BeforeSolveInstance();}
        protected override void RegisterInputParams(GH_InputParamManager p){ResultInput(p);int i=p.AddIntegerParameter("Field","F","Scalar to display",GH_ParamAccess.item,0);((Param_Integer)p[i]).AddNamedValue("Axial force N",0);((Param_Integer)p[i]).AddNamedValue("Force density N/m",1);((Param_Integer)p[i]).AddNamedValue("Free residual N",2);}
        protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddLineParameter("Cables","C","Solved axes",GH_ParamAccess.list);p.AddColourParameter("Colors","K","Corresponding colors",GH_ParamAccess.list);p.AddNumberParameter("Values","V","Corresponding scalar",GH_ParamAccess.list);p.AddIntervalParameter("Legend","L","Blue minimum to red maximum",GH_ParamAccess.item);}
        protected override void Solve(IGH_DataAccess d)
        {
            var m=Solved(d);if(m==null)return;int field=0;d.GetData(1,ref field);if(field<0||field>2)throw new ArgumentException("Field must be 0, 1 or 2.");
            var values=m.Beams.Select(b=>field==0?b.AxialForce:field==1?b.ForceDensity:Math.Max(ResultMath.Norm(m.Node(b.StartNodeId).Residual),ResultMath.Norm(m.Node(b.EndNodeId).Residual))).ToArray();
            double min=values.Min(),max=values.Max();lines=m.Beams.Select(b=>new Line(ToRhino(m.Node(b.StartNodeId).Position),ToRhino(m.Node(b.EndNodeId).Position))).ToArray();
            colors=values.Select(v=>{double t=max>min?(v-min)/(max-min):0.5;return Color.FromArgb((int)(255*t),64,(int)(255*(1-t)));}).ToArray();
            d.SetDataList(0,lines);d.SetDataList(1,colors);d.SetDataList(2,values);d.SetData(3,new Interval(min,max));
        }
        public override void DrawViewportWires(IGH_PreviewArgs args){for(int i=0;i<lines.Length;i++)args.Display.DrawLine(lines[i],colors[i],3);}
    }
    public sealed class DependencyCheckComponent:ComponentBase
    {
        public DependencyCheckComponent():base("Dependency Check","DLL","Inspect loaded DLL versions and locations, solver API and the managed ARAP engine on this computer.","0 Utilities"){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85313");
        protected override void RegisterInputParams(GH_InputParamManager p)=>p.AddBooleanParameter("Check ARAP","A","Run a small managed ARAP calculation",GH_ParamAccess.item,true);
        protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddBooleanParameter("Available","OK","All required dependencies passed inspection",GH_ParamAccess.item);p.AddTextParameter("Report","R","Versions, actual paths and errors",GH_ParamAccess.list);}
        protected override void Solve(IGH_DataAccess d)
        {
            bool arap=true,ok=true;d.GetData(0,ref arap);var report=new List<string>();
            foreach(string name in new[]{"Arachne.Core","Arachne.Arap","GPCGeometry","GPCUtilities","TensileLib","MathNet.Numerics","alglibnet2","RhinoCommon","Grasshopper"})
            {try{var a=AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x=>x.GetName().Name==name)??Assembly.Load(name);report.Add(name+" "+a.GetName().Version+" | "+a.Location);if(name=="TensileLib"&&a.GetType("TensileLib.FormFinding.Solver")?.GetProperty("IterationCompleted")==null){ok=false;report.Add("TensileLib lacks the required release-one iteration API.");}}catch(Exception e){ok=false;report.Add(name+": "+e.Message);}}
            if(arap)
            {
                try
                {
                    var mesh = new ArapMesh(new[]{new GPC.Geometry.Point3d(0,0,0),new GPC.Geometry.Point3d(1,0,0),new GPC.Geometry.Point3d(0,1,0)},new[]{0,1,2});
                    var result=ArapSolver.Unwrap(mesh);
                    ok &= result.Success;
                    report.Add("ARAP managed calculation: "+result.Status+" | "+result.Message);
                }
                catch(Exception e){ok=false;report.Add("ARAP managed calculation: "+e.Message);}
            }
            else report.Add("ARAP calculation was not checked; managed assembly loading was checked.");
            d.SetData(0,ok);d.SetDataList(1,report);
        }
    }
}
