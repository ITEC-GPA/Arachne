using System;
using System.Collections.Generic;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Sections
{
    public class BeamSectionBrepComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamSectionCComponent class.
        /// </summary>
        public BeamSectionBrepComponent()
          : base("Brep Section", "BS", "Defines a section composed by brep faces", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_SECTIONS)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddBrepParameter("Breps", "Bs", "Breps with only one face coplanar", GH_ParamAccess.list);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Section", "S", "The brep section", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            List<Brep> breps = new List<Brep>();
            if (!DA.GetDataList(0, breps)) return;

            //Test that all brep faces are coplanar
            Vector3d normal = Vector3d.Unset;

            List<GPC.Geometry.Shape2d> shapesList = new List<GPC.Geometry.Shape2d>();
            for (int j = 0; j < breps.Count; j++)
            {
                Brep b = breps[j];
                foreach (BrepFace bf in b.Faces)
                {
                    if (!bf.IsPlanar())
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Brep faces must be planar");
                        return;
                    }
                    if (normal == Vector3d.Unset)
                    {
                        normal = bf.NormalAt(0, 0);
                        normal.Unitize();
                    }
                    else
                    {
                        Vector3d faceNorm = bf.NormalAt(0, 0);
                        faceNorm.Unitize();
                        if (Math.Abs(faceNorm * normal) < 0.999)
                        {
                            AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Brep faces must be coplanar");
                        }
                    }
                }

                Transform tr = Transform.PlaneToPlane(new Plane(Point3d.Origin, normal), Plane.WorldXY);

                foreach (BrepFace bf in b.Faces)
                {
                    if (!bf.OuterLoop.To3dCurve().TryGetPolyline(out Polyline pl))
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Brep loops must be polylines");
                        return;
                    }
                    int lastIndex = pl.IsClosed ? pl.Count - 1 : pl.Count;
                    GPC.Geometry.Polygon2d fill = new GPC.Geometry.Polygon2d();
                    for (int i = 0; i < lastIndex; i++)
                    {
                        Point3d local = new Point3d(pl[i]);
                        local.Transform(tr);
                        fill.Add(local.X, local.Y);
                    }
                    List<GPC.Geometry.Polygon2d> holes = null;
                    foreach (BrepLoop bl in bf.Loops)
                    {
                        if (bl != bf.OuterLoop)
                        {
                            //is an hole
                            GPC.Geometry.Polygon2d hole;
                            if (!bl.To3dCurve().TryGetPolyline(out pl))
                            {
                                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Brep loops must be polylines");
                                return;
                            }
                            lastIndex = pl.IsClosed ? pl.Count - 1 : pl.Count;
                            hole = new GPC.Geometry.Polygon2d();
                            for (int i = 0; i < lastIndex; i++)
                            {
                                Point3d local = new Point3d(pl[i]);
                                local.Transform(tr);
                                hole.Add(local.X, local.Y);
                            }
                            if (holes == null) holes = new List<GPC.Geometry.Polygon2d>();
                            holes.Add(hole);
                        }
                    }
                    shapesList.Add(new GPC.Geometry.Shape2d(fill, holes?.ToArray()));
                }
            }

            if (shapesList.Count > 0)
            {
                SectionModel sectionModel = new SectionModel()
                {
                    SectionType = SectionModel.SectionTypes.GenericShapes,
                    GenericShapes = new Common.Geometry.Shapes(shapesList)
                };

                GH_BeamSection section = new GH_BeamSection(sectionModel);

                DA.SetData(0, section);
            }
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("b105ee76-1e25-432e-a2b6-8b0ae1b7ed54");
    }
}