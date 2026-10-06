using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;

// A headless Grasshopper document used by the checks and by the example writer.
internal sealed class Canvas : IDisposable
{
    public readonly GH_Document Document = new GH_Document { Enabled = true };

    public T Add<T>(T obj, float x = 0, float y = 0) where T : IGH_DocumentObject
    {
        obj.CreateAttributes();
        obj.Attributes.Pivot = new PointF(x, y);
        Document.AddObject(obj, false);
        return obj;
    }

    public static void Set<T>(GH_Component component, int input, params T[] values) where T : class, IGH_Goo
    {
        var parameter = (GH_PersistentParam<T>)component.Params.Input[input];
        parameter.PersistentData.Clear();
        parameter.SetPersistentData((IEnumerable<T>)values);
    }

    public static void Link(GH_Component target, int input, IGH_Param source) => target.Params.Input[input].AddSource(source);

    public static void Link(GH_Component target, int input, GH_Component source, int output = 0) => Link(target, input, source.Params.Output[output]);

    public void Solve() => Document.NewSolution(false);

    public static List<IGH_Goo> Out(GH_Component component, int output = 0) => component.Params.Output[output].VolatileData.AllData(true).ToList();

    public static T Single<T>(GH_Component component, int output = 0) where T : class, IGH_Goo
    {
        var data = Out(component, output);
        if (data.Count != 1 || !(data[0] is T value))
            throw new InvalidOperationException($"{component.Name}: expected one {typeof(T).Name} on output {output}, found {data.Count}. " + Messages(component));
        return value;
    }

    public static string Messages(GH_Component component)
        => string.Join("; ", component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Concat(component.RuntimeMessages(GH_RuntimeMessageLevel.Warning)));

    public static bool HasErrors(GH_Component component) => component.RuntimeMessages(GH_RuntimeMessageLevel.Error).Count > 0;

    public void Dispose() => Document.Dispose();
}
