using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public sealed class DemoComponent : ComponentBase
    {
        public DemoComponent() : base("Demo Saddle Net","Demo","Small saddle net with fixed perimeter and explicit quad faces, in metres.","0 Geometry") { }
        public override Guid ComponentGuid => new Guid("9b17b7aa-e7de-43f7-861c-34b620d156cc");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddIntegerParameter("Divisions","N","Divisions per side, 2 to 100",GH_ParamAccess.item,4);
            p.AddNumberParameter("Span","L","Square span in metres",GH_ParamAccess.item,10);
            p.AddNumberParameter("Rise","H","Corner elevation amplitude in metres",GH_ParamAccess.item,2);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p)
        {
            p.AddLineParameter("Lines","L","Connect to Cable",GH_ParamAccess.list);
            p.AddPointParameter("Supports","P","Connect to Node Conditions",GH_ParamAccess.list);
            p.AddMeshParameter("Faces","M","Connect to Model Faces",GH_ParamAccess.item);
        }
        protected override void Solve(IGH_DataAccess data)
        {
            int n=4;double span=10,rise=2;data.GetData(0,ref n);data.GetData(1,ref span);data.GetData(2,ref rise);
            if(n<2||n>100)throw new ArgumentException("Divisions must be between 2 and 100.");
            Arachne.Core.Validation.Positive(span,"Span");Arachne.Core.Validation.Finite(rise,"Rise");
            var points=new Point3d[n+1,n+1];var lines=new List<Line>();var supports=new List<Point3d>();var mesh=new Mesh();
            for(int y=0;y<=n;y++)for(int x=0;x<=n;x++)
            {
                bool border=x==0||x==n||y==0||y==n;
                var point=new Point3d(span*x/n,span*y/n,border?rise*(2.0*x/n-1)*(2.0*y/n-1):0);
                points[x,y]=point;mesh.Vertices.Add(point);if(border)supports.Add(point);
                if(x>0)lines.Add(new Line(points[x-1,y],point));if(y>0)lines.Add(new Line(points[x,y-1],point));
                if(x>0&&y>0){int d=y*(n+1)+x;mesh.Faces.AddFace(d-(n+1)-1,d-(n+1),d,d-1);}
            }
            mesh.Normals.ComputeNormals();data.SetDataList(0,lines);data.SetDataList(1,supports);data.SetData(2,mesh);
        }
    }
}
