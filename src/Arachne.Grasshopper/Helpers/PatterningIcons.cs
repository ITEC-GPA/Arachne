using System.Drawing;
using System.Reflection;

namespace Arachne.Grasshopper.Helpers
{
    internal static class PatterningIcons
    {
        public static Bitmap Get(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Arachne.Grasshopper.Resources.Patterning." + name + ".png"))
            {
                if (stream == null) return null;
                using (var source = new Bitmap(stream)) return new Bitmap(source);
            }
        }
    }
}
