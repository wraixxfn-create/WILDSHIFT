using UnityEngine;

namespace Wildshift.Environment.Samples
{
    /// <summary>
    /// Designer-authored identity and description for one collectable environmental sample.
    /// Treat this shared asset as read-only at runtime: collection state lives on the scene
    /// object (<see cref="EnvironmentalSampleInteractable"/>) and on the per-session state,
    /// never on the authored asset, so the same definition can be reused or duplicated.
    /// </summary>
    /// <remarks>
    /// The stable ID is typed once by an author and never generated from the scene hierarchy
    /// or GameObject name, so moving or renaming the scene object does not invalidate event
    /// records or saved state.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "EnvironmentalSampleDefinition",
        menuName = "Wildshift/Environment/Environmental Sample Definition")]
    public sealed class EnvironmentalSampleDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("Stable data ID used by events and saves, for example 'nacre/sample/disturbed-soil-a'. " +
                                 "Required, unique, and without spaces. It is never generated from the scene hierarchy.")]
        private string _stableId;

        [SerializeField, Tooltip("Short authored label shown in the interaction prompt and collection feedback.")]
        private string _displayName;

        [SerializeField, TextArea(2, 4), Tooltip("Author-facing interaction prompt, e.g. 'Press E to collect sample'. " +
                                                 "Used by the interaction UI while the sample is aimed at and uncollected.")]
        private string _interactPrompt = "Press E to collect sample";

        [SerializeField, TextArea(3, 6), Tooltip("Concise in-world description shown once when the sample is collected. " +
                                                 "Keep it to one or two sentences so the temporary feedback stays readable.")]
        private string _collectedDescription;

        /// <summary>Stable authored sample ID used by events and any future persistence.</summary>
        public string StableId => _stableId;

        /// <summary>Short authored label; never used as the identity key.</summary>
        public string DisplayName => _displayName;

        /// <summary>Prompt text shown while the player is aiming at an uncollected instance.</summary>
        public string InteractPrompt => _interactPrompt;

        /// <summary>Short description shown once on collection.</summary>
        public string CollectedDescription => _collectedDescription;
    }
}
