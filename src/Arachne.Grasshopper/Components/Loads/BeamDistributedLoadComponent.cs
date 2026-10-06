using System;
using System.Collections.Generic;
using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.ComponentAttributes;
using Arachne.Grasshopper.Components.Parameters;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Rhino.Geometry;

namespace Arachne.Grasshopper.Components.Loads
{
    public class BeamDistributedLoadComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the BeamDistributedLoadComponent class.
        /// </summary>
        public BeamDistributedLoadComponent()
          : base("Beam Distributed Load", "BDL", "Assign a beam distributed load", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_LOADS)
        {
        }

        public override void CreateAttributes()
        {
            ComponentWithImageAttributes attr = new ComponentWithImageAttributes(this, ComponentImages.Get("BeamDistributedLoadType1Image"));
            attr.AddOverlappedBitmap(new System.Drawing.PointF(5, -65), ComponentImages.Get("LocalAxisImage"));
            m_attributes = attr;
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The beam where add the distributed load", GH_ParamAccess.item);
            pManager.AddGenericParameter("LoadCase", "LC", "The load case", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Type", "T", "Load Type", GH_ParamAccess.item, 0);
            Param_Integer type_param = (Param_Integer)pManager[2];
            Array values = Enum.GetValues(typeof(BeamDistributedLoadModel.LoadSchema));
            foreach (BeamDistributedLoadModel.LoadSchema val in values)
                type_param.AddNamedValue(val.GetDescription(), (int)val);

            CoordinateSystemParam cs_param = new CoordinateSystemParam
            {
                Optional = true
            };
            cs_param.ObjectChanged += Param_CoordinateSystem_ObjectChanged;
            pManager.AddParameter(cs_param, "Coordinate System", "CS", "The coordinate system of the load", GH_ParamAccess.item);

            pManager.AddIntegerParameter("Direction", "D", "The load direction", GH_ParamAccess.item);
            Param_Integer dir_param = (Param_Integer)pManager[4];
            dir_param.AddNamedValue("1", 0);
            dir_param.AddNamedValue("2", 1);
            dir_param.AddNamedValue("3", 2);
            dir_param.Optional = true;

            pManager.AddNumberParameter("P1", "P1", "The P1 value", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("PA", "PA", "The PA value", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("PB", "PB", "The PB value", GH_ParamAccess.item);
            pManager[7].Optional = true;
            pManager.AddNumberParameter("P2", "P2", "The P2 value", GH_ParamAccess.item);
            pManager[8].Optional = true;
            pManager.AddNumberParameter("a", "a", "The distance ratio (0-1) from the starting node", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("b", "b", "The distance ratio (0-1) from the ending node", GH_ParamAccess.item, 0);
        }

        private void Param_CoordinateSystem_ObjectChanged(IGH_DocumentObject sender, GH_ObjectChangedEventArgs e)
        {
            // When the user changes the coordinate system, we unset the Direction parameter input.
            Param_Integer dir_param = (Param_Integer)Params.Input[4];
            dir_param.PersistentData.ClearData();

            // Change the Direction input choices
            SetDirectionContextMenuChoices();
        }

        private void SetDirectionContextMenuChoices()
        {
            CoordinateSystemParam cs_param = (CoordinateSystemParam)Params.Input[3];
            Param_Integer dir_param = (Param_Integer)Params.Input[4];
            dir_param.ClearNamedValues();
            if (cs_param.PersistentData.Branches.Count == 0)
            {
                dir_param.AddNamedValue("1", 0);
                dir_param.AddNamedValue("2", 1);
                dir_param.AddNamedValue("3", 2);
            }
            else
            {
                string[] options = Enum.GetNames(typeof(BeamDistributedLoadModel.LoadDirection));
                for (int i = 0; i < options.Length; i++)
                    dir_param.AddNamedValue(options[i].Replace('_', ' '), i);
            }
        }

        private void SetSchemaImage()
        {
            Param_Integer type_param = (Param_Integer)Params.Input[2];
            switch (type_param.PersistentData.Branches[0][0].Value)
            {
                case 0:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType1Image");
                    break;
                case 1:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType2Image");
                    break;
                case 2:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType3Image");
                    break;
                case 3:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType4Image");
                    break;
                case 4:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType5Image");
                    break;
                case 5:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType6Image");
                    break;
            }
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Beam", "B", "The modified beam", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            GH_Beam beam = null;
            GH_LoadCase lc = null;
            GH_CoordinateSystem cs = null;
            int schema = 0;
            int dir = 0;
            if (!DA.GetData("Beam", ref beam))
                return;
            if (!DA.GetData("LoadCase", ref lc))
                return;
            if (!DA.GetData("Type", ref schema))
                return;
            bool has_cs = DA.GetData("Coordinate System", ref cs);
            bool has_dir = DA.GetData("Direction", ref dir);

            double p1 = 0, pa = 0, pb = 0, p2 = 0;
            double a = 0, b = 0;
            bool p1_has_value = DA.GetData("P1", ref p1);
            bool pa_has_value = DA.GetData("PA", ref pa);
            bool pb_has_value = DA.GetData("PB", ref pb);
            bool p2_has_value = DA.GetData("P2", ref p2);
            if (!DA.GetData("a", ref a))
                return;
            if (!DA.GetData("b", ref b))
                return;

            Message = string.Format("Coordinate System: {0}\nDirection: {1}", cs == null ? "Local" : cs.Value.Name, cs == null ?
                string.Format("Local axis {0}", dir + 1) :
                Enum.GetName(typeof(BeamDistributedLoadModel.LoadDirection), dir).Replace('_', ' '));

            if (lc.Value.NonStructuralMassAcceleration)
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The connected LoadCase have the Non-structural mass flag set to on");

            bool input_errors = false;
            if (!has_dir)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter Direction failed to collect data");
                input_errors = true;
            }

            switch (schema)
            {
                case 0:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType1Image");
                    if (!pa_has_value)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter 'PA' failed to collect data");
                        input_errors = true;
                    }
                    break;
                case 1:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType2Image");
                    if (!pa_has_value || !pb_has_value)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'PA', 'PB' failed to collect data");
                        input_errors = true;
                    }
                    break;
                case 2:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType3Image");
                    if (!p1_has_value || !pa_has_value || !p2_has_value)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'P1', 'PA', 'P2' failed to collect data");
                        input_errors = true;
                    }
                    if (a == 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter 'a' must be greater than 0");
                        input_errors = true;
                    }
                    break;
                case 3:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType4Image");
                    if (!p1_has_value || !pa_has_value || !pb_has_value)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'P1', 'PA', 'PB' failed to collect data");
                        input_errors = true;
                    }
                    if (a == 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter 'a' must be greater than 0");
                        input_errors = true;
                    }
                    break;
                case 4:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType5Image");
                    if (!pa_has_value || !pb_has_value || !p2_has_value)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'PA', 'PB', 'P2' failed to collect data");
                        input_errors = true;
                    }
                    if (b == 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter 'b' must be greater than 0");
                        input_errors = true;
                    }
                    break;
                case 5:
                    ((ComponentWithImageAttributes)m_attributes).Bitmap = ComponentImages.Get("BeamDistributedLoadType6Image");
                    if (!p1_has_value || !pa_has_value || !pb_has_value || !p2_has_value)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'P1', 'PA', 'PB', 'P2' failed to collect data");
                        input_errors = true;
                    }
                    if (a == 0 || b == 0)
                    {
                        AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'a' and 'b' must be greater than 0");
                        input_errors = true;
                    }
                    break;
            }
            if (a >= 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The 'a' value must be lower than 1");
                input_errors = true;
            }
            if (b >= 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The 'b' value must be lower than 1");
                input_errors = true;
            }
            if (a + b >= 1)
            {
                AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The sum of a+b must less than 1");
                input_errors = true;
            }


            if (input_errors)
                return;

            BeamDistributedLoadModel.LoadSchema load_schema = (BeamDistributedLoadModel.LoadSchema)schema;
            BeamDistributedLoadModel.LoadData load_value = new BeamDistributedLoadModel.LoadData()
            {
                LoadSchema = load_schema,
                CoordinateSystem = cs?.Value,
                LoadDirection = (BeamDistributedLoadModel.LoadDirection)dir,
                P1 = p1,
                PA = pa,
                PB = pb,
                P2 = p2,
                A = a,
                B = b
            };
            BeamDistributedLoadModel load = new BeamDistributedLoadModel(lc.Value, load_value, BeamDistributedLoadModel.LoadType.Force);
            GH_Beam newBeam = new GH_Beam(beam);
            newBeam.Value.Loads.Add(load);
            DA.SetData(0, newBeam);
        }

        public override bool Read(GH_IReader reader)
        {
            bool success = base.Read(reader);
            // After the document was read, we have to set the right Direction input choices.
            SetDirectionContextMenuChoices();
            SetSchemaImage();

            return success;
        }

        public override GH_Exposure Exposure => GH_Exposure.secondary;

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("19f2292f-70a7-4fce-bc0d-2f8923829b69");
    }
}