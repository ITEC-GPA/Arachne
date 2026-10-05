using System;
using Arachne.Core;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public sealed class CableComponent : ComponentBase
    {
        public CableComponent() : base("Cable", "Cable", "Define a cable. Geometry in metres, forces in newtons.", "1 Model") { }
        public override Guid ComponentGuid => new Guid("2ef7dc6e-a449-4266-a4ed-f8c971f6a07c");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddLineParameter("Line", "L", "Cable axis in metres", GH_ParamAccess.item);
            p.AddNumberParameter("Target", "Q", "Force density N/m, cable force N, membrane N/m, or curvature denominator", GH_ParamAccess.item, 1);
            int i=p.AddIntegerParameter("Kind", "K", "Target type", GH_ParamAccess.item, 0);
            foreach (TargetKind kind in Enum.GetValues(typeof(TargetKind))) ((Param_Integer)p[i]).AddNamedValue(kind.ToString(), (int)kind);
            p.AddBooleanParameter("Project", "P", "Use length projected on the reference plane", GH_ParamAccess.item, false);
            p.AddNumberParameter("Target weight", "W", "Weight in the nonlinear solver; 0 disables", GH_ParamAccess.item, 0);
            i=p.AddIntegerParameter("Direction", "D", "Membrane direction", GH_ParamAccess.item, -1);
            foreach (MembraneDirection kind in Enum.GetValues(typeof(MembraneDirection))) ((Param_Integer)p[i]).AddNamedValue(kind.ToString(), (int)kind);
            p.AddNumberParameter("Chord", "C", "Chord in metres for curvature target", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Area", "A", "Cross-section area in m2 for self-weight", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Density", "Rho", "Mass density in kg/m3", GH_ParamAccess.item, 0);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) => p.AddGenericParameter("Cable", "C", "Arachne cable", GH_ParamAccess.item);
        protected override void Solve(IGH_DataAccess data)
        {
            Line line=Line.Unset; if (!data.GetData(0, ref line)) return;
            double target=1, weight=0, chord=0, area=0, density=0; int kind=0, direction=-1; bool project=false;
            data.GetData(1, ref target); data.GetData(2, ref kind); data.GetData(3, ref project); data.GetData(4, ref weight);
            data.GetData(5, ref direction); data.GetData(6, ref chord); data.GetData(7, ref area); data.GetData(8, ref density);
            var cable=new Cable { Start=ToCore(line.From), End=ToCore(line.To), Target=target, TargetKind=(TargetKind)kind,
                Project=project, TargetWeight=weight, Direction=(MembraneDirection)direction, Chord=chord, Area=area, Density=density };
            ModelBuilder.Build(new[] {cable});
            data.SetData(0, new GH_ObjectWrapper(cable));
        }
    }

    public sealed class CableConstraintComponent : ComponentBase
    {
        public CableConstraintComponent() : base("Cable Constraints", "Constraints", "Assign length, force or percentage shortening; returns a copy.", "1 Model") { }
        public override Guid ComponentGuid => new Guid("4084b890-de35-40e8-8d6f-3fc1b6fab7bb");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("Cable", "C", "Cable", GH_ParamAccess.item);
            p.AddNumberParameter("Length", "L", "Fixed length in m; 0 leaves unchanged", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Force", "F", "Fixed axial force in N; 0 leaves unchanged", GH_ParamAccess.item, 0);
            p.AddNumberParameter("Weight", "W", "Positive constraint weight", GH_ParamAccess.item, 1);
            p.AddNumberParameter("Compensation", "C%", "Shortening in percent; alternative to Length; 0 disables", GH_ParamAccess.item, 0);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) => p.AddGenericParameter("Cable", "C", "Modified cable", GH_ParamAccess.item);
        protected override void Solve(IGH_DataAccess data)
        {
            var source=Read<Cable>(data,0); if(source==null) return;
            double length=0, force=0, weight=1, compensation=0;
            data.GetData(1,ref length); data.GetData(2,ref force); data.GetData(3,ref weight); data.GetData(4,ref compensation);
            Validation.NonNegative(length,"Length"); Validation.NonNegative(force,"Force"); Validation.Positive(weight,"Weight"); Validation.Finite(compensation,"Compensation");
            if(length>0 && compensation!=0) throw new ArgumentException("Choose Length or Compensation.");
            if(compensation>=100) throw new ArgumentException("Compensation must be less than 100%.");
            var cable=source.Copy(); cable.ConstraintWeight=weight;
            if(length>0) cable.FixedLength=length;
            if(compensation!=0) cable.FixedLength=Validation.Distance(cable.Start,cable.End)*(1-compensation/100);
            if(force>0) cable.FixedForce=force;
            data.SetData(0,new GH_ObjectWrapper(cable));
        }
    }

    public sealed class CableLoadComponent : ComponentBase
    {
        public CableLoadComponent() : base("Cable Load", "Load C", "Add a concentrated load on a cable. Use its resultant for a distributed load.", "1 Model") { }
        public override Guid ComponentGuid => new Guid("ec9c8020-7c47-4ea6-84f4-87051559488c");
        protected override void RegisterInputParams(GH_InputParamManager p)
        {
            p.AddGenericParameter("Cable","C","Cable",GH_ParamAccess.item);
            p.AddVectorParameter("Force","F","Force vector in N",GH_ParamAccess.item);
            p.AddNumberParameter("Position","T","Fraction along the cable, between 0 and 1",GH_ParamAccess.item,0.5);
        }
        protected override void RegisterOutputParams(GH_OutputParamManager p) => p.AddGenericParameter("Cable","C","Loaded cable",GH_ParamAccess.item);
        protected override void Solve(IGH_DataAccess data)
        {
            var source=Read<Cable>(data,0); if(source==null)return;
            var force=Vector3d.Zero; double fraction=0.5; if(!data.GetData(1,ref force))return; data.GetData(2,ref fraction);
            var cable=source.Copy(); cable.Loads.Add(new CableLoad(fraction,ToCore(force)));
            ModelBuilder.Build(new[]{cable}); data.SetData(0,new GH_ObjectWrapper(cable));
        }
    }
}
