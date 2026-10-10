using System.Collections.Generic;
using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// Scene component that runs one creature instance. It owns that instance's
    /// <see cref="CreatureInstanceState"/>, samples perception, and turns the state's intent into
    /// CharacterController movement. Every tunable number it uses is read from the assigned
    /// <see cref="CreatureSpeciesDefinition"/>; this component holds no species values of its own.
    /// </summary>
    /// <remarks>
    /// <para>Only scene wiring lives here: which species asset to run, which actors to watch, and which
    /// layers block perception. That is what makes one component drive any species — a second species is a
    /// new asset and a new prefab, not a new component or a new field.</para>
    /// <para>The species asset is validated with <see cref="CreatureSpeciesValidator"/> before anything runs.
    /// An unassigned or invalid species logs each actionable message and disables the component, so a
    /// misconfigured creature stops visibly instead of standing still for reasons nobody can see.</para>
    /// <para>Nothing is persisted. The creature's position and behaviour are session state; a save/restore
    /// integration is a later, separate step.</para>
    /// </remarks>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class CreatureBehaviorController : MonoBehaviour
    {
        /// <summary>
        /// Downward acceleration in metres per second squared. Gravity is a world constant, not a species
        /// trait, so it is not authored per species; it matches the player movement default.
        /// </summary>
        private const float GravityMetresPerSecondSquared = 25f;

        /// <summary>Small downward speed that keeps the controller in contact with the ground.</summary>
        private const float GroundStickSpeedMetresPerSecond = 2f;

        private const float MovementEpsilon = 0.0001f;

        /// <summary>Height above the pivot that perception rays aim at on an actor, so rays meet the torso.</summary>
        private const float ActorPerceptionHeightMetres = 0.9f;

        private const int MaxOcclusionHits = 8;

        private const int RangeCircleSegments = 32;

        [Header("Species")]
        [SerializeField, Tooltip("Authored species definition this creature runs. Required. All speeds, ranges, and " +
                                 "timings come from it; this component stores no species values.")]
        private CreatureSpeciesDefinition _species;

        [Header("Perception wiring")]
        [SerializeField, Tooltip("Actors this creature watches for movement. Leave empty on the prefab and assign per " +
                                 "instance in the scene, so the species asset and prefab stay scene independent. An actor " +
                                 "counts as moving when its speed since the last sample reaches the species' Minimum Actor Speed.")]
        private Transform[] _perceivedActors;

        [SerializeField, Tooltip("Layers that block perception when the species requires line of sight. Include terrain " +
                                 "and props. Trigger colliders are ignored, and this creature's own colliders never block it.")]
        private LayerMask _occluderLayers = Physics.DefaultRaycastLayers;

        [Header("Debug")]
        [SerializeField, Tooltip("Draws the notice, flee, and startle radii, the home range, and the current movement " +
                                 "target in the Scene view, using the values from the species definition.")]
        private bool _drawRangeGizmos = true;

        private readonly List<string> _configurationErrors = new List<string>();
        private readonly List<string> _configurationWarnings = new List<string>();
        private readonly RaycastHit[] _occlusionHits = new RaycastHit[MaxOcclusionHits];
        private readonly Dictionary<Transform, Vector3> _lastActorPositions = new Dictionary<Transform, Vector3>();

        private CharacterController _characterController;
        private CreatureInstanceState _state;
        private Vector3 _horizontalVelocity;
        private float _verticalVelocity;
        private float _secondsSincePerceptionSample;
        private bool _configured;

        /// <summary>The assigned species definition, or null when none is assigned.</summary>
        public CreatureSpeciesDefinition Species => _species;

        /// <summary>Stable ID of the assigned species, or null when none is assigned.</summary>
        public string SpeciesId => _species != null ? _species.StableId : null;

        /// <summary>
        /// This instance's runtime state, or null when the species is missing or invalid. Owned by this
        /// component and never shared with another creature.
        /// </summary>
        public CreatureInstanceState State => _state;

        /// <summary>The instance's current behaviour, or Idle when it is not configured.</summary>
        public CreatureBehavior Behavior => _state != null ? _state.Behavior : CreatureBehavior.Idle;

        /// <summary>True when a species is assigned and passed validation, so the creature is running.</summary>
        public bool IsConfigured => _configured;

        /// <summary>Validation errors from the most recent configuration attempt. Empty when valid.</summary>
        public IReadOnlyList<string> ConfigurationErrors => _configurationErrors;

        /// <summary>Validation warnings from the most recent configuration attempt.</summary>
        public IReadOnlyList<string> ConfigurationWarnings => _configurationWarnings;

        /// <summary>
        /// Reports a sudden disturbance at a world position — a fall, a shot, a scripted bang, or a debug
        /// call. Inside the species' startle radius the creature flees at once; outside it, nothing happens.
        /// Other systems call this; the creature never searches the scene for sources.
        /// </summary>
        public void NotifySuddenDisturbance(Vector3 worldPosition)
        {
            if (!_configured || _state == null)
            {
                return;
            }

            _state.ReportSuddenDisturbance(worldPosition, transform.position);
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            Configure();
        }

        private void OnEnable()
        {
            if (_state != null)
            {
                _state.BehaviorChanged += OnBehaviorChanged;
            }
        }

        private void OnDisable()
        {
            if (_state != null)
            {
                _state.BehaviorChanged -= OnBehaviorChanged;
            }

            _horizontalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// One simulation step: sample perception when due, advance the state machine, then move. Internal so
        /// Edit Mode tests can drive it with a fixed delta time; the production path reads
        /// <see cref="Time.deltaTime"/>.
        /// </summary>
        internal void Tick(float deltaTime)
        {
            if (!_configured || _state == null || deltaTime <= 0f)
            {
                return;
            }

            _secondsSincePerceptionSample += deltaTime;
            if (_secondsSincePerceptionSample >= _species.PerceptionIntervalSeconds)
            {
                SamplePerception();
            }

            _state.Advance(deltaTime, transform.position);
            ApplyMovement(deltaTime);
        }

        private void Configure()
        {
            _configurationErrors.Clear();
            _configurationWarnings.Clear();
            _configured = false;
            _state = null;
            _secondsSincePerceptionSample = 0f;
            _lastActorPositions.Clear();

            if (!CreatureSpeciesValidator.Validate(_species, _configurationErrors, _configurationWarnings))
            {
                for (int i = 0; i < _configurationErrors.Count; i++)
                {
                    WildshiftLog.Error(
                        $"{nameof(CreatureBehaviorController)} on '{name}' cannot run: {_configurationErrors[i]}", this);
                }

                enabled = false;
                return;
            }

            for (int i = 0; i < _configurationWarnings.Count; i++)
            {
                WildshiftLog.Warning(
                    $"{nameof(CreatureBehaviorController)} on '{name}': {_configurationWarnings[i]}", this);
            }

            if (_characterController != null)
            {
                // Slope tolerance is a species trait; capsule dimensions stay on the prefab because they are
                // bound to the mesh, not to the species' behaviour.
                _characterController.slopeLimit = _species.MaximumSlopeDegrees;
            }

            _state = _species.CreateInitialState(transform.position, Random.Range);
            _configured = true;

            WildshiftLog.Info(
                $"Creature '{name}' configured as '{_species.DisplayName}' ({_species.StableId}), size class " +
                $"{_species.SizeClass}: notice {_species.NoticeRadiusMetres:0.##} m, flee " +
                $"{_species.FleeRadiusMetres:0.##} m, startle {_species.StartleRadiusMetres:0.##} m, wander " +
                $"{_species.WanderSpeedMetresPerSecond:0.##} m/s, flee {_species.FleeSpeedMetresPerSecond:0.##} m/s.",
                this);
        }

        private void SamplePerception()
        {
            float interval = _secondsSincePerceptionSample;
            _secondsSincePerceptionSample = 0f;

            if (_perceivedActors == null || _perceivedActors.Length == 0)
            {
                _state.ClearStimulus();
                return;
            }

            Vector3 origin = transform.position;
            float noticeRadius = _species.NoticeRadiusMetres;
            float minimumActorSpeed = _species.MinimumActorSpeedMetresPerSecond;
            bool found = false;
            float bestDistance = float.MaxValue;
            Vector3 bestPosition = origin;

            for (int i = 0; i < _perceivedActors.Length; i++)
            {
                Transform actor = _perceivedActors[i];
                if (actor == null || !actor.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector3 position = actor.position;
                bool hadPrevious = _lastActorPositions.TryGetValue(actor, out Vector3 previousPosition);
                _lastActorPositions[actor] = position;

                // Without a previous sample there is no speed yet, so the first sample never triggers.
                if (!hadPrevious || interval <= 0f)
                {
                    continue;
                }

                if (Vector3.Distance(previousPosition, position) / interval < minimumActorSpeed)
                {
                    continue;
                }

                float distance = FlatDistance(origin, position);
                if (distance > noticeRadius)
                {
                    continue;
                }

                if (_species.RequiresLineOfSight && !HasLineOfSight(position))
                {
                    continue;
                }

                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    bestPosition = position;
                }
            }

            if (found)
            {
                _state.SetStimulus(bestPosition);
            }
            else
            {
                _state.ClearStimulus();
            }
        }

        private bool HasLineOfSight(Vector3 actorPosition)
        {
            Vector3 from = transform.position + Vector3.up * _species.PerceptionHeightOffsetMetres;
            Vector3 to = actorPosition + Vector3.up * ActorPerceptionHeightMetres;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance <= MovementEpsilon)
            {
                return true;
            }

            int count = Physics.RaycastNonAlloc(
                from, delta / distance, _occlusionHits, distance, _occluderLayers, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return true;
            }

            if (count >= _occlusionHits.Length)
            {
                // Saturated buffer: fail closed, matching the scanner's raycast convention.
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = _occlusionHits[i].collider;
                if (hitCollider == null || hitCollider.transform.IsChildOf(transform))
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private void ApplyMovement(float deltaTime)
        {
            if (_characterController == null || !_characterController.enabled)
            {
                return;
            }

            Vector3 desiredVelocity = Vector3.zero;
            float desiredSpeed = _state.DesiredSpeed;
            if (desiredSpeed > 0f)
            {
                Vector3 toTarget = _state.MovementTarget - transform.position;
                toTarget.y = 0f;
                float planarDistance = toTarget.magnitude;
                if (planarDistance > CreatureInstanceState.WanderArrivalToleranceMetres)
                {
                    desiredVelocity = toTarget / planarDistance * desiredSpeed;
                }
            }

            float acceleration = Mathf.Max(_species.AccelerationMetresPerSecondSquared, 0f);
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, desiredVelocity, acceleration * deltaTime);

            if (_characterController.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -GroundStickSpeedMetresPerSecond;
            }
            else
            {
                _verticalVelocity -= GravityMetresPerSecondSquared * deltaTime;
            }

            Vector3 displacement = (_horizontalVelocity + Vector3.up * _verticalVelocity) * deltaTime;
            CollisionFlags collisionFlags = _characterController.Move(displacement);
            if ((collisionFlags & CollisionFlags.Below) != 0 && _verticalVelocity < 0f)
            {
                _verticalVelocity = -GroundStickSpeedMetresPerSecond;
            }

            RotateTowardIntent(deltaTime);
        }

        private void RotateTowardIntent(float deltaTime)
        {
            Vector3 facing = _horizontalVelocity;
            facing.y = 0f;

            if (facing.sqrMagnitude <= MovementEpsilon)
            {
                // Standing still but alert: turn toward what it noticed, which is what makes the alert readable.
                if (_state.Behavior == CreatureBehavior.Alert && _state.HasStimulus)
                {
                    facing = _state.StimulusPosition - transform.position;
                    facing.y = 0f;
                }
            }

            if (facing.sqrMagnitude <= MovementEpsilon)
            {
                return;
            }

            float turnRate = Mathf.Max(_species.TurnRateDegreesPerSecond, 0f);
            if (turnRate <= 0f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(facing.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, turnRate * deltaTime);
        }

        private void OnBehaviorChanged(CreatureBehavior previous, CreatureBehavior next)
        {
            WildshiftLog.Info(
                $"Creature '{name}' ({SpeciesId}) {previous} -> {next}" +
                (_state != null && _state.HasStimulus
                    ? $" with a stimulus {FlatDistance(transform.position, _state.StimulusPosition):0.##} m away."
                    : "."),
                this);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float deltaX = a.x - b.x;
            float deltaZ = a.z - b.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawRangeGizmos || _species == null)
            {
                return;
            }

            Vector3 ground = transform.position;
            DrawGroundCircle(ground, _species.NoticeRadiusMetres, new Color(1f, 0.82f, 0.2f, 0.9f));
            DrawGroundCircle(ground, _species.FleeRadiusMetres, new Color(1f, 0.34f, 0.24f, 0.95f));
            DrawGroundCircle(ground, _species.StartleRadiusMetres, new Color(0.62f, 0.42f, 1f, 0.6f));

            if (_state != null)
            {
                DrawGroundCircle(
                    _state.HomePosition,
                    _species.WanderRadiusMetres * CreatureInstanceState.FleeHomeRangeFactor,
                    new Color(0.35f, 0.75f, 1f, 0.35f));
                DrawGroundCircle(_state.HomePosition, _species.WanderRadiusMetres, new Color(0.35f, 0.75f, 1f, 0.8f));

                Color previousColor = Gizmos.color;
                Gizmos.color = new Color(0.35f, 1f, 0.5f, 0.9f);
                Gizmos.DrawLine(ground, _state.MovementTarget);
                Gizmos.DrawWireCube(_state.MovementTarget, Vector3.one * 0.2f);
                Gizmos.color = previousColor;
            }
        }

        private static void DrawGroundCircle(Vector3 center, float radius, Color color)
        {
            if (!float.IsFinite(radius) || radius <= 0f)
            {
                return;
            }

            Color previousColor = Gizmos.color;
            Gizmos.color = color;

            Vector3 previousPoint = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= RangeCircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / RangeCircleSegments;
                Vector3 point = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previousPoint, point);
                previousPoint = point;
            }

            Gizmos.color = previousColor;
        }
    }
}
