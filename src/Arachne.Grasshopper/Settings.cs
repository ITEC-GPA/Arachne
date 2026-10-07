using Grasshopper.Kernel;

namespace Arachne.Grasshopper
{
    /// <summary>
    /// Persistent plug-in settings. FeMM kept the node merging tolerance in its Rhino plug-in;
    /// Arachne keeps it here, with the same default value.
    /// </summary>
    public sealed class Settings
    {
        private static Settings _instance;
        private GH_SettingsServer _server;
        private bool _showSolids;
        private bool _showAttributes;
        private double _tolerance;

        public static Settings Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new Settings();
                return _instance;
            }
        }

        public bool ShowSolids
        {
            get => _showSolids;
            set
            {
                if (_showSolids != value)
                {
                    _showSolids = value;
                    _server.SetValue("ShowSolids", _showSolids);
                }
            }
        }

        public bool ShowAttributes
        {
            get => _showAttributes;
            set
            {
                if (_showAttributes != value)
                {
                    _showAttributes = value;
                    _server.SetValue("ShowAttributes", _showAttributes);
                }
            }
        }

        /// <summary>
        /// Distance under which Build Model merges two nodes, in model units.
        /// </summary>
        public double Tolerance
        {
            get => _tolerance;
            set
            {
                if (_tolerance != value)
                {
                    _tolerance = value;
                    _server.SetValue("Tolerance", _tolerance);
                }
            }
        }

        private Settings()
        {
            _server = new GH_SettingsServer("Arachne", true);
            _showSolids = _server.GetValue("ShowSolids", true);
            _showAttributes = _server.GetValue("ShowAttributes", true);
            _tolerance = _server.GetValue("Tolerance", 0.001);
        }

        public void Save()
        {
            _server.WritePersistentSettings();
        }
    }
}
