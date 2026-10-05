using System;
using System.Collections.Generic;
using System.Drawing;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components
{
    public abstract class ComponentBase : GH_Component
    {
        protected ComponentBase(string name, string nick, string description, string category)
            : base(name, nick, description, "Arachne", category) { }
        protected override Bitmap Icon => null;
        protected sealed override void SolveInstance(IGH_DataAccess data)
        {
            Message = "";
            try { Solve(data); }
            catch (Exception error) { Message = "Error"; AddRuntimeMessage(GH_RuntimeMessageLevel.Error, error.Message); }
        }
        protected abstract void Solve(IGH_DataAccess data);
        protected static T Read<T>(IGH_DataAccess data, int index) where T : class
        {
            GH_ObjectWrapper wrapper = null;
            if (!data.GetData(index, ref wrapper)) return null;
            return wrapper?.Value as T ?? throw new ArgumentException("Expected " + typeof(T).Name + ".");
        }
        protected static List<T> ReadList<T>(IGH_DataAccess data, int index) where T : class
        {
            var wrappers = new List<GH_ObjectWrapper>(); data.GetDataList(index, wrappers);
            var values = new List<T>();
            foreach (var wrapper in wrappers) values.Add(wrapper?.Value as T ?? throw new ArgumentException("Expected " + typeof(T).Name + "."));
            return values;
        }
        protected static GPC.Geometry.Point3d ToCore(Point3d p) => new GPC.Geometry.Point3d(p.X, p.Y, p.Z);
        protected static GPC.Geometry.Vector3d ToCore(Vector3d v) => new GPC.Geometry.Vector3d(v.X, v.Y, v.Z);
        protected static Point3d ToRhino(GPC.Geometry.Point3d p) => new Point3d(p.X, p.Y, p.Z);
    }
}
