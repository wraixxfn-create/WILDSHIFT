using UnityEngine;
using Wildshift.Player.Input;

namespace Wildshift.Player.Camera
{
    /// <summary>World-up orbit camera. The target is just a transform, not a character controller.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UnityEngine.Camera))]
    public sealed class ThirdPersonCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _target;
        [SerializeField] private PlayerInputReader _input;

        [Header("Orbit (degrees per mouse pixel)")]
        [SerializeField, Min(0f)] private float _sensitivity = 0.15f;
        [SerializeField] private bool _invertHorizontal;
        [SerializeField] private bool _invertVertical;
        [SerializeField, Range(-89f, 89f)] private float _minimumPitch = -35f;
        [SerializeField, Range(-89f, 89f)] private float _maximumPitch = 75f;
        [SerializeField, Range(-89f, 89f)] private float _initialPitch = 15f;
        [SerializeField] private float _initialYaw;
        [Tooltip("Explicit world-space roll. Zero keeps the horizon level regardless of target rotation.")]
        [SerializeField] private float _roll;

        [Header("Follow")]
        [SerializeField, Min(0f)] private float _followDistance = 4f;
        [Tooltip("World-up offset from target position to the orbit pivot.")]
        [SerializeField] private float _cameraHeight = 1.5f;
        [SerializeField, Min(0f)] private float _followSmoothTime = 0.06f;
        [SerializeField, Min(0f)] private float _obstructionRecoveryTime = 0.15f;

        [Header("Collision")]
        [Tooltip("Solid environment layers. Exclude the followed object's layer where possible.")]
        [SerializeField] private LayerMask _collisionLayers = Physics.DefaultRaycastLayers;
        [Tooltip("Minimum sphere radius. Automatically enlarged to contain the camera near plane.")]
        [SerializeField, Min(0.01f)] private float _collisionRadius = 0.2f;
        [SerializeField, Min(0f)] private float _collisionPadding = 0.03f;

        [Header("Gameplay cursor")]
        [Tooltip("Only captures while playing, focused, with a target and enabled gameplay input. Escape releases; Escape again resumes.")]
        [SerializeField] private bool _captureCursor = true;

        // Fixed buffers allocated once. Saturation fails closed rather than ignoring an unknown wall.
        private readonly RaycastHit[] _hits = new RaycastHit[32];
        private readonly Collider[] _overlaps = new Collider[32];
        private UnityEngine.Camera _camera;
        private PlayerInputReader _subscribedInput;
        private Transform _previousTarget;
        private Vector3 _pivot;
        private Vector3 _pivotVelocity;
        private float _distance;
        private float _distanceVelocity;
        private float _yaw;
        private float _pitch;
        private bool _initialized;
        private bool _focused = true;
        private bool _cursorSuspended;
        private bool _ownsCursor;
        private CursorLockMode _savedCursorLock;
        private bool _savedCursorVisible;

        public Transform Target
        {
            get => _target;
            set { _target = value; _initialized = false; RefreshCursor(); }
        }

        public float Sensitivity
        {
            get => _sensitivity;
            set => _sensitivity = Mathf.Max(0f, value);
        }

        public float Yaw => _yaw;
        public float Pitch => _pitch;

        private void Awake()
        {
            _camera = GetComponent<UnityEngine.Camera>();
            _yaw = _initialYaw;
            _pitch = Mathf.Clamp(_initialPitch, _minimumPitch, _maximumPitch);
        }

        private void OnEnable()
        {
            _initialized = false;
            SubscribeInput();
            RefreshCursor();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            RestoreCursor();
        }

        private void OnDestroy() => RestoreCursor();

        private void OnApplicationFocus(bool focused)
        {
            _focused = focused;
            if (!focused) _cursorSuspended = true;
            RefreshCursor();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) { _cursorSuspended = true; RestoreCursor(); }
        }

        private void LateUpdate()
        {
            SubscribeInput(); // Also supports changing the Inspector reference during play.
            // Respect Unity Editor Escape / external cursor release instead of fighting it every frame.
            if (_ownsCursor && Cursor.lockState != CursorLockMode.Locked)
            {
                _cursorSuspended = true;
            }
            RefreshCursor();
            if (_target == null)
            {
                _initialized = false;
                return; // Keep the last pose; no repeated logging or exceptions.
            }

            bool canLook = _focused && !_cursorSuspended && _input != null && _input.IsGameplayInputEnabled;
            Tick(Time.deltaTime, canLook ? _input.Look : Vector2.zero);
        }

        // Internal deterministic update seam for tests; the production path always reads Prompt 3 input.
        internal void Tick(float deltaTime, Vector2 mouseDelta)
        {
            if (_target == null) { _initialized = false; return; }
            if (_camera == null) _camera = GetComponent<UnityEngine.Camera>();
            deltaTime = Mathf.Max(0f, deltaTime);
            _yaw = Mathf.Repeat(_yaw + mouseDelta.x * _sensitivity * (_invertHorizontal ? -1f : 1f), 360f);
            // Mouse delta is already a displacement: do NOT multiply by deltaTime or smooth rotation.
            _pitch = Mathf.Clamp(_pitch + mouseDelta.y * _sensitivity * (_invertVertical ? 1f : -1f),
                _minimumPitch, _maximumPitch);
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, _roll);
            Vector3 rawPivot = _target.position + Vector3.up * _cameraHeight;
            if (!_initialized || _previousTarget != _target)
            {
                _pivot = rawPivot;
                _pivotVelocity = Vector3.zero;
                _distanceVelocity = 0f;
                _distance = _followDistance;
                _previousTarget = _target;
                _initialized = true;
            }

            float near = _camera.nearClipPlane;
            float halfHeight = near * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * _camera.aspect;
            float radius = Mathf.Max(_collisionRadius, Mathf.Sqrt(near * near + halfHeight * halfHeight + halfWidth * halfWidth));
            // A pivot embedded in a solid has no valid sweep origin. Hold the last pose until it is clear.
            if (IsBlocked(rawPivot, radius)) return;

            Vector3 nextPivot = _followSmoothTime <= 0f ? rawPivot : deltaTime <= 0f ? _pivot :
                Vector3.SmoothDamp(_pivot, rawPivot, ref _pivotVelocity, _followSmoothTime, Mathf.Infinity, deltaTime);
            // Smooth follow must not drag the cast origin through a wall behind a moving target.
            Vector3 pivotOffset = nextPivot - rawPivot;
            float pivotLength = pivotOffset.magnitude;
            if (pivotLength > 0.0001f)
            {
                float safeLength = SafeDistance(rawPivot, pivotOffset / pivotLength, pivotLength, radius);
                nextPivot = rawPivot + pivotOffset / pivotLength * safeLength;
                if (safeLength < pivotLength) _pivotVelocity = Vector3.zero;
            }
            _pivot = nextPivot;
            Vector3 backwards = rotation * Vector3.back;
            float allowedDistance = SafeDistance(_pivot, backwards, _followDistance, radius);
            if (allowedDistance <= _distance || _obstructionRecoveryTime <= 0f)
            {
                _distance = allowedDistance; // Immediate contraction prevents clipping; only recovery is damped.
                _distanceVelocity = 0f;
            }
            else if (deltaTime > 0f)
            {
                _distance = Mathf.SmoothDamp(_distance, allowedDistance, ref _distanceVelocity,
                    _obstructionRecoveryTime, Mathf.Infinity, deltaTime);
            }
            transform.SetPositionAndRotation(_pivot + backwards * _distance, rotation);
        }

        private bool IsIgnored(Collider collider) => collider == null ||
            collider.transform.IsChildOf(_target) || collider.transform.IsChildOf(transform);

        private bool IsBlocked(Vector3 position, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(position, radius, _overlaps, _collisionLayers, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return true;
            for (int i = 0; i < count; i++)
                if (!IsIgnored(_overlaps[i])) return true;
            return false;
        }

        private float SafeDistance(Vector3 origin, Vector3 direction, float length, float radius)
        {
            if (length <= 0f) return 0f;
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, _hits, length,
                _collisionLayers, QueryTriggerInteraction.Ignore);
            if (count == _hits.Length) return 0f;
            float result = length;
            for (int i = 0; i < count; i++)
                if (!IsIgnored(_hits[i].collider)) result = Mathf.Min(result, Mathf.Max(0f, _hits[i].distance - _collisionPadding));
            return result;
        }

        private void SubscribeInput()
        {
            if (_subscribedInput == _input) return;
            UnsubscribeInput();
            _subscribedInput = _input;
            if (_subscribedInput == null) return;
            _subscribedInput.GameplayInputEnabledChanged += OnGameplayInputChanged;
            _subscribedInput.ButtonPressed += OnButtonPressed;
        }

        private void UnsubscribeInput()
        {
            if (_subscribedInput != null)
            {
                _subscribedInput.GameplayInputEnabledChanged -= OnGameplayInputChanged;
                _subscribedInput.ButtonPressed -= OnButtonPressed;
            }
            _subscribedInput = null;
        }

        private void OnGameplayInputChanged(bool enabled)
        {
            _cursorSuspended = false;
            RefreshCursor();
        }

        private void OnButtonPressed(PlayerInputButton button)
        {
            if (button != PlayerInputButton.Pause) return;
            _cursorSuspended = !_cursorSuspended;
            RefreshCursor();
        }

        [ContextMenu("Resume Gameplay Camera Cursor")]
        public void ResumeGameplayCursor()
        {
            _cursorSuspended = false;
            RefreshCursor();
        }

        private void RefreshCursor()
        {
            if (!Application.isPlaying) return;
            bool capture = isActiveAndEnabled && _captureCursor && _focused && !_cursorSuspended &&
                _target != null && _input != null && _input.IsGameplayInputEnabled;
            if (!capture) { RestoreCursor(); return; }
            if (_ownsCursor) return;
            _savedCursorLock = Cursor.lockState;
            _savedCursorVisible = Cursor.visible;
            _ownsCursor = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void RestoreCursor()
        {
            if (!_ownsCursor) return;
            Cursor.lockState = _savedCursorLock;
            Cursor.visible = _savedCursorVisible;
            _ownsCursor = false;
        }

        private void OnValidate()
        {
            _sensitivity = Mathf.Max(0f, _sensitivity);
            _minimumPitch = Mathf.Clamp(_minimumPitch, -89f, 89f);
            _maximumPitch = Mathf.Clamp(_maximumPitch, _minimumPitch, 89f);
            _initialPitch = Mathf.Clamp(_initialPitch, _minimumPitch, _maximumPitch);
            _followDistance = Mathf.Max(0f, _followDistance);
            _followSmoothTime = Mathf.Max(0f, _followSmoothTime);
            _obstructionRecoveryTime = Mathf.Max(0f, _obstructionRecoveryTime);
            _collisionRadius = Mathf.Max(0.01f, _collisionRadius);
            _collisionPadding = Mathf.Max(0f, _collisionPadding);
        }
    }
}
