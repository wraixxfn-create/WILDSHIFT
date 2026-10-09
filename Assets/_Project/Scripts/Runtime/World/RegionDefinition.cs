using UnityEngine;

namespace Wildshift.World
{
    /// <summary>
    /// Designer-authored identity and starting configuration for one world region.
    /// Treat this shared asset as read-only at runtime; <see cref="WorldStateService"/> copies it
    /// into a fresh <see cref="RegionState"/> for each world-state instance.
    /// </summary>
    [CreateAssetMenu(fileName = "RegionDefinition", menuName = "Wildshift/World/Region Definition")]
    public sealed class RegionDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable data ID used by saves and gameplay. Do not use a scene or GameObject name.")]
        private string _stableId;

        [SerializeField, Tooltip("Optional authored label for display and debugging; this is not the region's identity.")]
        private string _displayName;

        [SerializeField, Tooltip("Foundation-only starting value copied into runtime state. This has no gameplay meaning.")]
        private int _initialTestValue;

        /// <summary>Stable authored region ID. It must be unique within a WorldStateService.</summary>
        public string StableId => _stableId;

        /// <summary>Optional human-readable label; never used as the region key.</summary>
        public string DisplayName => _displayName;

        /// <summary>Initial foundation-only test value copied into each runtime state.</summary>
        public int InitialTestValue => _initialTestValue;

        internal RegionState CreateInitialState()
        {
            return new RegionState(_stableId, _displayName, _initialTestValue);
        }
    }
}
