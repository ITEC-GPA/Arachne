using System;
using System.Linq;
using System.Runtime.InteropServices;
using Arachne.Arap;
using GPC.Geometry;

internal static class NativeComparison
{
    [DllImport("Arachne.IGL.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void SetConfig(int iterations,int perimeter,double tolerance,double angle,double step);
    [DllImport("Arachne.IGL.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void MeshUnwrap(double[] v,int[] f,int nv,int nf,int np,int[] pins,double[] positions,double[] output);
    [DllImport("Arachne.IGL.dll",CallingConvention=CallingConvention.Cdecl)] private static extern void MeshDeform(double[] v,int[] f,int nv,int nf,int np,int[] pins,double[] positions,double[] output);
    internal static void Run(Action<string,Action> check,Action<bool,string> require)
    {
        check("native comparison: planar constrained unwrap",()=>Compare(Program.Grid(5,4),false,0,2e-5,require));
        check("native comparison: nonrigid 3D panel",()=>Compare(Program.Grid(5,4),true,0.4,2e-4,require));
        check("native comparison: translated constrained unwrap",()=>Compare(Program.Grid(5,4,(x,y)=>new Point3d(x-20,y-30,-5)),false,0,2e-5,require));
    }
    private static void Compare(ArapMesh mesh,bool deform,double offset,double tolerance,Action<bool,string> require)
    {
        var original=mesh.GetVertices();var faces=mesh.GetTriangleIndices();var xyz=original.SelectMany(p=>new[]{p.X,p.Y,p.Z}).ToArray();
        int[] ids={0,4,original.Length-1};
        var pins=ids.Select(i=>new ArapPin(i,new Point3d(original[i].X,original[i].Y,deform?original[i].Z+(i==original.Length-1?offset:0):0))).ToArray();
        var result=Program.Success(deform?ArapSolver.Deform(mesh,pins,new ArapOptions{MaxIterations=5000,PositionTolerance=1e-9}):ArapSolver.Unwrap(mesh,pins,new ArapOptions{MaxIterations=5000,PositionTolerance=1e-9}));
        int dimension=deform?3:2;double scale=(Math.Max(xyz.Max(),1.17549435e-38)-xyz.Min())/100;
        var positions=pins.SelectMany(p=>deform?new[]{p.Position.X/scale,p.Position.Y/scale,p.Position.Z/scale}:new[]{p.Position.X/scale,p.Position.Y/scale}).ToArray();
        var output=new double[dimension*original.Length];SetConfig(5000,1000,0.5,7*Math.PI/8,0.1);
        if(deform)MeshDeform(xyz,faces,original.Length,faces.Length/3,ids.Length,ids,positions,output);else MeshUnwrap(xyz,faces,original.Length,faces.Length/3,ids.Length,ids,positions,output);
        var managed=result.GetVertices();double error=0;
        for(int i=0;i<original.Length;i++){var native=new Point3d(output[dimension*i],output[dimension*i+1],deform?output[dimension*i+2]:0);error=Math.Max(error,Program.Distance(managed[i],native));}
        Console.WriteLine("COMPARE | max vertex distance="+error.ToString("G17")+" | tolerance="+tolerance+" | managed iterations="+result.History.Count);
        require(error<=tolerance,"Native/managed difference "+error+" > "+tolerance);
    }
}
