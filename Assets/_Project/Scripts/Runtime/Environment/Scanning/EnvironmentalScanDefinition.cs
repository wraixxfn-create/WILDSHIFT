using UnityEngine;

namespace Wildshift.Environment.Scanning
{
    /// <summary>
    /// Designer-authored identity and scan text for one environmental scan target.
    /// Treat this shared asset as read-only at runtime: whether the player has already scanned
    /// the object lives on the player scanner, never on this asset.
    /// </summary>
    [CreateAssetMenu(
        fileName = "EnvironmentalScanDefinition",
        menuName = "Wildshift/Environment/Environmental Scan Definition")]
    public sealed class EnvironmentalScanDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable data ID used by events, for example 'nacre/sample/disturbed-soil-a'. " +
                                 "Required, unique, and without spaces. It is never generated from the scene hierarchy.")]
        private string _stableId;

        [SerializeField, Tooltip("Short authored label shown in scan feedback.")]
        private string _displayName;

        [SerializeField, TextArea(2, 4), Tooltip("Short description of what the player is looking at. One or two sentences.")]
        private string _description;

        [SerializeField, TextArea(2, 4), Tooltip("Simple authored scan result shown after a successful scan. Keep it readable; do not invent a classification system.")]
        private string _scanResult;

        /// <summary>Stable authored target ID used by events.</summary>
        public string StableId => _stableId;

        /// <summary>Short authored label; never used as the identity key.</summary>
        public string DisplayName => _displayName;

        /// <summary>Short authored description of the object.</summary>
        public string Description => _description;

        /// <summary>Simple authored scan readout.</summary>
        public string ScanResult => _scanResult;
    }
}
