using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Arachne.Core;
using Arachne.Grasshopper.Components;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;

internal static class ExampleAssets
{
    internal static void Icons(string root)
    {
        string folder=Path.Combine(root,"docs","icons");Directory.CreateDirectory(folder);
        var components=typeof(FormFindComponent).Assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(GH_Component).IsAssignableFrom(t))
            .Select(t=>(GH_Component)Activator.CreateInstance(t)).OrderBy(c=>c.SubCategory).ThenBy(c=>c.Name).ToArray();
        var hashes=new HashSet<string>();
        using(var sheet=new Bitmap(1120,80+((components.Length+3)/4)*105))
        using(var g=Graphics.FromImage(sheet))
        using(var title=new Font("Segoe UI",20,FontStyle.Bold))
        using(var label=new Font("Segoe UI",10))
        using(var small=new Font("Segoe UI",8))
        {
            g.Clear(Color.White);g.DrawString("ARACHNE / 43 component icons",title,Brushes.DarkSlateGray,25,20);
            for(int i=0;i<components.Length;i++)
            {
                var c=components[i];using(var icon=c.Icon_24x24)
                {
                    if(icon==null||icon.Width!=24||icon.Height!=24)throw new InvalidOperationException("Invalid icon: "+c.Name);
                    using(var stream=new MemoryStream())using(var sha=SHA256.Create())
                    {icon.Save(stream,ImageFormat.Png);if(!hashes.Add(Convert.ToBase64String(sha.ComputeHash(stream.ToArray()))))throw new InvalidOperationException("Duplicate icon: "+c.Name);}
                    icon.Save(Path.Combine(folder,c.GetType().Name+".png"),ImageFormat.Png);
                    int x=25+(i%4)*280,y=90+(i/4)*105;
                    g.InterpolationMode=InterpolationMode.NearestNeighbor;g.DrawImage(icon,new Rectangle(x,y,48,48));
                    g.DrawImageUnscaled(icon,x+55,y+12);g.DrawString(c.Name,label,Brushes.DarkSlateGray,x,y+53);g.DrawString(c.SubCategory,small,Brushes.Gray,x,y+73);
                }
            }
            sheet.Save(Path.Combine(root,"docs","ICONS.png"),ImageFormat.Png);
        }
        using(var icon=new Arachne.Grasshopper.AssemblyInfo().Icon)icon.Save(Path.Combine(folder,"Arachne.png"),ImageFormat.Png);
        Console.WriteLine("ICON PASS | 43 distinct 24px icons + assembly icon");
    }
    internal static void Canvas(GH_Document document,string path)
    {
        foreach(var obj in document.Objects)obj.Attributes.PerformLayout();
        var bounds=document.Objects.Select(o=>o.Attributes.Bounds).Aggregate(RectangleF.Union);bounds.Inflate(35,35);
        using(var canvas=new GH_Canvas())
        {
            canvas.Document=document;
            var view=new GH_Viewport {Size=new Size((int)Math.Ceiling(bounds.Width),(int)Math.Ceiling(bounds.Height)),Zoom=1,Tx=-(int)bounds.Left,Ty=-(int)bounds.Top};
            using(var bitmap=canvas.GenerateHiResImageTile(view,Color.FromArgb(247,248,248)))bitmap.Save(path,ImageFormat.Png);
            canvas.Document=null;
        }
    }
    internal static void Geometry(FormResult r,string path,string title)
    {
        var nodes=r.Solved.Nodes;
        Func<GPC.Geometry.Point3d,PointF> project=p=>new PointF((float)(p.X-.60*p.Y),(float)(.28*p.X+.38*p.Y-p.Z));
        var projected=nodes.Select(n=>project(n.Position)).ToArray();
        float minX=projected.Min(p=>p.X),maxX=projected.Max(p=>p.X),minY=projected.Min(p=>p.Y),maxY=projected.Max(p=>p.Y);
        float scale=Math.Min(940/Math.Max(1,maxX-minX),490/Math.Max(1,maxY-minY));
        Func<PointF,PointF> screen=p=>new PointF(80+(p.X-minX)*scale,150+(p.Y-minY)*scale);
        using(var image=new Bitmap(1100,760))using(var g=Graphics.FromImage(image))
        using(var heading=new Font("Segoe UI",21,FontStyle.Bold))using(var font=new Font("Segoe UI",12))
        using(var pen=new Pen(Color.Gray,2))
        {
            g.Clear(Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;
            g.DrawString(title,heading,Brushes.DarkSlateGray,35,25);
            g.DrawString(r.Settings.Kind+"   |   "+nodes.Count+" nodes   /   "+r.Solved.Beams.Count+" cables   /   "+r.Solved.Plates.Count+" plates",font,Brushes.Gray,35,68);
            foreach(var plate in r.Solved.Plates)
            {using(var fill=new SolidBrush(Color.FromArgb(18,0,137,153)))g.FillPolygon(fill,plate.NodeIds.Select(id=>screen(project(nodes[id-1].Position))).ToArray());}
            foreach(var edge in r.Solved.Beams)
            {
                pen.Color=edge.Direction==MembraneDirection.Warp?Color.FromArgb(0,137,153):edge.Direction==MembraneDirection.Weft?Color.FromArgb(224,131,40):Color.FromArgb(91,108,122);
                g.DrawLine(pen,screen(project(nodes[edge.StartNodeId-1].Position)),screen(project(nodes[edge.EndNodeId-1].Position)));
            }
            foreach(var node in nodes){var p=screen(project(node.Position));float radius=node.FixZ?4:2;g.FillEllipse(node.FixZ?Brushes.DarkSlateGray:Brushes.Gray,p.X-radius,p.Y-radius,2*radius,2*radius);}
            g.DrawString("Warp: teal   |   Weft: orange   |   Fixed nodes: dark dots",font,Brushes.DarkSlateGray,35,682);
            var eq=EquilibriumReport.Evaluate(r.Solved,.1,1);
            g.DrawString("Computed geometry · no vertical exaggeration · max free residual "+eq.MaxFreeResidual.ToString("G3")+" N",font,Brushes.Gray,35,712);
            image.Save(path,ImageFormat.Png);
        }
    }
}
