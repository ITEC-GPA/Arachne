using Rhino.Geometry;
using Arachne.Common.Helpers;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.Helpers;
using Arachne.Grasshopper.ComponentAttributes;
using Arachne.Grasshopper.Components.Parameters;
using Arachne.Grasshopper.DataTypes;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using System;

namespace Arachne.Grasshopper.Components.Loads
{
    /// <summary>
    /// FeMM beam distributed load, limited to the loads handled by the form finding:
    /// uniform and keystone forces in a global coordinate system.
    /// </summary>
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
            m_attributes = new ComponentWithImageAttributes(this, ComponentImages.Get("BeamDistributedLoadType1Image"));
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
            type_param.AddNamedValue(BeamLoadKinds.LoadSchema.Uniform.GetDescription(), (int)BeamLoadKinds.LoadSchema.Uniform);
            type_param.AddNamedValue(BeamLoadKinds.LoadSchema.Keystone.GetDescription(), (int)BeamLoadKinds.LoadSchema.Keystone);

            CoordinateSystemParam cs_param = new CoordinateSystemParam
            {
                Optional = true
            };
            pManager.AddParameter(cs_param, "Coordinate System", "CS", "The coordinate system of the load (Global if not given)", GH_ParamAccess.item);

            pManager.AddIntegerParameter("Direction", "D", "The load direction", GH_ParamAccess.item);
            Param_Integer dir_param = (Param_Integer)pManager[4];
            string[] options = Enum.GetNames(typeof(BeamLoadKinds.LoadDirection));
            Array values = Enum.GetValues(typeof(BeamLoadKinds.LoadDirection));
            for (int i = 0; i < options.Length; i++)
            {
                int value = (int)values.GetValue(i);
                if (value >= 0)
                    dir_param.AddNamedValue(options[i].Replace('_', ' '), value);
            }

            pManager.AddNumberParameter("PA", "PA", "The PA value", GH_ParamAccess.item);
            pManager[5].Optional = true;
            pManager.AddNumberParameter("PB", "PB", "The PB value (keystone only)", GH_ParamAccess.item);
            pManager[6].Optional = true;
            pManager.AddNumberParameter("a", "a", "The distance ratio (0-1) from the starting node", GH_ParamAccess.item, 0);
            pManager.AddNumberParameter("b", "b", "The distance ratio (0-1) from the ending node", GH_ParamAccess.item, 0);
        }

        private void SetSchemaImage(int schema)
        {
            if (m_attributes is ComponentWithImageAttributes attributes)
                attributes.Bitmap = ComponentImages.Get(
                schema == (int)BeamLoadKinds.LoadSchema.Keystone ? "BeamDistributedLoadType2Image" : "BeamDistributedLoadType1Image");
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
            int schema = 0, dir = 0;
            if (!DA.GetData("Beam", ref beam)) return;
            if (!DA.GetData("LoadCase", ref lc)) return;
            if (!DA.GetData("Type", ref schema)) return;
            DA.GetData("Coordinate System", ref cs);
            bool has_dir = DA.GetData("Direction", ref dir);
            double pa = 0, pb = 0, a = 0, b = 0;
            bool pa_has_value = DA.GetData("PA", ref pa);
            bool pb_has_value = DA.GetData("PB", ref pb);
            if (!DA.GetData("a", ref a)) return;
            if (!DA.GetData("b", ref b)) return;
            var coordinateSystem = cs?.Value ?? GPC.Geometry.CoordinateSystem.Global;
            bool known_dir = Enum.IsDefined(typeof(BeamLoadKinds.LoadDirection), dir) && dir >= 0;
            Message = string.Format("Coordinate System: {0}\nDirection: {1}", string.IsNullOrEmpty(coordinateSystem.Name) ? "Global" : coordinateSystem.Name,
                known_dir ? Enum.GetName(typeof(BeamLoadKinds.LoadDirection), dir).Replace('_', ' ') : "?");
            bool input_errors = false;
            if (!has_dir || !known_dir) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter Direction failed to collect data"); input_errors = true; }
            switch (schema)
            {
                case (int)BeamLoadKinds.LoadSchema.Uniform:
                    if (!pa_has_value) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameter 'PA' failed to collect data"); input_errors = true; }
                    break;
                case (int)BeamLoadKinds.LoadSchema.Keystone:
                    if (!pa_has_value || !pb_has_value) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "Input parameters 'PA', 'PB' failed to collect data"); input_errors = true; }
                    break;
                default:
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Load type must be Uniform (0) or Keystone (1)");
                    return;
            }
            SetSchemaImage(schema);
            if (a < 0 || b < 0) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The 'a' and 'b' values must not be negative"); input_errors = true; }
            if (a + b >= 1) { AddRuntimeMessage(GH_RuntimeMessageLevel.Warning, "The sum of a+b must less than 1"); input_errors = true; }
            if (input_errors) return;
            // The direction of the load as the former assembly: an axis of the coordinate system, or -Z for the gravity; the
            // projected loads act on the length projected on the plane normal to the direction.
            var direction = Vector3d.Zero; bool projected = false;
            switch ((BeamLoadKinds.LoadDirection)dir)
            {
                case BeamLoadKinds.LoadDirection.X_Projected: projected = true; goto case BeamLoadKinds.LoadDirection.X;
                case BeamLoadKinds.LoadDirection.X: direction = BeamLoadKinds.ToGlobal(coordinateSystem, Vector3d.XAxis); break;
                case BeamLoadKinds.LoadDirection.Y_Projected: projected = true; goto case BeamLoadKinds.LoadDirection.Y;
                case BeamLoadKinds.LoadDirection.Y: direction = BeamLoadKinds.ToGlobal(coordinateSystem, Vector3d.YAxis); break;
                case BeamLoadKinds.LoadDirection.Z_Projected: projected = true; goto case BeamLoadKinds.LoadDirection.Z;
                case BeamLoadKinds.LoadDirection.Z: direction = BeamLoadKinds.ToGlobal(coordinateSystem, Vector3d.ZAxis); break;
                case BeamLoadKinds.LoadDirection.Gravity_Projected: projected = true; goto case BeamLoadKinds.LoadDirection.Gravity;
                case BeamLoadKinds.LoadDirection.Gravity: direction = -Vector3d.ZAxis; break;
            }
            var start = direction * pa;
            var end = direction * (schema == (int)BeamLoadKinds.LoadSchema.Uniform ? pa : pb);
            var copy = new GH_Beam(beam);
            var line = copy.Value.Line;
            var loadCase = GPC.Model.Core.ModelValues.CopyValue(lc.Value);
            GPC.Geometry.Vector3d G(Vector3d v) => new GPC.Geometry.Vector3d(v.X, v.Y, v.Z);
            copy.Value.Assignments.Loads.Add(new GPC.Model.Loads.Assignments.BeamLoadAssignment
            {
                Start = a, End = 1 - b,
                // The distances as typed (the end fraction 1 - b does not give back b exactly): the former arithmetic uses b
                SourceRecord = TensileModelAdapter.BeamLoadRecord(a, b),
                StartIntensity = new GPC.Model.Loads.LineLoad(G(start), new GPC.Geometry.Vector3d(0, 0, 0), line, loadCase),
                EndIntensity = new GPC.Model.Loads.LineLoad(G(end), new GPC.Geometry.Vector3d(0, 0, 0), line, loadCase),
                LengthConvention = projected ? GPC.Model.Loads.Assignments.BeamLoadLengthConvention.ProjectedLength : GPC.Model.Loads.Assignments.BeamLoadLengthConvention.ActualLength,
                ProjectionPlaneNormal = projected ? G(direction) : null
            });
            DA.SetData(0, copy);
        }

        public override bool Read(GH_IReader reader)
        {
            bool success = base.Read(reader);
            // After the document was read, the image follows the saved load type
            if (((Param_Integer)Params.Input[2]).PersistentData.Branches.Count > 0)
                SetSchemaImage(((Param_Integer)Params.Input[2]).PersistentData.Branches[0][0].Value);
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
