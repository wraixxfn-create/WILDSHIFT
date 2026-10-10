using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.Ecology.Creatures;

namespace Wildshift.Tests
{
    /// <summary>
    /// Exercises the four behaviours a creature instance can be in, driven by a fixed delta time and a
    /// deterministic random function, so every transition is reproducible and no transition depends on
    /// frame timing.
    /// </summary>
    public sealed class CreatureInstanceStateTests
    {
        private const float Step = 0.1f;

        private CreatureSpeciesDefinition _species;
        private CreatureInstanceState _state;
        private List<CreatureBehavior> _transitions;

        /// <summary>Returns the upper bound of every random range, so idle and wander choices are predictable.</summary>
        private static float TakeMaximum(float minimum, float maximum)
        {
            return maximum;
        }

        /// <summary>Returns the lower bound of every random range.</summary>
        private static float TakeMinimum(float minimum, float maximum)
        {
            return minimum;
        }

        [SetUp]
        public void SetUp()
        {
            _species = ScriptableObject.CreateInstance<CreatureSpeciesDefinition>();
            SetString("_stableId", "test/species/grazer");
            SetString("_displayName", "Test Grazer");
            SetString("_loreDescription", "Test grazer lore.");
            SetFloat("_wanderSpeedMetresPerSecond", 1f);
            SetFloat("_fleeSpeedMetresPerSecond", 4f);
            SetFloat("_accelerationMetresPerSecondSquared", 9f);
            SetFloat("_turnRateDegreesPerSecond", 260f);
            SetFloat("_maximumSlopeDegrees", 40f);
            SetFloat("_wanderRadiusMetres", 4f);
            SetFloat("_noticeRadiusMetres", 9f);
            SetFloat("_fleeRadiusMetres", 3.5f);
            SetFloat("_startleRadiusMetres", 12f);
            SetFloat("_minimumActorSpeedMetresPerSecond", 0.6f);
            SetFloat("_perceptionHeightOffsetMetres", 0.3f);
            SetFloat("_perceptionIntervalSeconds", 0.15f);
            SetFloat("_idleMinSeconds", 2f);
            SetFloat("_idleMaxSeconds", 4f);
            SetFloat("_alertHoldSeconds", 1f);
            SetFloat("_calmDownSeconds", 3f);
            SetFloat("_fleeDurationSeconds", 1.5f);

            _transitions = new List<CreatureBehavior>();
            _state = CreateState(Vector3.zero, TakeMaximum);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_species);
        }

        [Test]
        public void ANewInstanceStartsIdleAtItsHomeWithNoStimulus()
        {
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle));
            Assert.That(_state.HomePosition, Is.EqualTo(Vector3.zero));
            Assert.That(_state.HasStimulus, Is.False);
            Assert.That(_state.SecondsSinceStimulus, Is.EqualTo(0f));
            Assert.That(_state.SpeciesId, Is.EqualTo("test/species/grazer"));
            Assert.That(_state.DesiredSpeed, Is.EqualTo(0f));
            Assert.That(_state.SecondsInBehavior, Is.EqualTo(0f));
        }

        [Test]
        public void ItGrazesForTheAuthoredIdleIntervalThenWanders()
        {
            Advance(3.9f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle), "It left Idle before the idle maximum.");

            Advance(0.2f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Wander));
            Assert.That(_state.DesiredSpeed, Is.EqualTo(_species.WanderSpeedMetresPerSecond));
        }

        [Test]
        public void ItsWanderTargetIsInsideTheHomeRange()
        {
            Advance(4.1f, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Wander));
            Vector3 offset = _state.WanderTarget - _state.HomePosition;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(_species.WanderRadiusMetres + 0.001f));
            Assert.That(offset.magnitude, Is.GreaterThan(0.1f), "The wander target should not be the creature's own feet.");
            Assert.That(_state.MovementTarget, Is.EqualTo(_state.WanderTarget));
        }

        [Test]
        public void ItGrazesAgainOnceItReachesItsWanderTarget()
        {
            Advance(4.1f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Wander));

            Advance(Step, _state.WanderTarget);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle));
            Assert.That(_state.DesiredSpeed, Is.EqualTo(0f));
        }

        [Test]
        public void ABlockedWanderLegIsAbandonedInsteadOfRunningForever()
        {
            Advance(4.1f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Wander));

            // Never move: the leg lasts at most the crossing time plus padding, then it re-picks a spot.
            float maximumLegSeconds = _species.WanderRadiusMetres * 2f / _species.WanderSpeedMetresPerSecond + 2f;
            Advance(maximumLegSeconds + Step, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle));
        }

        [Test]
        public void ANoticedStimulusInsideTheNoticeRadiusRaisesAnAlert()
        {
            _state.SetStimulus(new Vector3(6f, 0f, 0f));
            Advance(Step, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert));
            Assert.That(_state.HasStimulus, Is.True);
            Assert.That(_state.StimulusPosition, Is.EqualTo(new Vector3(6f, 0f, 0f)));
            Assert.That(_state.DesiredSpeed, Is.EqualTo(0f), "An alert creature stops rather than keeps grazing.");
        }

        [Test]
        public void AStimulusBeyondTheNoticeRadiusIsIgnored()
        {
            _state.SetStimulus(new Vector3(9.5f, 0f, 0f));
            Advance(4.1f, Vector3.zero);

            Assert.That(_state.Behavior, Is.Not.EqualTo(CreatureBehavior.Alert));
            Assert.That(_state.Behavior, Is.Not.EqualTo(CreatureBehavior.Flee));
        }

        [Test]
        public void AStimulusInsideTheFleeRadiusStartsFlightEvenWhileGrazing()
        {
            _state.SetStimulus(new Vector3(2f, 0f, 0f));
            Advance(Step, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Flee));
            Assert.That(_state.DesiredSpeed, Is.EqualTo(_species.FleeSpeedMetresPerSecond));
        }

        [Test]
        public void FlightLeadsAwayFromTheThreatAndStaysNearHome()
        {
            Vector3 threat = new Vector3(2f, 0f, 0f);
            _state.SetStimulus(threat);
            Advance(Step, Vector3.zero);

            Vector3 flee = _state.FleeTarget;
            Assert.That(_state.MovementTarget, Is.EqualTo(flee));
            Assert.That(FlatDistance(flee, threat), Is.GreaterThan(FlatDistance(Vector3.zero, threat)),
                "The flee target must be further from the threat than the creature was.");
            Assert.That(flee.x, Is.LessThan(0f), "Fleeing an eastern threat should head west.");

            float homeLimit = _species.WanderRadiusMetres * CreatureInstanceState.FleeHomeRangeFactor;
            Vector3 fromHome = flee - _state.HomePosition;
            fromHome.y = 0f;
            Assert.That(fromHome.magnitude, Is.LessThanOrEqualTo(homeLimit + 0.001f));
        }

        [Test]
        public void AFleeingCreatureKeepsFleeingWhileTheThreatStaysClose()
        {
            Vector3 threat = new Vector3(2f, 0f, 0f);
            _state.SetStimulus(threat);
            Advance(Step, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Flee));

            Advance(_species.FleeDurationSeconds + Step, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Flee));
            Assert.That(_state.SecondsInBehavior, Is.LessThanOrEqualTo(_species.FleeDurationSeconds));
        }

        [Test]
        public void AFleeingCreatureBecomesAlertWhenTheThreatDropsBackToTheNoticeBand()
        {
            _state.SetStimulus(new Vector3(2f, 0f, 0f));
            Advance(Step, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Flee));

            _state.SetStimulus(new Vector3(7f, 0f, 0f));
            Advance(_species.FleeDurationSeconds + Step, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert));
        }

        [Test]
        public void AFleeingCreatureGrazesAgainWhenTheThreatIsGone()
        {
            _state.SetStimulus(new Vector3(2f, 0f, 0f));
            Advance(Step, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Flee));

            _state.ClearStimulus();
            Advance(_species.FleeDurationSeconds + Step, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle));
        }

        [Test]
        public void AnAlertCreatureWaitsOutBothTheHoldAndTheCalmDownBeforeWandering()
        {
            _state.SetStimulus(new Vector3(6f, 0f, 0f));
            Advance(Step, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert));

            _state.ClearStimulus();

            // 0.8 s in: the 1 s hold has not elapsed, so the sighting still counts.
            Advance(_species.AlertHoldSeconds - 0.2f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert), "The hold must outlast a brief sighting.");

            // 2.0 s in: the hold has elapsed but the 3 s calm-down has not, so the creature is still wary.
            Advance(1.2f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert), "The calm-down must outlast the hold.");
            Assert.That(_state.SecondsInBehavior, Is.GreaterThanOrEqualTo(_species.AlertHoldSeconds));

            // 3.5 s since the sighting: the calm-down is over and it goes back to grazing.
            Advance(1.5f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Wander));
        }

        [Test]
        public void AStimulusSeenAgainDuringTheCalmDownRestartsIt()
        {
            _state.SetStimulus(new Vector3(6f, 0f, 0f));
            Advance(Step, Vector3.zero);
            _state.ClearStimulus();
            Advance(2f, Vector3.zero);

            _state.SetStimulus(new Vector3(6f, 0f, 0f));
            Advance(Step, Vector3.zero);
            Assert.That(_state.SecondsSinceStimulus, Is.EqualTo(0f));

            _state.ClearStimulus();
            Advance(2f, Vector3.zero);
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert));
        }

        [Test]
        public void ASuddenDisturbanceInsideTheStartleRadiusStartsFlightFromBeyondTheNoticeRadius()
        {
            _state.ReportSuddenDisturbance(new Vector3(11f, 0f, 0f), Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Flee));
            Assert.That(_state.HasStimulus, Is.False, "A disturbance is an event, not a tracked actor.");
            Assert.That(_state.FleeTarget.x, Is.LessThan(0f));
        }

        [Test]
        public void ASuddenDisturbanceOutsideTheStartleRadiusIsIgnored()
        {
            _state.ReportSuddenDisturbance(new Vector3(13f, 0f, 0f), Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle));
        }

        [Test]
        public void HeightDoesNotExtendPerceptionBecauseDistancesAreMeasuredOnTheGround()
        {
            // 8 m away horizontally but 20 m up: on the ground it is inside the notice radius, and the
            // vertical gap must not push it out.
            _state.SetStimulus(new Vector3(8f, 20f, 0f));
            Advance(Step, Vector3.zero);

            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Alert));
        }

        [Test]
        public void BehaviourChangesAreReportedOncePerTransition()
        {
            _state.SetStimulus(new Vector3(6f, 0f, 0f));
            Advance(Step, Vector3.zero);
            Advance(Step, Vector3.zero);

            Assert.That(_transitions, Is.EqualTo(new[] { CreatureBehavior.Alert }),
                "Re-entering the same behaviour must not fire again.");
        }

        [Test]
        public void TheIdleIntervalComesFromTheAuthoredRange()
        {
            CreatureInstanceState shortest = CreateState(Vector3.zero, TakeMinimum);
            shortest.Advance(2f + Step, Vector3.zero);

            Assert.That(shortest.Behavior, Is.EqualTo(CreatureBehavior.Wander),
                "With the lower bound chosen, the idle interval is the authored minimum.");
            Assert.That(_state.Behavior, Is.EqualTo(CreatureBehavior.Idle),
                "With the upper bound chosen, the same elapsed time is still inside the idle interval.");
        }

        [Test]
        public void ItWorksWithoutARandomFunction()
        {
            // A null random function is legal and takes the lower bound of every range, so the idle
            // interval is the authored minimum.
            CreatureInstanceState state = new CreatureInstanceState(_species, Vector3.zero, null);

            state.Advance(_species.IdleMinSeconds + Step, Vector3.zero);

            Assert.That(state.Behavior, Is.EqualTo(CreatureBehavior.Wander));
        }

        private CreatureInstanceState CreateState(Vector3 home, System.Func<float, float, float> randomRange)
        {
            CreatureInstanceState state = _species.CreateInitialState(home, randomRange);
            state.BehaviorChanged += OnBehaviorChanged;
            return state;
        }

        private void OnBehaviorChanged(CreatureBehavior previous, CreatureBehavior next)
        {
            _transitions.Add(next);
        }

        private void Advance(float seconds, Vector3 position)
        {
            float remaining = seconds;
            while (remaining > 0f)
            {
                float step = Mathf.Min(Step, remaining);
                _state.Advance(step, position);
                remaining -= step;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            float deltaX = a.x - b.x;
            float deltaZ = a.z - b.z;
            return Mathf.Sqrt(deltaX * deltaX + deltaZ * deltaZ);
        }

        private void SetString(string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(_species);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetFloat(string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(_species);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }


    }
}
