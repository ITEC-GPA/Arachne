using Arachne.Common.Helpers;
using Arachne.Grasshopper.Helpers;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using TensileLib.Geometry;

namespace Arachne.Grasshopper.Components.Tools
{
    public class CurveToMeshComponent : GH_Component
    {
        private GH_Mesh _mesh;

        /// <summary>
        /// Initializes a new instance of the FormFindComponent class.
        /// </summary>
        public CurveToMeshComponent()
          : base("Curve2Mesh", "C2M", "Create a mesh from a list of curve", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_TOOLS)
        {
            _mesh = new GH_Mesh();
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Curves", "C", "The list of curves", GH_ParamAccess.list);
            pManager.AddIntegerParameter("Max Face Edges", "E", "The maximum number of edges for each faces", GH_ParamAccess.item, 4);
            pManager.AddNumberParameter("Tolerance", "T", "The matching tolerance", GH_ParamAccess.item, 0.01);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "The resulting mesh usefull to geometric operations", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            Message = "";
            List<Curve> curves = new List<Curve>();
            int totalEdges = 0;
            double tol = 0;

            if (!DA.GetDataList(0, curves))
                return;
            DA.GetData(1, ref totalEdges);
            DA.GetData(2, ref tol);

            try
            {
                var mesh = FormFindingHelper.GenerateMeshFromCurves(curves, totalEdges - 1);
                _mesh = new GH_Mesh(mesh);

                /*
                Model ff_model = Common.Helpers.FormFindingHelper.AssemblyModel(curves, tol, out List<string> warnings);

                _mesh = new GH_Mesh();
                {
                    // Generate the plates
                    try
                    {
                        ff_model.GeneratePlatesFromBeams();
                    }
                    catch (Exception)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Fail to generate the mesh");
                    }

                    Mesh mesh = new Mesh();
                    Dictionary<int, int> idRhinoIndexMap = new Dictionary<int, int>();

                    for (int i = 0; i < ff_model.Nodes.Count; i++)
                    {
                        Node ff_node = ff_model.Nodes.ElementAt(i);
                        int tag = mesh.Vertices.Add(ff_node.X, ff_node.Y, ff_node.Z);
                        idRhinoIndexMap.Add(ff_node.Id, tag);
                    }

                    for (int i = 0; i < ff_model.Plates.Count; i++)
                    {
                        Plate ff_plate = ff_model.Plates.ElementAt(i);
                        int i1 = idRhinoIndexMap[ff_plate.Node1.Id];
                        int i2 = idRhinoIndexMap[ff_plate.Node2.Id];
                        int i3 = idRhinoIndexMap[ff_plate.Node3.Id];
                        int i4 = -1;

                        if (ff_plate.PlateType == Plate.PlateTypes.Quad4)
                            i4 = idRhinoIndexMap[ff_plate.Node4.Id];

                        if (ff_plate.PlateType == Plate.PlateTypes.Quad4)
                            mesh.Faces.AddFace(i1, i2, i3, i4);
                        else if (ff_plate.PlateType == Plate.PlateTypes.Tri3)
                            mesh.Faces.AddFace(i1, i2, i3);
                    }
                    mesh.Compact();
                    mesh.RebuildNormals();

                    _mesh = new GH_Mesh(mesh);
                }

                Message = "Done";*/
            }
            catch (Exception e)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message);
                Message = "Error";
            }

            DA.SetData(0, _mesh);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("923d40fe-63a7-4d04-84f3-6b8f825cabb9");

        public override GH_Exposure Exposure => GH_Exposure.primary;
    }
}