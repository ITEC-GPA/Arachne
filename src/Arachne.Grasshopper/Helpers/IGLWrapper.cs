using System.Runtime.InteropServices;

namespace Arachne.Grasshopper.Helpers
{
    // C ABI from Repository/maffeisigl/MaffeisIGL/mm_arap.h. Native x64, not a managed geometry assembly.
    internal static class IGLWrapper
    {
        private const string Library = "Arachne.IGL.dll";
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Version();
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void SetConfig(int arapIter, int perimeterIter, double perimeterTol, double angleTol, double stretchSpeed);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        internal static extern bool MeshFlattening([In] double[] vertices, [In] int[] faces, int nVer, int nTri, [Out] double[] result);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void MeshUnwrap([In] double[] vertices, [In] int[] faces, int nVer, int nTri, int nPins, [In] int[] pins, [In] double[] positions, [Out] double[] result);
        [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void MeshDeform([In] double[] vertices, [In] int[] faces, int nVer, int nTri, int nPins, [In] int[] pins, [In] double[] positions, [Out] double[] result);
    }
}
