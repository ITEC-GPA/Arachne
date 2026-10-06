using System;
using System.Collections.Generic;
using System.Linq;
using Arachne.Grasshopper.Geometry;
using Arachne.Grasshopper.Components.Networks;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

internal static class NetworkChecks
{
    private static void Require(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Run()
    {
        var empty=new List<Curve>();
        var border=new PolylineCurve(new[]{new Point3d(0,0,.8),new Point3d(8,0,-.8),new Point3d(8,6,.8),new Point3d(0,6,-.8),new Point3d(0,0,.8)}).DuplicateSegments().ToList();
        foreach(double snap in new[]{0,1e-5})
        {
            CableNetHelper.CreateCableNet2(border,empty,border,2,2,Plane.WorldXY,out var warp,out var weft,out _,out var fixedEdges,out var fixedPoints,out _,out var interior,1e-6,snap);
            Require(warp.Sum(b=>b.Count)==9&&weft.Sum(b=>b.Count)==8,"Rectangular net lost boundary links.");
            Require(fixedEdges.Sum(b=>b.Count)==14&&interior.Count==6,"Rectangular net has incorrect perimeter subdivisions.");
            foreach(var p in new[]{new Point3d(2,0,.4),new Point3d(2,6,-.4),new Point3d(0,2,.8/3),new Point3d(8,2,-.8/3)})
                Require(warp.Concat(weft).SelectMany(b=>b).Any(c=>c.PointAtStart.DistanceTo(p)<1e-6||c.PointAtEnd.DistanceTo(p)<1e-6),"Missing support connection: "+p);
            Console.WriteLine("NETWORK PASS Rectangular full perimeter, matching="+snap);
        }
        var radialBorder=new PolylineCurve(Enumerable.Range(0,17).Select(i=>new Point3d(4*Math.Cos(2*Math.PI*i/16),4*Math.Sin(2*Math.PI*i/16),0))).DuplicateSegments().ToList();
        foreach(double snap in new[]{0,1e-5})
        {
            CableNetHelper.CreateRadialCableNet2(radialBorder,empty,radialBorder,16,1,Plane.WorldXY,out var warp,out var weft,out _,out var fixedEdges,out _,out _,out var interior,1e-6,snap);
            Require(warp.Sum(b=>b.Count)==64,"Radial net must contain 16 complete spokes.");
            Require(weft.Sum(b=>b.Count)==48,"Radial net must contain three complete rings without crossing boundary chords; count="+weft.Sum(b=>b.Count));
            Require(fixedEdges.Sum(b=>b.Count)==16,"Radial perimeter must retain 16 sides.");
            Require(interior.Count==49,"Internal points contain duplicates (central load would be multiplied).");
            Console.WriteLine("NETWORK PASS Radial complete rings and unique center, matching="+snap);
        }
        foreach(double snap in new[]{0,1e-5})
        {
            var c=new CompoundNetComponent();
            Set(c,0,border.Select(b=>new GH_Curve(b)).ToArray());Set(c,2,border.Select(b=>new GH_Curve(b)).ToArray());
            Set(c,3,new GH_Number(2));Set(c,4,new GH_Number(2));Set(c,5,new GH_Number(0));Set(c,6,new GH_Point(Point3d.Origin));Set(c,8,new GH_Number(1e-6));Set(c,9,new GH_Number(snap));
            using(var document=new GH_Document{Enabled=true})
            {
                c.CreateAttributes();document.AddObject(c,false);document.NewSolution(false);
                Require(c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count==0,string.Join("; ",c.RuntimeMessages(GH_RuntimeMessageLevel.Error)));
                Require(c.Params.Output[0].VolatileDataCount==14,"Compound net splits edges at unrelated points when matching="+snap+"; count="+c.Params.Output[0].VolatileDataCount);
            }
            Console.WriteLine("NETWORK PASS Compound perimeter, matching="+snap);
        }
        Console.WriteLine("Network checks: 6 passed, 0 failed.");
    }
    private static void Set<T>(GH_Component c,int input,params T[] values) where T:class,IGH_Goo
    {var p=(GH_PersistentParam<T>)c.Params.Input[input];p.PersistentData.Clear();p.SetPersistentData((IEnumerable<T>)values);}
}
