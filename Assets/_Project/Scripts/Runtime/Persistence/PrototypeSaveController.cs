using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine.InputSystem;
#endif
using Wildshift.Core.Diagnostics;
using Wildshift.Environment.Samples;
using Wildshift.Player.Movement;
using Wildshift.Player.Regions;
using Wildshift.World;
using Wildshift.World.Clock;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Composition root that connects the local save foundation to the playable Nacre prototype. It owns
    /// the save service and the live <see cref="WorldStateService"/>, captures the player, world clock,
    /// region state, event history, and collected samples, and restores them. It is the only place that
    /// touches scene objects for saving, so the save data itself stays plain data.
    /// </summary>
    /// <remarks>
    /// <para><b>Explicit only.</b> Nothing saves on a timer or per frame. A save happens when
    /// <see cref="SaveGame"/> is called, and a load happens when <see cref="LoadGame"/> is called.
    /// Development builds and the editor also bind the keys configured below to those two calls.</para>
    /// <para><b>One operation at a time.</b> Each request is refused with
    /// <see cref="SaveLoadStatus.OperationInProgress"/> while another one is running, and the file layer
    /// serializes access to each save file as well.</para>
    /// <para><b>Staged load.</b> A load builds the restored world state, event history, and placement off
    /// to the side. It changes the live session only after every step has passed, so a rejected save leaves
    /// the current session exactly as it was. Restoring the event history, the clock, and the sample flags
    /// happens in the same call, so the player can never collect a sample again before its saved state is in
    /// place.</para>
    /// <para><b>Scope.</b> This restores the player pose, the region claim, the world clock, the region values,
    /// the bounded event history, and collected samples. It does not restore scan state, inventory, or
    /// anything else that does not exist in the prototype yet.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PrototypeSaveController : MonoBehaviour
    {
        /// <summary>Clearance added around the body when testing a restored position for obstruction.</summary>
        private const float BodySkin = 0.05f;

        [Header("World")]
        [SerializeField, Tooltip("Region catalog for this world. Required. It builds the registry that validates saved region IDs and creates fresh region state. Use the same catalog as the Region Locator.")]
        private WorldRegionCatalog _regionCatalog;

        [SerializeField, Tooltip("The player's region association. Required. Its position-derived region is compared with the saved claim after a load.")]
        private PlayerRegionAssociation _playerRegion;

        [SerializeField, Tooltip("The player movement component on the Player root. Required. Its transform and CharacterController are the body that is saved and placed.")]
        private ThirdPersonPlayerMovement _playerMovement;

        [SerializeField, Tooltip("World clock host. Required. Its elapsed time is saved and restored.")]
        private WorldClockHost _clockHost;

        [SerializeField, Tooltip("Event recorder host. Required. Its bounded history is saved, and its log is replaced on load.")]
        private PlayerActionEventRecorderHost _eventRecorderHost;

        [SerializeField, Tooltip("Every environmental sample in this scene whose collected state is saved. Each sample's stable ID comes from its definition.")]
        private EnvironmentalSampleInteractable[] _samples = Array.Empty<EnvironmentalSampleInteractable>();

        [SerializeField, Tooltip("Safe place for the player when a saved position cannot be used. Required. Place it in the prototype region, on the start pad.")]
        private Transform _safeSpawn;

        [Header("Placement")]
        [SerializeField, Tooltip("A saved position below this world height counts as fallen out of the world and is replaced by the safe spawn.")]
        private float _minimumValidHeight = -20f;

        [SerializeField, Tooltip("Layers that block a restored position. Trigger colliders are always ignored.")]
        private LayerMask _obstructionLayers = Physics.AllLayers;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Header("Development keys (editor and development builds only)")]
        [SerializeField, Tooltip("Key that saves the session. Set to None to disable.")]
        private Key _saveKey = Key.F5;

        [SerializeField, Tooltip("Key that loads the session. Set to None to disable.")]
        private Key _loadKey = Key.F9;
#endif

        private LocalSaveService _saveService;
        private WorldRegionRegistry _registry;
        private WorldStateService _worldState;
        private CharacterController _characterController;
        private bool _isReady;
        private bool _operationInProgress;
        private SaveOperationResult _lastResult;
        private bool _hasLastResult;

        /// <summary>True while a save or load is running. Requests made during that time are refused.</summary>
        public bool IsOperationInProgress => _operationInProgress;

        /// <summary>True once a request has been made, after which <see cref="LastResult"/> holds its outcome.</summary>
        public bool HasLastResult => _hasLastResult;

        /// <summary>Outcome of the most recent request. Its message is safe to show the player.</summary>
        public SaveOperationResult LastResult => _lastResult;

        /// <summary>World-state service that the next save captures. Replaced as a whole when a save is loaded.</summary>
        public WorldStateService WorldState => _worldState;

        /// <summary>
        /// Writes the current session to the primary save file, keeping the previous good save as the backup.
        /// Refused while another save or load is running.
        /// </summary>
        public SaveOperationResult SaveGame()
        {
            if (_operationInProgress)
            {
                return Record(RefuseBusy());
            }

            _operationInProgress = true;
            try
            {
                return Record(SaveCore());
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        /// <summary>
        /// Restores the session from the save file. A save that cannot be read or validated is reported and
        /// the current session is left unchanged. Refused while another save or load is running.
        /// </summary>
        public SaveOperationResult LoadGame()
        {
            if (_operationInProgress)
            {
                return Record(RefuseBusy());
            }

            _operationInProgress = true;
            try
            {
                return Record(LoadCore());
            }
            finally
            {
                _operationInProgress = false;
            }
        }

        /// <summary>
        /// Test seam: makes this controller use the given save service, such as one that writes to a temporary
        /// directory, instead of the default persistent-data location. Must be called before the first request.
        /// </summary>
        internal void UseSaveServiceForTests(LocalSaveService saveService)
        {
            _saveService = saveService;
        }

        private void Start()
        {
            if (!EnsureInitialized(out string error))
            {
                WildshiftLog.Warning($"Saving and loading are unavailable until the prototype is set up: {error}", this);
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (_saveKey != Key.None && keyboard[_saveKey].wasPressedThisFrame)
            {
                SaveGame();
            }
            else if (_loadKey != Key.None && keyboard[_loadKey].wasPressedThisFrame)
            {
                LoadGame();
            }
        }

        private void OnGUI()
        {
            // Development overlay only: a one-line status and any recovery notes. It never shows a file path.
            string saveHint = _saveKey == Key.None ? "save: unbound" : $"[{_saveKey}] save";
            string loadHint = _loadKey == Key.None ? "load: unbound" : $"[{_loadKey}] load";
            string status = _hasLastResult ? _lastResult.Message : "No save or load yet.";
            GUI.Label(new Rect(12f, 12f, 720f, 22f), $"{saveHint}   {loadHint}   {status}");

            if (!_hasLastResult)
            {
                return;
            }

            float y = 34f;
            foreach (string note in _lastResult.Recoveries)
            {
                GUI.Label(new Rect(12f, y, 900f, 22f), "Recovery: " + note);
                y += 20f;
            }
        }
#endif

        private SaveOperationResult SaveCore()
        {
            if (!EnsureInitialized(out string setupError))
            {
                WildshiftLog.Error($"Save refused: {setupError}", this);
                return Outcome(SaveLoadStatus.InvalidArgument, DescribeFailure(SaveLoadStatus.InvalidArgument, isLoad: false));
            }

            // Capture the region as of this moment. The association normally refreshes in LateUpdate, and a key
            // press can arrive after this frame's movement, so refresh it first.
            _playerRegion.RefreshRegionAssociation();
            Transform body = _playerMovement.transform;
            GameSaveData save = GameSaveMapper.Capture(
                body.position,
                body.rotation,
                _worldState,
                _eventRecorderHost.Recorder,
                GameSaveData.DefaultMaxWorldEvents,
                _playerRegion.CurrentRegionId,
                _clockHost.Clock.ElapsedTime,
                CollectCollectedSampleIds());

            SaveLoadStatus status = _saveService.TrySave(save, out _);
            if (status != SaveLoadStatus.Success)
            {
                return Outcome(status, DescribeFailure(status, isLoad: false));
            }

            return Outcome(SaveLoadStatus.Success, "Saved.");
        }

        private SaveOperationResult LoadCore()
        {
            if (!EnsureInitialized(out string setupError))
            {
                WildshiftLog.Error($"Load refused: {setupError}", this);
                return Outcome(SaveLoadStatus.InvalidArgument, DescribeFailure(SaveLoadStatus.InvalidArgument, isLoad: true));
            }

            // Read and validate the file. A failure here never changes the session or the file.
            SaveLoadStatus loadStatus = _saveService.TryLoad(out GameSaveData save, out _);
            if (loadStatus != SaveLoadStatus.Success && loadStatus != SaveLoadStatus.RecoveredFromBackup)
            {
                return Outcome(loadStatus, DescribeFailure(loadStatus, isLoad: true));
            }

            List<string> recoveries = new List<string>();

            // Stage 1: build every replacement off to the side. The live session is still untouched.
            WorldStateService stagedWorld = CreateFreshWorldState(out string worldError);
            if (stagedWorld == null)
            {
                WildshiftLog.Error($"Load refused: {worldError}", this);
                return Outcome(SaveLoadStatus.InvalidArgument, "The region setup is not valid, so nothing was loaded.");
            }

            PlayerActionEventRecorder stagedRecorder = new PlayerActionEventRecorder(_eventRecorderHost.Recorder.HistoryLimit);
            if (!GameSaveMapper.TryApply(save, stagedWorld, stagedRecorder, out PlayerSaveData player, out string applyError))
            {
                WildshiftLog.Error($"Load refused because the save could not be applied: {applyError}", this);
                return Outcome(SaveLoadStatus.InvalidData,
                    "The save could not be applied, so the current session was left unchanged.");
            }

            // Stage 2: decide the region and placement. These are decisions only and change nothing.
            SavedRegionResolution regionResolution =
                SaveRestorePolicy.ResolveRegion(player.RegionId, _registry, out _);
            if (regionResolution == SavedRegionResolution.Unregistered)
            {
                recoveries.Add("The saved region is not in this build's region catalog, so the region was taken from the restored position.");
            }

            SavedPlayerPlacement placement = ResolvePlacement(player);
            if (placement.IsRecovered)
            {
                recoveries.Add(
                    $"The saved position could not be used ({placement.RecoveryReason}), so the player was placed at the safe spawn point.");
            }

            HashSet<string> sceneSampleIds = SceneSampleIds();
            HashSet<string> savedSampleIds = new HashSet<string>(save.CollectedSampleIds, StringComparer.Ordinal);
            foreach (string sampleId in save.CollectedSampleIds)
            {
                if (!sceneSampleIds.Contains(sampleId))
                {
                    recoveries.Add($"The save lists sample '{sampleId}' as collected, but that sample is not in this scene. It was ignored.");
                }
            }

            // Stage 3: commit. None of these steps can fail for a validated save, and they all run in this call,
            // so the restored sample flags are in place before the player can act again.
            _worldState = stagedWorld;
            _eventRecorderHost.ReplaceRecorder(stagedRecorder);
            _clockHost.Clock.RestoreElapsedTicks(SaveRestorePolicy.ResolveElapsedWorldTicks(save));

            foreach (EnvironmentalSampleInteractable sample in SampleList())
            {
                if (sample != null && sample.Definition != null)
                {
                    sample.RestoreCollectedState(savedSampleIds.Contains(sample.Definition.StableId));
                }
            }

            ApplyPlacement(placement);
            _playerRegion.RefreshRegionAssociation();

            if (!placement.IsRecovered && regionResolution != SavedRegionResolution.Unregistered &&
                !string.Equals(player.RegionId, _playerRegion.CurrentRegionId, StringComparison.Ordinal))
            {
                recoveries.Add("The saved region did not match the restored position, so the region at the position was used.");
            }

            foreach (string note in recoveries)
            {
                WildshiftLog.Warning($"Load recovery: {note}", this);
            }

            string message;
            if (loadStatus == SaveLoadStatus.RecoveredFromBackup)
            {
                message = "The newest save could not be read, so the previous save was loaded.";
            }
            else
            {
                message = recoveries.Count == 0 ? "Loaded." : "Loaded, with recovery. See the notes.";
            }

            return new SaveOperationResult(loadStatus, message, recoveries);
        }

        private SavedPlayerPlacement ResolvePlacement(PlayerSaveData player)
        {
            // The body is switched off while the test runs, so the player's own capsule is not an obstacle.
            bool bodyWasEnabled = _characterController.enabled;
            _characterController.enabled = false;
            try
            {
                return SaveRestorePolicy.ResolvePlayerPlacement(
                    player,
                    _safeSpawn.position,
                    _safeSpawn.rotation,
                    _minimumValidHeight,
                    IsBodyObstructed);
            }
            finally
            {
                _characterController.enabled = bodyWasEnabled;
            }
        }

        private bool IsBodyObstructed(Vector3 position)
        {
            // Mirrors the CharacterController's capsule (centre, radius, and height, all scaled by the body's
            // transform), shrunk by a small skin so standing on the ground is not counted as overlap.
            Vector3 scale = _playerMovement.transform.lossyScale;
            float radius = Mathf.Max(0.01f,
                _characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)) - BodySkin);
            float height = Mathf.Max(radius * 2f,
                _characterController.height * Mathf.Abs(scale.y) - BodySkin * 2f);
            float halfSegment = (height * 0.5f) - radius;

            Vector3 centre = position + Vector3.Scale(_characterController.center, scale);
            Vector3 top = centre + (Vector3.up * halfSegment);
            Vector3 bottom = centre - (Vector3.up * halfSegment);
            return Physics.CheckCapsule(bottom, top, radius, _obstructionLayers, QueryTriggerInteraction.Ignore);
        }

        private void ApplyPlacement(SavedPlayerPlacement placement)
        {
            // Switching the movement component off also clears its stored velocity (see OnDisable), so the
            // player does not keep moving after the jump. The controller is moved while disabled, which skips
            // the collision sweep for the teleport.
            bool movementWasEnabled = _playerMovement.enabled;
            _playerMovement.enabled = false;
            _characterController.enabled = false;
            _playerMovement.transform.SetPositionAndRotation(placement.Position, placement.Orientation);
            _characterController.enabled = true;
            _playerMovement.enabled = movementWasEnabled;
        }

        private WorldStateService CreateFreshWorldState(out string error)
        {
            WorldStateService state = new WorldStateService();
            foreach (RegionDefinition definition in _registry.GetAllDefinitions())
            {
                if (!state.TryRegisterRegion(definition, out error))
                {
                    return null;
                }
            }

            error = null;
            return state;
        }

        private List<string> CollectCollectedSampleIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnvironmentalSampleInteractable sample in SampleList())
            {
                if (sample != null && sample.Definition != null && sample.IsCollected)
                {
                    ids.Add(sample.Definition.StableId);
                }
            }

            return new List<string>(ids);
        }

        private HashSet<string> SceneSampleIds()
        {
            HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (EnvironmentalSampleInteractable sample in SampleList())
            {
                if (sample != null && sample.Definition != null)
                {
                    ids.Add(sample.Definition.StableId);
                }
            }

            return ids;
        }

        private IReadOnlyList<EnvironmentalSampleInteractable> SampleList()
        {
            return _samples ?? Array.Empty<EnvironmentalSampleInteractable>();
        }

        /// <summary>
        /// Checks the scene wiring and builds the runtime objects the first time they are needed. It is retried
        /// on each request until it succeeds, so a request made before the scene is ready gets a clear answer.
        /// </summary>
        private bool EnsureInitialized(out string error)
        {
            if (_isReady)
            {
                error = null;
                return true;
            }

            List<string> problems = new List<string>();
            if (_regionCatalog == null)
            {
                problems.Add("the Region Catalog is not assigned");
            }

            if (_playerRegion == null)
            {
                problems.Add("the Player Region Association is not assigned");
            }

            if (_playerMovement == null)
            {
                problems.Add("the Player Movement is not assigned");
            }

            if (_clockHost == null)
            {
                problems.Add("the World Clock Host is not assigned");
            }

            if (_eventRecorderHost == null)
            {
                problems.Add("the Event Recorder Host is not assigned");
            }

            if (_safeSpawn == null)
            {
                problems.Add("the Safe Spawn point is not assigned");
            }

            if (problems.Count > 0)
            {
                error = $"the {name} save controller is incomplete: {string.Join("; ", problems)}.";
                return false;
            }

            CharacterController body = _playerMovement.GetComponent<CharacterController>();
            if (body == null)
            {
                error = "the Player Movement has no CharacterController to place.";
                return false;
            }

            if (_clockHost.Clock == null || _eventRecorderHost.Recorder == null)
            {
                error = "the world clock or event recorder has not started yet.";
                return false;
            }

            List<string> catalogErrors = new List<string>();
            if (!_regionCatalog.TryCreateRegistry(catalogErrors, out WorldRegionRegistry registry))
            {
                error = string.Join(" ", catalogErrors);
                return false;
            }

            _registry = registry;
            WorldStateService world = CreateFreshWorldState(out string worldError);
            if (world == null)
            {
                _registry = null;
                error = worldError;
                return false;
            }

            if (_saveService == null)
            {
                try
                {
                    _saveService = new LocalSaveService();
                }
                catch (ArgumentException exception)
                {
                    _registry = null;
                    error = "the save location is not available: " + exception.Message;
                    return false;
                }
            }

            _characterController = body;
            _worldState = world;
            _isReady = true;
            error = null;
            return true;
        }

        private SaveOperationResult Record(SaveOperationResult result)
        {
            _lastResult = result;
            _hasLastResult = true;
            return result;
        }

        private SaveOperationResult RefuseBusy()
        {
            WildshiftLog.Warning("A save or load is already running, so this request was refused.", this);
            return Outcome(SaveLoadStatus.OperationInProgress, DescribeFailure(SaveLoadStatus.OperationInProgress, isLoad: false));
        }

        private static SaveOperationResult Outcome(SaveLoadStatus status, string message)
        {
            return new SaveOperationResult(status, message, null);
        }

        private static string DescribeFailure(SaveLoadStatus status, bool isLoad)
        {
            switch (status)
            {
                case SaveLoadStatus.NoSaveFile:
                    return "No save was found.";
                case SaveLoadStatus.RecoveredFromBackup:
                    return "The newest save could not be read, so the previous save was loaded.";
                case SaveLoadStatus.InvalidArgument:
                    return "The save system is not set up in this scene.";
                case SaveLoadStatus.InvalidData:
                    return isLoad
                        ? "The save failed validation and was not applied."
                        : "The save data is not valid, so nothing was written.";
                case SaveLoadStatus.MalformedData:
                    return "The save file is not a readable WILDSHIFT save, so nothing was applied.";
                case SaveLoadStatus.IncompatibleVersion:
                    return "This save was written by a different version of the game, so nothing was applied.";
                case SaveLoadStatus.ReadFailed:
                    return "The save file exists but could not be read.";
                case SaveLoadStatus.WriteFailed:
                    return "The save could not be written. The previous save is still intact.";
                case SaveLoadStatus.OperationInProgress:
                    return "Another save or load is still running. Try again in a moment.";
                default:
                    return isLoad ? "The save could not be loaded." : "The save could not be written.";
            }
        }
    }
}
