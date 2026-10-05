using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public sealed class ModelComponent : ComponentBase
    {
        public ModelComponent() : base("Model", "Model", "Assemble cables and merge coincident endpoints. All inputs use metres and newtons.", "1 Model") { }
        public override Guid ComponentGuid => new Guid("ae31349d-f4e7-4ff1-97fc-cea5b67d1082");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("Cables","C","Cable definitions; flatten for one model",GH_ParamAccess.list);
            int i=p.AddGenericParameter("Conditions","N","Supports and nodal forces",GH_ParamAccess.list); p[i].Optional=true;
            i=p.AddMeshParameter("Faces","M","Optional triangle/quad topology; vertices must match cable endpoints",GH_ParamAccess.item); p[i].Optional=true;
            p.AddNumberParameter("Tolerance","T","Endpoint merge tolerance in metres",GH_ParamAccess.item,1e-6);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddGenericParameter("Model","M","Arachne model",GH_ParamAccess.item);
            p.AddPointParameter("Nodes","N","Merged nodes in metres",GH_ParamAccess.list);
            p.AddLineParameter("Cables","C","Merged cable axes",GH_ParamAccess.list);
        }
        protected override void Solve(IGH_DataAccess data)
        {
            var cables=ReadList<Cable>(data,0); var nodes=ReadList<NodeCondition>(data,1);
            Mesh mesh=null; data.GetData(2,ref mesh); double tolerance=1e-6; data.GetData(3,ref tolerance);
            var faces=new List<GPC.Geometry.Point3d[]>();
            if(mesh!=null)
            {
                if(!mesh.IsValid) throw new ArgumentException("Invalid input mesh.");
                foreach(var f in mesh.Faces)
                {
                    var indices=f.IsQuad? new[]{f.A,f.B,f.C,f.D}:new[]{f.A,f.B,f.C};
                    faces.Add(indices.Select(i=>ToCore(new Point3d(mesh.Vertices[i]))).ToArray());
                }
            }
            var model=ModelBuilder.Build(cables,nodes,faces,tolerance);
            var points=model.Nodes.Select(n=>ToRhino(n.Position)).ToList();
            data.SetData(0,new GH_ObjectWrapper(model)); data.SetDataList(1,points);
            data.SetDataList(2,model.Edges.Select(e=>new Line(points[e.Start],points[e.End])));
            Message=$"{model.Nodes.Count} N / {model.Edges.Count} C";
        }
    }
}
