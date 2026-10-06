using Rhino.Geometry;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace Arachne.Common.Models
{
    [Serializable]
    public class LoadCaseModel : CaseModel, IComparable<LoadCaseModel>, IEquatable<LoadCaseModel>
    {
        public enum GlobalInertiaLoads
        {
            None,
            Gravity,
            AccelerationAndVelocity,
            Seismic
        }

        public enum GravityDirections { X, Y, Z };

        protected string _id;
        protected double _temperature;
        protected bool _structuralMassAcceleration;
        protected bool _nonStructuralMassAcceleration;
        protected GlobalInertiaLoads _globalInertiaLoad;
        protected GravityDirections _gravityDirection;
        protected double _gravityValue;
        protected Vector3d _a;
        protected Vector3d _v;
        protected Vector3d _w;
        protected Point3d _origin;
        protected List<Stage> _stages;

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public double Temperature
        {
            get => _temperature;
            set => _temperature = value;
        }

        public bool StructuralMassAcceleration
        {
            get => _structuralMassAcceleration;
            set => _structuralMassAcceleration = value;
        }

        public bool NonStructuralMassAcceleration
        {
            get => _nonStructuralMassAcceleration;
            set => _nonStructuralMassAcceleration = value;
        }

        public GlobalInertiaLoads GlobalInertiaLoad
        {
            get => _globalInertiaLoad;
            set => _globalInertiaLoad = value;
        }

        public GravityDirections GravityDirection
        {
            get => _gravityDirection;
            set => _gravityDirection = value;
        }

        public double GravityValue
        {
            get => _gravityValue;
            set => _gravityValue = value;
        }

        public Vector3d A
        {
            get => _a;
            set => _a = value;
        }

        public Vector3d V
        {
            get => _v;
            set => _v = value;
        }

        public Vector3d W
        {
            get => _w;
            set => _w = value;
        }

        public Point3d Origin
        {
            get => _origin;
            set => _origin = value;
        }

        public List<Stage> Stages
        {
            get => _stages;
            set => _stages = value;
        }

        public readonly static LoadCaseModel Dead = new LoadCaseModel("1", "Dead")
        {
            GlobalInertiaLoad = GlobalInertiaLoads.Gravity,
            GravityDirection = GravityDirections.Z,
            GravityValue = -ModelModel.GRAVITY_mmSuS2,
            StructuralMassAcceleration = true,
            NonStructuralMassAcceleration = false,
            Stages = null
        };

        public LoadCaseModel(string id, string name = "")
            : base(name)
        {
            _id = id;
            _stages = new List<Stage>();
        }

        public LoadCaseModel()
            : base("")
        {

        }

        public LoadCaseModel(LoadCaseModel loadCase)
            : base(loadCase)
        {
            _id = loadCase._id;
            _temperature = loadCase._temperature;
            _structuralMassAcceleration = loadCase._structuralMassAcceleration;
            _nonStructuralMassAcceleration = loadCase._nonStructuralMassAcceleration;
            _globalInertiaLoad = loadCase._globalInertiaLoad;
            _gravityDirection = loadCase._gravityDirection;
            _gravityValue = loadCase._gravityValue;
            _a = loadCase._a;
            _v = loadCase._v;
            _w = loadCase._w;
            _origin = loadCase._origin;
            if (loadCase.Stages == null)
            {
                _stages = null;
            }
            else
            {
                _stages = new List<Stage>();
                foreach (Stage s in loadCase.Stages)
                {
                    _stages.Add(new Stage(s));
                }
            }
        }

        #region Serialization

        private string SERIALIZATION_RELEASE_NAME = "LoadCaseModel_SERIALIZATION_RELEASE"; //do not modify the name!
        private int SERIALIZATION_RELEASE = 2;

        protected LoadCaseModel(SerializationInfo info, StreamingContext context)
            : base(info, context)
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
            _id = info.GetString("Id");
            _temperature = info.GetDouble("Temperature");
            _structuralMassAcceleration = info.GetBoolean("StructuralMassAcceleration");
            _nonStructuralMassAcceleration = info.GetBoolean("NonStructuralMassAcceleration");
            _globalInertiaLoad = (GlobalInertiaLoads)info.GetValue("GlobalInertiaLoad", typeof(GlobalInertiaLoads));
            _gravityDirection = (GravityDirections)info.GetValue("GravityDirection", typeof(GravityDirections));
            _gravityValue = info.GetDouble("GravityValue");
            _a = (Vector3d)info.GetValue("A", typeof(Vector3d));
            _v = (Vector3d)info.GetValue("V", typeof(Vector3d));
            _w = (Vector3d)info.GetValue("W", typeof(Vector3d));
            _origin = (Point3d)info.GetValue("Origin", typeof(Point3d));
            if (savedRelease >= 2)
            {
                if (info.GetBoolean("HasStages"))
                {
                    _stages = (List<Stage>)info.GetValue("Stages", typeof(List<Stage>));
                }
                else
                {
                    _stages = null;
                }
            }
            else
            {
                _stages = null;
            }
        }

        [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
            info.AddValue("Id", _id);
            info.AddValue("Temperature", _temperature);
            info.AddValue("StructuralMassAcceleration", _structuralMassAcceleration);
            info.AddValue("NonStructuralMassAcceleration", _nonStructuralMassAcceleration);
            info.AddValue("GlobalInertiaLoad", _globalInertiaLoad, typeof(GlobalInertiaLoads));
            info.AddValue("GravityDirection", _gravityDirection, typeof(GravityDirections));
            info.AddValue("GravityValue", _gravityValue);
            info.AddValue("A", _a, typeof(Vector3d));
            info.AddValue("V", _v, typeof(Vector3d));
            info.AddValue("W", _w, typeof(Vector3d));
            info.AddValue("Origin", _origin, typeof(Point3d));
            info.AddValue("HasStages", _stages != null);
            if (_stages != null)
            {
                info.AddValue("Stages", _stages, typeof(List<Stage>));
            }
        }

        #endregion

        public int CompareTo(LoadCaseModel other)
        {
            if (other == null)
            {
                return -1;
            }
            else if (Name.Equals(other.Name))
            {
                return 0;
            }
            else if (Name == Dead.Name)
            {
                return -1;
            }
            else if (other.Name == Dead.Name)
            {
                return 1;
            }
            else
            {
                return Name.CompareTo(other.Name);
            }
        }

        public static bool operator ==(LoadCaseModel lc1, LoadCaseModel lc2)
        {
            if (ReferenceEquals(lc1, lc2))
                return true;
            return !(lc1 is null) && lc1.Equals(lc2);
        }

        public static bool operator !=(LoadCaseModel lc1, LoadCaseModel lc2)
        {
            return !(lc1 == lc2);
        }

        public bool Equals(LoadCaseModel other)
        {
            return !(other is null) && _id == other._id && _name == other._name && _temperature == other._temperature &&
                _structuralMassAcceleration == other._structuralMassAcceleration &&
                _nonStructuralMassAcceleration == other._nonStructuralMassAcceleration &&
                _globalInertiaLoad == other._globalInertiaLoad && _gravityDirection == other._gravityDirection &&
                _gravityValue == other._gravityValue && _a == other._a && _v == other._v && _w == other._w &&
                _origin == other._origin && _stages == other._stages;
        }

        public override bool Equals(object obj)
        {
            if (obj is LoadCaseModel lc)
                return Equals(lc);
            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            var hashCode = -23;
            hashCode = hashCode * -17 + _id.GetHashCode();
            hashCode = hashCode * -17 + _name.GetHashCode();
            hashCode = hashCode * -17 + _temperature.GetHashCode();
            hashCode = hashCode * -17 + _structuralMassAcceleration.GetHashCode();
            hashCode = hashCode * -17 + _nonStructuralMassAcceleration.GetHashCode();
            hashCode = hashCode * -17 + _globalInertiaLoad.GetHashCode();
            hashCode = hashCode * -17 + _gravityDirection.GetHashCode();
            hashCode = hashCode * -17 + _gravityValue.GetHashCode();
            hashCode = hashCode * -17 + _a.GetHashCode();
            hashCode = hashCode * -17 + _v.GetHashCode();
            hashCode = hashCode * -17 + _w.GetHashCode();
            hashCode = hashCode * -17 + _origin.GetHashCode();
            if (_stages != null)
                hashCode = hashCode * -17 + _stages.GetHashCode();
            return hashCode;
        }

        #region Stage

        [Serializable]
        public class Stage : ISerializable, IEquatable<Stage>
        {
            public enum BridgePhases
            {
                Unassigned,
                OnlySteel,
                NInfinite,
                NInstant,
                StrainInfinite,
                StrainInstant,
            }

            private string _name;
            private BridgePhases _bridgePhases;
            private List<Operation> _operations;

            public string Name => _name;

            public BridgePhases BridgePhase { get => _bridgePhases; set => _bridgePhases = value; }

            public List<Operation> Operations => _operations;

            public Stage(string name)
            {
                _name = name;
                _operations = new List<Operation>();
                _bridgePhases = BridgePhases.Unassigned;
            }

            public Stage(Stage stage) 
                : this(stage.Name)
            {
                foreach (Operation o in stage.Operations)
                {
                    _operations.Add(new Operation(o));
                }
                _bridgePhases = stage.BridgePhase;
            }

            #region Serialization

            private string SERIALIZATION_RELEASE_NAME = "LoadCaseModel_Stage_SERIALIZATION_RELEASE"; //do not modify the name!
            private int SERIALIZATION_RELEASE = 1;

            protected Stage(SerializationInfo info, StreamingContext context)
            {
                int savedRelease;
                savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);
                _name = info.GetString("Name");
                _operations = (List<Operation>)info.GetValue("Operations", typeof(List<Operation>));
                _bridgePhases = (BridgePhases)info.GetValue("BridgePhase", typeof(BridgePhases));
            }

            [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
            public void GetObjectData(SerializationInfo info, StreamingContext context)
            {
                info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
                info.AddValue("Name", _name);
                info.AddValue("Operations", _operations, typeof(List<Operation>));
                info.AddValue("BridgePhase", _bridgePhases, typeof(BridgePhases));
            }

            #endregion

            public override bool Equals(object obj)
            {
                return Equals(obj as Stage);
            }

            public bool Equals(Stage other)
            {
                return other != null && _name == other._name &&
                    EqualityComparer<List<Operation>>.Default.Equals(_operations, other._operations);
            }

            public override int GetHashCode()
            {
                var hashCode = -528097243;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_name);
                hashCode = hashCode * -1521134295 + EqualityComparer<List<Operation>>.Default.GetHashCode(_operations);
                return hashCode;
            }

            #region Operation

            [Serializable]
            public class Operation : ISerializable, IEquatable<Operation>
            {
                public enum Codes
                {
                    [Description("Add structure")] AddStructure = 0,
                    [Description("Remove structure")] RemoveStructure = 1,
                    [Description("Load objects if new")] LoadObjectsIfNew = 2,
                    [Description("Load objects")] LoadObjects = 3,
                    [Description("Change section properties")] ChangeSectionProperties = 4,
                    [Description("Change section property modifiers")] ChangeSectionPropertyModifiers = 5,
                    [Description("Change releases")] ChangeReleases = 6,
                    [Description("Change section properties and age")] ChangeSectionPropertiesAndAge = 7
                }

                public enum ObjectTypes
                {
                    [Description("Group")] Group = 0,
                    [Description("Frame")] Frame = 1,
                    [Description("Cable")] Cable = 2,
                    [Description("Tendon")] Tendon = 3,
                    [Description("Area")] Area = 4,
                    [Description("Solid")] Solid = 5,
                    [Description("Link")] Link = 6,
                    [Description("Point")] Point = 7
                }

                public enum TypeCodes
                {
                    [Description("Load case")] LoadCase = 0,
                    [Description("Acceleration")] Acceleration = 1,
                    [Description("Frame")] Frame = 2,
                    [Description("Cable")] Cable = 3,
                    [Description("Tendon")] Tendon = 4,
                    [Description("Area")] Area = 5,
                    [Description("Solid")] Solid = 6,
                    [Description("Link")] Link = 7,
                }

                private Codes _code;
                private ObjectTypes _objectType;
                private string _objectName;
                private double _age;
                private TypeCodes _typeCode;
                private string _name;
                private double _scaleFactor;

                public Codes Code => _code;
                public ObjectTypes ObjectType => _objectType;
                public string ObjectName => _objectName;
                public double Age => _age;
                public TypeCodes TypeCode => _typeCode;
                public string Name => _name;
                public double ScaleFactor => _scaleFactor;

                public Operation(Codes code, ObjectTypes objectType, string objectName, double age, TypeCodes typeCode,
                    string name, double scaleFactor)
                {
                    _code = code;
                    _objectType = objectType;
                    _objectName = objectName;
                    _age = age;
                    _typeCode = typeCode;
                    _name = name;
                    _scaleFactor = scaleFactor;
                }

                public Operation(Operation o)
                    : this(o.Code, o.ObjectType, o.ObjectName, o.Age, o.TypeCode, o.Name, o.ScaleFactor)
                {
                }

                #region Serialization

                private string SERIALIZATION_RELEASE_NAME = "LoadCaseModel_Stage_Operation_SERIALIZATION_RELEASE"; //do not modify the name!
                private int SERIALIZATION_RELEASE = 1;

                protected Operation(SerializationInfo info, StreamingContext context)
                {
                    int savedRelease;
                    savedRelease = info.GetInt32(SERIALIZATION_RELEASE_NAME);
                    _code = (Codes)info.GetValue("Code", typeof(Codes));
                    _objectType = (ObjectTypes)info.GetValue("ObjectType", typeof(ObjectTypes));
                    _objectName = info.GetString("ObjectName");
                    _age = info.GetDouble("Age");
                    _typeCode = (TypeCodes)info.GetValue("TypeCode", typeof(TypeCodes));
                    _name = info.GetString("Name");
                    _scaleFactor = info.GetDouble("ScaleFactor");
                }

                [SecurityPermission(SecurityAction.LinkDemand, SerializationFormatter = true)]
                public void GetObjectData(SerializationInfo info, StreamingContext context)
                {
                    info.AddValue(SERIALIZATION_RELEASE_NAME, SERIALIZATION_RELEASE);
                    info.AddValue("Code", _code, typeof(Codes));
                    info.AddValue("ObjectType", _objectType, typeof(ObjectTypes));
                    info.AddValue("ObjectName", _objectName);
                    info.AddValue("Age", _age);
                    info.AddValue("TypeCode", _typeCode, typeof(TypeCodes));
                    info.AddValue("Name", _name);
                    info.AddValue("ScaleFactor", _scaleFactor);
                }

                #endregion

                public override bool Equals(object obj)
                {
                    return Equals(obj as Operation);
                }

                public bool Equals(Operation other)
                {
                    return other != null && _code == other._code && _objectType == other._objectType &&
                        _objectName == other._objectName && _age == other._age && _typeCode == other._typeCode &&
                        _name == other._name && _scaleFactor == other._scaleFactor;
                }

                public override int GetHashCode()
                {
                    var hashCode = -1692659674;
                    hashCode = hashCode * -1521134295 + _code.GetHashCode();
                    hashCode = hashCode * -1521134295 + _objectType.GetHashCode();
                    hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_objectName);
                    hashCode = hashCode * -1521134295 + _age.GetHashCode();
                    hashCode = hashCode * -1521134295 + _typeCode.GetHashCode();
                    hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_name);
                    hashCode = hashCode * -1521134295 + _scaleFactor.GetHashCode();
                    return hashCode;
                }
            }

            #endregion
        }

        #endregion

        public override string ToString()
        {
            return $"FeMM LoadCase: {Name}";
        }
    }
}
