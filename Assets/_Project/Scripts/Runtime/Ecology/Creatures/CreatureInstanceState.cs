using System;
using UnityEngine;

namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// Runtime state for one creature instance: the behaviour it is in, the timers driving it, where it is
    /// going, and what it last noticed. This is the per-instance half of the split — every number it uses
    /// comes from the authored <see cref="CreatureSpeciesDefinition"/> it was created from, and it never
    /// writes back to that asset.
    /// </summary>
    /// <remarks>
    /// <para>Plain C# with no Unity lifecycle, so it can be stepped from a test with a fixed delta time and a
    /// deterministic random function. The owning <see cref="CreatureBehaviorController"/> supplies the delta
    /// time and the creature's position, and turns <see cref="DesiredSpeed"/> and <see cref="MovementTarget"/>
    /// into actual movement; this class only decides intent.</para>
    /// <para>Distances are measured on the XZ plane. A creature on the ground should not notice an actor
    /// because of a height difference, and it should not flee toward a point under or over a ledge.</para>
    /// <para>Transitions: <b>Idle</b> waits out a random interval from the species' idle range, then
    /// <b>Wander</b>s to a new point inside the home range. Reaching that point, or running out of leg time,
    /// returns to <b>Idle</b>. A stimulus inside the notice radius interrupts either one with <b>Alert</b>,
    /// and a stimulus inside the flee radius — or a sudden disturbance inside the startle radius — starts
    /// <b>Flee</b>. Fleeing lasts the species' flee duration and is then re-evaluated: still inside the flee
    /// radius it flees again, only inside the notice radius it becomes Alert, and with no stimulus left it
    /// stops and grazes.</para>
    /// </remarks>
    public sealed class CreatureInstanceState
    {
        /// <summary>How close the creature must get to its wander target before the leg counts as finished.</summary>
        public const float WanderArrivalToleranceMetres = 0.35f;

        /// <summary>
        /// How far outside its home range a fleeing creature may be pushed, as a multiple of the species'
        /// wander radius. Flight has to be able to leave the home range, but not to leave the region.
        /// </summary>
        public const float FleeHomeRangeFactor = 2f;

        /// <summary>Extra seconds allowed on a wander leg beyond the straight-line crossing time, so a blocked or
        /// circling creature eventually gives up and picks a new spot instead of grinding against an obstacle.</summary>
        private const float WanderLegPaddingSeconds = 2f;

        /// <summary>Smallest wander radius fraction a new grazing spot may be chosen at, so spots are spread out.</summary>
        private const float MinimumWanderRadiusFraction = 0.35f;

        private const float DegenerateDistanceSquared = 0.0001f;

        private readonly CreatureSpeciesDefinition _species;
        private readonly Func<float, float, float> _randomRange;
        private readonly Vector3 _homePosition;

        private CreatureBehavior _behavior = CreatureBehavior.Idle;
        private float _secondsInBehavior;
        private float _behaviorTimeLimit;
        private Vector3 _wanderTarget;
        private Vector3 _fleeTarget;
        private bool _hasStimulus;
        private Vector3 _stimulusPosition;
        private float _secondsSinceStimulus;

        /// <summary>Raised after a behaviour change, with the previous and the new behaviour.</summary>
        public event Action<CreatureBehavior, CreatureBehavior> BehaviorChanged;

        internal CreatureInstanceState(
            CreatureSpeciesDefinition species,
            Vector3 homePosition,
            Func<float, float, float> randomRange)
        {
            _species = species;
            _homePosition = homePosition;
            _randomRange = randomRange;
            _wanderTarget = homePosition;
            _fleeTarget = homePosition;
            _stimulusPosition = homePosition;
            _behaviorTimeLimit = species != null
                ? RandomBetween(species.IdleMinSeconds, species.IdleMaxSeconds)
                : 0f;
        }

        /// <summary>The authored species this instance was created from. Read-only; never written to.</summary>
        public CreatureSpeciesDefinition Species => _species;

        /// <summary>Stable ID of <see cref="Species"/>, or null when no species was supplied.</summary>
        public string SpeciesId => _species != null ? _species.StableId : null;

        /// <summary>The behaviour the creature is currently in.</summary>
        public CreatureBehavior Behavior => _behavior;

        /// <summary>Seconds spent in <see cref="Behavior"/> so far.</summary>
        public float SecondsInBehavior => _secondsInBehavior;

        /// <summary>Centre of the creature's home range, normally its spawn position.</summary>
        public Vector3 HomePosition => _homePosition;

        /// <summary>Seconds since perception last reported a stimulus. Zero while one is present.</summary>
        public float SecondsSinceStimulus => _secondsSinceStimulus;

        /// <summary>True while perception reports a stimulus. Cleared when perception reports none.</summary>
        public bool HasStimulus => _hasStimulus;

        /// <summary>The last stimulus position reported, whether or not one is currently present.</summary>
        public Vector3 StimulusPosition => _stimulusPosition;

        /// <summary>The grazing spot the creature walks toward while wandering.</summary>
        public Vector3 WanderTarget => _wanderTarget;

        /// <summary>The point the creature runs toward while fleeing.</summary>
        public Vector3 FleeTarget => _fleeTarget;

        /// <summary>
        /// The point the owner should move toward for the current behaviour. Meaningful only while
        /// <see cref="DesiredSpeed"/> is greater than zero.
        /// </summary>
        public Vector3 MovementTarget => _behavior == CreatureBehavior.Flee ? _fleeTarget : _wanderTarget;

        /// <summary>Speed the owner should aim for, in metres per second. Zero while idle or alert.</summary>
        public float DesiredSpeed
        {
            get
            {
                if (_species == null)
                {
                    return 0f;
                }

                switch (_behavior)
                {
                    case CreatureBehavior.Wander:
                        return _species.WanderSpeedMetresPerSecond;
                    case CreatureBehavior.Flee:
                        return _species.FleeSpeedMetresPerSecond;
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>
        /// Reports that perception currently sees a stimulus, and where it is. Call this once per perception
        /// sample while a stimulus is visible; the position is used for facing, for the flee direction, and
        /// for the range checks in <see cref="Advance"/>.
        /// </summary>
        public void SetStimulus(Vector3 worldPosition)
        {
            _hasStimulus = true;
            _stimulusPosition = worldPosition;
            _secondsSinceStimulus = 0f;
        }

        /// <summary>
        /// Reports that perception currently sees nothing. The last stimulus position is kept so the creature
        /// finishes facing the way it was looking, but <see cref="SecondsSinceStimulus"/> starts counting up
        /// and the calm-down rules apply.
        /// </summary>
        public void ClearStimulus()
        {
            _hasStimulus = false;
        }

        /// <summary>
        /// Reports a sudden disturbance at a world position — an event such as a fall, a shot, or a scripted
        /// bang, rather than a moving actor. Inside the species' startle radius it starts flight immediately,
        /// even from beyond the notice radius, which is what makes a disturbance read differently from a
        /// sighting. Outside that radius it is ignored, because a creature that flinches at anything anywhere
        /// has no readable behaviour.
        /// </summary>
        public void ReportSuddenDisturbance(Vector3 disturbancePosition, Vector3 creaturePosition)
        {
            if (_species == null)
            {
                return;
            }

            if (FlatDistance(creaturePosition, disturbancePosition) > _species.StartleRadiusMetres)
            {
                return;
            }

            EnterFlee(creaturePosition, disturbancePosition);
        }

        /// <summary>
        /// Advances timers and applies one round of transition rules. Call once per frame with the creature's
        /// current position; the owner reads <see cref="DesiredSpeed"/> and <see cref="MovementTarget"/> after.
        /// </summary>
        public void Advance(float deltaTime, Vector3 creaturePosition)
        {
            if (_species == null)
            {
                return;
            }

            _secondsInBehavior += deltaTime > 0f ? deltaTime : 0f;
            _secondsSinceStimulus = _hasStimulus ? 0f : _secondsSinceStimulus + (deltaTime > 0f ? deltaTime : 0f);

            float distanceToStimulus = _hasStimulus
                ? FlatDistance(creaturePosition, _stimulusPosition)
                : float.MaxValue;
            float fleeRadius = _species.FleeRadiusMetres;
            float noticeRadius = _species.NoticeRadiusMetres;

            switch (_behavior)
            {
                case CreatureBehavior.Flee:
                    if (_secondsInBehavior >= _species.FleeDurationSeconds)
                    {
                        if (distanceToStimulus <= fleeRadius)
                        {
                            EnterFlee(creaturePosition, _stimulusPosition);
                        }
                        else if (distanceToStimulus <= noticeRadius)
                        {
                            EnterAlert(_stimulusPosition);
                        }
                        else
                        {
                            EnterIdle();
                        }
                    }

                    break;

                case CreatureBehavior.Alert:
                    if (distanceToStimulus <= fleeRadius)
                    {
                        EnterFlee(creaturePosition, _stimulusPosition);
                        break;
                    }

                    // Both conditions are required: the hold stops a flicker, the calm-down makes wariness
                    // outlast the sighting itself.
                    if (!_hasStimulus &&
                        _secondsInBehavior >= _species.AlertHoldSeconds &&
                        _secondsSinceStimulus >= _species.CalmDownSeconds)
                    {
                        EnterWander(creaturePosition);
                    }

                    break;

                case CreatureBehavior.Wander:
                    if (distanceToStimulus <= fleeRadius)
                    {
                        EnterFlee(creaturePosition, _stimulusPosition);
                        break;
                    }

                    if (distanceToStimulus <= noticeRadius)
                    {
                        EnterAlert(_stimulusPosition);
                        break;
                    }

                    if (ReachedWanderTarget(creaturePosition) || _secondsInBehavior >= _behaviorTimeLimit)
                    {
                        EnterIdle();
                    }

                    break;

                default:
                    if (distanceToStimulus <= fleeRadius)
                    {
                        EnterFlee(creaturePosition, _stimulusPosition);
                        break;
                    }

                    if (distanceToStimulus <= noticeRadius)
                    {
                        EnterAlert(_stimulusPosition);
                        break;
                    }

                    if (_secondsInBehavior >= _behaviorTimeLimit)
                    {
                        EnterWander(creaturePosition);
                    }

                    break;
            }
        }

        private void EnterIdle()
        {
            _behaviorTimeLimit = RandomBetween(_species.IdleMinSeconds, _species.IdleMaxSeconds);
            SetBehavior(CreatureBehavior.Idle);
        }

        private void EnterWander(Vector3 creaturePosition)
        {
            _wanderTarget = PickWanderTarget(creaturePosition);
            _behaviorTimeLimit = MaximumWanderLegSeconds();
            SetBehavior(CreatureBehavior.Wander);
        }

        private void EnterAlert(Vector3 stimulusPosition)
        {
            _stimulusPosition = stimulusPosition;
            SetBehavior(CreatureBehavior.Alert);
        }

        private void EnterFlee(Vector3 creaturePosition, Vector3 threatPosition)
        {
            _stimulusPosition = threatPosition;
            _fleeTarget = ComputeFleeTarget(creaturePosition, threatPosition);
            SetBehavior(CreatureBehavior.Flee);
        }

        private void SetBehavior(CreatureBehavior next)
        {
            CreatureBehavior previous = _behavior;
            _behavior = next;

            // Reset unconditionally: re-entering Flee while already fleeing must restart its timer, or the
            // re-evaluation at the end of a flee leg would fire again on the very next frame.
            _secondsInBehavior = 0f;

            if (previous != next)
            {
                BehaviorChanged?.Invoke(previous, next);
            }
        }

        private Vector3 PickWanderTarget(Vector3 creaturePosition)
        {
            float wanderRadius = _species.WanderRadiusMetres;
            float angle = RandomBetween(0f, Mathf.PI * 2f);
            float radius = wanderRadius * RandomBetween(MinimumWanderRadiusFraction, 1f);
            Vector3 target = _homePosition + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

            // The target keeps the creature's current height: slopes are handled by the CharacterController,
            // and aiming at a point in the air or under the ground would stall the leg.
            return new Vector3(target.x, creaturePosition.y, target.z);
        }

        private Vector3 ComputeFleeTarget(Vector3 creaturePosition, Vector3 threatPosition)
        {
            Vector3 away = creaturePosition - threatPosition;
            away.y = 0f;

            if (away.sqrMagnitude <= DegenerateDistanceSquared)
            {
                // The threat is on top of the creature, so "away" is undefined: continue outward from home.
                away = creaturePosition - _homePosition;
                away.y = 0f;
            }

            if (away.sqrMagnitude <= DegenerateDistanceSquared)
            {
                away = Vector3.forward;
            }

            away.Normalize();
            Vector3 target = creaturePosition + away * _species.FleeRadiusMetres;

            Vector3 fromHome = target - _homePosition;
            fromHome.y = 0f;
            float homeLimit = _species.WanderRadiusMetres * FleeHomeRangeFactor;
            if (fromHome.magnitude > homeLimit && fromHome.sqrMagnitude > DegenerateDistanceSquared)
            {
                target = _homePosition + fromHome.normalized * homeLimit;
            }

            return new Vector3(target.x, creaturePosition.y, target.z);
        }

        private bool ReachedWanderTarget(Vector3 creaturePosition)
        {
            return FlatDistance(creaturePosition, _wanderTarget) <= WanderArrivalToleranceMetres;
        }

        private float MaximumWanderLegSeconds()
        {
            float speed = Mathf.Max(_species.WanderSpeedMetresPerSecond, 0.1f);

            // Two radii is the longest straight line across the home disc, so a leg that takes longer than
            // this plus padding is blocked and should be abandoned.
            return _species.WanderRadiusMetres * 2f / speed + WanderLegPaddingSeconds;
        }

        private float RandomBetween(float minimum, float maximum)
        {
            if (_randomRange == null || !float.IsFinite(minimum) || !float.IsFinite(maximum))
            {
                return float.IsFinite(minimum) ? minimum : 0f;
            }

            if (maximum < minimum)
            {
                return minimum;
            }

            return _randomRange(minimum, maximum);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float deltaX = a.x - b.x;
            float deltaZ = a.z - b.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }
    }
}
