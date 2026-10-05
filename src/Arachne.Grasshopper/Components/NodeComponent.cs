using System;
using Arachne.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public sealed class NodeComponent : ComponentBase
    {
        public NodeComponent() : base("Node Conditions", "Node", "Supports, nodal forces and nonlinear position weights. Repeated forces are summed.", "1 Model") { }
        public override Guid ComponentGuid => new Guid("965feffb-5220-4171-a067-45621431b569");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddPointParameter("Point","P","Cable endpoint in metres",GH_ParamAccess.item);
            p.AddBooleanParameter("Fix X","X","Restrain X",GH_ParamAccess.item,true);
            p.AddBooleanParameter("Fix Y","Y","Restrain Y",GH_ParamAccess.item,true);
            p.AddBooleanParameter("Fix Z","Z","Restrain Z",GH_ParamAccess.item,true);
            p.AddVectorParameter("Force","F","Nodal force in N. Disable restraints to load a free node.",GH_ParamAccess.item,Vector3d.Zero);
            p.AddVectorParameter("Position weight","W","Nonlinear XYZ weights, 0 disables",GH_ParamAccess.item,Vector3d.Zero);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) => p.AddGenericParameter("Condition","N","Arachne node condition",GH_ParamAccess.item);
        protected override void Solve(IGH_DataAccess data)
        {
            Point3d point=Point3d.Unset; if(!data.GetData(0,ref point))return;
            bool x=true,y=true,z=true; var force=Vector3d.Zero; var weight=Vector3d.Zero;
            data.GetData(1,ref x);data.GetData(2,ref y);data.GetData(3,ref z);data.GetData(4,ref force);data.GetData(5,ref weight);
            data.SetData(0,new GH_ObjectWrapper(new NodeCondition{Position=ToCore(point),FixX=x,FixY=y,FixZ=z,Force=ToCore(force),PositionWeight=ToCore(weight)}));
        }
    }
}
