using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildshift.Player.Input;
using Wildshift.Player.Scanning;
using Wildshift.World;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>
    /// Exercises scanner aim, range, line of sight, invalid targets, repeat-scan logging, region
    /// identification, and the Ability Primary binding using real physics raycasts.
    /// The fixture sits far from the graybox scene geometry.
    /// </summary>
    public sealed class PlayerEnvironmentalScannerTests
    {
        private const string InputActionsPath = "Assets/_Project/Data/Input/PlayerInputActions.inputactions";
        private static readonly Vector3 Offset = new Vector3(2000f, 0f, 2000f);

        private GameObject _inputObject;
        private GameObject _cameraObject;
        private GameObject _playerObject;
        private GameObject _targetObject;
        private GameObject _wallObject;
        private GameObject _hostObject;
        private PlayerInputReader _input;
        private PlayerEnvironmentalScanner _scanner;
        private RecordingScanTarget _target;
        private PlayerActionEventRecorderHost _host;
        private Keyboard _keyboard;

        // Camera at z=-6 looks along +Z. The player is at z=-3 (range origin); the target face is at z=-1.5.
        // The range origin to the target face is 1.5 m, inside the 2 m test scan range.
        [SetUp]
        public void SetUp()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(inputActions, Is.Not.Null, $"Could not load InputActionAsset at {InputActionsPath}.");

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _inputObject = new GameObject("Scanner Input Test");
            _inputObject.SetActive(false);
            _input = _inputObject.AddComponent<PlayerInputReader>();
            SetObjectReference(_input, "_inputActions", inputActions);
            _inputObject.SetActive(true);

            _cameraObject = new GameObject("Scanner Camera Test");
            _cameraObject.AddComponent<UnityEngine.Camera>();
            _cameraObject.transform.position = Offset + new Vector3(0f, 1f, -6f);
            _cameraObject.transform.rotation = Quaternion.identity;

            _hostObject = new GameObject("Scanner Event Recorder Host Test");
            _host = _hostObject.AddComponent<PlayerActionEventRecorderHost>();

            _playerObject = new GameObject("Scanner Player Test");
            _playerObject.transform.position = Offset + new Vector3(0f, 1f, -3f);
            _playerObject.SetActive(false);
            _scanner = _playerObject.AddComponent<PlayerEnvironmentalScanner>();
            SetObjectReference(_scanner, "_input", _input);
            SetObjectReference(_scanner, "_viewCamera", _cameraObject.GetComponent<UnityEngine.Camera>());
            SetObjectReference(_scanner, "_eventRecorderHost", _host);
            SetFloat(_scanner, "_scanRange", 2f);
            SetFloat(_scanner, "_probeRange", 20f);
            _playerObject.SetActive(true);

            _targetObject = new GameObject("Scanner Target Test");
            _targetObject.transform.position = Offset + new Vector3(0f, 1f, -1f);
            _targetObject.AddComponent<BoxCollider>();
            _target = _targetObject.AddComponent<RecordingScanTarget>();
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyIfPresent(_wallObject);
            DestroyIfPresent(_targetObject);
            DestroyIfPresent(_playerObject);
            DestroyIfPresent(_cameraObject);
            DestroyIfPresent(_inputObject);
            DestroyIfPresent(_hostObject);
            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }
        }

        [Test]
        public void SuccessfulScanRecordsTargetIdOnAbilityPrimary()
        {
            ScanAttemptResult captured = null;
            _scanner.ScanAttempted += result => captured = result;

            QueueKeyboardState(new KeyboardState(Key.Digit1));

            Assert.That(captured, Is.Not.Null);
            Assert.That(captured.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
            Assert.That(captured.TargetId, Is.EqualTo("test/scan/target-a"));
            Assert.That(captured.DisplayName, Is.EqualTo("Test scan target"));
            Assert.That(captured.Description, Is.EqualTo("A fixture used by scanner tests."));
            Assert.That(captured.ScanResultText, Is.EqualTo("Harmless residue."));
            Assert.That(captured.EventRecorded, Is.True);
            Assert.That(captured.IsRepeat, Is.False);

            IReadOnlyList<PlayerActionEvent> events = _host.Recorder.GetRecentEvents(10);
            Assert.That(events.Count, Is.EqualTo(1));
            Assert.That(events[0].EventType, Is.EqualTo(PlayerActionEventType.EnvironmentScan));
            Assert.That(events[0].TargetId, Is.EqualTo("test/scan/target-a"));
            Assert.That(events[0].HasMagnitude, Is.True);
            Assert.That(events[0].Magnitude, Is.EqualTo(1f));
            Assert.That(HasScanParameter(events[0]), Is.True);
        }

        [Test]
        public void InteractDoesNotActivateTheScanner()
        {
            QueueKeyboardState(new KeyboardState(Key.E));

            Assert.That(_scanner.LastResult, Is.Null);
            Assert.That(_host.Recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void InvalidTargetDoesNotRecordAnEvent()
        {
            Object.DestroyImmediate(_target);
            Physics.SyncTransforms();

            ScanAttemptResult result = _scanner.TryScan();

            Assert.That(result.Outcome, Is.EqualTo(ScanAttemptOutcome.InvalidTarget));
            Assert.That(result.EventRecorded, Is.False);
            Assert.That(_host.Recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void EmptyAimProducesInvalidTarget()
        {
            _cameraObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            Physics.SyncTransforms();

            ScanAttemptResult result = _scanner.TryScan();

            Assert.That(result.Outcome, Is.EqualTo(ScanAttemptOutcome.InvalidTarget));
            Assert.That(_host.Recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void OutOfRangeTargetIsReportedAndNotRecorded()
        {
            SetFloat(_scanner, "_scanRange", 1f);

            ScanAttemptResult result = _scanner.TryScan();

            Assert.That(result.Outcome, Is.EqualTo(ScanAttemptOutcome.OutOfRange));
            Assert.That(result.TargetId, Is.EqualTo("test/scan/target-a"));
            Assert.That(result.EventRecorded, Is.False);
            Assert.That(_host.Recorder.Count, Is.EqualTo(0));

            SetFloat(_scanner, "_scanRange", 2f);
            result = _scanner.TryScan();
            Assert.That(result.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
            Assert.That(_host.Recorder.Count, Is.EqualTo(1));
        }

        [Test]
        public void SolidObstacleBlocksLineOfSight()
        {
            _wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wallObject.transform.position = Offset + new Vector3(0f, 1f, -2f);
            _wallObject.transform.localScale = new Vector3(2f, 2f, 0.25f);
            Physics.SyncTransforms();

            ScanAttemptResult blocked = _scanner.TryScan();
            Assert.That(blocked.Outcome, Is.EqualTo(ScanAttemptOutcome.InvalidTarget),
                "A solid wall must block the scan ray.");
            Assert.That(_host.Recorder.Count, Is.EqualTo(0));

            _wallObject.SetActive(false);
            Physics.SyncTransforms();

            ScanAttemptResult clear = _scanner.TryScan();
            Assert.That(clear.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
            Assert.That(_host.Recorder.Count, Is.EqualTo(1));
        }

        [Test]
        public void PlayerOwnColliderDoesNotBlockTheScan()
        {
            CapsuleCollider body = _playerObject.AddComponent<CapsuleCollider>();
            body.height = 2f;
            body.radius = 0.5f;
            Physics.SyncTransforms();

            ScanAttemptResult result = _scanner.TryScan();
            Assert.That(result.Outcome, Is.EqualTo(ScanAttemptOutcome.Success),
                "The scanner must ignore colliders on its own GameObject.");
        }

        [Test]
        public void DisabledOrUnavailableTargetsAreInvalid()
        {
            _target.enabled = false;
            Assert.That(_scanner.TryScan().Outcome, Is.EqualTo(ScanAttemptOutcome.InvalidTarget));

            _target.enabled = true;
            _target.Available = false;
            Assert.That(_scanner.TryScan().Outcome, Is.EqualTo(ScanAttemptOutcome.InvalidTarget));
            Assert.That(_host.Recorder.Count, Is.EqualTo(0));
        }

        [Test]
        public void RepeatScansDoNotFloodTheEventLogByDefault()
        {
            ScanAttemptResult first = _scanner.TryScan();
            ScanAttemptResult second = _scanner.TryScan();
            ScanAttemptResult third = _scanner.TryScan();

            Assert.That(first.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
            Assert.That(first.EventRecorded, Is.True);
            Assert.That(second.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
            Assert.That(second.IsRepeat, Is.True);
            Assert.That(second.EventRecorded, Is.False);
            Assert.That(third.EventRecorded, Is.False);
            Assert.That(_host.Recorder.Count, Is.EqualTo(1),
                "Default configuration must record only the first scan of a target.");
            Assert.That(_scanner.HasScanned("test/scan/target-a"), Is.True);
        }

        [Test]
        public void RepeatScansCanBeRecordedWhenConfigured()
        {
            SetBool(_scanner, "_recordRepeatScans", true);

            _scanner.TryScan();
            ScanAttemptResult second = _scanner.TryScan();

            Assert.That(second.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
            Assert.That(second.IsRepeat, Is.True);
            Assert.That(second.EventRecorded, Is.True);
            Assert.That(_host.Recorder.Count, Is.EqualTo(2));
            Assert.That(_host.Recorder.GetRecentEvents(2, PlayerActionEventType.EnvironmentScan).Count, Is.EqualTo(2));
        }

        [Test]
        public void RepeatScansCanBeRejectedWhenConfigured()
        {
            SetBool(_scanner, "_allowRepeatScans", false);

            ScanAttemptResult first = _scanner.TryScan();
            ScanAttemptResult second = _scanner.TryScan();

            Assert.That(first.EventRecorded, Is.True);
            Assert.That(second.Outcome, Is.EqualTo(ScanAttemptOutcome.RepeatRejected));
            Assert.That(second.EventRecorded, Is.False);
            Assert.That(_host.Recorder.Count, Is.EqualTo(1));
        }

        [Test]
        public void SuccessfulScanRecordsTheContainingRegionId()
        {
            WorldRegionTestObjects objects = new WorldRegionTestObjects();
            try
            {
                RegionDefinition definition = objects.CreateDefinition("nacre/test/scanner-region", "Scanner Region");
                WorldRegionCatalog catalog = objects.CreateCatalog(definition);

                GameObject locatorRoot = objects.CreateObject("Scanner Region Locator");
                locatorRoot.SetActive(false);
                locatorRoot.transform.position = Offset;
                WorldRegionLocator locator = locatorRoot.AddComponent<WorldRegionLocator>();
                WorldRegionTestObjects.SetLocatorCatalog(locator, catalog);
                WorldRegionVolume volume = objects.CreateVolume(
                    locatorRoot.transform,
                    definition,
                    Vector3.zero,
                    new Vector3(20f, 20f, 20f),
                    0);
                WorldRegionTestObjects.SetLocatorVolumes(locator, new[] { volume });
                locatorRoot.SetActive(true);
                Assert.That(locator.IsAvailable, Is.True, string.Join("\n", locator.ValidationErrors));

                SetObjectReference(_scanner, "_regionLocator", locator);

                ScanAttemptResult result = _scanner.TryScan();

                Assert.That(result.Outcome, Is.EqualTo(ScanAttemptOutcome.Success));
                Assert.That(result.RegionId, Is.EqualTo("nacre/test/scanner-region"));
                Assert.That(_host.Recorder.Count, Is.EqualTo(1));
                Assert.That(_host.Recorder.GetRecentEvents(1)[0].RegionId, Is.EqualTo("nacre/test/scanner-region"));
                Assert.That(_host.Recorder.GetRecentEvents(1)[0].TargetId, Is.EqualTo("test/scan/target-a"));
            }
            finally
            {
                objects.DestroyAll();
            }
        }

        private static bool HasScanParameter(PlayerActionEvent evt)
        {
            foreach (PlayerActionEventParameter parameter in evt.Parameters)
            {
                if (parameter.Id == PlayerEnvironmentalScanner.ScanParameterId && parameter.Value == 1f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void DestroyIfPresent(GameObject gameObject)
        {
            if (gameObject != null)
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        private void QueueKeyboardState(KeyboardState state)
        {
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }

        private static void SetObjectReference(Object component, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Object component, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(Object component, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
