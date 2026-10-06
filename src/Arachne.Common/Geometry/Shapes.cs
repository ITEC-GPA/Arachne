using GPC.Geometry;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace Arachne.Common.Geometry
{
	[Serializable]
	public class Shapes : IEnumerable<Shape2d>, ISerializable
	{
		protected List<Shape2d> _items;

		public Shapes(IEnumerable<Shape2d> items)
		{
			_items = new List<Shape2d>(items);
		}

		public Shape2d this[int index] => _items[index];

		public IEnumerator<Shape2d> GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return _items.GetEnumerator();
		}

		public void MoveToMin()
		{
			BoundingBox2d bbox = GetBBox();
			Pan(-bbox.Min.X, -bbox.Min.Y);
		}

		public BoundingBox2d GetBBox()
		{
			BoundingBox2d bbox = new BoundingBox2d();
			foreach (Shape2d s in this)
			{
				BoundingBox2d shapeBBox = s.Get2dBoundingBox();
				bbox.Update(shapeBBox.Min);
				bbox.Update(shapeBBox.Max);
			}
			return bbox;
		}

		public void Pan(double dx, double dy)
		{
			foreach (Shape2d s in this)
			{
				s.Move(dx, dy);
			}
		}

		public int Count => _items.Count;

		public void GetAreaBarycentre(out double xG, out double yG, out double area)
		{
			List<Polygon2d> fills = new List<Polygon2d>();
			List<Polygon2d> holes = new List<Polygon2d>();

			for (int i = 0; i < _items.Count; i++)
			{
				fills.Add(_items[i].Fill2d);
				if (_items[i].Holes != null)
				{
					for (int j = 0; j < _items[i].Holes2d.Length; j++)
					{
						holes.Add(_items[i].Holes2d[j]);
					}
				}
			}

			GetAreaBarycentre(fills, holes, out area, out xG, out yG);
		}

		private void GetAreaBarycentre(List<Polygon2d> fills, List<Polygon2d> holes, out double area, out double xG, out double yG)
		{
			area = 0;
			xG = 0;
			yG = 0;

			double areaTotal = 0;
			for (int i = 0; i < fills.Count; i++)
			{
				double areaPolygon = Math.Abs(fills[i].GetSignedArea());

				var centroid = fills[i].GetCentroid();

				xG += centroid.X * areaPolygon;
				yG += centroid.Y * areaPolygon;

				areaTotal += areaPolygon;
			}

			for (int i = 0; i < holes.Count; i++)
			{
				double areaPolygon = -Math.Abs(holes[i].GetSignedArea());

				var centroid = fills[i].GetCentroid();

				xG += centroid.X * areaPolygon;
				yG += centroid.Y * areaPolygon;

				areaTotal += areaPolygon;
			}

			if (areaTotal == 0)
			{
				xG = 0;
				yG = 0;
				return;
			}

			area = areaTotal;
			xG /= areaTotal;
			yG /= areaTotal;
		}

		public void GetGeometricalDatas(bool mIsAlwaysMajorAxes, double mainJToll, out double xG, out double yG, out double area, out double jxG, out double jyG,
			out double jxyG, out double jm, out double jn, out double angleXToM, out double wx, out double wy, out double wm, out double wn, out double rho1, out double rho2)
		{
			List<Polygon2d> fills = new List<Polygon2d>();
			List<Polygon2d> holes = new List<Polygon2d>();
			foreach (Shape2d s in this)
			{
				fills.Add(s.Fill2d);
				if (s.Holes != null)
				{
					foreach (Polygon2d p in s.Holes2d)
					{
						holes.Add(p);
					}
				}
			}

			GetAreaBarycentre(fills, holes, out area, out xG, out yG);
			var barycenter = new Point2d(xG, yG);
			jxG = 0;
			jyG = 0;
			jxyG = 0;

			BoundingBox2d bbox = new BoundingBox2d();
			for (int i = 0; i < fills.Count; i++)
			{
				var fill = fills[i];

				for (int j = 0; j < fill.Count; j++)
				{
					var point = fill[j];
					var nextPoint = fill.GetNextPoint(j);

					bbox.Update(point);

					double multX = 0;
					if (point.X > nextPoint.X)
					{
						multX = +1.0;
					}
					else
					{
						multX = -1.0;
					}

					double multY = 0;
					if (point.Y > nextPoint.Y)
					{
						multY = +1.0;
					}
					else
					{
						multY = -1.0;
					}

					double yMean = (point.Y + nextPoint.Y) / 2.0;
					double xMean = (point.X + nextPoint.X) / 2.0;

					double deltaX = Math.Abs(point.X - nextPoint.X);
					double deltaY = Math.Abs(point.Y - nextPoint.Y);

					jxG += 1.0 / 3.0 * multX * deltaX * Math.Pow(yMean, 3.0);
					jyG += 1.0 / 3.0 * multY * deltaY * Math.Pow(xMean, 3.0);

					jxyG += multX * multY * (yMean - barycenter.Y) * deltaX * deltaY * (xMean - barycenter.X);

				}
			}

			for (int i = 0; i < holes.Count; i++)
			{
				var hole = holes[i];

				for (int j = 0; j < hole.Count; j++)
				{
					var point = hole[j];
					var nextPoint = hole.GetNextPoint(j);

					bbox.Update(point);

					double multX = 0;
					if (point.X > nextPoint.X)
					{
						multX = +1.0;
					}
					else
					{
						multX = -1.0;
					}

					double multY = 0;
					if (point.Y > nextPoint.Y)
					{
						multY = +1.0;
					}
					else
					{
						multY = -1.0;
					}

					double yMean = (point.Y + nextPoint.Y) / 2.0;
					double xMean = (point.X + nextPoint.X) / 2.0;

					double deltaX = Math.Abs(point.X - nextPoint.X);
					double deltaY = Math.Abs(point.Y - nextPoint.Y);

					jxG -= 1.0 / 3.0 * multX * deltaX * Math.Pow(yMean, 3.0);
					jyG -= 1.0 / 3.0 * multY * deltaY * Math.Pow(xMean, 3.0);

					jxyG -= multX * multY * (yMean - barycenter.Y) * deltaX * deltaY * (xMean - barycenter.X);
				}
			}


			jxG = Math.Abs(jxG) - area * Math.Pow(yG, 2);
			jyG = Math.Abs(jyG) - area * Math.Pow(xG, 2);

			wx = jxG / Math.Max(Math.Abs(bbox.Max.Y), Math.Abs(bbox.Min.Y));
			wy = jyG / Math.Max(Math.Abs(bbox.Max.X), Math.Abs(bbox.Min.X));

			var diff = jxG - jyG;

			if (Math.Abs(diff) < 1)
				diff = 0;

			if (Math.Abs(jxyG) < 1)
				angleXToM = 0;
			else if (diff == 0)
				angleXToM = 1.0 / 2.0 * Math.Atan2(-2.0 * jxyG, diff);
			else
				angleXToM = 1.0 / 2.0 * Math.Atan(-2.0 * jxyG / diff);

			if (double.IsNaN(angleXToM))
				angleXToM = 0;

			double cosAlfa = Math.Cos(angleXToM);
			double senAlfa = Math.Sin(angleXToM);
			jm = jxG * cosAlfa * cosAlfa + jyG * senAlfa * senAlfa - 2 * jxyG * senAlfa * cosAlfa;
			jn = jxG * senAlfa * senAlfa + jyG * cosAlfa * cosAlfa + 2 * jxyG * senAlfa * cosAlfa;

			if (mIsAlwaysMajorAxes && jm < jn - mainJToll)
			{
				double buffer = jm;
				jm = jn;
				jn = buffer;
				if (angleXToM < -0.000001)
				{
					angleXToM += Math.PI / 2;
				}
				else
				{
					angleXToM -= Math.PI / 2;
				}
				cosAlfa = Math.Cos(angleXToM);
				senAlfa = Math.Sin(angleXToM);
			}

			// Principal axes
			Point2d origin = new Point2d(xG, yG);
			CoordinateSystem toPrincipal = new CoordinateSystem(origin, origin + new Vector2d(cosAlfa, senAlfa),Vector3d.ZAxis);
			bbox = new BoundingBox2d();

			for (int i = 0; i < fills.Count; i++)
			{
				for (int j = 0; j < fills[i].Count; j++)
				{
					bbox.Update(toPrincipal.ToLocal(fills[i][j]));					
				}
			}

			for (int i = 0; i < holes.Count; i++)
			{
				for (int j = 0; j < holes[i].Count; j++)
				{
					bbox.Update(toPrincipal.ToLocal(holes[i][j]));					
				}
			}


			wm = jm / Math.Max(Math.Abs(bbox.Max.Y), Math.Abs(bbox.Min.Y));
			wn = jn / Math.Max(Math.Abs(bbox.Max.X), Math.Abs(bbox.Min.X));

			rho1 = Math.Sqrt(jm/area);
			rho2 = Math.Sqrt(jn/area);
		}

		#region Serializzazione

		private static int SERIALIZATION_RELEASE = 1;
		protected static string SERIALIZATION_RELEASE_NAME = "SERIALIZATION_RELEASE_NAME";
		private static string PREFIX = "Utilities.Shapes/";

		protected Shapes(SerializationInfo info, StreamingContext context)
		{
			_items = (List<Shape2d>)info.GetValue(PREFIX + "_items", typeof(List<Shape2d>));
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			info.AddValue(PREFIX + SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue(PREFIX + "_items", _items, typeof(List<Shape2d>));
		}

		#endregion Serializzazione

		public Shape2d[] ToShapes2d()
		{
			return _items.ToArray();
		}

		public override bool Equals(object obj)
		{
			return obj is Shapes shapes && _items.SequenceEqual(shapes._items);
		}

		public override int GetHashCode()
		{
			unchecked
			{
				return -17 + _items.Select(i => i.GetHashCode()).Sum();
			}
		}
	}
}
