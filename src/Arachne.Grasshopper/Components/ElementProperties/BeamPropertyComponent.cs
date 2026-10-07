using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Arachne.Grasshopper.Helpers;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using System;
using System.Collections.Generic;

namespace Arachne.Grasshopper.Components.ElementProperties
{
    public class BeamPropertyComponent : GH_Component
    {
        protected Dictionary<int, string> _propertyTypeOptions = new Dictionary<int, string>() {
            { 1, "Cable [1]" },
            { 2, "Truss [2]" },
            { 3, "Cutoff Bar [3]" },
            { 5, "Beam [5]" },
            { 8, "Connection [8]" } };

        private bool _disableSolution;

        /// <summary>
        /// Initializes a new instance of the BeamPropertyComponent class.
        /// </summary>
        public BeamPropertyComponent()
          : base("Beam Property", "BP", "Beam properties", CategoryNameConstants.CATEGORY_ARACHNE, CategoryNameConstants.SUBCATEGORY_PROPERTIES)
        {
            _disableSolution = false;
        }

        public override void ExpireSolution(bool recompute)
        {
            if (!_disableSolution)
            {
                try
                {
                    _disableSolution = true;
                    IGH_Param typeParam = Params.Input[1];
                    typeParam.CollectData();
                    if (typeParam.Name == "Type" && typeParam.VolatileDataCount > 0)
                    {
                        bool showAdditionalInputs = false;
                        bool setMessage = true;
                        for (int i = 0; i < typeParam.VolatileData.StructureProxy.Count; i++)
                        {
                            System.Collections.IList path = typeParam.VolatileData.StructureProxy[i];
                            for (int k = 0; k < path.Count; k++)
                            {
                                GH_Integer obj = (GH_Integer)path[k];
                                if (obj != null)
                                {
                                    if (setMessage)
                                    {
                                        Message = _propertyTypeOptions[obj.Value];
                                        setMessage = false;
                                    }
                                    if ((BeamPropertyType)obj.Value == BeamPropertyType.CutoffBar)
                                    {
                                        showAdditionalInputs = true;
                                        break;
                                    }
                                }
                            }
                            if (showAdditionalInputs) break;
                        }

                        if (showAdditionalInputs)
                        {
                            if (Params.Input.Count == 4)
                            {
                                Param_Number number;
                                number = new Param_Number
                                {
                                    Access = GH_ParamAccess.item,
                                    Name = "Max tension",
                                    NickName = "MT",
                                    Description = "Maximum tension for cutoff bars"
                                };
                                Params.RegisterInputParam(number);

                                number = new Param_Number
                                {
                                    Access = GH_ParamAccess.item,
                                    Name = "Max compression",
                                    NickName = "MC",
                                    Description = "Maximum compression for cutoff bars"
                                };
                                Params.RegisterInputParam(number);

                                Param_Integer integer;
                                integer = new Param_Integer
                                {
                                    Access = GH_ParamAccess.item,
                                    Name = "Cut off type",
                                    NickName = "T",
                                    Description = "Type of cutoff bar"
                                };
                                AddNamedValuesCutOffType(integer);
                                Params.RegisterInputParam(integer);

                                Param_Boolean boolean;
                                boolean = new Param_Boolean
                                {
                                    Access = GH_ParamAccess.item,
                                    Name = "Keep mass",
                                    NickName = "KM",
                                    Description = "Keep mass option for cutoff bars"
                                };
                                Params.RegisterInputParam(boolean);

                                Params.OnParametersChanged();
                            }
                        }
                        else
                        {
                            if (Params.Input.Count > 4)
                            {
                                for (int i = Params.Input.Count - 1; i > 3; i--)
                                {
                                    Params.UnregisterInputParameter(Params.Input[i]);
                                }
                                Params.OnParametersChanged();
                            }
                        }

                        //i fullname non sono visibili e non lo saranno, vedi
                        //https://discourse.mcneel.com/t/changing-input-parameter-names-always-shows-nickname/54071
                    }
                    base.ExpireSolution(recompute);
                }
                catch (Exception)
                {
                    AddRuntimeMessage(GH_RuntimeMessageLevel.Error, "Invalid property type");
                }
                finally
                {
                    _disableSolution = false;
                }
            }
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "N", "Property name", GH_ParamAccess.item);
            int i = pManager.AddIntegerParameter("Type", "T", "The property type", GH_ParamAccess.item);
            Param_Integer mtParam = pManager[i] as Param_Integer;
            foreach (KeyValuePair<int, string> v in _propertyTypeOptions)
                mtParam.AddNamedValue(v.Value, v.Key);
            pManager.AddGenericParameter("Section", "S", "Section", GH_ParamAccess.item);
            pManager.AddGenericParameter("Material", "M", "Material", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddGenericParameter("Property", "BP", "Beam property", GH_ParamAccess.item);
        }

        private void AddNamedValuesCutOffType(Param_Integer integer)
        {
            Array arr = Enum.GetValues(typeof(CutoffType));
            for (int i = 0; i < arr.Length; i++)
            {
                if ((CutoffType)arr.GetValue(i) != CutoffType.Undefined)
                {
                    integer.AddNamedValue(Enum.GetName(typeof(CutoffType), arr.GetValue(i)), (int)(CutoffType)arr.GetValue(i));
                }
            }
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string name = "";
            int type = 0;
            GH_BeamSection section = null;
            DataTypes.GH_Material material = null;
            double maxTensionCB = 0;
            double maxCompressionCB = 0;
            int cutOffTypeCB = 0;
            bool keepMassCB = false;

            if (!DA.GetData("Name", ref name))
                return;
            if (!DA.GetData("Type", ref type))
                return;
            if (!DA.GetData("Section", ref section))
                return;
            if (!DA.GetData("Material", ref material))
                return;
            if (Params.Input.Count > 4)
            {
                if (!DA.GetData(4, ref maxTensionCB))
                    return;
                if (!DA.GetData(5, ref maxCompressionCB))
                    return;
                if (!DA.GetData(6, ref cutOffTypeCB))
                    return;
                if (!DA.GetData(7, ref keepMassCB))
                    return;
            }

            SectionModel sectionModel = new SectionModel();

            if (section.SectionType != SectionModel.SectionTypes.Generic)
            {
                sectionModel.SectionType = section.SectionType;
                sectionModel.D = section.D;
                sectionModel.B = section.B;
                sectionModel.B1 = section.B1;
                sectionModel.B2 = section.B2;
                sectionModel.L = section.L;
                sectionModel.T = section.T;
                sectionModel.T1 = section.T1;
                sectionModel.T2 = section.T2;
                sectionModel.T3 = section.T3;
                sectionModel.Mirror = section.MirrorType;
                sectionModel.MirrorGapA = section.MirrorGapA;
                sectionModel.MirrorGapB = section.MirrorGapB;
                sectionModel.GenericShapes = section.GenericShapes;
                sectionModel.CalculateSection();
                sectionModel.I11 = section.I11;
                sectionModel.I22 = section.I22;
                sectionModel.W11 = section.W11;
                sectionModel.W22 = section.W22;
                sectionModel.SectionArea = section.SectionArea;
                sectionModel.J = section.J;
                sectionModel.Centroid = section.Centroid;

                if (section.IsDBSection)
                {
                    sectionModel.ShearL1 = section.SL1;
                    sectionModel.ShearL2 = section.SL2;
                    sectionModel.ShearA1 = section.SA1;
                    sectionModel.ShearA2 = section.SA2;
                    sectionModel.AngleX1Rad = section.AngleX1;
                }
            }
            else
            {
                sectionModel.SectionType = SectionModel.SectionTypes.Generic;
                sectionModel.SectionArea = section.SectionArea;
                sectionModel.I11 = section.I11;
                sectionModel.I22 = section.I22;
                sectionModel.J = section.J;
                sectionModel.ShearL1 = section.SL1;
                sectionModel.ShearL2 = section.SL2;
                sectionModel.ShearA1 = section.SA1;
                sectionModel.ShearA2 = section.SA2;
                sectionModel.W11 = section.W11;
                sectionModel.W22 = section.W22;
                sectionModel.Centroid = section.Centroid;
                sectionModel.AngleX1Rad = section.AngleX1;
            };

            BeamPropertyModel bp = new BeamPropertyModel
            {
                Name = name,
                Material = material.Value,
                Section = sectionModel,
                PropertyType = (BeamPropertyType)type,
            };

            if (bp.PropertyType == BeamPropertyType.CutoffBar)
            {
                bp.MaxTension = maxTensionCB;//10.0E8;
                bp.MaxCompression = maxCompressionCB;// 0;
                bp.CutoffType = (CutoffType)cutOffTypeCB;// CutoffType.Ductile;
                bp.KeepMass = keepMassCB;// true;
            }

            GH_BeamProperty gh_bp = new GH_BeamProperty(bp);
            DA.SetData(0, gh_bp);
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Arachne.Grasshopper.Helpers.ComponentIcons.Get(GetType().Name);

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid => new Guid("d44afeb3-cd24-42bf-a5d5-45a14cee69f3");

        public override bool Write(GH_IWriter writer)
        {
            //https://www.grasshopper3d.com/forum/topics/creating-a-component-with-a
            writer.SetInt32("Release", 1);
            Params.WriteAllParameterData(writer);
            return base.Write(writer);
        }

        public override bool Read(GH_IReader reader)
        {
            //Very important, we must make sure all parameters exist before we 
            //start with the main deserialization.
            int release = 0;
            if (reader.TryGetInt32("Release", ref release))
            {
                if (release >= 1)
                {
                    Params.Clear();
                    Params.ReadAllParameterData(reader);
                    //fix the number parameters
                    Param_Integer mtParam = Params.Input[1] as Param_Integer;
                    foreach (KeyValuePair<int, string> v in _propertyTypeOptions)
                        mtParam.AddNamedValue(v.Value, v.Key);
                    if (Params.Input.Count > 4)
                    {
                        AddNamedValuesCutOffType((Param_Integer)Params.Input[6]);
                    }
                }
            }
            return base.Read(reader);
        }
    }
}