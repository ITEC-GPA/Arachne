using System;
using System.Drawing;
using System.Linq;

namespace Arachne.Grasshopper.Helpers
{
    internal static class ComponentIcons
    {
        // Pre-rendered original vector artwork; no shared bitmap lifetime at runtime.
        public static Bitmap Get(string name)
        {
            string suffix=".Icons."+name+".png";
            string resource=typeof(ComponentIcons).Assembly.GetManifestResourceNames().SingleOrDefault(n=>n.EndsWith(suffix,StringComparison.OrdinalIgnoreCase));
            if(resource==null)throw new InvalidOperationException("Missing component icon: "+name);
            using(var stream=typeof(ComponentIcons).Assembly.GetManifestResourceStream(resource))
            {
                if(stream==null)throw new InvalidOperationException("Missing component icon: "+name);
                using(var original=new Bitmap(stream))return new Bitmap(original);
            }
        }
    }
}
