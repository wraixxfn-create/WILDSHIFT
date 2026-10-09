using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildshift.Interaction;
using Wildshift.Player.Input;
using Wildshift.Player.Interaction;

namespace Wildshift.Tests
{
    /// <summary>
    /// Exercises aim detection, range, line of sight, validity filtering, and the Interact button using real
    /// physics raycasts and simulated keyboard input. The fixture sits far from the graybox scene geometry.
    /// </summary>
    public sealed class PlayerInteractionDetectorTests
    {
        private const string InputActionsPath = "Assets/_Project/Data/Input/PlayerInputActions.inputactions";
        private static readonly Vector3 Offset = new Vector3(1000f, 0f, 1000f);

        private GameObject _inputObject;
        private GameObject _cameraObject;
        private GameObject _playerObject;
        private GameObject _targetObject;
        private GameObject _wallObject;
        private PlayerInputReader _input;
        private PlayerInteractionDetector _detector;
        private RecordingInteractable _target;
        private Keyboard _keyboard;

        // Camera at z=-6 looks along +Z. The player is at z=-3 (range origin); the target face is at z=-1.5.
        // The range origin to the target face is 1.5 m, inside the 2 m test range.
        [SetUp]
        public void SetUp()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(inputActions, Is.Not.Null, $"Could not load InputActionAsset at {InputActionsPath}.");

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _inputObject = new GameObject("Interaction Input Test");
            _inputObject.SetActive(false);
            _input = _inputObject.AddComponent<PlayerInputReader>();
            SetObjectReference(_input, "_inputActions", inputActions);
            _inputObject.SetActive(true);

            _cameraObject = new GameObject("Interaction Camera Test");
            _cameraObject.AddComponent<UnityEngine.Camera>();
            _cameraObject.transform.position = Offset + new Vector3(0f, 1f, -6f);
            _cameraObject.transform.rotation = Quaternion.identity;

            _playerObject = new GameObject("Interaction Player Test");
            _playerObject.transform.position = Offset + new Vector3(0f, 1f, -3f);
            _playerObject.SetActive(false);
            _detector = _playerObject.AddComponent<PlayerInteractionDetector>();
            SetObjectReference(_detector, "_input", _input);
            SetObjectReference(_detector, "_viewCamera", _cameraObject.GetComponent<UnityEngine.Camera>());
            SetFloat(_detector, "_interactionRange", 2f);
            _playerObject.SetActive(true);

            _targetObject = new GameObject("Interaction Target Test");
            _targetObject.transform.position = Offset + new Vector3(0f, 1f, -1f);
            _targetObject.AddComponent<BoxCollider>();
            _target = _targetObject.AddComponent<RecordingInteractable>();
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
            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }
        }

        [Test]
        public void SuccessfulInteractionRunsOnceOnInteractPress()
        {
            Assert.That(_detector.CurrentTarget, Is.Null, "Setup should begin with no target.");
            Assert.That(_target.InteractCount, Is.EqualTo(0));

            QueueKeyboardState(new KeyboardState(Key.E));

            Assert.That(_target.InteractCount, Is.EqualTo(1));
            Assert.That(_target.LastInteractor, Is.SameAs(_playerObject));
            Assert.That(_detector.CurrentTarget, Is.SameAs(_target));
        }

        [Test]
        public void RepeatedInputInteractsOnlyOnNewPresses()
        {
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(1));

            // Holding the key re-sends state without a new press, so it must not repeat the interaction.
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(1));

            QueueKeyboardState(new KeyboardState());
            Assert.That(_target.InteractCount, Is.EqualTo(1));

            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(2));
        }

        [Test]
        public void OutOfRangeTargetIsNotInteractedWith()
        {
            SetFloat(_detector, "_interactionRange", 1f);

            _detector.RefreshTarget();
            Assert.That(_detector.CurrentTarget, Is.Null);
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(0));

            SetFloat(_detector, "_interactionRange", 2f);
            QueueKeyboardState(new KeyboardState());
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(1), "Restoring range should make the same target valid.");
        }

        [Test]
        public void BlockedLineOfSightIsNotInteractedWith()
        {
            _wallObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wallObject.transform.position = Offset + new Vector3(0f, 1f, -2f);
            _wallObject.transform.localScale = new Vector3(2f, 2f, 0.25f);
            Physics.SyncTransforms();

            _detector.RefreshTarget();
            Assert.That(_detector.CurrentTarget, Is.Null, "A non-interactable wall must block aim.");
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(0));

            _wallObject.SetActive(false);
            Physics.SyncTransforms();
            QueueKeyboardState(new KeyboardState());
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(1), "Removing the wall should restore the target.");
        }

        [Test]
        public void PlayerOwnColliderDoesNotBlockAim()
        {
            CapsuleCollider body = _playerObject.AddComponent<CapsuleCollider>();
            body.height = 2f;
            body.radius = 0.5f;
            Physics.SyncTransforms();

            QueueKeyboardState(new KeyboardState(Key.E));

            Assert.That(_target.InteractCount, Is.EqualTo(1), "The detector must ignore colliders on its own GameObject.");
        }

        [Test]
        public void DisabledBehaviourTargetIsIgnored()
        {
            _target.enabled = false;

            _detector.RefreshTarget();
            Assert.That(_detector.CurrentTarget, Is.Null);
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(0));

            _target.enabled = true;
            QueueKeyboardState(new KeyboardState());
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(1));
        }

        [Test]
        public void InactiveTargetObjectIsIgnored()
        {
            _targetObject.SetActive(false);
            Physics.SyncTransforms();

            QueueKeyboardState(new KeyboardState(Key.E));

            Assert.That(_target.InteractCount, Is.EqualTo(0));
            Assert.That(_detector.CurrentTarget, Is.Null);
        }

        [Test]
        public void DestroyedTargetIsHandledSafely()
        {
            _detector.RefreshTarget();
            Assert.That(_detector.CurrentTarget, Is.SameAs(_target));

            Object.DestroyImmediate(_targetObject);
            Physics.SyncTransforms();

            Assert.That(_detector.CurrentTarget, Is.Null, "A destroyed cached target must not be returned.");
            Assert.DoesNotThrow(() => QueueKeyboardState(new KeyboardState(Key.E)));
            Assert.DoesNotThrow(() => _detector.RefreshTarget());
            Assert.That(_detector.CurrentTarget, Is.Null);
        }

        [Test]
        public void TargetRejectingInteractionIsNotATarget()
        {
            _target.Available = false;

            _detector.RefreshTarget();
            Assert.That(_detector.CurrentTarget, Is.Null, "CanInteract false should filter the object out.");
            QueueKeyboardState(new KeyboardState(Key.E));
            Assert.That(_target.InteractCount, Is.EqualTo(0));
        }

        [Test]
        public void AimedNonInteractableProducesNoTargetAndNoError()
        {
            // The collider stays but no IInteractable remains on the aimed object.
            Object.DestroyImmediate(_target);
            Physics.SyncTransforms();

            _detector.RefreshTarget();
            Assert.That(_detector.CurrentTarget, Is.Null);
            Assert.DoesNotThrow(() => QueueKeyboardState(new KeyboardState(Key.E)));
        }

        [Test]
        public void SingleUseTestInteractableAcceptsOnlyFirstInteraction()
        {
            GameObject testObject = new GameObject("Single Use Test Interactable");
            try
            {
                TestInteractable interactable = testObject.AddComponent<TestInteractable>();
                SerializedObject serialized = new SerializedObject(interactable);
                serialized.FindProperty("_singleUse").boolValue = true;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.That(interactable.CanInteract(_playerObject), Is.True);
                interactable.Interact(_playerObject);
                Assert.That(interactable.CanInteract(_playerObject), Is.False);
                interactable.Interact(_playerObject);
                Assert.That(interactable.InteractionCount, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(testObject);
            }
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
    }
}
