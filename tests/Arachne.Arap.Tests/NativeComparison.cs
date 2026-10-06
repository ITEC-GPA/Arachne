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
    [DllImport("Arachne.IGL.dll",CallingConvention=CallingConvention.Cdecl)] [return:MarshalAs(UnmanagedType.I1)] private static extern bool MeshFlattening(double[] v,int[] f,int nv,int nf,double[] output);
    internal static void Run(Action<string,Action> check,Action<bool,string> require)
    {
        check("native comparison: planar constrained unwrap",()=>Compare(Program.Grid(5,4),false,0,2e-5,require));
        check("native comparison: nonrigid 3D panel",()=>Compare(Program.Grid(5,4),true,0.4,2e-4,require));
        check("native comparison: translated constrained unwrap",()=>Compare(Program.Grid(5,4,(x,y)=>new Point3d(x-20,y-30,-5)),false,0,2e-5,require));
        check("native comparison: curved constrained unwrap",()=>Compare(Program.Cylinder(),false,0,2e-4,require));
        check("native comparison: planar flatten",()=>CompareFlatten(Program.Grid(5,4),false,require));
        check("native comparison: curved flatten boundary lengths",()=>CompareFlatten(Program.Fan(8,0.6),true,require));
    }
    private static void CompareFlatten(ArapMesh mesh,bool curved,Action<bool,string> require)
    {
        const double tolerance=1e-5;var original=mesh.GetVertices();var f=mesh.GetTriangleIndices();var output=new double[original.Length*2];
        var managed=Program.Success(ArapSolver.Flatten(mesh,new ArapOptions{MaxIterations=3000,PerimeterSolveIterations=1000,PerimeterTolerance=tolerance}));
        SetConfig(3000,1000,tolerance,7*Math.PI/8,0.1);
        require(MeshFlattening(original.SelectMany(p=>new[]{p.X,p.Y,p.Z}).ToArray(),f,original.Length,f.Length/3,output),"Native flatten did not converge");
        var native=Enumerable.Range(0,original.Length).Select(i=>new Point3d(output[2*i],output[2*i+1],0)).ToArray();double error=0;
        if(curved)
        {
            for(int i=1;i<original.Length;i++){int j=i==original.Length-1?1:i+1;error=Math.Max(error,Math.Abs(Program.Distance(native[i],native[j])-Program.Distance(original[i],original[j])));}
            Console.WriteLine("COMPARE | curved flatten max boundary error: native="+error.ToString("G17")+", managed="+managed.MaxPerimeterError.ToString("G17")+" | tolerance="+tolerance);
            require(error<=tolerance && managed.MaxPerimeterError<=tolerance,"Boundary lengths out of tolerance");
        }
        else
        {
            var v=managed.GetVertices();for(int i=0;i<v.Length;i++)error=Math.Max(error,Program.Distance(native[i],v[i]));
            Console.WriteLine("COMPARE | planar flatten max vertex distance="+error.ToString("G17"));
            require(error<2e-5,"Planar flatten mismatch");
        }
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
