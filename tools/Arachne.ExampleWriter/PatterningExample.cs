using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Arachne.Grasshopper.Components.Patterning;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

internal static class PatterningExample
{
    private static void Set<T>(GH_Component c, int index, params T[] values) where T : class, IGH_Goo
    {
        var p = (GH_PersistentParam<T>)c.Params.Input[index];
        p.PersistentData.Clear(); p.SetPersistentData((IEnumerable<T>)values);
    }

    internal static void Write(string root)
    {
        using (var document = new GH_Document { Enabled = true })
        {
            var mesh = new Mesh();
            mesh.Vertices.Add(1,1,1); mesh.Vertices.Add(3,1,1); mesh.Vertices.Add(3,3,2); mesh.Vertices.Add(1,3,2);
            mesh.Faces.AddFace(0,1,2,3); mesh.Normals.ComputeNormals();
            var source = new Param_Mesh { NickName = "Pannello 3D" };
            source.SetPersistentData(new GH_Mesh(mesh));
            var flat = new FlatteningComponent(); var arap = new FlatteningArapComponent();
            var compensation = new CompensationComponent(); var borders = new BordersComponent();
            var order = new OrderBordersComponent(); var descriptor = new DecompensationDescriptorComponent();
            var decompensation = new DecompensationComponent(); var lineup = new LineUpComponent();
            var note = new GH_Panel { UserText = "ARACHNE | Pannelli\nMesh inclinata -> sviluppo -> compensazione 2% / 1% -> bordi -> decompensazione -> disposizione.\nI quattro descrittori sono neutri (0). Scale dei descrittori in frazione: 0.02 = 2%.\nARAP e un percorso alternativo. Le unita sono quelle delle coordinate." };
            void Add(IGH_DocumentObject item, int x, int y)
            {
                item.CreateAttributes(); item.Attributes.Pivot = new PointF(x,y); document.AddObject(item,false);
            }
            Add(source,70,150); Add(flat,250,100); Add(arap,250,330); Add(compensation,480,120);
            Add(borders,720,120); Add(order,920,120); Add(descriptor,740,420);
            Add(decompensation,1130,140); Add(lineup,1360,140); Add(note,60,530);
            note.Attributes.Bounds = new RectangleF(40,510,580,160);
            flat.Params.Input[0].AddSource(source); arap.Params.Input[0].AddSource(source);
            compensation.Params.Input[0].AddSource(flat.Params.Output[0]);
            Set(compensation,1,new GH_Vector(Vector3d.XAxis)); Set(compensation,2,new GH_Number(2)); Set(compensation,3,new GH_Number(1));
            borders.Params.Input[0].AddSource(compensation.Params.Output[0]);
            order.Params.Input[0].AddSource(borders.Params.Output[0]);
            Set(descriptor,2,new GH_Number(0),new GH_Number(0),new GH_Number(0),new GH_Number(0));
            for(int i=3;i<6;i++) Set(descriptor,i,new GH_Number(0));
            decompensation.Params.Input[0].AddSource(order.Params.Output[0]);
            decompensation.Params.Input[1].AddSource(descriptor.Params.Output[0]);
            lineup.Params.Input[0].AddSource(decompensation.Params.Output[0]); Set(lineup,2,new GH_Number(0.2));
            document.NewSolution(false); Verify(document);
            foreach(string extension in new[]{".ghx",".gh"})
            {
                string path=Path.Combine(root,"examples","02_Patterning"+extension);
                var archive=new GH_Archive();
                if(!archive.AppendObject(document,"Definition") || !archive.WriteToFile(path,true,false)) throw new IOException("Cannot save "+path);
                var restored=new GH_Archive();
                using(var reopened=new GH_Document{Enabled=true})
                {
                    if(!restored.ReadFromFile(path) || !restored.ExtractObject(reopened,"Definition") || reopened.Objects.Count!=10)
                        throw new IOException("Cannot reopen complete Patterning example: "+path);
                    reopened.NewSolution(false); Verify(reopened);
                }
            }
            Console.WriteLine("Patterning example: 10 objects, 8 connections, both GH and GHX reopened and recalculated.");
        }
    }

    private static void Verify(GH_Document document)
    {
        foreach(var c in document.Objects.OfType<GH_Component>())
        {
            var messages=c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Concat(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning)).ToArray();
            if(messages.Length>0)throw new InvalidOperationException(c.Name+": "+string.Join("; ",messages));
            if(!c.Params.Output[0].VolatileData.AllData(true).Any()) throw new InvalidOperationException(c.Name+" produced no data.");
        }
        var compensated=document.Objects.OfType<CompensationComponent>().Single();
        var mesh=((GH_Mesh)compensated.Params.Output[0].VolatileData.AllData(true).Single()).Value;
        double expectedArea=2*Math.Sqrt(5)*0.98*0.99;
        if(Math.Abs(AreaMassProperties.Compute(mesh).Area-expectedArea)>0.002)throw new InvalidOperationException("Incorrect compensated example area.");
        var curves=document.Objects.OfType<DecompensationComponent>().Single().Params.Output[0].VolatileData.AllData(true).Cast<GH_Curve>().ToArray();
        if(curves.Length!=4 || curves.Any(c=>!c.Value.IsValid))throw new InvalidOperationException("Incorrect example borders.");
    }
}
