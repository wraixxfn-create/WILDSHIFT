using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildshift.Player.Input;
using Wildshift.Player.Movement;

namespace Wildshift.Tests
{
    /// <summary>Exercises third-person movement with the authored Prompt 3 input actions.</summary>
    public sealed class ThirdPersonPlayerMovementTests
    {
        private const string InputActionsPath = "Assets/_Project/Data/Input/PlayerInputActions.inputactions";

        private GameObject _inputObject;
        private GameObject _cameraObject;
        private GameObject _playerObject;
        private GameObject _ground;
        private GameObject _obstacle;
        private PlayerInputReader _input;
        private ThirdPersonPlayerMovement _movement;
        private CharacterController _characterController;
        private Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(inputActions, Is.Not.Null, $"Could not load InputActionAsset at {InputActionsPath}.");

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _inputObject = new GameObject("Movement Input Test");
            _inputObject.SetActive(false);
            _input = _inputObject.AddComponent<PlayerInputReader>();
            SerializedObject serializedInput = new SerializedObject(_input);
            serializedInput.FindProperty("_inputActions").objectReferenceValue = inputActions;
            serializedInput.ApplyModifiedPropertiesWithoutUndo();
            _inputObject.SetActive(true);

            _cameraObject = new GameObject("Movement Camera Orientation Test");
            _playerObject = new GameObject("Movement CharacterController Test");
            _playerObject.SetActive(false);
            _characterController = _playerObject.AddComponent<CharacterController>();
            _characterController.height = 2f;
            _characterController.radius = 0.5f;
            _characterController.center = Vector3.zero;
            _characterController.slopeLimit = 45f;
            _characterController.stepOffset = 0.3f;
            _characterController.skinWidth = 0.08f;
            _movement = _playerObject.AddComponent<ThirdPersonPlayerMovement>();
            SetObjectReference("_input", _input);
            SetObjectReference("_cameraTransform", _cameraObject.transform);
            SetFloat("_walkSpeed", 4f);
            SetFloat("_sprintSpeed", 8f);
            SetFloat("_acceleration", 100f);
            SetFloat("_deceleration", 20f);
            SetFloat("_rotationSpeed", 720f);
            SetFloat("_gravity", 0f);
            _playerObject.transform.position = new Vector3(100f, 10f, 100f);
            _playerObject.SetActive(true);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null) Object.DestroyImmediate(_playerObject);
            if (_cameraObject != null) Object.DestroyImmediate(_cameraObject);
            if (_inputObject != null) Object.DestroyImmediate(_inputObject);
            if (_ground != null) Object.DestroyImmediate(_ground);
            if (_obstacle != null) Object.DestroyImmediate(_obstacle);
            if (_keyboard != null && _keyboard.added) InputSystem.RemoveDevice(_keyboard);
        }

        [TestCase(Key.W, 0f, 1f)]
        [TestCase(Key.S, 0f, -1f)]
        [TestCase(Key.A, -1f, 0f)]
        [TestCase(Key.D, 1f, 0f)]
        public void CardinalInputsMoveForwardBackwardAndStrafe(Key key, float expectedX, float expectedZ)
        {
            Vector3 start = _playerObject.transform.position;
            QueueKeyboardState(new KeyboardState(key));

            _movement.Tick(0.1f);

            Vector3 displacement = _playerObject.transform.position - start;
            Assert.That(displacement.x, Is.EqualTo(expectedX * 0.4f).Within(0.01f));
            Assert.That(displacement.z, Is.EqualTo(expectedZ * 0.4f).Within(0.01f));
            Assert.That(displacement.y, Is.EqualTo(0f).Within(0.001f));
        }

        [TestCase(75f)]
        [TestCase(-75f)]
        public void CameraPitchDoesNotAddVerticalMotion(float pitch)
        {
            _cameraObject.transform.rotation = Quaternion.Euler(pitch, 90f, 0f);
            Vector3 start = _playerObject.transform.position;
            QueueKeyboardState(new KeyboardState(Key.W));

            _movement.Tick(0.1f);

            Vector3 displacement = _playerObject.transform.position - start;
            Assert.That(displacement.x, Is.EqualTo(0.4f).Within(0.01f));
            Assert.That(displacement.z, Is.EqualTo(0f).Within(0.01f));
            Assert.That(displacement.y, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DiagonalInputDoesNotIncreaseMovementSpeed()
        {
            Vector3 start = _playerObject.transform.position;
            QueueKeyboardState(new KeyboardState(Key.W, Key.D));

            _movement.Tick(0.1f);

            Vector3 displacement = _playerObject.transform.position - start;
            Assert.That(_input.Move.magnitude, Is.EqualTo(1f).Within(0.001f));
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(4f).Within(0.01f));
            Assert.That(new Vector2(displacement.x, displacement.z).magnitude, Is.EqualTo(0.4f).Within(0.01f));
        }

        [Test]
        public void AccelerationAndCharacterRotationAreSmooth()
        {
            SetFloat("_acceleration", 4f);
            SetFloat("_rotationSpeed", 180f);
            QueueKeyboardState(new KeyboardState(Key.D));

            _movement.Tick(0.25f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(1f).Within(0.01f));
            Assert.That(Quaternion.Angle(Quaternion.identity, _playerObject.transform.rotation),
                Is.EqualTo(45f).Within(0.1f));

            _movement.Tick(0.25f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(2f).Within(0.01f));
            Assert.That(Quaternion.Angle(Quaternion.identity, _playerObject.transform.rotation),
                Is.EqualTo(90f).Within(0.1f));
        }

        [Test]
        public void SprintTransitionsSmoothlyAndReleaseReturnsToWalkingSpeed()
        {
            SetFloat("_acceleration", 4f);
            SetFloat("_deceleration", 12f);
            QueueKeyboardState(new KeyboardState(Key.W));
            _movement.Tick(1f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(4f).Within(0.01f));

            QueueKeyboardState(new KeyboardState(Key.W, Key.LeftShift));
            _movement.Tick(0.25f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.GreaterThan(4f));
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.LessThan(8f));

            _movement.Tick(1f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(8f).Within(0.01f));
            QueueKeyboardState(new KeyboardState(Key.W));
            _movement.Tick(0.25f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.GreaterThan(4f));
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.LessThan(8f));
            _movement.Tick(1f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(4f).Within(0.01f));

            QueueKeyboardState(new KeyboardState());
            _movement.Tick(1f);
            Assert.That(_movement.HorizontalVelocity, Is.EqualTo(Vector3.zero));
            Vector3 stoppedPosition = _playerObject.transform.position;
            _movement.Tick(1f);
            Assert.That(_playerObject.transform.position, Is.EqualTo(stoppedPosition));
        }

        [Test]
        public void DisablingGameplayInputDeceleratesAndStopsTheCharacter()
        {
            QueueKeyboardState(new KeyboardState(Key.W));
            _movement.Tick(1f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.EqualTo(4f).Within(0.01f));

            _input.SetGameplayInputEnabled(false);
            _movement.Tick(0.1f);
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.LessThan(4f));
            Assert.That(_movement.HorizontalVelocity.magnitude, Is.GreaterThan(0f));
            _movement.Tick(1f);
            Assert.That(_movement.HorizontalVelocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void CharacterControllerBlocksMovementThroughAWall()
        {
            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.transform.position = new Vector3(100f, 10f, 102f);
            _obstacle.transform.localScale = new Vector3(4f, 4f, 0.2f);
            Physics.SyncTransforms();
            QueueKeyboardState(new KeyboardState(Key.W));

            for (int i = 0; i < 40; i++) _movement.Tick(0.1f);

            float forwardTravel = _playerObject.transform.position.z - 100f;
            Assert.That(forwardTravel, Is.GreaterThan(0.5f));
            Assert.That(forwardTravel, Is.LessThan(1.6f), "The capsule must remain on its side of the wall.");
        }

        [Test]
        public void CharacterControllerStaysGroundedOnAnOrdinarySlope()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(100f, -0.25f, 104f);
            _ground.transform.localScale = new Vector3(40f, 0.5f, 40f);

            _obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstacle.name = "15 Degree Movement Test Slope";
            _obstacle.transform.position = new Vector3(100f, 0.935f, 104f);
            _obstacle.transform.rotation = Quaternion.Euler(-15f, 0f, 0f);
            _obstacle.transform.localScale = new Vector3(6f, 0.2f, 8f);

            _playerObject.transform.position = new Vector3(100f, 1f, 99f);
            SetFloat("_gravity", 25f);
            SetFloat("_walkSpeed", 2f);
            SetFloat("_acceleration", 100f);
            Physics.SyncTransforms();
            QueueKeyboardState(new KeyboardState(Key.W));

            for (int i = 0; i < 120; i++) _movement.Tick(1f / 60f);

            Assert.That(_playerObject.transform.position.z, Is.GreaterThan(101f));
            Assert.That(_playerObject.transform.position.y, Is.GreaterThan(1.4f),
                "The controller should follow the ramp instead of tunnelling into its face.");
            Assert.That(_movement.IsGrounded, Is.True);
        }

        [Test]
        public void GravityPullsTheCharacterDownWhenItIsNotGrounded()
        {
            SetFloat("_gravity", 10f);
            Vector3 start = _playerObject.transform.position;

            _movement.Tick(0.1f);
            _movement.Tick(0.1f);

            Assert.That(_playerObject.transform.position.y, Is.LessThan(start.y));
        }

        [Test]
        public void GroundStickKeepsTheCharacterGroundedWithoutBouncing()
        {
            _ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _ground.transform.position = new Vector3(100f, -0.25f, 100f);
            _ground.transform.localScale = new Vector3(20f, 0.5f, 20f);
            _playerObject.transform.position = new Vector3(100f, 1f, 100f);
            SetFloat("_gravity", 25f);
            Physics.SyncTransforms();
            QueueKeyboardState(new KeyboardState());

            for (int i = 0; i < 20; i++) _movement.Tick(1f / 60f);
            Assert.That(_movement.IsGrounded, Is.True);
            float groundedHeight = _playerObject.transform.position.y;
            for (int i = 0; i < 60; i++) _movement.Tick(1f / 60f);

            Assert.That(_movement.IsGrounded, Is.True);
            Assert.That(_playerObject.transform.position.y, Is.EqualTo(groundedHeight).Within(0.02f));
        }

        private void SetFloat(string fieldName, float value)
        {
            SerializedObject serialized = new SerializedObject(_movement);
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, $"Missing serialized movement field '{fieldName}'.");
            property.floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetObjectReference(string fieldName, Object value)
        {
            SerializedObject serialized = new SerializedObject(_movement);
            SerializedProperty property = serialized.FindProperty(fieldName);
            Assert.That(property, Is.Not.Null, $"Missing serialized movement field '{fieldName}'.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void QueueKeyboardState(KeyboardState state)
        {
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }
    }
}
