using System;
using System.Collections.Generic;
using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// Designer-authored configuration for one creature species: its identity, size class, movement,
    /// detection ranges, and baseline behaviour parameters. One asset describes the whole species and
    /// is shared by every instance of it.
    /// </summary>
    /// <remarks>
    /// <para>Treat this shared asset as read-only at runtime. Per-creature state — which behaviour it is
    /// in, its timers, its wander target, what it has noticed — lives in a <see cref="CreatureInstanceState"/>
    /// that a <see cref="CreatureBehaviorController"/> owns, never here, so the same asset can be reused
    /// by any number of creatures and across play sessions.</para>
    /// <para>Runtime behaviour reads every number it needs from this asset. Nothing species-specific is
    /// duplicated on the controller, the prefab, or an unrelated system, so re-tuning a species is an
    /// asset edit and adding a species is a new asset — no code change.</para>
    /// <para>Values are checked by <see cref="CreatureSpeciesValidator"/>, which reports errors for
    /// unusable or contradictory authoring and warnings for authoring that is legal but usually a mistake.
    /// Use the asset's <b>Validate Species</b> context-menu item to run the same rules from the Inspector.</para>
    /// </remarks>
    [CreateAssetMenu(
        fileName = "CreatureSpeciesDefinition",
        menuName = "Wildshift/Ecology/Creature Species Definition")]
    public sealed class CreatureSpeciesDefinition : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField, Tooltip("Stable data ID used by logs, events, and any future save data, for example " +
                                 "'nacre/species/siltveil-grazer'. Required, unique, and without spaces. It is never " +
                                 "generated from a prefab, scene object, or asset name, and it must not change once " +
                                 "other data references it.")]
        private string _stableId;

        [SerializeField, Tooltip("Short authored label used in logs and any future UI. This is not the species' identity.")]
        private string _displayName;

        [SerializeField, TextArea(3, 6), Tooltip("Two or three sentences of in-world lore: what the creature is, where it " +
                                                 "lives, and how it behaves. Designer and player facing text only; it drives no rules.")]
        private string _loreDescription;

        [SerializeField, Tooltip("Coarse body-size band (Tiny to Enormous). A label for authors and text, not a numeric " +
                                 "measurement; the visible size comes from the creature's prefab.")]
        private CreatureSizeClass _sizeClass = CreatureSizeClass.Small;

        [Header("Movement")]
        [SerializeField, Tooltip("Speed in metres per second while wandering between grazing spots. Slow enough that the " +
                                 "player can walk alongside it. Must be greater than zero and lower than Flee Speed.")]
        private float _wanderSpeedMetresPerSecond = 0.7f;

        [SerializeField, Tooltip("Speed in metres per second while fleeing. Must be greater than Wander Speed, otherwise the " +
                                 "species cannot escape anything it notices.")]
        private float _fleeSpeedMetresPerSecond = 3.4f;

        [SerializeField, Tooltip("How quickly the creature reaches its target speed or stops, in metres per second squared. " +
                                 "Used for both speeding up and slowing down.")]
        private float _accelerationMetresPerSecondSquared = 9f;

        [SerializeField, Tooltip("Maximum turn rate in degrees per second. Low values read as a heavy animal, high values as " +
                                 "a darting one.")]
        private float _turnRateDegreesPerSecond = 260f;

        [SerializeField, Tooltip("Steepest slope in degrees the creature can walk up. Applied to its CharacterController when " +
                                 "it initializes. Keep below 89 so the controller stays valid.")]
        private float _maximumSlopeDegrees = 40f;

        [Header("Home range")]
        [SerializeField, Tooltip("Radius in metres of the area around the creature's spawn point that it wanders inside. " +
                                 "Flight is allowed out to twice this radius so a chase does not push it home through the threat.")]
        private float _wanderRadiusMetres = 4f;

        [Header("Detection")]
        [SerializeField, Tooltip("Distance in metres at which a moving actor is noticed and puts the creature into Alert. " +
                                 "Measured horizontally from the creature. Must be at least the Flee Radius.")]
        private float _noticeRadiusMetres = 9f;

        [SerializeField, Tooltip("Distance in metres at which a noticed stimulus becomes a threat and the creature flees. " +
                                 "Must be greater than zero and no greater than the Notice Radius, so nothing is fled from " +
                                 "before it is noticed.")]
        private float _fleeRadiusMetres = 3.5f;

        [SerializeField, Tooltip("Distance in metres at which a sudden disturbance (a reported event, not a moving actor) " +
                                 "makes the creature flee immediately. Usually larger than the Notice Radius, because a loud " +
                                 "or abrupt event carries farther than the sight of movement.")]
        private float _startleRadiusMetres = 12f;

        [SerializeField, Tooltip("Minimum actor speed in metres per second that counts as movement. An actor slower than " +
                                 "this is ignored, so a player who stands still can be approached.")]
        private float _minimumActorSpeedMetresPerSecond = 0.6f;

        [SerializeField, Tooltip("Height in metres above the creature's pivot that perception rays start from. Set it near " +
                                 "the creature's sensory organs so props block sight at the right height. Must not be negative.")]
        private float _perceptionHeightOffsetMetres = 0.3f;

        [SerializeField, Tooltip("When on, terrain and props on the occluder layers block perception, so cover works. Turn " +
                                 "off for a species that senses through obstacles.")]
        private bool _requiresLineOfSight = true;

        [SerializeField, Tooltip("Seconds between perception samples. Larger values are cheaper but notice fast actors later; " +
                                 "values above 0.5 s are reported as a warning.")]
        private float _perceptionIntervalSeconds = 0.15f;

        [Header("Behaviour timing")]
        [SerializeField, Tooltip("Shortest time in seconds the creature grazes in place before walking to a new spot.")]
        private float _idleMinSeconds = 2.5f;

        [SerializeField, Tooltip("Longest time in seconds the creature grazes in place before walking to a new spot. The " +
                                 "actual idle time is drawn between the minimum and this maximum, and must be at least the minimum.")]
        private float _idleMaxSeconds = 7f;

        [SerializeField, Tooltip("Shortest time in seconds the creature stays Alert once it has noticed something, even if " +
                                 "the stimulus stops immediately. Stops a passing actor from causing a flicker.")]
        private float _alertHoldSeconds = 1.2f;

        [SerializeField, Tooltip("Seconds without a stimulus before an Alert creature calms down and wanders again. Longer " +
                                 "values read as a warier species.")]
        private float _calmDownSeconds = 3.5f;

        [SerializeField, Tooltip("Seconds the creature keeps fleeing before it looks again. If the threat is still inside the " +
                                 "Flee Radius it flees again; if it is only inside the Notice Radius the creature becomes Alert; " +
                                 "otherwise it stops and grazes.")]
        private float _fleeDurationSeconds = 1.6f;

        /// <summary>Stable authored species ID used by logs, events, and any future persistence.</summary>
        public string StableId => _stableId;

        /// <summary>Short authored label; never used as the identity key.</summary>
        public string DisplayName => _displayName;

        /// <summary>Short authored lore text. It drives no rules.</summary>
        public string LoreDescription => _loreDescription;

        /// <summary>Coarse body-size band, authored as a label.</summary>
        public CreatureSizeClass SizeClass => _sizeClass;

        /// <summary>Speed while wandering, in metres per second.</summary>
        public float WanderSpeedMetresPerSecond => _wanderSpeedMetresPerSecond;

        /// <summary>Speed while fleeing, in metres per second.</summary>
        public float FleeSpeedMetresPerSecond => _fleeSpeedMetresPerSecond;

        /// <summary>Speed-up and slow-down rate, in metres per second squared.</summary>
        public float AccelerationMetresPerSecondSquared => _accelerationMetresPerSecondSquared;

        /// <summary>Maximum turn rate, in degrees per second.</summary>
        public float TurnRateDegreesPerSecond => _turnRateDegreesPerSecond;

        /// <summary>Steepest walkable slope, in degrees.</summary>
        public float MaximumSlopeDegrees => _maximumSlopeDegrees;

        /// <summary>Radius of the home range the creature wanders inside, in metres.</summary>
        public float WanderRadiusMetres => _wanderRadiusMetres;

        /// <summary>Distance at which movement is noticed, in metres.</summary>
        public float NoticeRadiusMetres => _noticeRadiusMetres;

        /// <summary>Distance at which a noticed stimulus triggers flight, in metres.</summary>
        public float FleeRadiusMetres => _fleeRadiusMetres;

        /// <summary>Distance at which a sudden disturbance triggers flight, in metres.</summary>
        public float StartleRadiusMetres => _startleRadiusMetres;

        /// <summary>Slowest actor speed that counts as movement, in metres per second.</summary>
        public float MinimumActorSpeedMetresPerSecond => _minimumActorSpeedMetresPerSecond;

        /// <summary>Height above the pivot that perception rays start from, in metres.</summary>
        public float PerceptionHeightOffsetMetres => _perceptionHeightOffsetMetres;

        /// <summary>Whether terrain and props on the occluder layers block perception.</summary>
        public bool RequiresLineOfSight => _requiresLineOfSight;

        /// <summary>Seconds between perception samples.</summary>
        public float PerceptionIntervalSeconds => _perceptionIntervalSeconds;

        /// <summary>Shortest idle interval, in seconds.</summary>
        public float IdleMinSeconds => _idleMinSeconds;

        /// <summary>Longest idle interval, in seconds.</summary>
        public float IdleMaxSeconds => _idleMaxSeconds;

        /// <summary>Minimum time spent Alert after noticing something, in seconds.</summary>
        public float AlertHoldSeconds => _alertHoldSeconds;

        /// <summary>Time without a stimulus before calming down, in seconds.</summary>
        public float CalmDownSeconds => _calmDownSeconds;

        /// <summary>Time spent fleeing before re-checking the threat, in seconds.</summary>
        public float FleeDurationSeconds => _fleeDurationSeconds;

        /// <summary>
        /// Creates the runtime state for one new creature of this species. The returned state reads this
        /// asset but never writes to it, and it owns everything that changes per instance.
        /// </summary>
        /// <param name="homePosition">World position the creature's home range is centred on, normally its spawn point.</param>
        /// <param name="randomRange">
        /// Supplies a value between two bounds. Pass <c>UnityEngine.Random.Range</c> in play, or a deterministic
        /// function in tests. Null is allowed and makes every random choice take the lower bound.
        /// </param>
        internal CreatureInstanceState CreateInitialState(Vector3 homePosition, Func<float, float, float> randomRange)
        {
            return new CreatureInstanceState(this, homePosition, randomRange);
        }

        /// <summary>
        /// Runs <see cref="CreatureSpeciesValidator"/> over this asset and logs every error and warning it
        /// finds. Available from the asset's context menu so an author can check their edit without entering
        /// Play Mode. It changes nothing.
        /// </summary>
        [ContextMenu("Validate Species")]
        private void ValidateFromContextMenu()
        {
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            bool valid = CreatureSpeciesValidator.Validate(this, errors, warnings);

            for (int i = 0; i < errors.Count; i++)
            {
                WildshiftLog.Error("Species '" + name + "' is invalid: " + errors[i], this);
            }

            for (int i = 0; i < warnings.Count; i++)
            {
                WildshiftLog.Warning("Species '" + name + "': " + warnings[i], this);
            }

            if (valid && warnings.Count == 0)
            {
                WildshiftLog.Info(
                    "Species '" + name + "' (" + _stableId + ") passed validation with no warnings.", this);
            }
        }
    }
}
