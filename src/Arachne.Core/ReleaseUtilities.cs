using System;
using System.Collections.Generic;
using System.Linq;
using GPC.Geometry;
using GPC.Utilities.Units;

namespace Arachne.Core
{
    public enum Quantity { Length, Area, Force, ForcePerLength, Pressure, MassDensity, Mass }
    public sealed class UnitSettings
    {
        public UnitsConvert.LengthUnits Length { get; }
        public UnitsConvert.ForceUnits Force { get; }
        public UnitsConvert.MassUnits Mass { get; }
        public UnitSettings(UnitsConvert.LengthUnits length=UnitsConvert.LengthUnits.m,UnitsConvert.ForceUnits force=UnitsConvert.ForceUnits.N,UnitsConvert.MassUnits mass=UnitsConvert.MassUnits.kg)
        { if(!Enum.IsDefined(typeof(UnitsConvert.LengthUnits),length)||!Enum.IsDefined(typeof(UnitsConvert.ForceUnits),force)||!Enum.IsDefined(typeof(UnitsConvert.MassUnits),mass))throw new ArgumentException("Unknown units.");Length=length;Force=force;Mass=mass; }
        public double Factor(Quantity quantity)
        {
            double l=UnitsConvert.Convert(1,Length,UnitsConvert.LengthUnits.m,1), f=UnitsConvert.Convert(1,Force,UnitsConvert.ForceUnits.N,1),m=UnitsConvert.Convert(1,Mass,UnitsConvert.MassUnits.kg,1);
            switch(quantity){case Quantity.Length:return l;case Quantity.Area:return l*l;case Quantity.Force:return f;case Quantity.ForcePerLength:return f/l;case Quantity.Pressure:return f/(l*l);case Quantity.MassDensity:return m/(l*l*l);case Quantity.Mass:return m;default:throw new ArgumentException("Unknown quantity.");}
        }
        public double Convert(double value,Quantity quantity,bool toSI=true){Validation.Finite(value,"Value");double converted=toSI?value*Factor(quantity):value/Factor(quantity);Validation.Finite(converted,"Converted value");return converted;}
        public override string ToString()=>$"{Length}, {Force}, {Mass} -> m, N, kg";
    }
    public sealed class PanelDistortion
    {
        public IReadOnlyList<double> EdgeChanges { get; private set; }
        public IReadOnlyList<double> AreaChanges { get; private set; }
        public IReadOnlyList<int[]> EdgeVertices { get; private set; }
        public double MaxEdgeChange { get; private set; }
        public double MaxAreaChange { get; private set; }
        public bool Accepted { get; private set; }
        // Compare corresponding vertices/faces; the caller must check both mesh topologies match.
        public static PanelDistortion Evaluate(IReadOnlyList<Point3d> original,IReadOnlyList<Point3d> developed,IReadOnlyList<int[]> faces,double edgeTolerance=0.02,double areaTolerance=0.04)
        {
            Validation.NonNegative(edgeTolerance,"Edge tolerance");Validation.NonNegative(areaTolerance,"Area tolerance");
            if(original==null||developed==null||faces==null||original.Count!=developed.Count||faces.Count==0)throw new ArgumentException("Matching non-empty mesh topology required.");
            foreach(var p in original.Concat(developed))Validation.Point(p);
            var edges=new SortedSet<(int,int)>();var areaChanges=new List<double>();
            foreach(var face in faces)
            {
                if(face==null || (face.Length!=3&&face.Length!=4) || face.Any(i=>i<0||i>=original.Count) ||face.Distinct().Count()!=face.Length)throw new ArgumentException("Invalid face.");
                var a=face.Select(i=>original[i]).ToArray();var b=face.Select(i=>developed[i]).ToArray();double area=ResultMath.Area(a),newArea=ResultMath.Area(b);
                Validation.Positive(area,"Original face area");Validation.Positive(newArea,"Developed face area");areaChanges.Add(newArea/area-1);
                for(int i=0;i<face.Length;i++){int u=face[i],v=face[(i+1)%face.Length];edges.Add((Math.Min(u,v),Math.Max(u,v)));}
            }
            var edgeChanges=edges.Select(e=>{double length=Validation.Distance(original[e.Item1],original[e.Item2]);Validation.Positive(length,"Original edge length");return Validation.Distance(developed[e.Item1],developed[e.Item2])/length-1;}).ToArray();
            var r=new PanelDistortion{EdgeVertices=edges.Select(e=>new[]{e.Item1,e.Item2}).ToArray(),EdgeChanges=Array.AsReadOnly(edgeChanges),AreaChanges=areaChanges.AsReadOnly(),MaxEdgeChange=edgeChanges.Max(Math.Abs),MaxAreaChange=areaChanges.Max(Math.Abs)};
            r.Accepted=r.MaxEdgeChange<=edgeTolerance&&r.MaxAreaChange<=areaTolerance;return r;
        }
    }
}
