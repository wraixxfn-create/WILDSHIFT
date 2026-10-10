using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Interaction;
using Wildshift.World.Clock;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Environment.Samples
{
    /// <summary>
    /// Interactable environmental sample that can be collected once per play session.
    /// The authored identity lives in the referenced <see cref="EnvironmentalSampleDefinition"/>
    /// asset; the mutable "has been collected" flag is per-instance, per-session runtime state.
    /// </summary>
    /// <remarks>
    /// <para>When the player aims at an uncollected instance within range the interaction detector
    /// (already in place from Prompt 7) treats it as any other <see cref="IInteractable"/>. A press
    /// of the existing Interact input records a
    /// <see cref="PlayerActionEventType.ResourceExtraction"/> event with the sample's stable ID as
    /// <c>targetId</c> and the containing region as <c>regionId</c>, marks the instance collected
    /// for this session, surfaces concise feedback via <see cref="SampleCollectionFeedback"/>, and
    /// disables further interaction until the scene is reloaded.</para>
    /// <para>No persistent save is made. Duplicate collection is guarded both by
    /// <see cref="CanInteract"/> returning false after collection and by an idempotency check
    /// inside <see cref="Interact"/>.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentalSampleInteractable : MonoBehaviour, IInteractable, IInteractionPromptProvider
    {
        /// <summary>Parameter ID recorded on every sample-collection event, carrying 1f.</summary>
        public const string CollectedParameterId = "sample-collected";

        [Header("Authoring")]
        [SerializeField, Tooltip("Authored sample definition. Required; defines the stable ID, prompt, and description.")]
        private EnvironmentalSampleDefinition _definition;

        [Header("Scene references (optional; auto-resolved if unset)")]
        [SerializeField, Tooltip("World clock host used to timestamp the collection event. Falls back to FindFirstObjectByType if unset.")]
        private WorldClockHost _clockHost;
        [SerializeField, Tooltip("Region locator used to tag the event with the containing region. Falls back to FindFirstObjectByType if unset.")]
        private WorldRegionLocator _regionLocator;
        [SerializeField, Tooltip("Event recorder host that receives the collection event. Falls back to PlayerActionEventRecorderHost.FindFirstAvailable if unset.")]
        private PlayerActionEventRecorderHost _eventRecorderHost;

        private bool _collected;

        /// <summary>True after a successful collection for this play session.</summary>
        public bool IsCollected => _collected;

        /// <summary>The authored definition driving this instance. Exposed for the UI prompt.</summary>
        public EnvironmentalSampleDefinition Definition => _definition;

        private void Awake()
        {
            if (_definition == null)
            {
                WildshiftLog.Error(
                    $"{nameof(EnvironmentalSampleInteractable)} on '{name}' has no EnvironmentalSampleDefinition " +
                    "assigned. The sample will not accept interactions.", this);
            }
        }

        /// <summary>
        /// Returns the interact prompt from the authored definition while the sample is still
        /// collectable, or null once it has been collected (so no prompt hovers over a spent sample).
        /// </summary>
        public string GetInteractionPrompt()
        {
            if (!CanInteract(gameObject) || _definition == null)
            {
                return null;
            }

            string prompt = _definition.InteractPrompt;
            return string.IsNullOrWhiteSpace(prompt) ? null : prompt;
        }

        /// <inheritdoc />
        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled
                   && interactor != null
                   && _definition != null
                   && !string.IsNullOrWhiteSpace(_definition.StableId)
                   && !_collected;
        }

        /// <inheritdoc />
        public void Interact(GameObject interactor)
        {
            // Defensive double-check so repeated presses or direct calls cannot duplicate.
            if (!CanInteract(interactor))
            {
                return;
            }

            _collected = true;

            string regionId = ResolveRegionId();
            double elapsedTime = ResolveElapsedTime();

            RecordCollectionEvent(elapsedTime, regionId);
            ShowCollectionFeedback();

            WildshiftLog.Info(
                $"Collected environmental sample '{_definition.DisplayName}' ({_definition.StableId}) " +
                $"in region '{regionId}' at world time {elapsedTime:0.###}s.", this);
        }

        private void RecordCollectionEvent(double elapsedWorldTime, string regionId)
        {
            PlayerActionEventRecorder recorder = ResolveRecorder();
            if (recorder == null)
            {
                WildshiftLog.Warning(
                    $"Cannot record collection event for sample '{_definition.StableId}' because no " +
                    "PlayerActionEventRecorderHost is available in the scene.", this);
                return;
            }

            PlayerActionEvent evt = new PlayerActionEvent(
                PlayerActionEvent.NewId(),
                PlayerActionEventType.ResourceExtraction,
                elapsedWorldTime,
                regionId: regionId,
                targetId: _definition.StableId,
                magnitude: 1f,
                parameters: new[]
                {
                    new PlayerActionEventParameter(CollectedParameterId, 1f),
                });

            if (!recorder.TryRecord(evt, out _, out string error))
            {
                WildshiftLog.Warning(
                    $"Failed to record collection event for sample '{_definition.StableId}': {error}", this);
            }
        }

        private void ShowCollectionFeedback()
        {
            SampleCollectionFeedback feedback = SampleCollectionFeedback.Instance;
            if (feedback != null)
            {
                feedback.Show(_definition);
            }
        }

        private PlayerActionEventRecorder ResolveRecorder()
        {
            if (_eventRecorderHost != null && _eventRecorderHost.Recorder != null)
            {
                return _eventRecorderHost.Recorder;
            }

            PlayerActionEventRecorderHost host = PlayerActionEventRecorderHost.FindFirstAvailable();
            if (host != null)
            {
                _eventRecorderHost = host;
                return host.Recorder;
            }

            return null;
        }

        private double ResolveElapsedTime()
        {
            if (_clockHost == null || _clockHost.Clock == null)
            {
                _clockHost = FindFirstObjectByType<WorldClockHost>();
            }

            if (_clockHost != null && _clockHost.Clock != null)
            {
                return _clockHost.Clock.ElapsedTime;
            }

            return 0d;
        }

        private string ResolveRegionId()
        {
            if (_regionLocator == null)
            {
                _regionLocator = FindFirstObjectByType<WorldRegionLocator>();
            }

            if (_regionLocator == null || !_regionLocator.IsAvailable)
            {
                return null;
            }

            WorldRegionQueryResult result = _regionLocator.FindRegionAt(transform.position);
            string stableId = result.RegionId;
            return string.IsNullOrWhiteSpace(stableId) ? null : stableId;
        }
    }
}
