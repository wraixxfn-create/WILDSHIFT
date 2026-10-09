using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildshift.Player.Camera;
using Wildshift.Player.Input;

namespace Wildshift.Tests
{
    public sealed class ThirdPersonCameraTests
    {
        private GameObject _cameraObject;
        private GameObject _target;
        private GameObject _wall;
        private GameObject _inputObject;
        private ThirdPersonCamera _camera;
        private PlayerInputReader _reader;
        private Mouse _mouse;

        [SetUp]
        public void SetUp()
        {
            _target = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _target.transform.position = new Vector3(1000f, 10f, 1000f);
            _cameraObject = new GameObject("Camera Test");
            _cameraObject.SetActive(false);
            _camera = _cameraObject.AddComponent<ThirdPersonCamera>();
            SetFloat("_initialPitch", 0f);
            SetFloat("_followSmoothTime", 0f);
            SetBool("_captureCursor", false);
            _camera.Target = _target.transform;
            _cameraObject.SetActive(true);
            _camera.SendMessage("Awake");
            _camera.Tick(1f / 60f, Vector2.zero);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraObject);
            if (_target != null) Object.DestroyImmediate(_target);
            if (_wall != null) Object.DestroyImmediate(_wall);
            if (_inputObject != null) Object.DestroyImmediate(_inputObject);
            if (_mouse != null && _mouse.added) InputSystem.RemoveDevice(_mouse);
        }

        [Test]
        public void HorizontalRotationIsImmediateAndIndependentOfFrameDuration()
        {
            _camera.Tick(1f / 30f, new Vector2(100f, 0f));
            Assert.That(_camera.Yaw, Is.EqualTo(15f).Within(0.001f));
            _camera.Tick(1f / 120f, new Vector2(100f, 0f));
            Assert.That(_camera.Yaw, Is.EqualTo(30f).Within(0.001f));
            Assert.That(Quaternion.Angle(_camera.transform.rotation, Quaternion.Euler(0f, 30f, 0f)), Is.LessThan(0.001f));
        }

        [Test]
        public void VerticalRotationClampsBothLimits()
        {
            _camera.Tick(0.016f, new Vector2(0f, 10000f));
            Assert.That(_camera.Pitch, Is.EqualTo(-35f));
            _camera.Tick(0.016f, new Vector2(0f, -10000f));
            Assert.That(_camera.Pitch, Is.EqualTo(75f));
        }

        [Test]
        public void FollowsMovingTargetWithSmoothingThenSettles()
        {
            SetFloat("_followSmoothTime", 0.06f);
            Vector3 before = _camera.transform.position;
            _target.transform.position += Vector3.right * 2f;
            _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position.x, Is.GreaterThan(before.x));
            Assert.That(_camera.transform.position.x, Is.LessThan(before.x + 2f));
            for (int i = 0; i < 120; i++) _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position.x, Is.EqualTo(before.x + 2f).Within(0.001f));
        }

        [Test]
        public void DistanceAndWorldUpHeightAreConfigurableAndTargetRollIsIgnored()
        {
            SetFloat("_followDistance", 6f);
            SetFloat("_cameraHeight", 2f);
            SetFloat("_obstructionRecoveryTime", 0f);
            _target.transform.rotation = Quaternion.Euler(20f, 55f, 70f);
            _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position.y, Is.EqualTo(_target.transform.position.y + 2f).Within(0.001f));
            Assert.That(_camera.transform.position.z, Is.EqualTo(_target.transform.position.z - 6f).Within(0.001f));
            Assert.That(Vector3.Dot(_camera.transform.right, Vector3.up), Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void WallContractsImmediatelyAndRecoversSmoothly()
        {
            Vector3 pivot = _target.transform.position + Vector3.up * 1.5f;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = pivot + Vector3.back * 2f;
            _wall.transform.localScale = new Vector3(10f, 10f, 0.2f);
            Physics.SyncTransforms();
            _camera.Tick(0.016f, Vector2.zero);
            float blocked = Vector3.Distance(pivot, _camera.transform.position);
            Assert.That(blocked, Is.LessThan(1.9f));
            Assert.That(blocked, Is.GreaterThan(1f));
            _wall.SetActive(false);
            Physics.SyncTransforms();
            _camera.Tick(0.016f, Vector2.zero);
            float recovering = Vector3.Distance(pivot, _camera.transform.position);
            Assert.That(recovering, Is.GreaterThan(blocked));
            Assert.That(recovering, Is.LessThan(4f));
            for (int i = 0; i < 120; i++) _camera.Tick(0.016f, Vector2.zero);
            Assert.That(Vector3.Distance(pivot, _camera.transform.position), Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void TriggersAndTargetCollidersDoNotObstructCamera()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _target.transform.position + Vector3.up * 1.5f + Vector3.back * 2f;
            _wall.GetComponent<Collider>().isTrigger = true;
            Physics.SyncTransforms();
            _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position.z, Is.EqualTo(_target.transform.position.z - 4f).Within(0.001f));
        }

        [Test]
        public void EmbeddedPivotHoldsLastPoseUntilClear()
        {
            Vector3 before = _camera.transform.position;
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.transform.position = _target.transform.position + Vector3.up * 1.5f;
            _wall.transform.localScale = Vector3.one * 2f;
            Physics.SyncTransforms();
            _camera.Tick(0.016f, new Vector2(100f, 0f));
            Assert.That(_camera.transform.position, Is.EqualTo(before));
            _wall.SetActive(false);
            Physics.SyncTransforms();
            _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position, Is.Not.EqualTo(before));
        }

        [Test]
        public void SensitivityChangesAndBothInversionsAreApplied()
        {
            _camera.Sensitivity = 0.3f;
            SetBool("_invertHorizontal", true);
            SetBool("_invertVertical", true);
            _camera.Tick(0.016f, new Vector2(10f, 10f));
            Assert.That(_camera.Yaw, Is.EqualTo(357f).Within(0.001f));
            Assert.That(_camera.Pitch, Is.EqualTo(3f).Within(0.001f));
            _camera.Sensitivity = 0f;
            _camera.Tick(0.016f, new Vector2(100f, 100f));
            Assert.That(_camera.Pitch, Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void MissingOrDestroyedTargetKeepsPoseAndCanBeReassigned()
        {
            Vector3 before = _camera.transform.position;
            Object.DestroyImmediate(_target);
            for (int i = 0; i < 10; i++) Assert.DoesNotThrow(() => _camera.Tick(0.016f, Vector2.one));
            Assert.That(_camera.transform.position, Is.EqualTo(before));
            _target = new GameObject("Replacement Target");
            _target.transform.position = new Vector3(1010f, 10f, 1000f);
            _camera.Target = _target.transform;
            _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position.x, Is.EqualTo(1010f).Within(0.001f));
        }

        [Test]
        public void LateUpdateConsumesPromptThreeMouseLookAndBlocksItInMenu()
        {
            _mouse = InputSystem.AddDevice<Mouse>();
            _inputObject = new GameObject("Camera Input Test");
            _inputObject.SetActive(false);
            _reader = _inputObject.AddComponent<PlayerInputReader>();
            SerializedObject reader = new SerializedObject(_reader);
            reader.FindProperty("_inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
                "Assets/_Project/Data/Input/PlayerInputActions.inputactions");
            reader.ApplyModifiedPropertiesWithoutUndo();
            _inputObject.SetActive(true);
            SerializedObject camera = new SerializedObject(_camera);
            camera.FindProperty("_input").objectReferenceValue = _reader;
            camera.ApplyModifiedPropertiesWithoutUndo();
            InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(100f, 0f) });
            InputSystem.Update();
            _camera.SendMessage("LateUpdate");
            Assert.That(_camera.Yaw, Is.EqualTo(15f).Within(0.001f));
            _reader.SetGameplayInputEnabled(false);
            InputSystem.QueueStateEvent(_mouse, new MouseState { delta = new Vector2(100f, 0f) });
            InputSystem.Update();
            _camera.SendMessage("LateUpdate");
            Assert.That(_camera.Yaw, Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void ZeroDeltaTimeDoesNotPoisonSmoothingOnResume()
        {
            SetFloat("_followSmoothTime", 0.06f);
            Vector3 before = _camera.transform.position;
            _target.transform.position += Vector3.right;
            _camera.Tick(0f, Vector2.zero);
            Assert.That(_camera.transform.position, Is.EqualTo(before));
            for (int i = 0; i < 120; i++) _camera.Tick(0.016f, Vector2.zero);
            Assert.That(_camera.transform.position.x, Is.EqualTo(before.x + 1f).Within(0.001f));
        }

        [Test]
        public void EditModeNeverChangesCursorState()
        {
            CursorLockMode mode = Cursor.lockState;
            bool visible = Cursor.visible;
            SetBool("_captureCursor", true);
            _camera.ResumeGameplayCursor();
            _camera.SendMessage("LateUpdate");
            _cameraObject.SetActive(false);
            Assert.That(Cursor.lockState, Is.EqualTo(mode));
            Assert.That(Cursor.visible, Is.EqualTo(visible));
        }

        private void SetFloat(string field, float value)
        {
            SerializedObject serialized = new SerializedObject(_camera);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetBool(string field, bool value)
        {
            SerializedObject serialized = new SerializedObject(_camera);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
