using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Interaction;

namespace Wildshift.Environment.Scanning
{
    /// <summary>
    /// Scene component that marks an object as a valid environmental scan target.
    /// Authored identity and text live on the referenced
    /// <see cref="EnvironmentalScanDefinition"/>; this component only exposes them through
    /// <see cref="IScanTarget"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnvironmentalScanTarget : MonoBehaviour, IScanTarget
    {
        [SerializeField, Tooltip("Authored scan definition. Required; defines the stable ID, display name, description, and scan result.")]
        private EnvironmentalScanDefinition _definition;

        /// <summary>The authored definition driving this instance.</summary>
        public EnvironmentalScanDefinition Definition => _definition;

        /// <inheritdoc />
        public string TargetId => _definition != null ? _definition.StableId : null;

        /// <inheritdoc />
        public string DisplayName => _definition != null ? _definition.DisplayName : null;

        /// <inheritdoc />
        public string Description => _definition != null ? _definition.Description : null;

        /// <inheritdoc />
        public string ScanResult => _definition != null ? _definition.ScanResult : null;

        /// <inheritdoc />
        public bool IsScanAvailable =>
            isActiveAndEnabled
            && _definition != null
            && !string.IsNullOrWhiteSpace(_definition.StableId);

        private void Awake()
        {
            if (_definition == null)
            {
                WildshiftLog.Error(
                    $"{nameof(EnvironmentalScanTarget)} on '{name}' has no EnvironmentalScanDefinition assigned. " +
                    "The scanner will treat this object as an invalid target.",
                    this);
            }
        }
    }
}
