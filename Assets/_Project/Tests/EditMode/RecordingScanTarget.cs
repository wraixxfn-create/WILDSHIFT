using UnityEngine;
using Wildshift.Interaction;

namespace Wildshift.Tests
{
    /// <summary>Test double that exposes authored scan fields without a ScriptableObject definition.</summary>
    public sealed class RecordingScanTarget : MonoBehaviour, IScanTarget
    {
        public string TargetId { get; set; } = "test/scan/target-a";
        public string DisplayName { get; set; } = "Test scan target";
        public string Description { get; set; } = "A fixture used by scanner tests.";
        public string ScanResult { get; set; } = "Harmless residue.";
        public bool Available = true;

        public bool IsScanAvailable => Available && !string.IsNullOrWhiteSpace(TargetId);
    }
}
