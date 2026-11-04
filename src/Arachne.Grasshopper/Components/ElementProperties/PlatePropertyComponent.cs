using System;
using Arachne.Common.Analysis;
using Arachne.Grasshopper.Helpers;
using Arachne.Grasshopper.DataTypes;
using Grasshopper.Kernel;

namespace Arachne.Grasshopper.Components.ElementProperties
{
    public class PlatePropertyComponent : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the PlatePropertyComponent class.
        /// </summary>
        public PlatePropertyComponent()
          : base("Plate Property", "PP", "Plate properties", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PROPERTIES)
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Property name", GH_ParamAccess.item);
            pManager.AddNumberParameter("Thickness", "T", "The thickness", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "M", "Material", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Property", "PP", "Plate property", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            double thickness = 0;
            GH_Material material = null;
            if (!DA.GetData(0, ref name) || !DA.GetData(1, ref thickness) || !DA.GetData(2, ref material)) return;
            if (!(thickness > 0)) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "The thickness must be greater than 0"); return; }
            GPC.Model.ElementProperties.PlateProperty property;
            if (material.Membrane != null)
            {
                // A fabric: its own values per unit width (the thickness only says that the plates weigh, as before). A new property
                // with its own identity and name: the membrane material it comes from is immutable and shared (GpcGoo), and a copy that
                // kept its identity would be one property with two names for Build Model, which merges the properties by identity.
                property = Renamed(material.Membrane, name);
            }
            else if (material.Material != null)
            {
                try { property = new GPC.Model.ElementProperties.ElasticPlateProperty(name, material.Material, thickness, thickness, GPC.Model.ElementProperties.ElasticPlateFormulation.Membrane); }
                catch (ArgumentException e) { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, e.Message); return; }
            }
            else { AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid material"); return; }
            DA.SetData(0, new GH_PlateProperty(property));
        }

        /// <summary>The values of <paramref name="membrane"/> in a new property named <paramref name="name"/> (new identity).</summary>
        internal static GPC.Model.ElementProperties.MembranePlateProperty Renamed(GPC.Model.ElementProperties.MembranePlateProperty membrane, string name)
            => new GPC.Model.ElementProperties.MembranePlateProperty(name)
            {
                PhysicalThickness = membrane.PhysicalThickness, MassPerArea = membrane.MassPerArea, StiffnessWarp = membrane.StiffnessWarp, StiffnessWeft = membrane.StiffnessWeft,
                ShearStiffness = membrane.ShearStiffness, NuWarpWeft = membrane.NuWarpWeft, StrengthWarp = membrane.StrengthWarp, StrengthWeft = membrane.StrengthWeft
            };

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        // New GUID: FeMM had the SAP shell Type and two thicknesses (membrane and bending)
        public override Guid ComponentGuid => new Guid("77ce7fd4-99f4-4bc1-9d3f-5c6ce0955ab1");
    }
}
