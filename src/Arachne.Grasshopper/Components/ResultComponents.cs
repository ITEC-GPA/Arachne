using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public abstract class ResultComponentBase : ComponentBase
    {
        protected ResultComponentBase(string name,string nick,string description):base(name,nick,description,"4 Results"){}
        protected static Vector3d V(GPC.Geometry.Vector3d v)=>new Vector3d(v.X,v.Y,v.Z);
        protected static SolvedModel Solved(IGH_DataAccess data)
        {
            var obj=Read<object>(data,0);if(obj==null)return null;
            return (obj as SolvedModel)??(obj as FormResult)?.Solved??throw new ArgumentException("A converged Arachne result is required.");
        }
        protected static int[] Ids(IGH_DataAccess data,int count)
        { var ids=new List<int>();data.GetDataList(1,ids);return ids.Count==0?Enumerable.Range(1,count).ToArray():ids.ToArray(); }
        protected void ResultInput(GH_InputParamManager p,bool ids=false)
        { p.AddGenericParameter("Result","R","Arachne result or solved model",GH_ParamAccess.item);if(ids){int i=p.AddIntegerParameter("IDs","ID","One-based IDs, empty selects all; order is preserved",GH_ParamAccess.list);p[i].Optional=true;} }
    }

    public sealed class DeconstructModelComponent : ComponentBase
    {
        public DeconstructModelComponent():base("Deconstruct Model","DM","Inspect initial model topology, restraints and applied loads. IDs are one-based within this model.","1 Model"){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85301");
        protected override void RegisterInputParams(GH_InputParamManager p)=>p.AddGenericParameter("Model","M","Model or Form Find result (initial model)",GH_ParamAccess.item);
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddTextParameter("Model ID","MID","Identity of this model",GH_ParamAccess.item);
            p.AddPointParameter("Nodes","P","Initial node positions, m",GH_ParamAccess.list);
            p.AddIntegerParameter("Node IDs","NID","Node IDs",GH_ParamAccess.list);
            p.AddLineParameter("Cables","C","Initial cable axes, m",GH_ParamAccess.list);
            p.AddIntegerParameter("Cable IDs","CID","Cable IDs",GH_ParamAccess.list);
            p.AddIntegerParameter("Connectivity","CN","Start/end node IDs, branch {cable ID}",GH_ParamAccess.tree);
            p.AddIntegerParameter("Plates","PN","Ordered node IDs, branch {plate ID}",GH_ParamAccess.tree);
            p.AddBooleanParameter("Restraints","FIX","X/Y/Z restraints, branch {node ID}",GH_ParamAccess.tree);
            p.AddVectorParameter("Applied forces","F","Direct nodal loads, N; excludes generated pressure and self weight",GH_ParamAccess.list);
            p.AddGenericParameter("Cable data","CD","Initial cable settings and loads",GH_ParamAccess.list);
        }
        protected override void Solve(IGH_DataAccess d)
        {
            var value=Read<object>(d,0);if(value==null)return;var m=value as FormModel??(value as FormResult)?.Model??throw new ArgumentException("Expected Model or Result.");
            d.SetData(0,m.Id.ToString());d.SetDataList(1,m.Nodes.Select(n=>ToRhino(n.Position)));d.SetDataList(2,m.Nodes.Select(n=>n.Id));
            d.SetDataList(3,m.Edges.Select(e=>new Line(ToRhino(m.Nodes[e.Start].Position),ToRhino(m.Nodes[e.End].Position))));d.SetDataList(4,m.Edges.Select(e=>e.Id));
            var edges=new GH_Structure<GH_Integer>();foreach(var e in m.Edges)edges.AppendRange(new[]{new GH_Integer(e.Start+1),new GH_Integer(e.End+1)},new GH_Path(e.Id));d.SetDataTree(5,edges);
            var faces=new GH_Structure<GH_Integer>();int id=0;foreach(var face in m.Faces)faces.AppendRange(face.Select(i=>new GH_Integer(i+1)),new GH_Path(++id));d.SetDataTree(6,faces);
            var fix=new GH_Structure<GH_Boolean>();foreach(var n in m.Nodes)fix.AppendRange(new[]{new GH_Boolean(n.FixX),new GH_Boolean(n.FixY),new GH_Boolean(n.FixZ)},new GH_Path(n.Id));d.SetDataTree(7,fix);
            d.SetDataList(8,m.Nodes.Select(n=>new Vector3d(n.Force.X,n.Force.Y,n.Force.Z)));d.SetDataList(9,m.Edges.Select(e=>new GH_ObjectWrapper(e.Cable)));
        }
    }

    public sealed class DeconstructResultComponent : ResultComponentBase
    {
        public DeconstructResultComponent():base("Deconstruct Result","DR","Separate initial model, solved elements, status and diagnostics."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85302");
        protected override void RegisterInputParams(GH_InputParamManager p)=>ResultInput(p);
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGenericParameter("Initial model","M","Snapshot used for this calculation",GH_ParamAccess.item);p.AddGenericParameter("Solved model","S","Equilibrium geometry and element results",GH_ParamAccess.item);
            p.AddTextParameter("Status","ST","Solver status; geometry exists only for convergence",GH_ParamAccess.item);p.AddTextParameter("Log","L","Diagnostics",GH_ParamAccess.list);
            p.AddTextParameter("Result ID","ID","Unique calculation ID",GH_ParamAccess.item);p.AddIntegerParameter("Internal iterations","I","Actual recorded steps, not a guessed count",GH_ParamAccess.item);
        }
        protected override void Solve(IGH_DataAccess d)
        { var r=Read<FormResult>(d,0);if(r==null)return;if(r.Model!=null)d.SetData(0,new GH_ObjectWrapper(r.Model));if(r.Solved!=null)d.SetData(1,new GH_ObjectWrapper(r.Solved));d.SetData(2,r.Status.ToString());d.SetDataList(3,r.Messages);d.SetData(4,r.Id.ToString());d.SetData(5,r.History.Count); }
    }

    public sealed class NodeResultsComponent : ResultComponentBase
    {
        public NodeResultsComponent():base("Node Results","NR","Query nodes by ID: displacement, loads, support reactions and free-DOF residuals. SI units."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85303");
        protected override void RegisterInputParams(GH_InputParamManager p)=>ResultInput(p,true);
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddIntegerParameter("IDs","ID","Node IDs",GH_ParamAccess.list);p.AddPointParameter("Positions","P","Solved positions, m",GH_ParamAccess.list);
            p.AddVectorParameter("Displacement","U","Solved minus initial position, m",GH_ParamAccess.list);p.AddVectorParameter("External force","F","All applied nodal force contributions, N",GH_ParamAccess.list);
            p.AddVectorParameter("Reaction","R","Support force acting on model, N",GH_ParamAccess.list);p.AddVectorParameter("Residual","E","Internal + external + reaction, N; constrained components are zero",GH_ParamAccess.list);
            p.AddVectorParameter("Pressure","PRES","Equivalent pressure loads, N",GH_ParamAccess.list);p.AddVectorParameter("Self weight","W","Equivalent self weight, N",GH_ParamAccess.list);
            p.AddVectorParameter("Cable loads","CL","Equivalent cable point loads, N",GH_ParamAccess.list);p.AddVectorParameter("Applied force","A","Direct nodal force, N",GH_ParamAccess.list);
            p.AddBooleanParameter("Restraints","FIX","X/Y/Z, branch {node ID}",GH_ParamAccess.tree);p.AddGenericParameter("Node records","N","Full immutable node results",GH_ParamAccess.list);
        }
        protected override void Solve(IGH_DataAccess d)
        {
            var m=Solved(d);if(m==null)return;var ns=Ids(d,m.Nodes.Count).Select(m.Node).ToArray();
            d.SetDataList(0,ns.Select(n=>n.Id));d.SetDataList(1,ns.Select(n=>ToRhino(n.Position)));d.SetDataList(2,ns.Select(n=>V(n.Displacement)));d.SetDataList(3,ns.Select(n=>V(n.ExternalForce)));
            d.SetDataList(4,ns.Select(n=>V(n.Reaction)));d.SetDataList(5,ns.Select(n=>V(n.Residual)));d.SetDataList(6,ns.Select(n=>V(n.PressureForce)));d.SetDataList(7,ns.Select(n=>V(n.SelfWeightForce)));
            d.SetDataList(8,ns.Select(n=>V(n.CableLoadForce)));d.SetDataList(9,ns.Select(n=>V(n.AppliedForce)));
            var fix=new GH_Structure<GH_Boolean>();foreach(var n in ns)fix.AppendRange(new[]{new GH_Boolean(n.FixX),new GH_Boolean(n.FixY),new GH_Boolean(n.FixZ)},new GH_Path(n.Id));d.SetDataTree(10,fix);
            d.SetDataList(11,ns.Select(n=>new GH_ObjectWrapper(n)));
        }
    }

    public sealed class BeamResultsComponent : ResultComponentBase
    {
        public BeamResultsComponent():base("Beam Results","BR","Query cable geometry, axial force and force density by ID. Positive axial force is tension."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85304");
        protected override void RegisterInputParams(GH_InputParamManager p)=>ResultInput(p,true);
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddIntegerParameter("IDs","ID","Cable IDs",GH_ParamAccess.list);p.AddLineParameter("Axes","L","Solved axes, m",GH_ParamAccess.list);p.AddIntegerParameter("Start node","A","Node ID",GH_ParamAccess.list);p.AddIntegerParameter("End node","B","Node ID",GH_ParamAccess.list);
            p.AddNumberParameter("Axial force","N","N; positive tension",GH_ParamAccess.list);p.AddNumberParameter("Force density","Q","N/m",GH_ParamAccess.list);p.AddNumberParameter("Length","L1","Solved length, m",GH_ParamAccess.list);p.AddNumberParameter("Initial length","L0","Initial length, m",GH_ParamAccess.list);
            p.AddGenericParameter("Cable records","B","Full cable results with target and direction",GH_ParamAccess.list);
        }
        protected override void Solve(IGH_DataAccess d)
        {
            var m=Solved(d);if(m==null)return;var bs=Ids(d,m.Beams.Count).Select(m.Beam).ToArray();
            d.SetDataList(0,bs.Select(b=>b.Id));d.SetDataList(1,bs.Select(b=>new Line(ToRhino(m.Node(b.StartNodeId).Position),ToRhino(m.Node(b.EndNodeId).Position))));d.SetDataList(2,bs.Select(b=>b.StartNodeId));d.SetDataList(3,bs.Select(b=>b.EndNodeId));
            d.SetDataList(4,bs.Select(b=>b.AxialForce));d.SetDataList(5,bs.Select(b=>b.ForceDensity));d.SetDataList(6,bs.Select(b=>b.Length));d.SetDataList(7,bs.Select(b=>b.InitialLength));d.SetDataList(8,bs.Select(b=>new GH_ObjectWrapper(b)));
        }
    }

    public sealed class PlateResultsComponent : ResultComponentBase
    {
        public PlateResultsComponent():base("Plate Results","PR","Query face geometry and ordered boundary cable forces. Membrane Nx/Ny/Nxy are unavailable in TensileLib."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85305");
        protected override void RegisterInputParams(GH_InputParamManager p)=>ResultInput(p,true);
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddIntegerParameter("IDs","ID","Plate IDs",GH_ParamAccess.list);p.AddMeshParameter("Plates","M","Solved face meshes",GH_ParamAccess.list);
            p.AddIntegerParameter("Node IDs","NID","Ordered vertices; branch {plate ID}",GH_ParamAccess.tree);p.AddIntegerParameter("Boundary cable IDs","BID","One per ordered side; 0 means no cable",GH_ParamAccess.tree);
            p.AddNumberParameter("Boundary axial forces","N","Cable forces in N, not membrane resultants; NaN for missing side",GH_ParamAccess.tree);
            p.AddNumberParameter("Area","A1","Solved area, m2; quad diagonal 0-2",GH_ParamAccess.list);p.AddNumberParameter("Initial area","A0","Initial area, m2",GH_ParamAccess.list);
            p.AddBooleanParameter("Membrane results available","OK","False until a validated membrane-resultant recovery is implemented",GH_ParamAccess.list);p.AddTextParameter("Membrane status","S","Explanation of missing membrane resultants",GH_ParamAccess.list);
            p.AddGenericParameter("Plate records","P","Plate geometry/topology/boundary force records",GH_ParamAccess.list);
        }
        protected override void Solve(IGH_DataAccess d)
        {
            var m=Solved(d);if(m==null)return;var ps=Ids(d,m.Plates.Count).Select(m.Plate).ToArray();
            var meshes=new List<Mesh>();var nodes=new GH_Structure<GH_Integer>();var beams=new GH_Structure<GH_Integer>();var forces=new GH_Structure<GH_Number>();
            foreach(var p in ps){var mesh=new Mesh();mesh.Vertices.AddVertices(p.NodeIds.Select(id=>ToRhino(m.Node(id).Position)));if(p.NodeIds.Count==3)mesh.Faces.AddFace(0,1,2);else mesh.Faces.AddFace(0,1,2,3);mesh.Normals.ComputeNormals();meshes.Add(mesh);
                var path=new GH_Path(p.Id);nodes.AppendRange(p.NodeIds.Select(i=>new GH_Integer(i)),path);beams.AppendRange(p.BoundaryBeamIds.Select(i=>new GH_Integer(i)),path);forces.AppendRange(p.BoundaryAxialForces.Select(f=>new GH_Number(f)),path);}
            d.SetDataList(0,ps.Select(p=>p.Id));d.SetDataList(1,meshes);d.SetDataTree(2,nodes);d.SetDataTree(3,beams);d.SetDataTree(4,forces);d.SetDataList(5,ps.Select(p=>p.Area));d.SetDataList(6,ps.Select(p=>p.InitialArea));d.SetDataList(7,ps.Select(p=>p.HasMembraneResultants));d.SetDataList(8,ps.Select(p=>p.MembraneResultantsStatus));d.SetDataList(9,ps.Select(p=>new GH_ObjectWrapper(p)));
        }
    }

    public sealed class EquilibriumReportComponent : ResultComponentBase
    {
        public EquilibriumReportComponent():base("Equilibrium Report","EQ","Check free-node residuals and global force/moment balance independently of solver convergence."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85306");
        protected override void RegisterInputParams(GH_InputParamManager p){ResultInput(p);p.AddNumberParameter("Force tolerance","FT","Absolute N",GH_ParamAccess.item,1e-4);p.AddNumberParameter("Moment tolerance","MT","Absolute N*m",GH_ParamAccess.item,1e-4);p.AddPointParameter("Origin","O","Moment reference in metres",GH_ParamAccess.item,Point3d.Origin);}
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {p.AddBooleanParameter("Accepted","OK","All three checks pass",GH_ParamAccess.item);p.AddVectorParameter("External","F","Total external force, N",GH_ParamAccess.item);p.AddVectorParameter("Reactions","R","Total support reaction, N",GH_ParamAccess.item);p.AddVectorParameter("Force imbalance","EF","N",GH_ParamAccess.item);p.AddVectorParameter("Moment imbalance","EM","N*m",GH_ParamAccess.item);p.AddNumberParameter("Max residual","E","Largest free-node residual norm, N",GH_ParamAccess.item);}
        protected override void Solve(IGH_DataAccess d){var m=Solved(d);if(m==null)return;double ft=1e-4,mt=1e-4;var o=Point3d.Origin;d.GetData(1,ref ft);d.GetData(2,ref mt);d.GetData(3,ref o);var r=EquilibriumReport.Evaluate(m,ft,mt,ToCore(o));d.SetData(0,r.Accepted);d.SetData(1,V(r.TotalExternal));d.SetData(2,V(r.TotalReaction));d.SetData(3,V(r.ForceImbalance));d.SetData(4,V(r.MomentImbalance));d.SetData(5,r.MaxFreeResidual);if(!r.Accepted)AddRuntimeMessage(GH_RuntimeMessageLevel.Warning,"Equilibrium exceeds the specified tolerances.");}
    }

    public sealed class SolverHistoryComponent : ResultComponentBase
    {
        public SolverHistoryComponent():base("Solver History","HIST","Actual internal iteration criteria and timing, including interrupted/failed runs. These criteria are not force residuals."){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85307");
        protected override void RegisterInputParams(GH_InputParamManager p)=>ResultInput(p);
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {p.AddIntegerParameter("Outer","O","Solver call",GH_ParamAccess.list);p.AddIntegerParameter("Inner","I","Internal step",GH_ParamAccess.list);p.AddNumberParameter("Criterion","C","Solver-specific relative criterion",GH_ParamAccess.list);p.AddTextParameter("Metric","M","Criterion definition",GH_ParamAccess.list);p.AddNumberParameter("Elapsed","T","Milliseconds since solve start",GH_ParamAccess.list);}
        protected override void Solve(IGH_DataAccess d){var r=Read<FormResult>(d,0);if(r==null)return;d.SetDataList(0,r.History.Select(h=>h.Outer));d.SetDataList(1,r.History.Select(h=>h.Inner));d.SetDataList(2,r.History.Select(h=>h.Criterion));d.SetDataList(3,r.History.Select(h=>h.Metric));d.SetDataList(4,r.History.Select(h=>h.ElapsedMilliseconds));}
    }

    public sealed class CheckModelComponent : ComponentBase
    {
        public CheckModelComponent():base("Check Model","CHK","Check connectivity, supports, collapsed geometry and plate topology before solving.","1 Model"){}
        public override Guid ComponentGuid=>new Guid("e64bd7b8-5b21-4485-a27d-c61a0cd85308");
        protected override void RegisterInputParams(GH_InputParamManager p)=>p.AddGenericParameter("Model","M","Arachne model",GH_ParamAccess.item);
        protected override void RegisterOutputParams(GH_OutputParamManager p){p.AddBooleanParameter("Valid","V","No blocking errors",GH_ParamAccess.item);p.AddTextParameter("Errors","E","Blocking issues",GH_ParamAccess.list);p.AddTextParameter("Warnings","W","Review notes",GH_ParamAccess.list);p.AddIntegerParameter("Parts","P","Connected parts",GH_ParamAccess.item);}
        protected override void Solve(IGH_DataAccess d){var m=Read<FormModel>(d,0);if(m==null)return;var r=ModelChecks.Check(m);d.SetData(0,r.Valid);d.SetDataList(1,r.Errors);d.SetDataList(2,r.Warnings);d.SetData(3,r.ConnectedParts);}
    }
}
