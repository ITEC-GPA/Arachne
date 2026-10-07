using System;
using Rhino.Display;
using Rhino.Geometry;

namespace Arachne.Common.Helpers
{
    public static class DrawingHelper
    {
        public static readonly double ArrowSize = 0.3;

        public static double GetScaleFactor(RhinoViewport viewport, Point3d point)
        {
            double scale; // pixel per unit length in WorldXY
            viewport.GetWorldToScreenScale(point, out scale);
            return 20 / scale;
        }
    }
}
