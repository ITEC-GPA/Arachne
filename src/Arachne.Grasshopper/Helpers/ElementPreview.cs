using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GPC.Model.Elements;
using GPC.Model.Loads;
using Rhino.Display;
using Rhino.Geometry;
using GSection = GPC.Model.Sections.Section;

namespace Arachne.Grasshopper.Helpers
{
    /// <summary>Rhino preview of the GPC elements of the Grasshopper data (document units): nodes, beam axes and their section
    /// extruded along them, plates, restraints and loads. Presentation only.</summary>
    internal static class ElementPreview
    {
        internal static Point3d Rh(GPC.Geometry.Point3d p) => new Point3d(p.X, p.Y, p.Z);
        internal static Vector3d Rh(GPC.Geometry.Vector3d v) => new Vector3d(v.X, v.Y, v.Z);

        internal static void Node(NodeElement node, DisplayPipeline display, Color color)
        {
            var p = Rh(node.Position);
            display.DrawPoint(p, color);
            if (!Settings.Instance.ShowAttributes) return;
            var restraint = node.Assignments.Restrains.FirstOrDefault()?.Restrain;
            if (restraint != null)
            {
                var axes = Common.Analysis.ElementDrafts.IsGlobalRestraint(restraint) ? new[] { Vector3d.XAxis, Vector3d.YAxis, Vector3d.ZAxis }
                    : new[] { Rh(restraint.CoordinateSystem.V1), Rh(restraint.CoordinateSystem.V2), Rh(restraint.CoordinateSystem.V3) };
                double size = Scale(display, p);
                var dofs = new[] { GPC.Model.Restraints.GeometryRestrain.DOF.DX, GPC.Model.Restraints.GeometryRestrain.DOF.DY, GPC.Model.Restraints.GeometryRestrain.DOF.DZ };
                for (int i = 0; i < 3; i++)
                    if (restraint.Restrains.Any(d => d.Dof == dofs[i] && d.IsRestrained)) display.DrawLine(p - axes[i] * size, p, Color.SeaGreen, 2);
            }
            foreach (var load in node.Loads.Values.OfType<PointLoad>())
            {
                var f = new Vector3d(load.F1, load.F2, load.F3);
                if (f.Length == 0) continue;
                f.Unitize(); display.DrawArrow(new Line(p - f * Scale(display, p) * 3, p), Color.IndianRed);
            }
        }

        private static double Scale(DisplayPipeline display, Point3d p)
        {
            display.Viewport.GetWorldToScreenScale(p, out double pixelsPerUnit);
            return pixelsPerUnit > 0 ? 10 / pixelsPerUnit : 1;
        }

        internal static void Beam(BeamElement beam, DisplayPipeline display, Color color)
        {
            display.DrawLine(Rh(beam.StartPoint), Rh(beam.EndPoint), color);
            if (Settings.Instance.ShowSolids) foreach (var brep in Shapes(beam)) display.DrawBrepWires(brep, color);
        }

        /// <summary>The section outline swept along the beam axis (a circle of the same area for a section without outline).</summary>
        internal static Brep[] Shapes(BeamElement beam)
        {
            var from = Rh(beam.StartPoint); var to = Rh(beam.EndPoint);
            var axis = to - from;
            if (!(axis.Length > 0)) return new Brep[0];
            var section = (beam.BeamProperty as GPC.Model.ElementProperties.ElasticBeamProperty)?.Section as GSection;
            if (section == null) return new Brep[0];
            var plane = new Plane(from, axis);
            if (beam.RotationAroundFirstAxis != 0) plane.Rotate(beam.RotationAroundFirstAxis, plane.ZAxis);
            var offset = beam.Assignments.SectionCentroidOffset;
            var curves = new List<Curve>();
            if (section.Shape == null)
            {
                double radius = section.Area > 0 ? Math.Sqrt(section.Area / Math.PI) : 0;
                if (radius > 0) curves.Add(new Circle(plane, radius).ToNurbsCurve());
            }
            else
            {
                var centroid = section.Centroid;
                void Add(GPC.Geometry.Shape2d shape)
                {
                    foreach (var polygon in new[] { shape.Fill2d }.Concat(shape.Holes2d ?? new GPC.Geometry.Polygon2d[0]))
                    {
                        var points = polygon.Select(q => plane.PointAt(q.X - centroid.X + (offset?.X ?? 0), q.Y - centroid.Y + (offset?.Y ?? 0))).ToList();
                        if (points.Count < 3) continue;
                        points.Add(points[0]); curves.Add(new PolylineCurve(points));
                    }
                    foreach (var child in shape.Childs2d ?? new GPC.Geometry.Shape2d[0]) Add(child);
                }
                Add(section.Shape);
            }
            var result = new List<Brep>();
            foreach (var curve in curves)
            {
                var surface = Surface.CreateExtrusion(curve, axis);
                var brep = surface?.ToBrep();
                if (brep != null) result.Add(brep);
            }
            return result.ToArray();
        }

        internal static void Plate(AreaElement area, DisplayPipeline display, Color color)
        {
            var points = area.Points.Select(Rh).ToList();
            if (points.Count < 3) return;
            points.Add(points[0]);
            display.DrawPolyline(points, color);
        }
    }
}
