using System;
using System.Collections.Generic;
using System.Drawing;

namespace Arachne.Grasshopper.Helpers
{
    /// <summary>
    /// Load diagrams drawn on the load components (FeMM originals in Resources/Images).
    /// </summary>
    internal static class ComponentImages
    {
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();

        public static Bitmap Get(string name)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(name, out Bitmap image))
                {
                    string resource = "Arachne.Grasshopper.Resources.Images." + name + ".png";
                    using (var stream = typeof(ComponentImages).Assembly.GetManifestResourceStream(resource))
                    {
                        if (stream == null)
                            throw new InvalidOperationException("Missing component image: " + name);
                        using (var source = new Bitmap(stream))
                            image = new Bitmap(source);
                    }
                    Cache.Add(name, image);
                }
                return image;
            }
        }
    }
}
