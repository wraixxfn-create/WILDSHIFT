using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Wildshift.Player.Input;

namespace Wildshift.Tests
{
    /// <summary>Exercises the authored keyboard bindings through the same reader used by gameplay.</summary>
    public sealed class PlayerInputReaderTests
    {
        private const string InputActionsPath = "Assets/_Project/Data/Input/PlayerInputActions.inputactions";

        private GameObject _readerObject;
        private PlayerInputReader _reader;
        private Keyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(inputActions, Is.Not.Null, $"Could not load InputActionAsset at {InputActionsPath}.");

            _keyboard = InputSystem.AddDevice<Keyboard>();
            _readerObject = new GameObject("Player Input Reader Test");
            _readerObject.SetActive(false);
            _reader = _readerObject.AddComponent<PlayerInputReader>();

            SerializedObject serializedReader = new SerializedObject(_reader);
            SerializedProperty actionsProperty = serializedReader.FindProperty("_inputActions");
            Assert.That(actionsProperty, Is.Not.Null, "PlayerInputReader is missing its serialized action asset field.");
            actionsProperty.objectReferenceValue = inputActions;
            serializedReader.ApplyModifiedPropertiesWithoutUndo();

            _readerObject.SetActive(true);
            Assert.That(_reader.IsGameplayInputEnabled, Is.True, "The configured Gameplay map should enable with the reader.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_readerObject != null)
            {
                Object.DestroyImmediate(_readerObject);
            }

            if (_keyboard != null && _keyboard.added)
            {
                InputSystem.RemoveDevice(_keyboard);
            }
        }

        [Test]
        public void MoveReturnsVector2FromWASDAndClearsAfterRelease()
        {
            QueueKeyboardState(new KeyboardState(Key.W, Key.D));

            Vector2 move = _reader.Move;
            Assert.That(move.x, Is.GreaterThan(0f));
            Assert.That(move.y, Is.GreaterThan(0f));
            Assert.That(move.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));

            QueueKeyboardState(new KeyboardState());
            Assert.That(_reader.Move, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ButtonActionsRaiseOnePressAndOneRelease()
        {
            int pressCount = 0;
            int releaseCount = 0;
            _reader.ButtonPressed += button =>
            {
                if (button == PlayerInputButton.Jump)
                {
                    pressCount++;
                }
            };
            _reader.ButtonReleased += button =>
            {
                if (button == PlayerInputButton.Jump)
                {
                    releaseCount++;
                }
            };

            QueueKeyboardState(new KeyboardState(Key.Space));
            Assert.That(_reader.IsButtonPressed(PlayerInputButton.Jump), Is.True);
            Assert.That(pressCount, Is.EqualTo(1));

            // Re-sending a held state must not create repeat press notifications.
            QueueKeyboardState(new KeyboardState(Key.Space));
            Assert.That(pressCount, Is.EqualTo(1));

            QueueKeyboardState(new KeyboardState());
            Assert.That(_reader.IsButtonPressed(PlayerInputButton.Jump), Is.False);
            Assert.That(releaseCount, Is.EqualTo(1));

            QueueKeyboardState(new KeyboardState());
            Assert.That(releaseCount, Is.EqualTo(1));
        }

        [Test]
        public void DisablingGameplayInputBlocksReadsAndReleasesHeldButtons()
        {
            int releaseCount = 0;
            _reader.ButtonReleased += button =>
            {
                if (button == PlayerInputButton.Jump)
                {
                    releaseCount++;
                }
            };

            QueueKeyboardState(new KeyboardState(Key.W, Key.Space));
            Assert.That(_reader.Move, Is.Not.EqualTo(Vector2.zero));
            Assert.That(_reader.IsButtonPressed(PlayerInputButton.Jump), Is.True);

            _reader.SetGameplayInputEnabled(false);

            Assert.That(_reader.IsGameplayInputEnabled, Is.False);
            Assert.That(_reader.Move, Is.EqualTo(Vector2.zero));
            Assert.That(_reader.Look, Is.EqualTo(Vector2.zero));
            Assert.That(_reader.IsButtonPressed(PlayerInputButton.Jump), Is.False);
            Assert.That(releaseCount, Is.EqualTo(1), "Disabling the map must not leave a held button stuck.");

            QueueKeyboardState(new KeyboardState(Key.D));
            Assert.That(_reader.Move, Is.EqualTo(Vector2.zero), "Disabled gameplay input must ignore device state changes.");
            Assert.That(releaseCount, Is.EqualTo(1));
        }

        [Test]
        public void GameplayAvailabilityReportsTransitionsWithoutDuplicateNotifications()
        {
            int count = 0;
            bool latest = true;
            _reader.GameplayInputEnabledChanged += enabled => { count++; latest = enabled; };
            _reader.SetGameplayInputEnabled(false);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(latest, Is.False);
            _reader.SetGameplayInputEnabled(false);
            Assert.That(count, Is.EqualTo(1));
            _reader.SetGameplayInputEnabled(true);
            Assert.That(count, Is.EqualTo(2));
            Assert.That(latest, Is.True);
            _reader.enabled = false;
            Assert.That(count, Is.EqualTo(3));
            Assert.That(latest, Is.False);
            _reader.enabled = true;
            Assert.That(count, Is.EqualTo(4));
            Assert.That(latest, Is.True);
        }

        [Test]
        public void EditorResetAssignsAuthoredActionAssetToEmptyField()
        {
            InputActionAsset authored = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            Assert.That(authored, Is.Not.Null, $"Could not load InputActionAsset at {InputActionsPath}.");

            FieldInfo actionsField = typeof(PlayerInputReader).GetField(
                "_inputActions", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo reset = typeof(PlayerInputReader).GetMethod(
                "Reset", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(actionsField, Is.Not.Null, "PlayerInputReader is missing its action asset field.");
            Assert.That(reset, Is.Not.Null, "PlayerInputReader should define the editor-only Reset message.");

            GameObject owner = new GameObject("Reset Input Test");
            try
            {
                // Inactive, so Awake does not run (and log the missing asset) before Reset assigns the field.
                owner.SetActive(false);
                PlayerInputReader reader = owner.AddComponent<PlayerInputReader>();
                reset.Invoke(reader, null);

                Assert.That(actionsField.GetValue(reader), Is.EqualTo(authored));
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        private void QueueKeyboardState(KeyboardState state)
        {
            InputSystem.QueueStateEvent(_keyboard, state);
            InputSystem.Update();
        }
    }
}
