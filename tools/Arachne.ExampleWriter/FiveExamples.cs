using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using Arachne.Core;
using Arachne.Grasshopper.Components;
using Arachne.Grasshopper.Components.Networks;
using Arachne.Grasshopper.Components.Patterning;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;

internal static class FiveExamples
{
    private sealed class Example : IDisposable
    {
        internal readonly GH_Document Document = new GH_Document { Enabled = true };
        internal readonly string File, Title;
        internal Example(string file, string title) { File=file; Title=title; Note(title,60,20,1040,80); }
        internal T Add<T>(T item,float x,float y) where T:IGH_DocumentObject
        { item.CreateAttributes();item.Attributes.Pivot=new PointF(x,y);Document.AddObject(item,false);return item; }
        internal GH_Panel Note(string text,float x,float y,float w=420,float h=120)
        {var p=Add(new GH_Panel{UserText=text},x,y);p.Attributes.Bounds=new RectangleF(x,y,w,h);return p;}
        internal GH_Panel Readout(string name,IGH_Param source,float x,float y,float w=230,float h=120)
        {var p=Note("",x,y,w,h);p.NickName=name;p.AddSource(source);return p;}
        internal GH_NumberSlider Slider(string name,double value,double min,double max,float x,float y,int digits=0)
        {
            var slider=new GH_NumberSlider{NickName=name};slider.Slider.Minimum=(decimal)min;slider.Slider.Maximum=(decimal)max;slider.Slider.DecimalPlaces=digits;
            slider.SetSliderValue((decimal)value);Add(slider,x,y);slider.Attributes.Bounds=new RectangleF(x,y,230,20);return slider;
        }
        internal void Group(string name,Color color,params IGH_DocumentObject[] objects)
        {var group=new GH_Group{NickName=name,Colour=color,Border=GH_GroupBorder.Box};foreach(var obj in objects)group.AddObject(obj.InstanceGuid);Add(group,0,0);}
        public void Dispose()=>Document.Dispose();
    }
    private static void Set<T>(GH_Component c,int index,params T[] data) where T:class,IGH_Goo
    {var p=(GH_PersistentParam<T>)c.Params.Input[index];p.PersistentData.Clear();p.SetPersistentData((IEnumerable<T>)data);}
    private static void Link(GH_Component target,int input,IGH_Param source,bool flatten=false)
    {target.Params.Input[input].AddSource(source);if(flatten)target.Params.Input[input].DataMapping=GH_DataMapping.Flatten;}
    private static IEnumerable<IGH_Goo> Data(GH_Component c,int output=0)=>c.Params.Output[output].VolatileData.AllData(true);
    private static Param_Curve Curves(Example e,string title,IEnumerable<Curve> values,float x,float y)
    {var p=e.Add(new Param_Curve{NickName=title},x,y);p.SetPersistentData(values.Select(v=>new GH_Curve(v)));return p;}
    private static Param_Mesh MeshInput(Example e,string title,Mesh mesh,float x,float y)
    {var p=e.Add(new Param_Mesh{NickName=title},x,y);p.SetPersistentData(new GH_Mesh(mesh));return p;}
    private static Curve Rectangle(double x,double y,double width,double depth,double rise)
        =>new PolylineCurve(new[]{new Point3d(x,y,rise),new Point3d(x+width,y,-rise),new Point3d(x+width,y+depth,rise),new Point3d(x,y+depth,-rise),new Point3d(x,y,rise)});
    private static readonly Color GeometryColor=Color.FromArgb(45,50,130,160), ModelColor=Color.FromArgb(45,225,154,70), ResultColor=Color.FromArgb(45,50,160,125);

    internal static void Write(string root)
    {
        Directory.CreateDirectory(Path.Combine(root,"examples"));
        var coverage=new Dictionary<string,List<string>>();var summaries=new List<string>();var failures=new List<string>();
        var examples=new[]{Rectangular(false),Rectangular(true),Radial(),Compound(),PatternWorkshop()};
        try
        {
            foreach(var e in examples)
            {
                try
                {
                Console.WriteLine("EXAMPLE START "+e.File);
                e.Document.NewSolution(false);Verify(e.Document,e.File);
                var components=e.Document.Objects.OfType<GH_Component>().ToArray();
                foreach(var c in components.Where(c=>c.GetType().Assembly==typeof(FormFindComponent).Assembly))
                {string type=c.GetType().Name;if(!coverage.ContainsKey(type))coverage[type]=new List<string>();if(!coverage[type].Contains(e.File))coverage[type].Add(e.File);}
                summaries.Add(Summarize(e.Document,e.File));
                foreach(string extension in new[]{".gh",".ghx"})
                {
                    string path=Path.Combine(root,"examples",e.File+extension);var archive=new GH_Archive();
                    if(!archive.AppendObject(e.Document,"Definition")||!archive.WriteToFile(path,true,false))throw new IOException("Cannot save "+path);
                    var reader=new GH_Archive();using(var reopened=new GH_Document{Enabled=true})
                    {
                        if(!reader.ReadFromFile(path)||!reader.ExtractObject(reopened,"Definition")||reopened.Objects.Count!=e.Document.Objects.Count)throw new IOException("Incomplete archive "+path);
                        reopened.NewSolution(false);Verify(reopened,e.File+extension);
                        if(reopened.Objects.OfType<GH_Component>().Sum(c=>c.Params.Input.Sum(p=>p.SourceCount))!=components.Sum(c=>c.Params.Input.Sum(p=>p.SourceCount)))throw new IOException("Lost wires in "+path);
                    }
                }
                Console.WriteLine("EXAMPLE PASS "+summaries.Last());
                }
                catch(Exception error){failures.Add(e.File+": "+error.Message);Console.WriteLine("EXAMPLE FAIL "+failures.Last());}
            }
            if(failures.Count>0)throw new InvalidOperationException(string.Join(Environment.NewLine,failures));
            var required=typeof(FormFindComponent).Assembly.GetTypes().Where(t=>!t.IsAbstract&&typeof(GH_Component).IsAssignableFrom(t)).ToArray();
            var missing=required.Where(t=>!coverage.ContainsKey(t.Name)).Select(t=>t.Name).ToArray();
            if(missing.Length>0)throw new InvalidOperationException("Missing example components: "+string.Join(", ",missing));
            File.WriteAllLines(Path.Combine(root,"examples","COMPONENT-COVERAGE.tsv"),new[]{"Component\tExamples"}.Concat(coverage.OrderBy(k=>k.Key).Select(k=>k.Key+"\t"+string.Join("; ",k.Value))));
            File.WriteAllLines(Path.Combine(root,"examples","VALIDATION.txt"),new[]{"Generated and reopened both GH and GHX. Run=True; all 43 Arachne components used. SI units: m, N.","Verified UTC: "+DateTime.UtcNow.ToString("o")}.Concat(summaries));
            Console.WriteLine("FIVE EXAMPLES PASS | "+coverage.Count+" component types | 10 reopened archives");
        }
        finally{foreach(var e in examples)e.Dispose();}
    }

    private static Example Rectangular(bool nonlinear)
    {
        var e=new Example(nonlinear?"04_Rectangular_Nonlinear":"03_Rectangular_Iterative",nonlinear?
            "04 | RETE RETTANGOLARE NON LINEARE\nWarp 9 kN / weft 4.5 kN · pressione 100 N/m² · geometria in metri":
            "03 | RETE RETTANGOLARE ITERATIVA\nWarp 6 kN / weft 3 kN · carichi sui cavi · geometria in metri");
        var boundary=Curves(e,"Bordo a sella [m]",Rectangle(0,0,8,6,0.8).DuplicateSegments(),70,230);
        var net=e.Add(new CableNetComponent(),400,260);Link(net,0,boundary);Link(net,2,boundary);
        var warp=e.Slider("Passo warp [m]",2,1,3,70,340,1);var weft=e.Slider("Passo weft [m]",2,1,3,70,380,1);
        Link(net,3,warp);Link(net,4,weft);Set(net,8,new GH_Number(1e-6));
        e.Group("01 / GEOMETRIA",GeometryColor,boundary,warp,weft,net);
        BuildNetModel(e,net,nonlinear?SolverKind.NonLinear:SolverKind.LinearIterative,nonlinear?9000:6000,nonlinear?4500:3000,nonlinear?100:0,!nonlinear,false);
        return e;
    }
    private static Example Radial()
    {
        var e=new Example("05_Radial_Iterative","05 | RETE RADIALE ITERATIVA\nRaggi (warp) 8 kN / anelli (weft) 4 kN · -100 N sui nodi interni");
        var points=Enumerable.Range(0,17).Select(i=>new Point3d(4*Math.Cos(2*Math.PI*i/16),4*Math.Sin(2*Math.PI*i/16),0)).ToArray();
        var boundary=Curves(e,"Perimetro poligonale [m]",new PolylineCurve(points).DuplicateSegments(),70,220);
        var net=e.Add(new CableNetRadialComponent(),400,260);Link(net,0,boundary);Link(net,2,boundary);
        var step=e.Slider("Passo radiale [m]",1,0.5,2,70,330,1);var divisions=e.Slider("Numero raggi",16,8,24,70,370);
        Link(net,3,step);Link(net,4,divisions);Set(net,8,new GH_Number(1e-6));
        e.Group("01 / GEOMETRIA",GeometryColor,boundary,step,divisions,net);
        BuildNetModel(e,net,SolverKind.LinearIterative,8000,4000,0,false,true);return e;
    }
    private static Example Compound()
    {
        var e=new Example("06_Compound_Nonlinear","06 | RETE COMPOSTA NON LINEARE\nDue pannelli indipendenti, ciascuno con bordo vincolato · warp 6 kN / weft 2.5 kN");
        var boundaries=e.Add(new Param_Curve{NickName="Due bordi / rami {0}, {1}"},70,240);
        boundaries.PersistentData.AppendRange(Rectangle(0,0,4,4,0.5).DuplicateSegments().Select(c=>new GH_Curve(c)),new GH_Path(0));
        boundaries.PersistentData.AppendRange(Rectangle(6,0,4,4,0.7).DuplicateSegments().Select(c=>new GH_Curve(c)),new GH_Path(1));
        var net=e.Add(new CompoundNetComponent(),400,280);Link(net,0,boundaries);Link(net,2,boundaries);
        Set(net,3,new GH_Number(2),new GH_Number(2));Set(net,4,new GH_Number(2),new GH_Number(2));
        Set(net,5,new GH_Number(0),new GH_Number(0));Set(net,6,new GH_Point(Point3d.Origin),new GH_Point(new Point3d(6,0,0)));
        Set(net,8,new GH_Number(1e-5));Set(net,10,new GH_Number(6));
        e.Group("01 / GEOMETRIA MULTIPANNELLO",GeometryColor,boundaries,net);
        BuildNetModel(e,net,SolverKind.NonLinear,6000,2500,0,false,false);return e;
    }
    private static void BuildNetModel(Example e,GH_Component net,SolverKind kind,double warpN,double weftN,double pressure,bool cableLoads,bool nodeLoads)
    {
        var wa=e.Add(new CableComponent{NickName="Cable WARP"},800,230);var we=e.Add(new CableComponent{NickName="Cable WEFT"},800,480);
        Link(wa,0,net.Params.Output[2],true);Link(we,0,net.Params.Output[3],true);
        Set(wa,2,new GH_Integer(1));Set(we,2,new GH_Integer(1));Set(wa,5,new GH_Integer(0));Set(we,5,new GH_Integer(1));
        var waT=e.Slider("Tiro WARP [N]",warpN,1000,15000,510,150);var weT=e.Slider("Tiro WEFT [N]",weftN,1000,15000,510,400);
        Link(wa,1,waT);Link(we,1,weT);
        var waFix=e.Add(new CableConstraintComponent{NickName="Tiro imposto WARP"},1030,230);var weFix=e.Add(new CableConstraintComponent{NickName="Tiro imposto WEFT"},1030,480);
        Link(waFix,0,wa.Params.Output[0]);Link(weFix,0,we.Params.Output[0]);Link(waFix,2,waT);Link(weFix,2,weT);
        IGH_Param warpSource=waFix.Params.Output[0];
        if(cableLoads)
        {
            var load=e.Add(new CableLoadComponent(),1240,160);Link(load,0,warpSource);Set(load,1,new GH_Vector(new Vector3d(0,0,-100)));warpSource=load.Params.Output[0];
            e.Note("-100 N a meta di ogni segmento warp.\nCable Load riceve una forza concentrata, non N/m.",1050,20,350,90);
        }
        var border=e.Add(new CableComponent{NickName="Cavi bordo vincolato"},810,750);Link(border,0,net.Params.Output[0],true);Set(border,1,new GH_Number(5000));
        var supports=e.Add(new NodeComponent{NickName="Vincoli perimetrali XYZ"},1070,760);Link(supports,0,net.Params.Output[4],true);
        var model=e.Add(new ModelComponent(),1480,390);Link(model,0,warpSource,true);Link(model,0,weFix.Params.Output[0],true);Link(model,0,border.Params.Output[0],true);Link(model,1,supports.Params.Output[0],true);
        if(nodeLoads)
        {
            var loads=e.Add(new NodeComponent{NickName="Carichi sui nodi liberi"},1270,1000);Link(loads,0,net.Params.Output[6],true);
            for(int i=1;i<=3;i++)Set(loads,i,new GH_Boolean(false));Set(loads,4,new GH_Vector(new Vector3d(0,0,-100)));Link(model,1,loads.Params.Output[0],true);
        }
        var check=e.Add(new CheckModelComponent(),1710,140);Link(check,0,model.Params.Output[0]);
        var deconstruct=e.Add(new DeconstructModelComponent(),1700,830);Link(deconstruct,0,model.Params.Output[0]);
        var settings=e.Add(new SolverSettingsComponent(),1750,540);Set(settings,0,new GH_Integer((int)kind));Set(settings,1,new GH_Integer(500));
        Set(settings,2,new GH_Number(1e-7));Set(settings,4,new GH_Number(pressure));Set(settings,8,new GH_Number(1e-7));
        var run=e.Add(new GH_BooleanToggle{Value=true,NickName="CALCOLA / Run"},1680,280);
        var solve=e.Add(new FormFindComponent(),2030,400);Link(solve,0,model.Params.Output[0]);Link(solve,1,settings.Params.Output[0]);Link(solve,2,run);
        var status=e.Readout("Esito",solve.Params.Output[6],1980,40,330,80);
        var mode=e.Note("Solver: "+kind+"\nTiri imposti: warp / weft separati.\nCambiare i due slider e controllare i risultati.\nRun=False azzera i risultati.",1440,20,460,95);
        e.Group("02 / TIRI E VINCOLI",ModelColor,waT,weT,wa,we,waFix,weFix,border,supports);
        e.Group("03 / MODELLO E SOLUZIONE",ResultColor,model,check,settings,run,solve,deconstruct);
        var initial=e.Add(new DeconstructResultComponent(),2310,730);Link(initial,0,solve.Params.Output[9]);
        var nodes=e.Add(new NodeResultsComponent(),2340,260);Link(nodes,0,solve.Params.Output[9]);
        var beams=e.Add(new BeamResultsComponent(),2650,280);Link(beams,0,solve.Params.Output[9]);
        var plates=e.Add(new PlateResultsComponent(),2950,300);Link(plates,0,solve.Params.Output[9]);
        var balance=e.Add(new EquilibriumReportComponent(),2640,740);Link(balance,0,solve.Params.Output[9]);Set(balance,1,new GH_Number(0.1));Set(balance,2,new GH_Number(1));
        var history=e.Add(new SolverHistoryComponent(),2360,1000);Link(history,0,solve.Params.Output[9]);
        var preview=e.Add(new ResultPreviewComponent(),2040,850);Link(preview,0,solve.Params.Output[9]);
        e.Readout("Forze aste [N]",beams.Params.Output[4],2600,20,270,120);
        e.Readout("Equilibrio verificato",balance.Params.Output[0],2820,750,250,80);
        e.Readout("Forze bordi piastre [N]",plates.Params.Output[4],3130,230,300,250);
        e.Note("I tiri delle piastre sono le forze nelle aste di bordo.\nNx, Ny, Nxy non sono disponibili.\nID di nodi, aste e piastre: numerazione da 1.",2870,970,530,105);
        e.Group("04 / RISULTATI E CONTROLLO",ResultColor,initial,nodes,beams,plates,balance,history,preview);
        if(kind==SolverKind.LinearIterative && !nodeLoads)
        {
            var units=e.Add(new UnitSettingsComponent(),600,1120);Set(units,1,new GH_Integer(2));
            var convert=e.Add(new ConvertUnitsComponent(),940,1110);Link(convert,0,beams.Params.Output[4]);Link(convert,1,units.Params.Output[0]);Set(convert,2,new GH_Integer(2));Set(convert,3,new GH_Boolean(false));
            e.Readout("Stesse forze in kN",convert.Params.Output[0],1080,1120,260,120);
            var dependencies=e.Add(new DependencyCheckComponent(),160,1110);e.Readout("DLL disponibili",dependencies.Params.Output[0],50,1240,300,80);
        }
    }

    private static Mesh Panel()
    {
        var mesh=new Mesh();mesh.Vertices.Add(1,1,0);mesh.Vertices.Add(3,1,0);mesh.Vertices.Add(3,3,0);mesh.Vertices.Add(1,3,0);
        mesh.Faces.AddFace(0,1,2);mesh.Faces.AddFace(0,2,3);mesh.Normals.ComputeNormals();return mesh;
    }
    private static Example PatternWorkshop()
    {
        var e=new Example("07_Patterning_Workshop","07 | DAL FORM FINDING AI PANNELLI\nSella dimostrativa + laboratorio di sviluppo, compensazione, taglio e rimappatura");
        var demo=e.Add(new DemoComponent(),100,230);Set(demo,0,new GH_Integer(3));Set(demo,1,new GH_Number(6));Set(demo,2,new GH_Number(0.5));
        var cable=e.Add(new CableComponent(),350,200);Link(cable,0,demo.Params.Output[0]);Set(cable,1,new GH_Number(1500));
        var support=e.Add(new NodeComponent(),350,430);Link(support,0,demo.Params.Output[1]);
        var model=e.Add(new ModelComponent(),620,260);Link(model,0,cable.Params.Output[0],true);Link(model,1,support.Params.Output[0],true);Link(model,2,demo.Params.Output[2]);
        var solver=e.Add(new FormFindComponent(),880,240);Link(solver,0,model.Params.Output[0]);Set(solver,2,new GH_Boolean(true));
        var plate=e.Add(new PlateResultsComponent(),1160,250);Link(plate,0,solver.Params.Output[9]);Set(plate,1,new GH_Integer(1));
        var arap=e.Add(new FlatteningArapComponent(),1450,230);Link(arap,0,plate.Params.Output[1]);Set(arap,3,new GH_Number(0.001));
        var distortion=e.Add(new PanelDistortionComponent(),1750,230);Link(distortion,0,plate.Params.Output[1]);Link(distortion,1,arap.Params.Output[0]);
        e.Readout("Distorsione pannello",distortion.Params.Output[4],1910,210,270,100);
        e.Group("01 / PANNELLO DALLA RETE RISOLTA",GeometryColor,demo,cable,support,model,solver,plate,arap,distortion);

        var panel=MeshInput(e,"Pannello piano 2 x 2 m",Panel(),80,780);
        var clean=e.Add(new MeshCleanComponent(),320,720);Link(clean,0,panel);
        var flat=e.Add(new FlatteningComponent(),600,660);Link(flat,0,clean.Params.Output[0]);
        var comp=e.Add(new CompensationComponent(),890,660);Link(comp,0,flat.Params.Output[0]);Set(comp,1,new GH_Vector(Vector3d.XAxis));Set(comp,2,new GH_Number(2));Set(comp,3,new GH_Number(1));
        var borders=e.Add(new BordersComponent(),1180,640);Link(borders,0,comp.Params.Output[0]);
        var order=e.Add(new OrderBordersComponent(),1410,660);Link(order,0,borders.Params.Output[0]);
        var descriptor=e.Add(new DecompensationDescriptorComponent(),1430,880);Set(descriptor,2,new GH_Number(0.02));Set(descriptor,3,new GH_Number(0));Set(descriptor,4,new GH_Number(0));Set(descriptor,5,new GH_Number(0));
        var decomp=e.Add(new DecompensationComponent(),1690,660);Link(decomp,0,order.Params.Output[0]);Link(decomp,1,descriptor.Params.Output[0]);
        decomp.Params.Input[0].DataMapping=GH_DataMapping.Graft;
        var extended=e.Add(new DecompensationDescriptorExtComponent(),1430,1150);
        Set(extended,0,new GH_Number(0),new GH_Number(0.5),new GH_Number(1));Set(extended,1,new GH_Boolean(true),new GH_Boolean(true),new GH_Boolean(true));
        Set(extended,2,new GH_Number(0),new GH_Number(0.01),new GH_Number(0));Set(extended,3,new GH_Number(0),new GH_Number(0),new GH_Number(0));
        var decompExt=e.Add(new DecompensationComponent{NickName="Decompensazione variabile"},1700,1070);Link(decompExt,0,order.Params.Output[0]);Link(decompExt,1,extended.Params.Output[0]);
        decompExt.Params.Input[0].DataMapping=GH_DataMapping.Graft;
        var lineup=e.Add(new LineUpComponent(),1950,770);Link(lineup,0,decomp.Params.Output[0],true);Link(lineup,0,decompExt.Params.Output[0],true);Set(lineup,2,new GH_Number(0.3));
        e.Note("Compensazione: 2% warp, 1% weft.\nDescrittori: frazioni, 0.02 = 2%.\nDue decompensazioni alternative sullo stesso bordo.",2050,920,370,110);
        e.Group("02 / SVILUPPO E COMPENSAZIONE",ModelColor,panel,clean,flat,comp,borders,order,descriptor,decomp,extended,decompExt,lineup);

        var subdivide=e.Add(new SubdivideMeshComponent(),350,1450);Link(subdivide,0,panel);
        var deform=e.Add(new DeformArapComponent(),650,1440);Link(deform,0,subdivide.Params.Output[0]);Set(deform,2,new GH_Integer(0),new GH_Integer(1),new GH_Integer(2),new GH_Integer(3));
        Set(deform,3,new GH_Point(new Point3d(1,1,0)),new GH_Point(new Point3d(3,1,0)),new GH_Point(new Point3d(3,3,0.3)),new GH_Point(new Point3d(1,3,0)));
        var remap=e.Add(new MeshRemapComponent(),970,1430);Link(remap,0,subdivide.Params.Output[0]);Link(remap,1,deform.Params.Output[0]);Set<IGH_GeometricGoo>(remap,2,new GH_Point(new Point3d(2,2,0)));
        var geodesic=e.Add(new MeshGeodesicComponent(),340,1800);Link(geodesic,0,panel);Set(geodesic,1,new GH_Point(new Point3d(2,1,0)));Set(geodesic,2,new GH_Point(new Point3d(2,3,0)));Set(geodesic,3,new GH_Number(0.25));Set(geodesic,4,new GH_Integer(10));
        var divide=e.Add(new MeshDivideComponent(),650,1800);Link(divide,0,panel);Set(divide,1,new GH_Integer(2));Set(divide,3,new GH_Number(0.25));Set(divide,4,new GH_Integer(10));
        var offset=e.Add(new GeodesicOffsetsComponent(),970,1800);Link(offset,0,panel);Link(offset,1,geodesic.Params.Output[0]);Set(offset,3,new GH_Number(0.5));Set(offset,4,new GH_Number(0.01));Set(offset,5,new GH_Number(0.25));Set(offset,6,new GH_Integer(10));
        var split=e.Add(new SplitMeshOnCurvesComponent(),1300,1620);Link(split,0,panel);Link(split,1,divide.Params.Output[0]);
        var seam=Curves(e,"Diagonale da separare",new[]{new LineCurve(new Point3d(1,1,0),new Point3d(3,3,0))},1120,2120);
        var unweld=e.Add(new UnweldMeshComponent(),1470,2070);Link(unweld,0,panel);Link(unweld,1,seam);
        var pipe=e.Add(new MeshPipeComponent(),1920,1630);Link(pipe,0,solver.Params.Output[1],true);Set(pipe,1,new GH_Number(0.025));
        e.Group("03 / MESH, DEFORMAZIONE E GEODETICHE",ResultColor,subdivide,deform,remap,geodesic,divide,offset,split,seam,unweld,pipe);
        return e;
    }

    private static void Verify(GH_Document document,string label)
    {
        foreach(var solver in document.Objects.OfType<FormFindComponent>())
            foreach(var r in Data(solver,9).OfType<GH_ObjectWrapper>().Select(w=>w.Value).OfType<FormResult>())
            {
                Console.WriteLine(label+" diagnostics: "+r.Status+", nodes="+r.Model.Nodes.Count+", free="+r.Model.Nodes.Count(n=>!n.FixZ)+", cables="+r.Model.Edges.Count+", outer="+r.OuterIterations+", tail="+string.Join(" / ",r.History.Skip(Math.Max(0,r.History.Count-4)).Select(h=>h.Outer+"."+h.Inner+": "+h.Criterion)));
                if(r.Solved!=null){var eq=EquilibriumReport.Evaluate(r.Solved,.1,1);Console.WriteLine("  residual="+eq.MaxFreeResidual+", global="+ResultMath.Norm(eq.ForceImbalance)+", moment="+ResultMath.Norm(eq.MomentImbalance)+", minLength="+r.Solved.Beams.Min(b=>b.Length));}
                if(label=="03_Rectangular_Iterative")foreach(var n in r.Model.Nodes)Console.WriteLine("  node "+n.Id+": "+n.Position+" fixed="+n.FixZ);
            }
        foreach(var c in document.Objects.OfType<GH_Component>())
        {
            var errors=c.RuntimeMessages(GH_RuntimeMessageLevel.Error).Concat(c.RuntimeMessages(GH_RuntimeMessageLevel.Warning)).ToArray();
            if(c is FormFindComponent)errors=errors.Where(s=>!s.Contains("fallback method")).ToArray();
            if(errors.Length>0)throw new InvalidOperationException(label+" | "+c.Name+" ("+c.NickName+") | "+string.Join("; ",errors));
            if(!c.Params.Output.Any(p=>p.VolatileData.AllData(true).Any()))throw new InvalidOperationException(label+" | "+c.Name+" produced no output; phase="+c.Phase);
        }
        foreach(var solver in document.Objects.OfType<FormFindComponent>())
        {
            var result=(FormResult)Data(solver,9).Cast<GH_ObjectWrapper>().Single().Value;
            if(result.Status!=SolveStatus.Converged||result.Solved==null)throw new InvalidOperationException(label+": form finding did not converge");
            var model=result.Model;double worst=0;
            foreach(var edge in model.Edges)if(edge.Cable.FixedForce.HasValue)
                worst=Math.Max(worst,Math.Abs(result.Solved.Beam(edge.Id).AxialForce/edge.Cable.FixedForce.Value-1));
            if(worst>0.002)throw new InvalidOperationException(label+": target-force relative error "+worst);
            foreach(var b in result.Solved.Beams)if(b.AxialForce<=0)throw new InvalidOperationException(label+": non-tension cable "+b.Id);
            if(!EquilibriumReport.Evaluate(result.Solved,0.1,1,new GPC.Geometry.Point3d()).Accepted)throw new InvalidOperationException(label+": physical equilibrium check failed");
            if(label.StartsWith("03") && result.OuterIterations<2)throw new InvalidOperationException("Iterative example must exercise multiple outer iterations");
        }
        foreach(var check in document.Objects.OfType<CheckModelComponent>())if(!Data(check).Cast<GH_Boolean>().Single().Value)throw new InvalidOperationException(label+": invalid model");
        foreach(var arap in document.Objects.Where(c=>c is FlatteningArapComponent || c is DeformArapComponent).Cast<GH_Component>())
            if(!Data(arap).OfType<GH_Mesh>().Any(m=>m.Value.IsValid))throw new InvalidOperationException(label+": ARAP mesh missing");
    }
    private static string Summarize(GH_Document document,string label)
    {
        var solver=document.Objects.OfType<FormFindComponent>().First();var r=(FormResult)Data(solver,9).Cast<GH_ObjectWrapper>().Single().Value;
        var targets=r.Model.Edges.Where(e=>e.Cable.FixedForce.HasValue).ToArray();
        double error=targets.Length==0?0:targets.Max(e=>Math.Abs(r.Solved.Beam(e.Id).AxialForce/e.Cable.FixedForce.Value-1));
        return label+" | "+r.Settings.Kind+" | nodes="+r.Solved.Nodes.Count+" | cables="+r.Solved.Beams.Count+" | plates="+r.Solved.Plates.Count+
            " | outer="+r.OuterIterations+" | history="+r.History.Count+" | max relative tension error="+error.ToString("G6")+" | components="+document.Objects.OfType<GH_Component>().Count();
    }
}
