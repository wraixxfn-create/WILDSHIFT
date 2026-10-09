using UnityEngine;

namespace Wildshift.World
{
    /// <summary>
    /// Designer-authored identity and starting configuration for one world region. This is the single
    /// authored region asset: the region catalog, the region registry, and region volumes all reference it.
    /// Treat this shared asset as read-only at runtime; <see cref="WorldStateService"/> copies it
    /// into a fresh <see cref="RegionState"/> for each world-state instance.
    /// </summary>
    /// <remarks>
    /// Authoring rules are documented in <c>docs/world-regions.md</c>. The stable ID is never generated
    /// automatically: an author types it once, and it must not change after saves or other data reference it.
    /// </remarks>
    [CreateAssetMenu(fileName = "RegionDefinition", menuName = "Wildshift/World/Region Definition")]
    public sealed class RegionDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable data ID used by saves and gameplay, for example 'nacre/coast/north'. " +
                                 "Required, unique within a region catalog, and without spaces. It is never generated automatically. " +
                                 "Do not use a scene, GameObject, or asset name, and do not change it once saves reference it.")]
        private string _stableId;

        [SerializeField, Tooltip("Optional authored label for display and debugging; this is not the region's identity.")]
        private string _displayName;

        [SerializeField, TextArea(2, 4),
         Tooltip("Short authored description of the region. Designer-facing text only; it has no gameplay meaning yet.")]
        private string _description;

        [SerializeField, Tooltip("Foundation-only starting value copied into runtime state. This has no gameplay meaning.")]
        private int _initialTestValue;

        /// <summary>Stable authored region ID. It must be unique within a region catalog and a WorldStateService.</summary>
        public string StableId => _stableId;

        /// <summary>Optional human-readable label; never used as the region key.</summary>
        public string DisplayName => _displayName;

        /// <summary>Optional short authored description; never used as the region key.</summary>
        public string Description => _description;

        /// <summary>Initial foundation-only test value copied into each runtime state.</summary>
        public int InitialTestValue => _initialTestValue;

        internal RegionState CreateInitialState()
        {
            return new RegionState(_stableId, _displayName, _initialTestValue);
        }
    }
}
