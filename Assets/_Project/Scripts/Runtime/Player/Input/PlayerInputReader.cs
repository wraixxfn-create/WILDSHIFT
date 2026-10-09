using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Wildshift.Core.Diagnostics;

namespace Wildshift.Player.Input
{
    /// <summary>
    /// Reads the Gameplay action map and exposes input state without implementing movement,
    /// camera, combat, interaction, or presentation behavior.
    /// </summary>
    /// <remarks>
    /// Assign an InputActionAsset containing the required Gameplay map in the Inspector.
    /// A private runtime copy is used so enabling this reader never changes the imported asset
    /// or another reader's action state.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerInputReader : MonoBehaviour
    {
        private const string GameplayMapName = "Gameplay";
        private const string Vector2ControlType = "Vector2";
        private const string ButtonControlType = "Button";
        private const int ButtonCount = (int)PlayerInputButton.Pause + 1;

        [SerializeField]
        [Tooltip("Input Action Asset with a Gameplay map containing the required WILDSHIFT actions.")]
        private InputActionAsset _inputActions;

        private readonly bool[] _buttonPressed = new bool[ButtonCount];

        private InputActionAsset _runtimeActions;
        private InputActionMap _gameplayMap;
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction[] _buttonActions;
        private Dictionary<InputAction, PlayerInputButton> _buttonActionLookup;
        private bool _gameplayInputAllowed = true;
        private bool _buttonCallbacksSubscribed;

        /// <summary>Raised once when a button action becomes pressed.</summary>
        public event Action<PlayerInputButton> ButtonPressed;

        /// <summary>
        /// Raised once when a button action is released. Also raised for held buttons when
        /// gameplay input is disabled, so consumers cannot retain a stale held state.
        /// </summary>
        public event Action<PlayerInputButton> ButtonReleased;

        /// <summary>True while this component's Gameplay map is enabled and allowed to read input.</summary>
        public bool IsGameplayInputEnabled =>
            isActiveAndEnabled && _gameplayInputAllowed && _gameplayMap != null && _gameplayMap.enabled;

        /// <summary>Current move value, or zero while gameplay input is disabled.</summary>
        public Vector2 Move => IsGameplayInputEnabled && _moveAction != null
            ? _moveAction.ReadValue<Vector2>()
            : Vector2.zero;

        /// <summary>Current look value, or zero while gameplay input is disabled.</summary>
        public Vector2 Look => IsGameplayInputEnabled && _lookAction != null
            ? _lookAction.ReadValue<Vector2>()
            : Vector2.zero;

        /// <summary>
        /// Returns whether a button is currently held. Returns false when gameplay input is disabled
        /// or when <paramref name="button"/> is not a defined value.
        /// </summary>
        public bool IsButtonPressed(PlayerInputButton button)
        {
            int index = (int)button;
            return IsGameplayInputEnabled && index >= 0 && index < _buttonPressed.Length && _buttonPressed[index];
        }

        /// <summary>
        /// Enables or disables processing of the Gameplay action map. Menus can call this when they
        /// open and restore it when they close; disabling also clears held button state and zeros
        /// Move and Look reads.
        /// </summary>
        public void SetGameplayInputEnabled(bool enabled)
        {
            _gameplayInputAllowed = enabled;
            RefreshGameplayInput();
        }

        private void Awake()
        {
            TryInitializeActions();
        }

        private void OnEnable()
        {
            RefreshGameplayInput();
        }

        private void OnDisable()
        {
            DisableGameplayActionMap();
        }

        private void OnDestroy()
        {
            DisableGameplayActionMap();
            UnsubscribeFromButtonActions();
            ReleaseRuntimeActionAsset();
        }

        private bool TryInitializeActions()
        {
            if (_inputActions == null)
            {
                WildshiftLog.Error(
                    $"{nameof(PlayerInputReader)} on '{name}' has no Input Action Asset assigned. " +
                    $"Assign an asset with a '{GameplayMapName}' map containing Move, Look, and all button actions.",
                    this);
                return false;
            }

            _runtimeActions = Instantiate(_inputActions);
            _gameplayMap = _runtimeActions.FindActionMap(GameplayMapName, throwIfNotFound: false);
            if (_gameplayMap == null)
            {
                WildshiftLog.Error(
                    $"Input Action Asset '{_inputActions.name}' assigned to {nameof(PlayerInputReader)} " +
                    $"on '{name}' is missing the required '{GameplayMapName}' action map.",
                    this);
                ReleaseRuntimeActionAsset();
                return false;
            }

            List<string> configurationProblems = new List<string>();
            _moveAction = FindRequiredAction(
                "Move", InputActionType.Value, Vector2ControlType, configurationProblems);
            _lookAction = FindRequiredAction(
                "Look", InputActionType.Value, Vector2ControlType, configurationProblems);

            _buttonActions = new InputAction[ButtonCount];
            RegisterRequiredButtonAction(PlayerInputButton.Sprint, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.Jump, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.Interact, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.LightAttack, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.HeavyAttack, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.Dodge, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.AbilityPrimary, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.AbilitySecondary, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.AbilityTertiary, configurationProblems);
            RegisterRequiredButtonAction(PlayerInputButton.Pause, configurationProblems);

            if (configurationProblems.Count > 0)
            {
                WildshiftLog.Error(
                    $"Invalid input configuration in '{_inputActions.name}' for {nameof(PlayerInputReader)} " +
                    $"on '{name}': {string.Join("; ", configurationProblems)}. " +
                    $"Expected a '{GameplayMapName}' map with Move and Look as Value/Vector2 actions, " +
                    "and Sprint, Jump, Interact, LightAttack, HeavyAttack, Dodge, AbilityPrimary, " +
                    "AbilitySecondary, AbilityTertiary, and Pause as Button actions.",
                    this);
                ReleaseRuntimeActionAsset();
                return false;
            }

            SubscribeToButtonActions();
            return true;
        }

        private InputAction FindRequiredAction(
            string actionName,
            InputActionType expectedType,
            string expectedControlType,
            List<string> configurationProblems)
        {
            InputAction action = _gameplayMap.FindAction(actionName, throwIfNotFound: false);
            if (action == null)
            {
                configurationProblems.Add($"missing action '{actionName}'");
                return null;
            }

            bool isValid = true;
            if (action.type != expectedType)
            {
                configurationProblems.Add(
                    $"'{actionName}' must be a {expectedType} action (found {action.type})");
                isValid = false;
            }

            if (!string.Equals(action.expectedControlType, expectedControlType, StringComparison.Ordinal))
            {
                configurationProblems.Add(
                    $"'{actionName}' must expect {expectedControlType} controls " +
                    $"(found '{action.expectedControlType}')");
                isValid = false;
            }

            if (action.bindings.Count == 0)
            {
                configurationProblems.Add($"'{actionName}' has no bindings");
                isValid = false;
            }

            return isValid ? action : null;
        }

        private void RegisterRequiredButtonAction(
            PlayerInputButton button,
            List<string> configurationProblems)
        {
            InputAction action = FindRequiredAction(
                button.ToString(), InputActionType.Button, ButtonControlType, configurationProblems);
            _buttonActions[(int)button] = action;
        }

        private void SubscribeToButtonActions()
        {
            _buttonActionLookup = new Dictionary<InputAction, PlayerInputButton>(ButtonCount);
            for (int i = 0; i < _buttonActions.Length; i++)
            {
                InputAction action = _buttonActions[i];
                PlayerInputButton button = (PlayerInputButton)i;
                _buttonActionLookup.Add(action, button);
                action.performed += OnButtonPerformed;
                action.canceled += OnButtonCanceled;
            }

            _buttonCallbacksSubscribed = true;
        }

        private void UnsubscribeFromButtonActions()
        {
            if (!_buttonCallbacksSubscribed)
            {
                return;
            }

            for (int i = 0; i < _buttonActions.Length; i++)
            {
                InputAction action = _buttonActions[i];
                action.performed -= OnButtonPerformed;
                action.canceled -= OnButtonCanceled;
            }

            _buttonCallbacksSubscribed = false;
            _buttonActionLookup.Clear();
        }

        private void RefreshGameplayInput()
        {
            if (_gameplayMap == null)
            {
                return;
            }

            if (isActiveAndEnabled && _gameplayInputAllowed)
            {
                if (!_gameplayMap.enabled)
                {
                    _gameplayMap.Enable();
                }

                return;
            }

            DisableGameplayActionMap();
        }

        private void DisableGameplayActionMap()
        {
            if (_gameplayMap != null && _gameplayMap.enabled)
            {
                _gameplayMap.Disable();
            }

            ClearPressedButtons();
        }

        private void ClearPressedButtons()
        {
            for (int i = 0; i < _buttonPressed.Length; i++)
            {
                if (_buttonPressed[i])
                {
                    SetButtonReleased((PlayerInputButton)i);
                }
            }
        }

        private void OnButtonPerformed(InputAction.CallbackContext context)
        {
            if (!IsGameplayInputEnabled || _buttonActionLookup == null ||
                !_buttonActionLookup.TryGetValue(context.action, out PlayerInputButton button))
            {
                return;
            }

            int index = (int)button;
            if (_buttonPressed[index])
            {
                return;
            }

            _buttonPressed[index] = true;
            ButtonPressed?.Invoke(button);
        }

        private void OnButtonCanceled(InputAction.CallbackContext context)
        {
            if (_buttonActionLookup == null ||
                !_buttonActionLookup.TryGetValue(context.action, out PlayerInputButton button))
            {
                return;
            }

            SetButtonReleased(button);
        }

        private void SetButtonReleased(PlayerInputButton button)
        {
            int index = (int)button;
            if (index < 0 || index >= _buttonPressed.Length || !_buttonPressed[index])
            {
                return;
            }

            _buttonPressed[index] = false;
            ButtonReleased?.Invoke(button);
        }

        private void ReleaseRuntimeActionAsset()
        {
            if (_runtimeActions == null)
            {
                return;
            }

            InputActionAsset runtimeActions = _runtimeActions;
            _runtimeActions = null;
            _gameplayMap = null;
            _moveAction = null;
            _lookAction = null;
            _buttonActions = null;
            _buttonActionLookup = null;

            if (Application.isPlaying)
            {
                Destroy(runtimeActions);
            }
            else
            {
                DestroyImmediate(runtimeActions);
            }
        }
    }
}
