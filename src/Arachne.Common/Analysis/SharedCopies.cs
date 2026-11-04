using GPC.Model.Core;

namespace Arachne.Common.Analysis
{
    /// <summary>What the copies of Arachne share with their source (user decisions of 4/11/2026, lane L9): the properties with their
    /// materials and sections («Condividi proprietà») and the coordinate systems («si», provided a test shows they stay unchanged:
    /// headless check F6 SharedProperties). Elements, nodes, loads, cases and groups are copied. No component changes a property, a
    /// material, a section or a coordinate system it receives: one that needs another creates a new one and assigns it.</summary>
    public static class SharedCopies
    {
        public static readonly ModelCopyOptions Options = ModelCopyOptions.ShareProperties | ModelCopyOptions.ShareCoordinateSystems;
    }
}
