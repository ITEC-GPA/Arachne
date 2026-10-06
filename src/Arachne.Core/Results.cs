using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;

namespace Arachne.Core
{
    public sealed class SolverIteration
    {
        public int Outer { get; }
        public int Inner { get; }
        public double Criterion { get; }
        public string Metric { get; }
        public double ElapsedMilliseconds { get; }
        public SolverIteration(int outer,int inner,double criterion,string metric,double elapsed)
        { Outer=outer;Inner=inner;Criterion=criterion;Metric=metric;ElapsedMilliseconds=elapsed; }
    }

    public sealed class NodeResult
    {
        private readonly Point3d initial,position;
        private readonly Vector3d applied,weight,beamLoad,pressure,internalForce,reaction,residual;
        public int Id { get; }
        public bool FixX { get; }
        public bool FixY { get; }
        public bool FixZ { get; }
        public Point3d InitialPosition => new Point3d(initial);
        public Point3d Position => new Point3d(position);
        public Vector3d Displacement => ResultMath.Difference(position,initial);
        public Vector3d AppliedForce => new Vector3d(applied);
        public Vector3d SelfWeightForce => new Vector3d(weight);
        public Vector3d CableLoadForce => new Vector3d(beamLoad);
        public Vector3d PressureForce => new Vector3d(pressure);
        public Vector3d ExternalForce => ResultMath.Sum(applied,weight,beamLoad,pressure);
        public Vector3d InternalForce => new Vector3d(internalForce);
        public Vector3d Reaction => new Vector3d(reaction);
        public Vector3d Residual => new Vector3d(residual);
        internal NodeResult(FormNode node,Point3d solved,Vector3d selfWeight,Vector3d cableLoad,Vector3d pressureForce,Vector3d internalLoad)
        {
            Id=node.Id;FixX=node.FixX;FixY=node.FixY;FixZ=node.FixZ;initial=node.Position;position=new Point3d(solved);
            applied=node.Force;weight=new Vector3d(selfWeight);beamLoad=new Vector3d(cableLoad);pressure=new Vector3d(pressureForce);internalForce=new Vector3d(internalLoad);
            var imbalance=ResultMath.Sum(ExternalForce,internalForce);
            reaction=new Vector3d(FixX?-imbalance.X:0,FixY?-imbalance.Y:0,FixZ?-imbalance.Z:0);
            residual=ResultMath.Sum(imbalance,reaction);
        }
        public override string ToString() => $"Node {Id}: residual {ResultMath.Norm(residual):G5} N";
    }

    public sealed class BeamResult
    {
        public int Id { get; }
        public int StartNodeId { get; }
        public int EndNodeId { get; }
        public double InitialLength { get; }
        public double Length { get; }
        public double AxialForce { get; }
        public double ForceDensity { get; }
        public TargetKind TargetKind { get; }
        public double Target { get; }
        public MembraneDirection Direction { get; }
        internal BeamResult(FormModel model,FormEdge edge,IReadOnlyList<Point3d> points,double force,double q)
        { Id=edge.Id;StartNodeId=model.Nodes[edge.Start].Id;EndNodeId=model.Nodes[edge.End].Id;
          InitialLength=Validation.Distance(model.Nodes[edge.Start].Position,model.Nodes[edge.End].Position);
          Length=Validation.Distance(points[edge.Start],points[edge.End]);AxialForce=force;ForceDensity=q;
          var cable=edge.Cable;TargetKind=cable.TargetKind;Target=cable.Target;Direction=cable.Direction; }
        public override string ToString() => $"Cable {Id}: N={AxialForce:G5} N, q={ForceDensity:G5} N/m";
    }

    public sealed class PlateResult
    {
        public int Id { get; }
        public IReadOnlyList<int> NodeIds { get; }
        // One entry per ordered side; 0 and NaN identify a side without a cable.
        public IReadOnlyList<int> BoundaryBeamIds { get; }
        public IReadOnlyList<double> BoundaryAxialForces { get; }
        public double InitialArea { get; }
        public double Area { get; }
        public bool HasMembraneResultants => false;
        public string MembraneResultantsStatus => "Unavailable: TensileLib solves a cable network; no plate Nx/Ny/Nxy tensor is returned.";
        internal PlateResult(int id,int[] face,FormModel model,IReadOnlyList<Point3d> points,IReadOnlyList<BeamResult> beams,Dictionary<(int,int),int> edgeMap)
        {
            Id=id;NodeIds=Array.AsReadOnly(face.Select(i=>model.Nodes[i].Id).ToArray());
            var ids=new int[face.Length];var forces=new double[face.Length];
            for(int j=0;j<face.Length;j++)
            {
                int a=face[j],b=face[(j+1)%face.Length];
                if(edgeMap.TryGetValue((Math.Min(a,b),Math.Max(a,b)),out int k)){ids[j]=beams[k].Id;forces[j]=beams[k].AxialForce;}
                else forces[j]=double.NaN;
            }
            BoundaryBeamIds=Array.AsReadOnly(ids);BoundaryAxialForces=Array.AsReadOnly(forces);
            InitialArea=ResultMath.Area(face.Select(i=>model.Nodes[i].Position).ToArray());Area=ResultMath.Area(face.Select(i=>points[i]).ToArray());
        }
        public override string ToString() => $"Plate {Id}: area {Area:G5} m2; boundary cable forces only";
    }

    public sealed class SolvedModel
    {
        public Guid ModelId { get; }
        public IReadOnlyList<NodeResult> Nodes { get; }
        public IReadOnlyList<BeamResult> Beams { get; }
        public IReadOnlyList<PlateResult> Plates { get; }
        internal SolvedModel(FormModel source,IReadOnlyList<Point3d> positions,IReadOnlyList<double> forces,IReadOnlyList<double> densities,
            IReadOnlyList<Vector3d> weights,IReadOnlyList<Vector3d> loads,IReadOnlyList<Vector3d> pressures,IReadOnlyList<int[]> faces)
        {
            ModelId=source.Id;
            var internalForces=source.Nodes.Select(_=>new Vector3d(0,0,0)).ToArray();
            var beams=new List<BeamResult>();var edgeMap=new Dictionary<(int,int),int>();
            for(int i=0;i<source.Edges.Count;i++)
            {
                var edge=source.Edges[i];var f=ResultMath.Scale(ResultMath.Difference(positions[edge.End],positions[edge.Start]),densities[i]);
                internalForces[edge.Start]=ResultMath.Sum(internalForces[edge.Start],f);
                internalForces[edge.End]=ResultMath.Sum(internalForces[edge.End],ResultMath.Scale(f,-1));
                beams.Add(new BeamResult(source,edge,positions,forces[i],densities[i]));edgeMap.Add((Math.Min(edge.Start,edge.End),Math.Max(edge.Start,edge.End)),i);
            }
            Beams=beams.AsReadOnly();
            Nodes=Array.AsReadOnly(source.Nodes.Select((n,i)=>new NodeResult(n,positions[i],weights[i],loads[i],pressures[i],internalForces[i])).ToArray());
            Plates=Array.AsReadOnly(faces.Select((f,i)=>new PlateResult(i+1,f,source,positions,Beams,edgeMap)).ToArray());
        }
        public NodeResult Node(int id) => id>0 && id<=Nodes.Count?Nodes[id-1]:throw new ArgumentException("Unknown node ID: "+id);
        public BeamResult Beam(int id) => id>0 && id<=Beams.Count?Beams[id-1]:throw new ArgumentException("Unknown cable ID: "+id);
        public PlateResult Plate(int id) => id>0 && id<=Plates.Count?Plates[id-1]:throw new ArgumentException("Unknown plate ID: "+id);
        public override string ToString() => $"Solved Arachne model: {Nodes.Count} nodes, {Beams.Count} cables, {Plates.Count} plates";
    }

    public sealed class EquilibriumReport
    {
        public Vector3d TotalExternal { get; private set; }
        public Vector3d TotalReaction { get; private set; }
        public Vector3d ForceImbalance { get; private set; }
        public Vector3d MomentImbalance { get; private set; }
        public double MaxFreeResidual { get; private set; }
        public bool Accepted { get; private set; }
        public static EquilibriumReport Evaluate(SolvedModel model,double forceTolerance=1e-4,double momentTolerance=1e-4,Point3d origin=null)
        {
            if(model==null)throw new ArgumentNullException(nameof(model));Validation.Positive(forceTolerance,"Force tolerance");Validation.Positive(momentTolerance,"Moment tolerance");
            origin=origin??Point3d.Origin;Validation.Point(origin);
            var report=new EquilibriumReport { TotalExternal=ResultMath.Sum(model.Nodes.Select(n=>n.ExternalForce).ToArray()),TotalReaction=ResultMath.Sum(model.Nodes.Select(n=>n.Reaction).ToArray()) };
            report.ForceImbalance=ResultMath.Sum(report.TotalExternal,report.TotalReaction);
            report.MomentImbalance=ResultMath.Sum(model.Nodes.Select(n=>ResultMath.Cross(ResultMath.Difference(n.Position,origin),ResultMath.Sum(n.ExternalForce,n.Reaction))).ToArray());
            report.MaxFreeResidual=model.Nodes.Max(n=>ResultMath.Norm(n.Residual));
            report.Accepted=report.MaxFreeResidual<=forceTolerance && ResultMath.Norm(report.ForceImbalance)<=forceTolerance && ResultMath.Norm(report.MomentImbalance)<=momentTolerance;
            return report;
        }
    }

    public static class ResultMath
    {
        public static Vector3d Difference(Point3d a,Point3d b)=>new Vector3d(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static Vector3d Sum(params Vector3d[] values)=>new Vector3d(values.Sum(v=>v.X),values.Sum(v=>v.Y),values.Sum(v=>v.Z));
        public static Vector3d Scale(Vector3d v,double s)=>new Vector3d(v.X*s,v.Y*s,v.Z*s);
        public static double Norm(Vector3d v)=>Math.Sqrt(v.X*v.X+v.Y*v.Y+v.Z*v.Z);
        public static Vector3d Cross(Vector3d a,Vector3d b)=>new Vector3d(a.Y*b.Z-a.Z*b.Y,a.Z*b.X-a.X*b.Z,a.X*b.Y-a.Y*b.X);
        public static double Area(IReadOnlyList<Point3d> p)
        { double area=0;for(int i=1;i<p.Count-1;i++)area+=Norm(Cross(Difference(p[i],p[0]),Difference(p[i+1],p[0])))*0.5;return area; }
    }
}
