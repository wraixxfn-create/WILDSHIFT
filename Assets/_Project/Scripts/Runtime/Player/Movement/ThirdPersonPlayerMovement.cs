using UnityEngine;
using Wildshift.Player.Input;

namespace Wildshift.Player.Movement
{
    /// <summary>
    /// Camera-relative third-person locomotion for a CharacterController-driven placeholder or model.
    /// The camera owns its orbit; this component only reads its horizontal orientation.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class ThirdPersonPlayerMovement : MonoBehaviour
    {
        private const float MovementInputEpsilon = 0.0001f;

        [Header("References")]
        [SerializeField, Tooltip("Prompt 3 input reader that supplies Move and Sprint.")]
        private PlayerInputReader _input;
        [SerializeField, Tooltip("Camera transform used for horizontal movement orientation. Pitch is ignored.")]
        private Transform _cameraTransform;

        [Header("Movement Speeds")]
        [SerializeField, Min(0f)] private float _walkSpeed = 4f;
        [SerializeField, Min(0f), Tooltip("Sprint speed must be higher than Walk Speed.")]
        private float _sprintSpeed = 7f;
        [SerializeField, Min(0f), Tooltip("Horizontal acceleration while movement input is held, in metres per second squared.")]
        private float _acceleration = 18f;
        [SerializeField, Min(0f), Tooltip("Horizontal deceleration when the requested speed drops or movement input is released, in metres per second squared.")]
        private float _deceleration = 24f;

        [Header("Rotation")]
        [SerializeField, Min(0f), Tooltip("Maximum character turn rate in degrees per second.")]
        private float _rotationSpeed = 540f;

        [Header("Gravity")]
        [SerializeField, Min(0f), Tooltip("Downward acceleration in metres per second squared. Jumping is not implemented.")]
        private float _gravity = 25f;
        [SerializeField, Min(0f), Tooltip("Small downward speed used to keep the controller in contact with the ground.")]
        private float _groundStickSpeed = 2f;

        private CharacterController _characterController;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;

        /// <summary>The current commanded horizontal velocity, before collision resolution.</summary>
        public Vector3 HorizontalVelocity => _horizontalVelocity;

        /// <summary>CharacterController ground state from its most recent Move call.</summary>
        public bool IsGrounded => _characterController != null && _characterController.isGrounded;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void OnDisable()
        {
            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // Internal deterministic seam for Edit Mode tests; the production path reads the Prompt 3 reader.
        internal void Tick(float deltaTime)
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_characterController == null || !_characterController.enabled)
            {
                return;
            }

            deltaTime = Mathf.Max(0f, deltaTime);
            Vector2 moveInput = _input != null
                ? Vector2.ClampMagnitude(_input.Move, 1f)
                : Vector2.zero;
            Vector3 cameraRelativeInput = GetCameraRelativeInput(moveInput);
            float inputMagnitude = cameraRelativeInput.magnitude;

            if (inputMagnitude * inputMagnitude > MovementInputEpsilon)
            {
                Vector3 moveDirection = cameraRelativeInput / inputMagnitude;
                bool sprinting = _input != null && _input.IsButtonPressed(PlayerInputButton.Sprint);
                float targetSpeed = sprinting ? _sprintSpeed : _walkSpeed;
                Vector3 targetVelocity = moveDirection * (targetSpeed * inputMagnitude);
                float speedChangeRate = targetVelocity.sqrMagnitude < _horizontalVelocity.sqrMagnitude
                    ? _deceleration
                    : _acceleration;
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    targetVelocity,
                    speedChangeRate * deltaTime);
                RotateToward(moveDirection, deltaTime);
            }
            else
            {
                _horizontalVelocity = Vector3.MoveTowards(
                    _horizontalVelocity,
                    Vector3.zero,
                    _deceleration * deltaTime);
                if (_horizontalVelocity.sqrMagnitude <= MovementInputEpsilon)
                {
                    _horizontalVelocity = Vector3.zero;
                }
            }

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -_groundStickSpeed;
            }
            else
            {
                _verticalVelocity -= _gravity * deltaTime;
            }

            Vector3 displacement = (_horizontalVelocity + Vector3.up * _verticalVelocity) * deltaTime;
            CollisionFlags collisionFlags = _characterController.Move(displacement);
            if ((collisionFlags & CollisionFlags.Below) != 0 && _verticalVelocity < 0f)
            {
                _verticalVelocity = -_groundStickSpeed;
            }
        }

        private Vector3 GetCameraRelativeInput(Vector2 input)
        {
            Transform orientation = _cameraTransform != null ? _cameraTransform : transform;
            Vector3 forward = Vector3.ProjectOnPlane(orientation.forward, Vector3.up);
            if (forward.sqrMagnitude <= MovementInputEpsilon)
            {
                // A top-down camera has no horizontal forward vector, but its right axis still carries yaw.
                Vector3 cameraRight = Vector3.ProjectOnPlane(orientation.right, Vector3.up);
                if (cameraRight.sqrMagnitude > MovementInputEpsilon)
                {
                    forward = Vector3.Cross(cameraRight, Vector3.up);
                }
                else
                {
                    forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                }
            }

            if (forward.sqrMagnitude <= MovementInputEpsilon)
            {
                forward = Vector3.forward;
            }

            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 cameraRelativeInput = right * input.x + forward * input.y;
            return Vector3.ClampMagnitude(cameraRelativeInput, 1f);
        }

        private void RotateToward(Vector3 moveDirection, float deltaTime)
        {
            if (_rotationSpeed <= 0f || deltaTime <= 0f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotationSpeed * deltaTime);
        }

        private void OnValidate()
        {
            _walkSpeed = Mathf.Max(0f, _walkSpeed);
            _sprintSpeed = Mathf.Max(_walkSpeed + 0.01f, _sprintSpeed);
            _acceleration = Mathf.Max(0f, _acceleration);
            _deceleration = Mathf.Max(0f, _deceleration);
            _rotationSpeed = Mathf.Max(0f, _rotationSpeed);
            _gravity = Mathf.Max(0f, _gravity);
            _groundStickSpeed = Mathf.Max(0f, _groundStickSpeed);
        }
    }
}
