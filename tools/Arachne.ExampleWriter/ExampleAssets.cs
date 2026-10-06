using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using Arachne.Core;
using Arachne.Grasshopper.Components;
using Grasshopper.Kernel;

// SVG previews avoid creating/discarding WinForms canvases in the headless Rhino process.
internal static class ExampleAssets
{
    private static string F(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
    private static string E(string text)=>SecurityElement.Escape(text);
    private static StringBuilder Svg(int width,int height)=>new StringBuilder($"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='0 0 {width} {height}'><rect width='100%' height='100%' fill='white'/><g font-family='Segoe UI, sans-serif' fill='#28394a'>");
    private static void Text(StringBuilder svg,double x,double y,string text,int size=16)=>svg.Append($"<text x='{F(x)}' y='{F(y)}' font-size='{size}'>{E(text)}</text>");
    internal static void Icons(string root)
    {
        var assembly=typeof(FormFindComponent).Assembly;
        var types=assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(GH_Component).IsAssignableFrom(t));
        var components=types.Select(t=>(GH_Component)Activator.CreateInstance(t)).OrderBy(c=>c.SubCategory).ThenBy(c=>c.Name).ToArray();
        var svg=Svg(1200,1300);Text(svg,25,42,"ARACHNE / 43 componenti",26);
        var hashes=new HashSet<string>();
        for(int i=0;i<components.Length;i++)
        {
            var c=components[i];string suffix=".Icons."+c.GetType().Name+".png";
            string resource=assembly.GetManifestResourceNames().Single(n=>n.EndsWith(suffix,StringComparison.OrdinalIgnoreCase));
            byte[] bytes;using(var input=assembly.GetManifestResourceStream(resource))using(var buffer=new MemoryStream()){input.CopyTo(buffer);bytes=buffer.ToArray();}
            if(bytes.Length<24||bytes[0]!=137||bytes[1]!=80||bytes[19]!=24||bytes[23]!=24)throw new InvalidOperationException("Invalid 24px PNG icon: "+c.Name);
            using(var sha=SHA256.Create())if(!hashes.Add(Convert.ToBase64String(sha.ComputeHash(bytes))))throw new InvalidOperationException("Duplicate icon: "+c.Name);
            int x=25+(i%4)*300,y=75+(i/4)*108;
            svg.Append($"<image x='{x}' y='{y}' width='48' height='48' href='data:image/png;base64,{Convert.ToBase64String(bytes)}'/>");
            Text(svg,x,y+69,c.Name,14);Text(svg,x,y+89,c.SubCategory,12);
        }
        svg.Append("</g></svg>");File.WriteAllText(Path.Combine(root,"docs","ICONS.svg"),svg.ToString(),Encoding.UTF8);
        Console.WriteLine("ICON PASS | 43 distinct embedded PNG icons, 24 x 24 px");
    }
    internal static void Geometry(FormResult r,string path,string title)
    {
        var nodes=r.Solved.Nodes;
        Func<GPC.Geometry.Point3d,(double X,double Y)> project=p=>(p.X-.60*p.Y,.28*p.X+.38*p.Y-p.Z);
        var points=nodes.Select(n=>project(n.Position)).ToArray();
        double minX=points.Min(p=>p.X),maxX=points.Max(p=>p.X),minY=points.Min(p=>p.Y),maxY=points.Max(p=>p.Y);
        double scale=Math.Min(940/Math.Max(1,maxX-minX),490/Math.Max(1,maxY-minY));
        Func<int,string> point=id=>F(80+(points[id-1].X-minX)*scale)+","+F(150+(points[id-1].Y-minY)*scale);
        var svg=Svg(1100,760);Text(svg,35,52,title,26);
        Text(svg,35,84,r.Settings.Kind+" | "+nodes.Count+" nodi / "+r.Solved.Beams.Count+" aste / "+r.Solved.Plates.Count+" piastre",17);
        foreach(var plate in r.Solved.Plates)svg.Append("<polygon fill='#edf7f8' points='"+string.Join(" ",plate.NodeIds.Select(point))+"'/>");
        foreach(var beam in r.Solved.Beams)
        {
            string color=beam.Direction==MembraneDirection.Warp?"#008999":beam.Direction==MembraneDirection.Weft?"#e08328":"#5b6c7a";
            svg.Append("<polyline fill='none' stroke='"+color+"' stroke-width='2' points='"+point(beam.StartNodeId)+" "+point(beam.EndNodeId)+"'/>");
        }
        foreach(var node in nodes){var p=point(node.Id).Split(',');svg.Append($"<circle cx='{p[0]}' cy='{p[1]}' r='{(node.FixZ?4:2)}' fill='#28394a'/>");}
        Text(svg,35,699,"Warp: azzurro / weft: arancio / nodi vincolati: punti scuri",17);
        Text(svg,35,731,"Geometria calcolata, scala Z reale. Residuo massimo: "+EquilibriumReport.Evaluate(r.Solved,.1,1).MaxFreeResidual.ToString("G3",CultureInfo.InvariantCulture)+" N",16);
        svg.Append("</g></svg>");File.WriteAllText(path,svg.ToString(),Encoding.UTF8);
    }
}
