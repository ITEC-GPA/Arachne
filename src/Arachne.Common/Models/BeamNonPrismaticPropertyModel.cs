using Rhino.Geometry;
using System;
using System.Runtime.Serialization;

namespace Arachne.Common.Models
{
	[Serializable]
	public class BeamNonPrismaticPropertyModel : BeamPropertyModel, ISerializable
	{
		public enum LengthTypes
		{
			Variable = 0,
			Absolute = 1,
		}

		public enum InertiaTypes
		{
			Linear = 0,
			Parabolic = 1,
			Cubic = 2,
		}

		public SectionModel EndSection { get; set; }
		public double Length { get; set; }
		public LengthTypes LengthType { get; set; }
		public InertiaTypes Inertia33Type { get; set; }
		public InertiaTypes Inertia22Type { get; set; }


		public double EndSectionSectionArea => EndSection.SectionArea;
		public double EndSectionIxx => EndSection.Ixx;
		public double EndSectionIyy => EndSection.Iyy;
		public double EndSectionIxy => EndSection.Ixy;
		public double EndSectionI11 => EndSection.I11;
		public double EndSectionI22 => EndSection.I22;
		public double EndSectionJ => EndSection.J;
		public double EndSectionW11 => EndSection.W11;
		public double EndSectionW22 => EndSection.W22;
		public double EndSectionRho11 => EndSection.Rho11;
		public double EndSectionRho22 => EndSection.Rho22;
		public double EndSectionWxx => EndSection.Wxx;
		public double EndSectionWyy => EndSection.Wyy;
		public double EndSectionAngleX1Rad => EndSection.AngleX1Rad;
		public double EndSectionShearL1 => EndSection.ShearL1;
		public double EndSectionShearL2 => EndSection.ShearL2;
		public double EndSectionShearA1 => EndSection.ShearA1;
		public double EndSectionShearA2 => EndSection.ShearA2;
		public int EndSectionIntegrationPoints => EndSection.IntegrationPoints;
		public SectionModel.SectionTypes EndSectionSectionType => EndSection.SectionType;
		public double EndSectionD => EndSection.D;
		public double EndSectionB => EndSection.B;
		public double EndSectionB1 => EndSection.B1;
		public double EndSectionB2 => EndSection.B2;
		public double EndSectionL => EndSection.L;
		public double EndSectionT => EndSection.T;
		public double EndSectionT1 => EndSection.T1;
		public double EndSectionT2 => EndSection.T2;
		public double EndSectionT3 => EndSection.T3;
		public SectionModel.MirrorTypes EndSectionMirror => EndSection.Mirror;
		public double EndSectionMirrorGapA => EndSection.MirrorGapA;
		public double EndSectionMirrorGapB => EndSection.MirrorGapB;
		public Point2d EndSectionCentroid => EndSection.Centroid;
		public bool EndSectionIncludeTorsionalStiffness => EndSection.IncludeTorsionalStiffness;


		public BeamNonPrismaticPropertyModel()
		{
		}

		public BeamNonPrismaticPropertyModel(BeamNonPrismaticPropertyModel attr)
		{
			Name = attr.Name;
			EndSection = attr.EndSection;
			Length = attr.Length;
			LengthType = attr.LengthType;
			Inertia33Type = attr.Inertia33Type;
			Inertia22Type = attr.Inertia22Type;
		}

		#region Serialization

		private readonly string SERIALIZATION_RELEASE_NAME = "BeamNonPrismaticPropertyModel_SERIALIZATION_RELEASE"; //do not modify the name!
		private readonly int SERIALIZATION_RELEASE = 1;

		protected BeamNonPrismaticPropertyModel(SerializationInfo info, StreamingContext context)
		{
			int savedRelease;
			try
			{
				savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);
			}
			catch
			{
				savedRelease = 0;
			}

			EndSection = (SectionModel)info.GetValue("EndSection", typeof(SectionModel));
			Length = (double)info.GetValue("Length", typeof(double));
			Name = (string)info.GetValue("Name", typeof(string));
			LengthType = (LengthTypes)info.GetValue("LengthType", typeof(string));
			Inertia33Type = (InertiaTypes)info.GetValue("Inertia33Type", typeof(InertiaTypes));
			Inertia22Type = (InertiaTypes)info.GetValue("Inertia22Type", typeof(InertiaTypes));
		}

		public override void GetObjectData(SerializationInfo info, StreamingContext context)
		{
			base.GetObjectData(info, context);
			info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
			info.AddValue("EndSection", EndSection);
			info.AddValue("Length", Length);
			info.AddValue("Name", Name);
			info.AddValue("LengthType", LengthType);
			info.AddValue("Inertia33Type", Inertia33Type);
			info.AddValue("Inertia22Type", Inertia22Type);
		}

		#endregion


		public override int GetHashCode()
		{
			var hashCode = 23;
			hashCode = hashCode * 17 + EndSection.GetHashCode();
			hashCode = hashCode * 17 + Length.GetHashCode();
			hashCode = hashCode * 17 + LengthType.GetHashCode();
			hashCode = hashCode * 17 + Name.GetHashCode();
			hashCode = hashCode * 17 + Inertia33Type.GetHashCode();
			hashCode = hashCode * 17 + Inertia22Type.GetHashCode();

			return hashCode;
		}
	}
}
