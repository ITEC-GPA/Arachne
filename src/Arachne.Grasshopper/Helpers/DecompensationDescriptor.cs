using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arachne.Grasshopper.Helpers
{
    public class DecompensationDescriptor
    {

        public class DecompensationPoint
        {
            public bool IsNormalized = true;
            public double Length = 0;
            public double Warp = 0;
            public double Weft = 0;

            public DecompensationPoint(bool norm, double len, double warp, double weft)
            {
                IsNormalized = norm;
                Length = len;
                Warp = warp;
                Weft = weft;
            }
        }

        public List<DecompensationPoint> Points = new List<DecompensationPoint>();
    }
}
