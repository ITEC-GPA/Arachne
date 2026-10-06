using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.Helpers;
using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino;
using Rhino.DocObjects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;

namespace Arachne.Grasshopper.DataTypes
{
    public class GH_Model : GH_Goo<ModelModel>, IGH_BakeAwareData
    {
        protected Dictionary<ElementModel, Guid> _guids;

        public Dictionary<ElementModel, Guid> Guids => _guids;

        public override bool IsValid => Value != null && Value.Elements != null && Value.Elements.Count() > 0;

        public override string TypeName => "FEM Model";

        public override string TypeDescription => "The FEM model definition";

        public GH_Model()
        {
            _guids = new Dictionary<ElementModel, Guid>();
        }

        public GH_Model(ModelModel.ModelUnits units, IEnumerable<IGH_GeometricGoo> elements, IEnumerable<GH_LoadCombination> combinations,
            IEnumerable<GH_NamedSets> namedSets, IEnumerable<GH_StagedConstruction> stages)
            : this()
        {
            Helpers.ModelHelper.ParseElements(elements, out List<GH_Node> nodes, out List<GH_Beam> beams, out List<GH_Plate> plates);
            Helpers.ModelHelper.BuildModel(ref nodes, ref beams, ref plates);

            Value = new ModelModel(units);

            Dictionary<ElementModel, Guid> bufferGuidDict = new Dictionary<ElementModel, Guid>();
            List<ElementModel> elementsBag = new List<ElementModel>();

            for (int i = 0; i < nodes.Count; i++)
            {
                GH_Node node = nodes[i];
                if (!bufferGuidDict.ContainsKey(node.Value))
                    bufferGuidDict.Add(node.Value, node.Guid);
                elementsBag.Add(node.Value);
            }
            for (int i = 0; i < beams.Count; i++)
            {
                GH_Beam beam = beams[i];
                if (!bufferGuidDict.ContainsKey(beam.Value))
                    bufferGuidDict.Add(beam.Value, beam.Guid);
                elementsBag.Add(beam.Value);
            }
            for (int i = 0; i < plates.Count; i++)
            {
                GH_Plate plate = plates[i];
                if (!bufferGuidDict.ContainsKey(plate.Value))
                    bufferGuidDict.Add(plate.Value, plate.Guid);
                elementsBag.Add(plate.Value);
            }

            _guids = new Dictionary<ElementModel, Guid>(bufferGuidDict);

            Value.AddElements(elementsBag);

            Value.AddLoadCombinations(combinations.Select(c => c.Value));

            Value.AddNamedSets(namedSets.Select(c => c.Value));

            Value.AddStagedConstructions(stages.Select(s => s.Value));
        }

        public GH_Model(GH_Model model)
            : this()
        {
            Value = model.Value;
            _guids = model.Guids;
        }

        /// <summary>
        /// Make a complete duplicate of this instance. No shallow copies.
        /// </summary>
        /// <returns>The deep copy of the model</returns>
        public override IGH_Goo Duplicate()
        {
            GH_Model copy = new GH_Model { Value = new ModelModel(Value) };

            // Rebuild the Guids association
            foreach (ElementModel copy_element in copy.Value.Elements)
            {
                ElementModel element;
                if (copy_element is NodeModel node)
                {
                    try
                    {
                        element = Value.GetNode(node.NodeId);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else if (copy_element is BeamModel beam)
                {
                    try
                    {
                        element = Value.GetBeam(beam.BeamId);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else if (copy_element is PlateModel plate)
                {
                    try
                    {
                        element = Value.GetPlate(plate.PlateId);
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else
                    continue;

                // A model read from a file has no Guid associations to restore
                if (element != null && Guids.TryGetValue(element, out Guid guid))
                    copy.Guids.Add(copy_element, guid);
            }

            return copy;
        }

        public void AddGuid(ElementModel elementModel, Guid guid)
        {
            _guids.Add(elementModel, guid);
        }

        public override bool Write(GH_IWriter writer)
        {
            if (base.Write(writer))
            {
                try
                {
                    // Written with the same formatter used by Read (FeMM wrote JSON and read binary data)
                    writer.SetByteArray("Data", SerializationHelper.ToBytes(Value));
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Model.Write: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        public override bool Read(GH_IReader reader)
        {
            if (base.Read(reader))
            {
                try
                {
                    byte[] buffer = reader.GetByteArray("Data");
                    using (MemoryStream ms = new MemoryStream(buffer))
                    {
                        BinaryFormatter bf = new BinaryFormatter();
                        Value = ModelModel.Deserialize(bf, ms);
                    }
                    return true;
                }
                catch (Exception e)
                {
                    System.Diagnostics.Debug.WriteLine("GH_Model.Read: {0}", e.Message);
                    return false;
                }
            }
            return false;
        }

        // Arachne has no FeMM Rhino plug-in objects: the elements are baked as plain geometry.
        public bool BakeGeometry(RhinoDoc doc, ObjectAttributes att, out Guid obj_guid)
        {
            foreach (ElementModel element in Value.Elements)
            {
                if (element is NodeModel node)
                    new GH_Node(node).BakeGeometry(doc, att, out _);
                else if (element is BeamModel beam)
                    new GH_Beam(beam).BakeGeometry(doc, att, out _);
                else if (element is PlateModel plate)
                    new GH_Plate(plate).BakeGeometry(doc, att, out _);
            }
            obj_guid = Guid.Empty;
            return true;
        }

        public override string ToString()
        {
            if (Value == null)
                return "Null Model";
            else if (Value.Elements.Count() == 0)
                return "Empty Model";
            else
                return Value.ToString();
        }
    }
}
