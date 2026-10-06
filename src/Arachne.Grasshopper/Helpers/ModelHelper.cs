using Arachne.Common.Helpers;
using Arachne.Common.Models;
using Arachne.Grasshopper.DataTypes;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Helpers
{
    public static class ModelHelper
    {
        public static void GetLoadCases(List<IGH_GeometricGoo> elements, out List<LoadCaseModel> loadCases)
        {
            ParseElements(elements, out List<GH_Node> nodes, out List<GH_Beam> beams, out List<GH_Plate> plates);
            loadCases = new List<LoadCaseModel>();// { LoadCaseModel.Dead };
            loadCases.AddRange(nodes.SelectMany(n => n.Value.Loads).Select(l => l.LoadCase)
                .Union(beams.SelectMany(b => b.Value.Loads).Select(l => l.LoadCase))
                .Union(plates.SelectMany(p => p.Value.Loads).Select(l => l.LoadCase))
                .Distinct(new LoadCaseModelsEqualityComparer()));
            Common.Helpers.ModelHelper.AddDeadLoadCase(ref loadCases);
            loadCases.Sort();
        }

        public static List<LoadCaseModel> GetLoadCases(List<ElementModel> elements)
        {
            var loadCases = new List<LoadCaseModel>();// { LoadCaseModel.Dead };
            loadCases.AddRange(elements.SelectMany(n => n.Loads).Select(l => l.LoadCase).Distinct(new LoadCaseModelsEqualityComparer()));
            Common.Helpers.ModelHelper.AddDeadLoadCase(ref loadCases);
            loadCases.Sort();
            return loadCases;
        }

        public static bool ParseElements(IEnumerable<IGH_GeometricGoo> elements, out List<GH_Node> nodes,
            out List<GH_Beam> beams, out List<GH_Plate> plates)
        {
            try
            {
                nodes = new List<GH_Node>();
                beams = new List<GH_Beam>();
                plates = new List<GH_Plate>();

                var elementsArray = elements.ToArray();
                for (int i = 0; i < elementsArray.Length; i++)
                {
                    IGH_GeometricGoo element = elementsArray[i];
                    if (element is GH_Node node)
                        nodes.Add(node);

                    if (element is GH_Beam beam)
                        beams.Add(beam);

                    if (element is GH_Plate plate)
                        plates.Add(plate);
                }

                return true;
            }
            catch (Exception)
            {
                nodes = null;
                beams = null;
                plates = null;
                return false;
            }
        }

        public static double GetSquaredTolerance(ModelModel.ModelUnits units)
        {
            return GPC.Utilities.Maths.ErrorPropagation.DefaultProductSquareTolerance(Settings.Instance.Tolerance);
        }

        public static double GetTolerance()
        {
            return Settings.Instance.Tolerance;
        }

        /// <summary>
        /// Assign the elements id and creates the missing nodes
        /// </summary>
        /// <param name="nodes">The nodes list</param>
        /// <param name="beams">The beams list</param>
        /// <param name="plates">The plates list</param>
        public static void BuildModel(ref List<GH_Node> nodes, ref List<GH_Beam> beams, ref List<GH_Plate> plates)
        {
            Dictionary<Guid, GH_Node> nodesDict = new Dictionary<Guid, GH_Node>();
            Dictionary<Guid, GH_Beam> beamsDict = new Dictionary<Guid, GH_Beam>();
            Dictionary<Guid, GH_Plate> platesDict = new Dictionary<Guid, GH_Plate>();

            GH_Node[] localNodes = nodes.ToArray();
            GH_Beam[] localBeams = beams.ToArray();
            GH_Plate[] localPlates = plates.ToArray();

            int n_node = 1;
            List<AttributeModel> nodeAttributeIds = localNodes.Select(i => i.Value.Attributes.FirstOrDefault(at => at is ElementIdAttributeModel)).Where(i => i != null).ToList();
            List<string> nodeIds = nodeAttributeIds.Select(i => ((ElementIdAttributeModel)i).Value).ToList();

            for (int i = 0; i < localNodes.Length; i++)
            {
                if (nodesDict.ContainsKey(localNodes[i].Guid))
                {
                    nodesDict[localNodes[i].Guid].Value.Merge(localNodes[i].Value);
                }
                else
                {
                    GH_Node node = new GH_Node(localNodes[i]);

                    var id = node.Value.Attributes.FirstOrDefault(at => at is ElementIdAttributeModel);
                    if (id != null)
                    {
                        node.Value.NodeId = ((ElementIdAttributeModel)id).Value;
                        nodesDict.Add(localNodes[i].Guid, node);
                        continue;
                    }

                    while (nodeIds.Contains(n_node.ToString()))
                    {
                        n_node++;
                    }
                    node.Value.NodeId = n_node.ToString();
                    nodesDict.Add(localNodes[i].Guid, node);
                    n_node++;
                }
            }

            int n_beam = 1;
            List<AttributeModel> beamAttributeIds = localBeams.Select(i => i.Value.Attributes.FirstOrDefault(at => at is ElementIdAttributeModel)).Where(i => i != null).ToList();
            List<string> beamIds = beamAttributeIds.Select(i => ((ElementIdAttributeModel)i).Value).ToList();

            for (int i = 0; i < localBeams.Length; i++)
            {
                if (beamsDict.ContainsKey(localBeams[i].Guid))
                {
                    beamsDict[localBeams[i].Guid].Value.Merge(localBeams[i].Value);
                }
                else
                {
                    GH_Beam beam = new GH_Beam(localBeams[i]);

                    var id = beam.Value.Attributes.FirstOrDefault(at => at is ElementIdAttributeModel);
                    if (id != null)
                    {
                        beam.Value.BeamId = ((ElementIdAttributeModel)id).Value;
                        beamsDict.Add(localBeams[i].Guid, beam);
                        continue;
                    }

                    while (beamIds.Contains(n_beam.ToString()))
                    {
                        n_beam++;
                    }
                    beam.Value.BeamId = n_beam.ToString();
                    beamsDict.Add(localBeams[i].Guid, beam);
                    n_beam++;
                }
            }

            List<GH_Beam> orderedBeamsList;
            try
            {
                orderedBeamsList = beamsDict.OrderBy(i => int.TryParse(i.Value.Value.BeamId, out int id) ? id : int.MaxValue).Select(i => i.Value).ToList();
            }
            catch (Exception)
            {
                orderedBeamsList = beamsDict.Values.ToList();
            }


            int n_plate = 1;
            List<AttributeModel> platettributeIds = localPlates.Select(i => i.Value.Attributes.FirstOrDefault(at => at is ElementIdAttributeModel)).Where(i => i != null).ToList();
            List<string> plateIds = platettributeIds.Select(i => ((ElementIdAttributeModel)i).Value).ToList();

            for (int i = 0; i < localPlates.Length; i++)
            {
                if (platesDict.ContainsKey(localPlates[i].Guid))
                {
                    platesDict[localPlates[i].Guid].Value.Merge(localPlates[i].Value);
                }
                else
                {
                    GH_Plate plate = new GH_Plate(localPlates[i]);

                    var id = plate.Value.Attributes.FirstOrDefault(at => at is ElementIdAttributeModel);
                    if (id != null)
                    {
                        plate.Value.PlateId = ((ElementIdAttributeModel)id).Value;
                        platesDict.Add(localPlates[i].Guid, plate);
                        continue;
                    }

                    while (plateIds.Contains(n_plate.ToString()))
                    {
                        n_plate++;
                    }
                    plate.Value.PlateId = n_plate.ToString();
                    platesDict.Add(localPlates[i].Guid, plate);
                    n_plate++;
                }
            }

            List<GH_Plate> orderedPlateList;
            try
            {
                orderedPlateList = platesDict.OrderBy(i => int.TryParse(i.Value.Value.PlateId, out int id)? id : int.MaxValue).Select(i => i.Value).ToList();
            }
            catch (Exception)
            {
                orderedPlateList = platesDict.Values.ToList();
            }

            double tol = ModelHelper.GetTolerance();

            List<GH_Node> nodeList = nodesDict.Values.ToList();
            List<GH_Node> orderedNodeList = new List<GH_Node>();

            RTree rTree = new RTree();

            for (int i = 0; i < nodeList.Count; i++)
            {
                Point3d pt = nodeList[i].Value.Position;
                bool found = false;
                int foundId = -1;
                rTree.Search(new Sphere(pt, tol), new EventHandler<RTreeEventArgs>((sender, e) => { found = true; foundId = e.Id; }));

                if (!found)
                {
                    orderedNodeList.Add(new GH_Node(nodeList[i]));
                    rTree.Insert(pt, orderedNodeList.Count - 1);
                }
                else
                {
                    // A node defined twice at the same point keeps the attributes and loads of both
                    orderedNodeList[foundId].Value.Merge(nodeList[i].Value);
                }
            }

            for (int i = 0; i < orderedBeamsList.Count; i++)
            {
                BeamModel beam = orderedBeamsList[i].Value;

                Point3d startPt = beam.PointFrom;
                Point3d endPt = beam.PointTo;

                bool found = false;
                int foundId = -1;
                rTree.Search(new Sphere(startPt, tol), new EventHandler<RTreeEventArgs>((sender, e) => { found = true; foundId = e.Id; }));

                if (found && foundId != -1)
                {
                    beam.PointFrom = orderedNodeList[foundId].Value.Position;
                }
                else
                {
                    NodeModel newNode = new NodeModel(n_node.ToString(), startPt);
                    n_node++;

                    orderedNodeList.Add(new GH_Node(newNode));
                    rTree.Insert(startPt, orderedNodeList.Count - 1);
                }

                found = false;
                foundId = -1;
                rTree.Search(new Sphere(endPt, tol), new EventHandler<RTreeEventArgs>((sender, e) => { found = true; foundId = e.Id; }));

                if (found && foundId != -1)
                {
                    beam.PointTo = orderedNodeList[foundId].Value.Position;
                }
                else
                {
                    NodeModel newNode = new NodeModel(n_node.ToString(), endPt);
                    n_node++;

                    orderedNodeList.Add(new GH_Node(newNode));
                    rTree.Insert(endPt, orderedNodeList.Count - 1);
                }
            }

            for (int i = orderedBeamsList.Count - 1; i >= 0; i--) {
                var beam = orderedBeamsList[i].Value;
                if (beam.PointFrom == beam.PointTo)
                {
                    orderedBeamsList.RemoveAt(i);
                }
            }

            for (int i = 0; i < orderedPlateList.Count; i++)
            {
                PlateModel plate = orderedPlateList[i].Value;

                for (int j = 0; j < plate.Points.Count; j++)
                {
                    Point3d pt = plate.Points[j];

                    bool found = false;
                    int foundId = -1;
                    rTree.Search(new Sphere(pt, tol), new EventHandler<RTreeEventArgs>((sender, e) => { found = true; foundId = e.Id; }));

                    if (found && foundId != -1)
                    {
                        plate.Points[j] = orderedNodeList[foundId].Value.Position;
                    }
                    else
                    {
                        NodeModel newNode = new NodeModel(n_node.ToString(), pt);
                        n_node++;

                        orderedNodeList.Add(new GH_Node(newNode));
                        rTree.Insert(pt, orderedNodeList.Count - 1);
                    }
                }
            }

            // Returns the new lists
            nodes = orderedNodeList;
            beams = orderedBeamsList;
            plates = orderedPlateList;
        }

        /// <summary>
        /// Use this function in the analysis components to restore the Guids in the loaded_model 
        /// getting the values from the input_model after loaded. 
        /// </summary>
        /// <param name="input_model">The model from the input parameter</param>
        /// <param name="model">The model where to restore the Guids</param>
        public static void RestoreModelGuids(GH_Model input_model, ref GH_Model model)
        {
            if (input_model != null && input_model.IsValid && model != null && model.Value != null)
            {
                foreach (KeyValuePair<ElementModel, Guid> item in input_model.Guids)
                {
                    if (item.Key is NodeModel input_node)
                    {
                        NodeModel node = model.Value.GetNode(input_node.NodeId);
                        if (node != null)
                            model.Guids.Add(node, item.Value);
                    }
                    else if (item.Key is BeamModel input_beam)
                    {
                        BeamModel beam = model.Value.GetBeam(input_beam.BeamId);
                        if (beam != null)
                            model.Guids.Add(beam, item.Value);
                    }
                    else if (item.Key is PlateModel input_plate)
                    {
                        PlateModel plate = model.Value.GetPlate(input_plate.PlateId);
                        if (plate != null)
                            model.Guids.Add(plate, item.Value);
                    }
                }
            }
        }
    }
}
