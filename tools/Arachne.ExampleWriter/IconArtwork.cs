using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;

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
        private static readonly Color Shade = Color.FromArgb(70, 0, 137, 153);
        public static Bitmap Get(string name)
        {
            lock (Cache)
            {
                if (!Cache.TryGetValue(name, out var icon)) Cache.Add(name, icon = Draw(name));
                return new Bitmap(icon);
            }
        }
        public static bool Has(string name)
        {
            try { using (Draw(name)) return true; }
            catch (ArgumentException) { return false; }
        }
        private static Bitmap Draw(string name)
        {
            using (var large = new Bitmap(96, 96))
            using (var g = Graphics.FromImage(large))
            {
                g.ScaleTransform(4, 4); g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
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
            private void Fill(Color color, params float[] xy)
            {
                var points = new PointF[xy.Length / 2];
                for (int i = 0; i < points.Length; i++) points[i] = new PointF(xy[2 * i], xy[2 * i + 1]);
                using (var b = new SolidBrush(color)) G.FillPolygon(b, points);
            }
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
            private void Badge(string value,Color color)
            {
                using(var b=new SolidBrush(Color.White))G.FillRectangle(b,12,12,12,12);
                using(var font=new Font("Segoe UI",8,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(color))G.DrawString(value,font,brush,13,12);
            }
            private void Text(string value,float x,float y,float size,Color color)
            {
                using(var font=new Font("Segoe UI",size,FontStyle.Bold,GraphicsUnit.Pixel))
                using(var brush=new SolidBrush(color))
                using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center})
                    G.DrawString(value,font,brush,x,y,format);
            }
            private void Plot()
            {Path(Ink,3,3,3,21,21,21);}
            private void Bar(float x,float y,float xx,float yy)
            {L(x,y,xx,yy,Warp);Circle(x,y,1.8f,Ink,true);Circle(xx,yy,1.8f,Ink,true);}
            private void Section(Color fill,params float[] xy)
            {Fill(fill,xy);Path(Ink,xy);}
            private void Funnel()
            {Fill(Shade,3,3,21,3,14,11,14,19,10,21,10,11);Path(Ink,3,3,21,3,14,11,14,19,10,21,10,11,3,3);}
            private void Swatch(Color top,Color side,string label)
            {
                Fill(top,3,8,12,4,21,8,12,12);Fill(side,3,8,12,12,12,21,3,17);Fill(Color.FromArgb(150,side),12,12,21,8,21,17,12,21);
                Path(Ink,3,8,12,4,21,8,21,17,12,21,3,17,3,8);L(3,8,12,12);L(12,12,21,8);L(12,12,12,21);
                Badge(label,Ink);
            }
            internal void Paint(string name)
            {
                switch(name)
                {
                    case "Arachne":
                        for(int i=0;i<8;i++){double t=i*Math.PI/4;L(12,12,12+10*(float)Math.Cos(t),12+10*(float)Math.Sin(t),Warp);}
                        Circle(12,12,8);Circle(12,12,4,Weft);Circle(12,12,1.8f,Warp,true);break;

                    // 01-Parameters
                    case "CoordinateSystemParam":
                        Arrow(5,19,21,19,Warp);Arrow(5,19,5,3,Weft);Arrow(5,19,15,9,Blue);Circle(5,19,1.8f,Ink,true);break;

                    // 02-Materials
                    case "MaterialAluminiumComponent": Swatch(Color.FromArgb(214,221,229),Color.FromArgb(160,172,186),"Al");break;
                    case "MaterialConcreteComponent": Swatch(Color.FromArgb(206,201,190),Color.FromArgb(150,146,136),"C");break;
                    case "MaterialCustomComponent": Swatch(Color.FromArgb(150,0,137,153),Color.FromArgb(220,0,137,153),"E");break;
                    case "MaterialSteelComponent": Swatch(Color.FromArgb(160,174,196),Color.FromArgb(92,110,138),"S");break;
                    case "MaterialMembraneComponent":
                        // A sagging fabric between two edges, with warp and weft
                        Fill(Color.FromArgb(235,231,214),3,5,21,5,21,13,12,17,3,13);
                        L(9,5,9,15,Weft);L(15,5,15,15,Weft);L(3,9,21,9,Warp);Path(Warp,3,13,12,17,21,13);
                        Path(Ink,3,5,21,5,21,13);Path(Ink,3,5,3,13);Badge("M",Ink);break;

                    // 03-Sections
                    case "BeamSectionAngleComponent": Section(Shade,6,3,10,3,10,16,20,16,20,20,6,20,6,3);break;
                    case "BeamSectionBrepComponent": Section(Shade,5,6,14,3,20,9,18,19,8,20,4,13,5,6);Circle(11,12,2.5f,Weft);break;
                    case "BeamSectionCComponent": Section(Shade,19,4,6,4,6,20,19,20,19,16.5f,9.5f,16.5f,9.5f,7.5f,19,7.5f,19,4);break;
                    case "BeamSectionGenericComponent": Circle(12,12,8.5f,Ink);Text("A",12,12.5f,10,Warp);break;
                    case "BeamSectionHollowCircleComponent":
                        Circle(12,12,8.5f,Warp,true);Circle(12,12,5.5f,Color.White,true);Circle(12,12,8.5f);Circle(12,12,5.5f);break;
                    case "BeamSectionHollowRectComponent":
                        Fill(Shade,4,4,20,4,20,20,4,20);Fill(Color.White,7.5f,7.5f,16.5f,7.5f,16.5f,16.5f,7.5f,16.5f);
                        Path(Ink,4,4,20,4,20,20,4,20,4,4);Path(Ink,7.5f,7.5f,16.5f,7.5f,16.5f,16.5f,7.5f,16.5f,7.5f,7.5f);break;
                    case "BeamSectionIComponent": Section(Shade,4,3,20,3,20,6.5f,13.7f,6.5f,13.7f,17.5f,20,17.5f,20,21,4,21,4,17.5f,10.3f,17.5f,10.3f,6.5f,4,6.5f,4,3);break;
                    case "BeamSectionSolidCircleComponent": Circle(12,12,8.5f,Warp,true);Circle(12,12,8.5f);break;
                    case "BeamSectionSolidRectComponent": Section(Color.FromArgb(200,0,137,153),5,3,19,3,19,21,5,21,5,3);break;
                    case "BeamSectionTComponent": Section(Shade,3,4,21,4,21,8,14,8,14,21,10,21,10,8,3,8,3,4);break;
                    case "SectionGeometricPropertyComponent":
                        Section(Shade,6,4,18,4,18,7,13.5f,7,13.5f,17,18,17,18,20,6,20,6,17,10.5f,17,10.5f,7,6,7,6,4);L(2,12,22,12,Weft);L(12,2,12,22,Blue);Circle(12,12,1.8f,Ink,true);break;

                    // 04-Definitions
                    case "LoadCaseComponent": Path(Ink,3,5,21,5,21,19,3,19,3,5);Text("LC",12,12.5f,9,Weft);break;
                    case "GroupComponent":
                        Path(Blue,3,4,21,4,21,20,3,20,3,4);Circle(8,9,2.5f,Warp,true);Circle(16,9,2.5f,Weft,true);Circle(12,15.5f,2.5f,Ink,true);break;

                    // 05-Properties
                    case "BeamPropertyComponent":
                        Bar(3,21,12,12);Section(Shade,13,3,22,3,22,5.5f,18.8f,5.5f,18.8f,10.5f,22,10.5f,22,13,13,13,13,10.5f,16.2f,10.5f,16.2f,5.5f,13,5.5f,13,3);break;
                    case "PlatePropertyComponent":
                        Fill(Shade,3,13,13,8,21,12,11,17);Path(Ink,3,13,13,8,21,12,11,17,3,13);Path(Warp,3,17,11,21,21,16);L(3,13,3,17,Warp);L(11,17,11,21,Warp);L(21,12,21,16,Warp);
                        Arrow(17,2,17,8,Weft);break;

                    // 06-Model
                    case "NodeComponent": Circle(12,12,4.5f,Warp,true);L(3,12,7,12);L(17,12,21,12);L(12,3,12,7);L(12,17,12,21);break;
                    case "BeamComponent": P.Width=3;L(4,19,20,5,Warp);P.Width=1.55f;Circle(4,19,2.2f,Ink,true);Circle(20,5,2.2f,Ink,true);break;
                    case "PlateComponent": Fill(Shade,4,6,19,3,21,18,3,20);Path(Ink,4,6,19,3,21,18,3,20,4,6);Circle(4,6,1.6f,Ink,true);Circle(19,3,1.6f,Ink,true);Circle(21,18,1.6f,Ink,true);Circle(3,20,1.6f,Ink,true);break;

                    // 09-Model
                    case "BuildModelComponent": Mesh();Circle(3,7,2,Weft,true);Circle(17,3,2,Weft,true);Circle(21,17,2,Weft,true);Circle(7,21,2,Weft,true);break;
                    case "DecomposeModelComponent":
                        Path(Warp,2,6,10,3,11,11,3,13,2,6);Circle(2,6,1.5f,Ink,true);Circle(10,3,1.5f,Ink,true);
                        L(13,12,21,9,Ink);Circle(13,12,1.5f,Ink,true);Circle(21,9,1.5f,Ink,true);Circle(9,19,2.2f,Weft,true);Arrow(16,15,20,20,Blue);break;
                    case "CheckModelComponent":
                        Mesh();Circle(17,17,6,Color.White,true);Circle(17,17,6,Warp);P.Width=2.2f;Path(Warp,14,17,16.5f,19.5f,20.5f,14.5f);P.Width=1.55f;break;

                    // 07-Attributes
                    case "ForceDensityComponent": Bar(3,7,21,7);Text("q",8,16,11,Weft);Text("F/L",17,17,6.5f,Blue);break;
                    case "ForceDensityCableComponent":
                        L(3,7,21,7,Ink);Path(Warp,3,7,6,11.5f,9,14,12,15,15,14,18,11.5f,21,7);Circle(3,7,1.8f,Ink,true);Circle(21,7,1.8f,Ink,true);Arrow(12,7,12,13.5f,Weft);Text("f/c",12,20.5f,6,Blue);break;
                    case "ForceDensityMembraneComponent":
                        Fill(Color.FromArgb(40,0,137,153),3,4,21,4,21,20,3,20);Path(Ink,3,4,21,4,21,20,3,20,3,4);
                        L(3,9.5f,21,9.5f,Warp);L(3,14.5f,21,14.5f,Warp);L(9,4,9,20,Weft);L(15,4,15,20,Weft);break;
                    case "FixedLengthComponent": Bar(4,16,20,16);L(4,5,4,11);L(20,5,20,11);Arrow(12,8,4.5f,8,Weft);Arrow(12,8,19.5f,8,Weft);break;
                    case "FixedAxialForceComponent": Bar(7,12,17,12);Arrow(7,12,1.5f,12,Weft);Arrow(17,12,22.5f,12,Weft);Text("N",12,5,7,Blue);break;
                    case "FixedCompensationComponent": Bar(3,6,21,6);Arrow(2,12,8,12,Weft);Arrow(22,12,16,12,Weft);Text("%",12,18.5f,9,Blue);break;
                    case "KeepPositionComponent":
                        Circle(12,12,6.5f,Ink);Circle(12,12,2.6f,Warp,true);L(12,1.5f,12,6,Weft);L(12,18,12,22.5f,Weft);L(1.5f,12,6,12,Weft);L(18,12,22.5f,12,Weft);break;
                    case "NodeRestraintsComponent":
                        Circle(12,7,3,Warp,true);Path(Ink,12,10,5.5f,18,18.5f,18,12,10);L(3,21,21,21);L(6,21,4,23.5f);L(11,21,9,23.5f);L(16,21,14,23.5f);L(21,21,19,23.5f);break;
                    case "ElementIdComponent":
                        Fill(Shade,3,7,15,7,22,12,15,17,3,17);Path(Ink,3,7,15,7,22,12,15,17,3,17,3,7);Circle(17,12,1.5f,Ink,true);Text("ID",9,12.3f,7.5f,Weft);break;

                    // 08-Loads
                    case "NodeLoadComponent": Circle(12,18,3,Warp,true);Arrow(12,2,12,13.5f,Weft);Arrow(3,18,8,18,Blue);break;
                    case "BeamPointLoadComponent": Bar(3,18,21,18);Arrow(9,3,9,15,Weft);break;
                    case "BeamDistributedLoadComponent": Bar(3,19,21,19);L(4,4,20,4,Weft);Arrow(5,4,5,16,Weft);Arrow(12,4,12,16,Weft);Arrow(19,4,19,16,Weft);break;

                    // 13-Patterning
                    case "CableNetComponent": Net();break;
                    case "CableNetRadialComponent":
                        Circle(12,12,9,Weft);Circle(12,12,5,Weft);
                        for(int i=0;i<8;i++){double t=i*Math.PI/4;L(12,12,12+9*(float)Math.Cos(t),12+9*(float)Math.Sin(t),Warp);}break;
                    case "CompoundNetComponent":
                        Path(Warp,2,7,10,3,10,15,2,19,2,7);Path(Weft,14,9,22,5,22,17,14,21,14,9);
                        L(2,13,10,9,Warp);L(14,15,22,11,Weft);L(6,5,6,17);L(18,7,18,19);break;
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
                    case "PanelDistortionComponent":
                        Fill(Shade,3,4,16,3,17,16,4,17);Path(Ink,3,4,16,3,17,16,4,17,3,4);P.Width=2.2f;L(3,4,17,16,Weft);L(16,3,4,17,Blue);P.Width=1.55f;Badge("%",Ink);break;
                    case "RemeshComponent":
                        Path(Ink,3,4,21,4,21,20,3,20,3,4);L(3,12,21,12,Warp);L(9,4,9,20,Warp);L(15,4,15,20,Warp);L(3,4,9,12,Weft);L(9,12,15,20,Weft);L(15,4,21,12,Weft);L(9,4,3,12,Weft);L(21,12,15,20,Weft);break;

                    // 12-Analysis
                    case "FormFindComponent": Mesh();Arrow(5,2,12,2,Weft);Arrow(22,6,22,13,Weft);Badge("F",Warp);break;
                    case "SolverOptionsComponent":
                        // Three sliders: the options of the form finding
                        L(3,6,21,6);Circle(8,6,2.4f,Warp,true);L(3,12,21,12);Circle(16,12,2.4f,Weft,true);L(3,18,21,18);Circle(11,18,2.4f,Blue,true);break;
                    case "BeamForcesComponent":
                        P.Width=3.4f;L(3,19,12,12,Weft);P.Width=2.3f;L(12,12,21,5,Warp);P.Width=1.2f;L(12,12,21,19,Blue);P.Width=1.55f;
                        Circle(12,12,1.8f,Ink,true);Circle(3,19,1.8f,Ink,true);Circle(21,5,1.8f,Ink,true);Circle(21,19,1.8f,Ink,true);Text("N",6,6,8.5f,Ink);break;
                    case "SupportReactionsComponent":
                        Path(Ink,12,13,6,20,18,20,12,13);L(3,22.5f,21,22.5f);Circle(12,12,2,Warp,true);Arrow(12,10,12,2,Weft);Arrow(12,10,20,5,Blue);break;

                    // 14-Tools
                    case "CurveToMeshComponent":
                        Path(Warp,2,4,5,9,4,15);Path(Warp,6,3,9,9,8,16);Arrow(10,10,14,10,Weft);
                        Fill(Shade,15,4,22,5,21,16,14,15);Path(Ink,15,4,22,5,21,16,14,15,15,4);L(15,4,21,16,Warp);break;
                    case "NodeFilterComponent": Funnel();Circle(19.5f,18.5f,3,Weft,true);break;
                    case "BeamFilterComponent":
                        Funnel();P.Width=2.4f;L(16,22,22.5f,15,Weft);P.Width=1.55f;Circle(16,22,1.5f,Ink,true);Circle(22.5f,15,1.5f,Ink,true);break;
                    case "PlateFilterComponent":
                        Funnel();Fill(Color.FromArgb(150,224,131,40),16,15,22.5f,14,22.5f,22,15.5f,22);Path(Ink,16,15,22.5f,14,22.5f,22,15.5f,22,16,15);break;

                    default: throw new ArgumentException("Missing Arachne icon: " + name, nameof(name));
                }
            }
        }
    }
}
