using Rhino.Geometry;
using System.Collections.Generic;
using System.Linq;

namespace Arachne.Grasshopper.Geometry
{
	public class MeshGeodesic
	{
		protected class Particle
		{
			protected Point3d _point;
			protected Vector3d _vector;
			protected bool _anchor;

			public Particle(Point3d point, bool anchor)
			{
				_point = point;
				_anchor = anchor;
				_vector = Vector3d.Zero;
			}

			public Point3d Point { get => _point; set => _point = value; }

			public Vector3d Vector { get => _vector; set => _vector = value; }

			public bool Anchor { get => _anchor; set => _anchor = value; }
		}

		protected class Spring
		{
			protected Particle _start;
			protected Particle _end;

			public Spring(Particle start, Particle end)
			{
				_start = start;
				_end = end;
			}

			public void UpdateVector(double stepsize)
			{
				Vector3d force = new Vector3d(_end.Point - _start.Point) / 2;
				_start.Vector += force * stepsize;
				_end.Vector += -force * stepsize;
			}
		}

		private List<Particle> _particles;
		private List<Spring> _springs;

		public MeshGeodesic(Mesh mesh, Point3d p1, Point3d p2, double res = 0.5, double stepsize = 1.0, int steps = 1000)
		{
			Line line = new Line(p1, p2);

			SpringsFromLine(line, res);
			Minimize(mesh, steps, stepsize);
		}

		public Polyline Polyline => new Polyline(_particles.Select(i => i.Point).ToArray());

		public double Length
		{
			get
			{
				List<Point3d> pts = _particles.Select(i => i.Point).ToList();
				double length = 0;
				for (int i = 0; i < pts.Count - 1; i++)
					length += pts[i].DistanceTo(pts[i + 1]);
				return length;
			}
		}

		private void SpringsFromLine(Line line, double resolution)
		{
			_particles = new List<Particle>();
			_springs = new List<Spring>();

			Point3d[] array = line.ToNurbsCurve().DivideEquidistant(resolution);

			for (int i = 0; i < array.Length; i++)			
				_particles.Add(new Particle(array[i], false));
			

			_particles[0].Anchor = true;
			_particles.Add(new Particle(line.To, true));

			for (int i = 0; i < _particles.Count - 1; i++)
				_springs.Add(new Spring(_particles[i], _particles[i + 1]));
		}

		private void Minimize(Mesh mesh, int steps, double stepsize)
		{
			while (--steps > 0)
			{
				for (int i = 0; i < _springs.Count; i++)
				{
					Spring spring = _springs[i];
					spring.UpdateVector(stepsize);
				}

				bool canExit = true;
				for (int i = 0; i < _particles.Count; i++)
				{
					Particle particle = _particles[i];
					if (particle.Anchor)
						continue;

					Point3d buffer = particle.Point + particle.Vector;
					buffer = mesh.ClosestPoint(buffer);

					if ((particle.Point - buffer).Length > Rhino.RhinoDoc.ActiveDoc.ModelAbsoluteTolerance)
					{
						particle.Point = buffer;
						canExit = false;
					}
					particle.Vector = Vector3d.Zero;
				}
				if (canExit) 
					break;
			}
		}
	}
}

