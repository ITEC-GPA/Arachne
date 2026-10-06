using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Arachne.Examples
{
    // Original vector artwork, drawn at 4x resolution for the 24 px Grasshopper palette.
    // Semantic glyphs are keyed by component type, independently of names and GUIDs.
    internal static class IconArtwork
    {
        private static readonly Dictionary<string, Bitmap> Cache = new Dictionary<string, Bitmap>();
        private static readonly Color Ink = Color.FromArgb(40, 57, 74);
        private static readonly Color Warp = Color.FromArgb(0, 137, 153);
        private static readonly Color Weft = Color.FromArgb(224, 131, 40);
        private static readonly Color Blue = Color.FromArgb(77, 103, 199);
        public static Bitmap Get(string name)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(name, out var icon)) Cache.Add(name, icon = Draw(name));
                return new Bitmap(icon);
            }
        }
        private static Bitmap Draw(string name)
        {
            using (var large = new Bitmap(96, 96))
            using (var g = Graphics.FromImage(large))
            {
                g.ScaleTransform(4, 4); g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var a = new Artist(g)) a.Paint(name);
                var result = new Bitmap(24, 24);
                using (var small = Graphics.FromImage(result))
                { small.InterpolationMode = InterpolationMode.HighQualityBicubic; small.DrawImage(large, 0, 0, 24, 24); }
                return result;
            }
        }
        private sealed class Artist : IDisposable
        {
            private readonly Graphics G;
            private readonly Pen P = new Pen(Ink, 1.55f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
            internal Artist(Graphics g) { G = g; }
            public void Dispose() => P.Dispose();
            private void L(float x, float y, float xx, float yy, Color? color = null)
            { P.Color = color ?? Ink; G.DrawLine(P, x, y, xx, yy); }
            private void Path(Color color, params float[] xy)
            { for (int i = 2; i < xy.Length; i += 2) L(xy[i - 2], xy[i - 1], xy[i], xy[i + 1], color); }
            private void Circle(float x, float y, float radius, Color? color = null, bool filled = false)
            {
                P.Color = color ?? Ink;
                if (filled) { using (var b = new SolidBrush(P.Color)) G.FillEllipse(b, x-radius, y-radius, radius*2, radius*2); }
                else G.DrawEllipse(P, x-radius, y-radius, radius*2, radius*2);
            }
            private void Arrow(float x, float y, float xx, float yy, Color? color = null)
            {
                L(x,y,xx,yy,color); double d = Math.Atan2(yy-y,xx-x);
                for (int s = -1; s <= 1; s += 2) L(xx,yy,xx-3*(float)Math.Cos(d+s*.55),yy-3*(float)Math.Sin(d+s*.55),color);
            }
            private void Mesh(bool dense = false)
            {
                Path(Ink,3,7,17,3,21,17,7,21,3,7);L(3,7,21,17,Warp);L(17,3,7,21,Warp);
                if (dense) {L(10,5,14,19,Warp);L(5,14,19,10,Weft);}
            }
            private void Net(bool saddle = false)
            {
                for(int i=0;i<3;i++) {float p=4+i*8;L(p,4,p,saddle?17+i:20,Warp);L(4,p,20,p,Weft);}
                if(saddle) Path(Ink,4,4,12,1.5f,20,4,20,20,12,17,4,20,4,4);
            }
            private void Tick(float x=12,float y=15) => Path(Warp,x-4,y,x-1,y+3,x+5,y-4);
            private void Badge(string value,Color color)
            {
                using(var b=new SolidBrush(Color.White))G.FillRectangle(b,12,12,12,12);
                using(var font=new Font("Segoe UI",8,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(color))G.DrawString(value,font,brush,13,12);
            }
            private void Plot()
            {Path(Ink,3,3,3,21,21,21);}
            internal void Paint(string name)
            {
                switch(name)
                {
                    case "Arachne":
                        for(int i=0;i<8;i++){double t=i*Math.PI/4;L(12,12,12+10*(float)Math.Cos(t),12+10*(float)Math.Sin(t),Warp);}
                        Circle(12,12,8);Circle(12,12,4,Weft);Circle(12,12,1.8f,Warp,true);break;
                    case "CableNetComponent": Net();break;
                    case "CableNetRadialComponent":
                        Circle(12,12,9,Weft);Circle(12,12,5,Weft);
                        for(int i=0;i<8;i++){double t=i*Math.PI/4;L(12,12,12+9*(float)Math.Cos(t),12+9*(float)Math.Sin(t),Warp);}break;
                    case "CompoundNetComponent":
                        Path(Warp,2,7,10,3,10,15,2,19,2,7);Path(Weft,14,9,22,5,22,17,14,21,14,9);
                        L(2,13,10,9,Warp);L(14,15,22,11,Weft);L(6,5,6,17);L(18,7,18,19);break;
                    case "DemoComponent": Net(true);break;
                    case "CableComponent": Path(Warp,3,5,8,13,16,16,21,8);Circle(3,5,2);Circle(21,8,2);break;
                    case "CableConstraintComponent": L(4,12,20,12,Warp);L(4,4,4,20);L(20,4,20,20);Arrow(8,5,16,5,Weft);break;
                    case "CableLoadComponent": Path(Warp,3,10,12,16,21,10);Arrow(12,2,12,12,Weft);Circle(3,10,1.7f);Circle(21,10,1.7f);break;
                    case "NodeComponent": Circle(12,10,3,Warp,true);Path(Ink,12,14,6,21,18,21,12,14);L(12,2,12,6,Weft);L(3,10,8,10,Weft);L(16,10,21,10,Weft);break;
                    case "ModelComponent": Mesh();Circle(3,7,2,Weft,true);Circle(17,3,2,Weft,true);Circle(21,17,2,Weft,true);Circle(7,21,2,Weft,true);break;
                    case "SolverSettingsComponent":
                        for(int i=0;i<3;i++){float x=5+7*i;L(x,3,x,21);Circle(x,6+5*i,2.5f,i==1?Weft:Warp,true);}break;
                    case "FormFindComponent": Mesh();Arrow(5,2,12,2,Weft);Arrow(22,6,22,13,Weft);Badge("F",Warp);break;
                    case "DeconstructModelComponent": Mesh();Badge("M",Blue);break;
                    case "DeconstructResultComponent": Plot();Path(Warp,5,16,9,11,13,14,20,5);Badge("R",Blue);break;
                    case "NodeResultsComponent": Circle(8,8,4,Warp);Arrow(8,8,20,8,Weft);Arrow(8,8,8,21,Weft);Circle(8,8,1.5f,Ink,true);break;
                    case "BeamResultsComponent": L(3,12,21,12,Warp);Arrow(9,6,3,6,Weft);Arrow(15,6,21,6,Weft);Path(Blue,3,20,3,16,21,16,21,20);break;
                    case "PlateResultsComponent": Mesh();Arrow(5,4,12,2,Weft);Arrow(21,10,23,17,Weft);Badge("N",Blue);break;
                    case "EquilibriumReportComponent": Path(Ink,4,20,20,20,12,13,4,20);L(12,13,12,5);L(3,8,21,8);Circle(12,5,2,Warp,true);Arrow(5,2,5,7,Weft);Arrow(19,2,19,7,Weft);break;
                    case "SolverHistoryComponent": Plot();Path(Warp,5,5,9,11,13,16,17,18,21,18);Circle(9,11,1.5f,Weft,true);Circle(17,18,1.5f,Weft,true);break;
                    case "CheckModelComponent": Mesh();Badge("✓",Warp);break;
                    case "UnitSettingsComponent": Path(Ink,3,7,21,7,21,17,3,17,3,7);for(int i=0;i<5;i++)L(5+3*i,7,5+3*i,i%2==0?12:10,Weft);break;
                    case "ConvertUnitsComponent": Arrow(3,7,21,7,Warp);Arrow(21,17,3,17,Weft);L(7,3,7,5);L(17,19,17,21);break;
                    case "PanelDistortionComponent": Mesh();Path(Weft,5,9,15,5,18,15,8,18,5,9);Badge("%",Blue);break;
                    case "ResultPreviewComponent": Path(Ink,2,12,7,6,17,6,22,12,17,18,7,18,2,12);Circle(12,12,4,Warp);Circle(12,12,1.5f,Weft,true);break;
                    case "DependencyCheckComponent": Path(Ink,4,3,14,3,14,13,4,13,4,3);L(8,3,8,13,Warp);Tick(16,17);break;
                    case "BordersComponent": Mesh();P.Width=2.4f;Path(Weft,3,7,17,3,21,17,7,21,3,7);break;
                    case "OrderBordersComponent": Path(Warp,4,7,4,19,19,19,19,5);Arrow(19,5,7,5,Weft);Circle(4,7,2,Ink,true);break;
                    case "CompensationComponent": Mesh();Arrow(1,12,7,12,Weft);Arrow(23,12,17,12,Weft);break;
                    case "DecompensationComponent": Mesh();Arrow(7,12,1,12,Weft);Arrow(17,12,23,12,Weft);break;
                    case "DecompensationDescriptorComponent": Plot();Path(Warp,4,15,8,8,17,8,21,15);Badge("D",Blue);break;
                    case "DecompensationDescriptorExtComponent": Plot();Path(Warp,4,16,8,8,12,12,16,5,21,15);Circle(8,8,1.5f,Weft,true);Circle(16,5,1.5f,Weft,true);break;
                    case "FlatteningComponent": Path(Ink,2,8,7,3,12,8,7,13,2,8);Path(Warp,12,16,18,13,22,17,16,21,12,16);Arrow(3,17,9,17,Weft);break;
                    case "FlatteningArapComponent": Mesh();Badge("A",Blue);Arrow(5,1,12,1,Weft);break;
                    case "DeformArapComponent": Path(Warp,3,7,17,2,21,19,7,17,3,7);Path(Warp,3,7,21,19,17,2,7,17);Circle(3,7,2,Weft,true);Circle(21,19,2,Weft,true);Arrow(15,10,21,5,Blue);break;
                    case "GeodesicOffsetsComponent": Mesh();Path(Weft,3,10,11,11,20,14);Path(Blue,5,16,13,17,21,19);break;
                    case "MeshDivideComponent": Mesh();L(8,4,12,21,Weft);L(13,3,17,19,Weft);break;
                    case "MeshGeodesicComponent": Mesh();Path(Weft,4,10,10,9,15,12,20,14);Circle(4,10,1.8f,Weft,true);Circle(20,14,1.8f,Weft,true);break;
                    case "MeshCleanComponent": Mesh();Badge("+",Warp);L(19,1,19,6,Weft);L(17,3.5f,21,3.5f,Weft);break;
                    case "MeshPipeComponent": Path(Warp,4,8,16,3,21,8,9,13,4,8);Path(Ink,4,8,4,16,9,21,21,16,21,8);L(9,13,9,21,Warp);Circle(7,15,2,Weft);break;
                    case "MeshRemapComponent": Path(Warp,2,3,10,3,10,11,2,11,2,3);Path(Blue,14,13,20,11,23,20,15,22,14,13);Arrow(4,16,12,18,Weft);L(2,3,10,11);L(14,13,23,20);break;
                    case "SplitMeshOnCurvesComponent": Mesh();P.Width=2.5f;Path(Color.White,9,3,13,11,12,22);P.Width=1.5f;Path(Weft,9,3,13,11,12,22);break;
                    case "SubdivideMeshComponent": Mesh(true);break;
                    case "UnweldMeshComponent": Path(Warp,2,7,15,3,5,20,2,7);Path(Weft,19,5,23,19,9,22,19,5);break;
                    case "LineUpComponent": Path(Warp,2,6,7,6,7,18,2,18,2,6);Path(Weft,10,9,15,9,15,18,10,18,10,9);Path(Blue,18,4,23,4,23,18,18,18,18,4);L(1,21,23,21);break;
                    default: throw new ArgumentException("Missing Arachne icon: " + name, nameof(name));
                }
            }
        }
    }
}
