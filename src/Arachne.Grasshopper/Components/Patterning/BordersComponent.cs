using Arachne.Grasshopper.Helpers;
using System;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Grasshopper.Kernel.Parameters;
using System.Collections.Generic;
using Rhino;
namespace Arachne.Grasshopper.Components.Patterning
{
    public class BordersComponent : GH_Component
    {


        /// <summary>
        /// Initializes a new instance of the BordersComponent class.
        /// </summary>
        public BordersComponent()
          : base("Borders", "Bs", "Extract the borders of a mesh, joined with a tolerance", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PATTERNING)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The mesh", GH_ParamAccess.item);
            pManager.AddAngleParameter("Angle", "A", "Tolerance angle of consecutive pieces of a border", GH_ParamAccess.item, 15);
            ((Param_Number)pManager[pManager.ParamCount - 1]).UseDegrees = true;
            pManager.AddBooleanParameter("Smooth", "S", "Smooth the polyline with a curve", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddCurveParameter("Borders", "Bs", "The borders of the mesh", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Mesh m = null;
            double angle = 0;
            bool smooth = true;

            if (!DA.GetData(0, ref m))
                return;
            if (!DA.GetData(1, ref angle))
                return;
            if (!DA.GetData(2, ref smooth))
                return;

            Param_Number angleParameter = Params.Input[1] as Param_Number;
            if (angleParameter != null)
            {
                if (angleParameter.UseDegrees)
                {
                    angle *= Math.PI / 180;
                }
            }
            else
            {
                return;
            }

            Polyline[] pls = m.GetNakedEdges();
            List<Polyline> borders = BreakAtPolylines(pls, angle);

            if (smooth)
            {
                List<Curve> curves;

                curves = new List<Curve>();
                foreach (Polyline pl in borders)
                {
                    List<Point3d> points;
                    points = new List<Point3d>();
                    for (int i = 0; i < pl.Count - 1; i++)
                    {
                        points.Add(pl[i]);
                        //points.Add(0.5 * (pl[i] + pl[i + 1]));
                    }
                    points.Add(pl[pl.Count - 1]);
                    curves.Add(Curve.CreateControlPointCurve(points));
                }
                DA.SetDataList(0, curves);
            }
            else
            {
                DA.SetDataList(0, borders);
            }
        }

        internal static List<Polyline> BreakAtPolylines(Polyline[] pls, double angle)
        {
            //i don't use the follow command for the rest because it break also at start and end of the polyline
            List<object> lines = new List<object>();
            for (int i = 0; i < pls.Length; i++)
            {
                Line[] array = pls[i].GetSegments();
                for (int j = 0; j < array.Length; j++)
                {
                    lines.Add(array[j]);
                }
            }

            List<Polyline> result = new List<Polyline>();

            do
            {
                Vector3d startingDir = new Vector3d();
                Vector3d endingDir = new Vector3d();
                Polyline seed = null;

                for (int i = 0; i < lines.Count; i++)
                {
                    if (lines[i] != null)
                    {
                        Line line;
                        line = (Line)lines[i];
                        seed = new Polyline(new Point3d[] { line.PointAt(0), line.PointAt(1) });
                        startingDir = line.Direction;
                        endingDir = startingDir;
                        lines[i] = null;
                        break;
                    }
                }

                if (seed == null)
                {
                    break;
                }

                do
                {
                    double TOLL = RhinoDoc.ActiveDoc.ModelAbsoluteTolerance;
                    bool exit = true;
                    for (int i = 0; i < lines.Count; i++)
                    {
                        if (lines[i] != null)
                        {
                            Line line = (Line)lines[i];
                            Point3d startPI = line.PointAt(0);
                            Point3d lastPI = line.PointAt(1);
                            Vector3d dir = line.Direction;

                            if (startPI.DistanceToSquared(seed.Last) < TOLL && dir.IsParallelTo(endingDir, angle) == 1)
                            {
                                //first attached to last of seed
                                seed.Add(lastPI);
                                endingDir = dir;
                                lines[i] = null;
                                exit = false;
                                break;
                            }
                            else if (lastPI.DistanceToSquared(seed.Last) < TOLL && dir.IsParallelTo(endingDir, angle) == -1)
                            {
                                //last attached to last of seed
                                seed.Add(startPI);
                                endingDir = -dir;
                                lines[i] = null;
                                exit = false;
                                break;
                            }
                            else if (lastPI.DistanceToSquared(seed.First) < TOLL && dir.IsParallelTo(startingDir, angle) == 1)
                            {
                                //last attached to first of seed
                                seed.Insert(0, startPI);
                                startingDir = dir;
                                lines[i] = null;
                                exit = false;
                                break;
                            }
                            else if (startPI.DistanceToSquared(seed.First) < TOLL && dir.IsParallelTo(startingDir, angle) == -1)
                            {
                                //start attached to first of seed
                                seed.Insert(0, lastPI);
                                startingDir = -dir;
                                lines[i] = null;
                                exit = false;
                                break;
                            }
                        }
                    }
                    if (exit)
                    {
                        break;
                    }

                } while (true == true);

                result.Add(seed);

            } while (true == true);

            return result;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("810c4b07-ff37-5c08-b92e-a7a9a288ef41");
    }
}