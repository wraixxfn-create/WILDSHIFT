using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Interaction;
using Wildshift.Player.Input;

namespace Wildshift.Player.Interaction
{
    /// <summary>
    /// Finds the interactable the player is aiming at and forwards the Interact button to it.
    /// Detection is a single camera raycast per frame, so no scene scan happens at any time.
    /// </summary>
    /// <remarks>
    /// The ray starts at the view camera and follows its forward axis. Colliders on this GameObject are ignored, so the
    /// player's own body never blocks aim. The nearest remaining hit decides the result: only if that hit belongs to an
    /// enabled, active <see cref="IInteractable"/> that reports <c>CanInteract</c> and lies within range does it become
    /// the target. Any other nearest hit (wall, prop, disabled or spent interactable) blocks line of sight.
    /// Range is measured from this GameObject's position to the hit point, not from the camera, because the
    /// third-person camera sits several metres behind the player.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionDetector : MonoBehaviour
    {
        // Fixed buffer. A saturated result fails closed (no target) rather than risking an unseen nearer hit.
        private const int MaxHits = 16;

        [Header("References")]
        [SerializeField, Tooltip("Prompt 3 input reader that supplies the Interact button.")]
        private PlayerInputReader _input;
        [SerializeField, Tooltip("Camera whose forward axis aims the detection ray. Usually the Main Camera.")]
        private UnityEngine.Camera _viewCamera;

        [Header("Detection")]
        [SerializeField, Min(0f), Tooltip("Maximum distance in metres from this object's position to the target's hit point.")]
        private float _interactionRange = 2.5f;
        [SerializeField, Tooltip("Layers the aim ray can hit. Include interactable layers and the environment layers that should block line of sight. Trigger colliders are ignored.")]
        private LayerMask _detectionLayers = Physics.DefaultRaycastLayers;

        private readonly RaycastHit[] _hits = new RaycastHit[MaxHits];
        private IInteractable _currentTarget;
        private PlayerInputReader _subscribedInput;

        /// <summary>
        /// The interactable currently aimed at and in range, or null. Returns null if the cached target has since
        /// been destroyed or disabled.
        /// </summary>
        public IInteractable CurrentTarget => IsUsable(_currentTarget) ? _currentTarget : null;

        private void Awake()
        {
            if (_input == null)
            {
                WildshiftLog.Error(
                    $"{nameof(PlayerInteractionDetector)} on '{name}' has no Player Input Reader assigned. " +
                    "The Interact button will not reach interactables.",
                    this);
            }

            if (_viewCamera == null)
            {
                WildshiftLog.Error(
                    $"{nameof(PlayerInteractionDetector)} on '{name}' has no View Camera assigned. " +
                    "Interaction detection is disabled.",
                    this);
            }
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
            _currentTarget = null;
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
        }

        private void Update()
        {
            RefreshTarget();
        }

        /// <summary>Re-runs aim detection now. Used per frame and immediately before an interaction.</summary>
        internal void RefreshTarget()
        {
            _currentTarget = null;
            if (_viewCamera == null)
            {
                return;
            }

            Transform view = _viewCamera.transform;
            Vector3 origin = view.position;
            Vector3 anchor = transform.position;

            // Any point within range of the anchor is at most (range + |origin - anchor|) from the camera along the ray.
            float maxDistance = _interactionRange + Vector3.Distance(origin, anchor);
            int count = Physics.RaycastNonAlloc(
                origin, view.forward, _hits, maxDistance, _detectionLayers, QueryTriggerInteraction.Ignore);
            if (count <= 0 || count >= _hits.Length)
            {
                return;
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

            if (!hasNearest || Vector3.Distance(nearest.point, anchor) > _interactionRange)
            {
                return;
            }

            IInteractable candidate = nearest.collider.GetComponentInParent<IInteractable>();
            if (!IsUsable(candidate) || !candidate.CanInteract(gameObject))
            {
                return;
            }

            _currentTarget = candidate;
        }

        /// <summary>Refreshes the target, then interacts with it if one is valid. Returns true when an interaction ran.</summary>
        internal bool TryInteract()
        {
            RefreshTarget();
            IInteractable target = CurrentTarget;
            if (target == null)
            {
                return false;
            }

            target.Interact(gameObject);
            return true;
        }

        private void OnButtonPressed(PlayerInputButton button)
        {
            if (button == PlayerInputButton.Interact)
            {
                TryInteract();
            }
        }

        private bool IsOwnCollider(Collider hitCollider)
        {
            return hitCollider == null || hitCollider.transform.IsChildOf(transform);
        }

        // Unity's overloaded null check is needed here: a destroyed object is not null through an interface reference.
        private static bool IsUsable(IInteractable candidate)
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
