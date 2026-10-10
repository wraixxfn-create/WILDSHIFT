using System;
using System.Collections.Generic;
using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Interaction;
using Wildshift.Player.Input;
using Wildshift.Player.Regions;
using Wildshift.World.Clock;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Player.Scanning
{
    /// <summary>
    /// Handheld environmental scanner. On the configured ability button it casts one camera ray,
    /// classifies the aimed object, and records a successful scan in the player action event log.
    /// Presentation is not done here: listeners of <see cref="ScanAttempted"/> display feedback.
    /// </summary>
    /// <remarks>
    /// Raycast rules match <c>PlayerInteractionDetector</c>: the ray starts at the view camera and
    /// follows its forward axis, colliders on this GameObject are ignored, trigger colliders are
    /// ignored, the nearest remaining hit decides the result, and a saturated hit buffer fails
    /// closed. Range is measured from this GameObject to the hit point, not from the camera.
    /// Solid obstacles always block scans; there is no option to scan through geometry.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerEnvironmentalScanner : MonoBehaviour
    {
        /// <summary>Parameter ID recorded on every logged scan event, carrying 1f.</summary>
        public const string ScanParameterId = "environment-scan";

        private const int MaxHits = 16;

        [Header("References")]
        [SerializeField, Tooltip("Prompt 3 input reader. The scanner listens for the configured scan button.")]
        private PlayerInputReader _input;
        [SerializeField, Tooltip("Camera whose forward axis aims the scan ray. Usually the Main Camera.")]
        private UnityEngine.Camera _viewCamera;
        [SerializeField, Tooltip("World clock host used to timestamp scan events. Falls back to FindFirstObjectByType if unset.")]
        private WorldClockHost _clockHost;
        [SerializeField, Tooltip("Player region association used to tag successful scan events at commit time. Falls back to this GameObject's component if unset.")]
        private PlayerRegionAssociation _regionAssociation;
        [SerializeField, Tooltip("Event recorder host that receives successful scans. Falls back to PlayerActionEventRecorderHost.FindFirstAvailable if unset.")]
        private PlayerActionEventRecorderHost _eventRecorderHost;

        [Header("Input")]
        [SerializeField, Tooltip("Existing gameplay button that activates the scanner. Defaults to Ability Primary (keyboard 1).")]
        private PlayerInputButton _scanButton = PlayerInputButton.AbilityPrimary;

        [Header("Detection")]
        [SerializeField, Min(0f), Tooltip("Maximum distance in metres from this object's position to a valid scan hit.")]
        private float _scanRange = 8f;
        [SerializeField, Min(0f), Tooltip("How far the ray looks when classifying an out-of-range scan target. Must be at least the scan range.")]
        private float _probeRange = 24f;
        [SerializeField, Tooltip("Layers the aim ray can hit. Include scan-target layers and environment layers that should block line of sight. Trigger colliders are ignored.")]
        private LayerMask _detectionLayers = Physics.DefaultRaycastLayers;

        [Header("Repeat scans")]
        [SerializeField, Tooltip("When enabled, the player may scan the same target again this session and see its authored result. When disabled, further scans of that target are rejected.")]
        private bool _allowRepeatScans = true;
        [SerializeField, Tooltip("When enabled, every successful scan of a target is recorded in the event log. When disabled (the default), only the first successful scan of each target ID is recorded so repeats cannot flood the log.")]
        private bool _recordRepeatScans;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private readonly HashSet<string> _scannedTargetIds = new HashSet<string>(StringComparer.Ordinal);
        private PlayerInputReader _subscribedInput;
        private bool _missingRegionContextLogged;

        /// <summary>Raised after every scan attempt, including invalid, out-of-range, and rejected repeats.</summary>
        public event Action<ScanAttemptResult> ScanAttempted;

        /// <summary>Result of the most recent attempt, or null before the first press.</summary>
        public ScanAttemptResult LastResult { get; private set; }

        /// <summary>True when this session has already logged or accepted a scan of <paramref name="targetId"/>.</summary>
        public bool HasScanned(string targetId)
        {
            return !string.IsNullOrWhiteSpace(targetId) && _scannedTargetIds.Contains(targetId);
        }

        private void Awake()
        {
            if (_input == null)
            {
                WildshiftLog.Error(
                    $"{nameof(PlayerEnvironmentalScanner)} on '{name}' has no Player Input Reader assigned. " +
                    "The scanner button will not fire.",
                    this);
            }

            if (_viewCamera == null)
            {
                WildshiftLog.Error(
                    $"{nameof(PlayerEnvironmentalScanner)} on '{name}' has no View Camera assigned. " +
                    "Scanning is disabled.",
                    this);
            }

            ResolveRegionContext();
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
        }

        /// <summary>
        /// Runs one scan from the current camera aim. Used by the input button and by Edit Mode tests.
        /// Always produces a <see cref="ScanAttemptResult"/> and raises <see cref="ScanAttempted"/>.
        /// </summary>
        internal ScanAttemptResult TryScan()
        {
            ScanAttemptResult result = PerformScan();
            LastResult = result;
            ScanAttempted?.Invoke(result);
            return result;
        }

        private ScanAttemptResult PerformScan()
        {
            if (_viewCamera == null)
            {
                return ScanAttemptResult.InvalidTarget();
            }

            Transform view = _viewCamera.transform;
            Vector3 origin = view.position;
            Vector3 anchor = transform.position;
            float scanRange = Mathf.Max(0f, _scanRange);
            float probeRange = Mathf.Max(scanRange, _probeRange);
            float maxDistance = probeRange + Vector3.Distance(origin, anchor);

            int count = Physics.RaycastNonAlloc(
                origin, view.forward, _hits, maxDistance, _detectionLayers, QueryTriggerInteraction.Ignore);
            if (count <= 0 || count >= _hits.Length)
            {
                return ScanAttemptResult.InvalidTarget();
            }

            bool hasNearest = false;
            RaycastHit nearest = default;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (IsOwnCollider(hit.collider))
                {
                    continue;
                }

                if (!hasNearest || hit.distance < nearest.distance)
                {
                    nearest = hit;
                    hasNearest = true;
                }
            }

            if (!hasNearest)
            {
                return ScanAttemptResult.InvalidTarget();
            }

            IScanTarget candidate = nearest.collider.GetComponentInParent<IScanTarget>();
            if (!IsUsable(candidate) || !candidate.IsScanAvailable || string.IsNullOrWhiteSpace(candidate.TargetId))
            {
                return ScanAttemptResult.InvalidTarget();
            }

            float playerDistance = Vector3.Distance(nearest.point, anchor);
            if (playerDistance > scanRange)
            {
                return ScanAttemptResult.OutOfRange(candidate.TargetId, candidate.DisplayName);
            }

            bool isRepeat = _scannedTargetIds.Contains(candidate.TargetId);
            if (isRepeat && !_allowRepeatScans)
            {
                return ScanAttemptResult.RepeatRejected(
                    candidate.TargetId,
                    candidate.DisplayName,
                    candidate.Description,
                    candidate.ScanResult,
                    regionId: null);
            }

            // A successful in-range classification is the scan's commit point. Resolve the player's
            // position now, so movement during any future multi-frame scan records the completion region.
            string regionId = ResolveRegionIdForAction();
            bool shouldRecord = !isRepeat || _recordRepeatScans;
            bool recorded = false;
            if (shouldRecord)
            {
                recorded = TryRecordScan(candidate.TargetId, regionId);
            }

            _scannedTargetIds.Add(candidate.TargetId);

            if (recorded)
            {
                string regionDescription = regionId != null
                    ? $"region '{regionId}'"
                    : "unregistered space";
                WildshiftLog.Info(
                    $"Scanned '{candidate.DisplayName}' ({candidate.TargetId}) in {regionDescription}.",
                    this);
            }

            return ScanAttemptResult.Success(
                candidate.TargetId,
                candidate.DisplayName,
                candidate.Description,
                candidate.ScanResult,
                regionId,
                recorded,
                isRepeat);
        }

        private bool TryRecordScan(string targetId, string regionId)
        {
            PlayerActionEventRecorder recorder = ResolveRecorder();
            if (recorder == null)
            {
                WildshiftLog.Warning(
                    $"Cannot record scan event for '{targetId}' because no PlayerActionEventRecorderHost is available in the scene.",
                    this);
                return false;
            }

            PlayerActionEvent evt = new PlayerActionEvent(
                PlayerActionEvent.NewId(),
                PlayerActionEventType.EnvironmentScan,
                ResolveElapsedTime(),
                regionId: regionId,
                targetId: targetId,
                magnitude: 1f,
                parameters: new[]
                {
                    new PlayerActionEventParameter(ScanParameterId, 1f),
                });

            if (!recorder.TryRecord(evt, out _, out string error))
            {
                WildshiftLog.Warning($"Failed to record scan event for '{targetId}': {error}", this);
                return false;
            }

            return true;
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

        private string ResolveRegionIdForAction()
        {
            IWorldRegionContext regionContext = ResolveRegionContext();
            return regionContext != null && regionContext.TryResolveRegionForAction(out string stableId)
                ? stableId
                : null;
        }

        private IWorldRegionContext ResolveRegionContext()
        {
            if (_regionAssociation == null)
            {
                // Local component lookup only; never search the scene from the scan path.
                _regionAssociation = GetComponent<PlayerRegionAssociation>();
            }

            if (_regionAssociation != null)
            {
                return _regionAssociation;
            }

            if (!_missingRegionContextLogged)
            {
                WildshiftLog.Warning(
                    $"{nameof(PlayerEnvironmentalScanner)} on '{name}' has no {nameof(PlayerRegionAssociation)}. " +
                    "Any recorded scan will omit the region ID.",
                    this);
                _missingRegionContextLogged = true;
            }

            return null;
        }

        private void OnButtonPressed(PlayerInputButton button)
        {
            if (button == _scanButton)
            {
                TryScan();
            }
        }

        private bool IsOwnCollider(Collider hitCollider)
        {
            return hitCollider == null || hitCollider.transform.IsChildOf(transform);
        }

        private static bool IsUsable(IScanTarget candidate)
        {
            if (candidate is not Component component || component == null)
            {
                return false;
            }

            if (!component.gameObject.activeInHierarchy)
            {
                return false;
            }

            return component is not Behaviour behaviour || behaviour.enabled;
        }

        private void SubscribeInput()
        {
            if (_subscribedInput != null || _input == null)
            {
                return;
            }

            _subscribedInput = _input;
            _subscribedInput.ButtonPressed += OnButtonPressed;
        }

        private void UnsubscribeInput()
        {
            if (_subscribedInput == null)
            {
                return;
            }

            _subscribedInput.ButtonPressed -= OnButtonPressed;
            _subscribedInput = null;
        }
    }
}
