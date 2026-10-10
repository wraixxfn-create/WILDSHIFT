using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Ecology.Creatures;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies that a creature instance takes its behaviour from the assigned species asset, refuses to run
    /// on missing or contradictory authoring, and reacts to movement and to sudden disturbances. Movement
    /// itself is not asserted here: <see cref="CharacterController.Move"/> needs a physics step, and the
    /// decision layer is covered by <c>CreatureInstanceStateTests</c>.
    /// </summary>
    public sealed class CreatureBehaviorControllerTests
    {
        private GameObject _creatureObject;
        private GameObject _actorObject;
        private GameObject _occluderObject;
        private CreatureBehaviorController _creature;
        private CharacterController _characterController;
        private CreatureSpeciesDefinition _species;
        private bool _previousIgnoreFailingMessages;

        [SetUp]
        public void SetUp()
        {
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            _species = ScriptableObject.CreateInstance<CreatureSpeciesDefinition>();
            SetString(_species, "_stableId", "test/species/grazer");
            SetString(_species, "_displayName", "Test Grazer");
            SetString(_species, "_loreDescription", "Test grazer lore.");
            SetFloat(_species, "_wanderSpeedMetresPerSecond", 1f);
            SetFloat(_species, "_fleeSpeedMetresPerSecond", 4f);
            SetFloat(_species, "_accelerationMetresPerSecondSquared", 9f);
            SetFloat(_species, "_turnRateDegreesPerSecond", 260f);
            SetFloat(_species, "_maximumSlopeDegrees", 33f);
            SetFloat(_species, "_wanderRadiusMetres", 4f);
            SetFloat(_species, "_noticeRadiusMetres", 9f);
            SetFloat(_species, "_fleeRadiusMetres", 3.5f);
            SetFloat(_species, "_startleRadiusMetres", 12f);
            SetFloat(_species, "_minimumActorSpeedMetresPerSecond", 0.6f);
            SetFloat(_species, "_perceptionHeightOffsetMetres", 0.3f);
            SetBool(_species, "_requiresLineOfSight", true);
            SetFloat(_species, "_perceptionIntervalSeconds", 0.15f);
            SetFloat(_species, "_idleMinSeconds", 2f);
            SetFloat(_species, "_idleMaxSeconds", 4f);
            SetFloat(_species, "_alertHoldSeconds", 1f);
            SetFloat(_species, "_calmDownSeconds", 3f);
            SetFloat(_species, "_fleeDurationSeconds", 1.5f);

            _actorObject = new GameObject("Perceived Actor Test");
            _actorObject.transform.position = new Vector3(0f, 0f, 6f);

            _creatureObject = new GameObject("Creature Test");
            _creatureObject.SetActive(false);
            _creatureObject.transform.position = new Vector3(100f, 10f, 100f);
            _characterController = _creatureObject.AddComponent<CharacterController>();
            _characterController.height = 0.55f;
            _characterController.radius = 0.25f;
            _characterController.center = new Vector3(0f, 0.275f, 0f);
            _characterController.slopeLimit = 45f;
            _creature = _creatureObject.AddComponent<CreatureBehaviorController>();

            // Stand the actor 6 m away, inside the notice radius, before anything samples it.
            _actorObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 0f, 6f);
        }

        [TearDown]
        public void TearDown()
        {
            if (_creatureObject != null) Object.DestroyImmediate(_creatureObject);
            if (_actorObject != null) Object.DestroyImmediate(_actorObject);
            if (_occluderObject != null) Object.DestroyImmediate(_occluderObject);
            if (_species != null) Object.DestroyImmediate(_species);
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
        }

        [Test]
        public void ARunningCreatureReportsTheSpeciesItWasConfiguredFrom()
        {
            ActivateWithSpecies();

            Assert.That(_creature.IsConfigured, Is.True);
            Assert.That(_creature.Species, Is.SameAs(_species));
            Assert.That(_creature.SpeciesId, Is.EqualTo("test/species/grazer"));
            Assert.That(_creature.State, Is.Not.Null);
            Assert.That(_creature.State.SpeciesId, Is.EqualTo("test/species/grazer"));
            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Idle));
            Assert.That(_creature.ConfigurationErrors, Is.Empty);
            Assert.That(_creature.enabled, Is.True);
        }

        [Test]
        public void TheSpeciesSlopeLimitIsAppliedToTheCharacterController()
        {
            ActivateWithSpecies();

            Assert.That(_characterController.slopeLimit, Is.EqualTo(33f),
                "Slope tolerance is species data, so it must come from the asset rather than the prefab.");
        }

        [Test]
        public void ACreatureWithoutASpeciesDoesNotRunAndSaysWhy()
        {
            _creatureObject.SetActive(true);

            Assert.That(_creature.IsConfigured, Is.False);
            Assert.That(_creature.State, Is.Null);
            Assert.That(_creature.enabled, Is.False);
            Assert.That(_creature.ConfigurationErrors, Has.Count.EqualTo(1));
            Assert.That(_creature.ConfigurationErrors[0], Does.Contain("No Species Definition is assigned"));
        }

        [Test]
        public void AContradictorySpeciesDoesNotRunAndSaysWhy()
        {
            SetFloat(_species, "_fleeSpeedMetresPerSecond", 0.5f);

            ActivateWithSpecies();

            Assert.That(_creature.IsConfigured, Is.False);
            Assert.That(_creature.enabled, Is.False);
            Assert.That(string.Join(" | ", _creature.ConfigurationErrors),
                Does.Contain("must be greater than Wander Speed"));
        }

        [Test]
        public void LegalButUnusualAuthoringIsReportedAsAWarningAndStillRuns()
        {
            SetString(_species, "_loreDescription", "");

            ActivateWithSpecies();

            Assert.That(_creature.IsConfigured, Is.True);
            Assert.That(_creature.ConfigurationErrors, Is.Empty);
            Assert.That(string.Join(" | ", _creature.ConfigurationWarnings), Does.Contain("Lore Description is empty"));
        }

        [Test]
        public void TickingBeforeConfigurationDoesNothing()
        {
            _creatureObject.SetActive(true);
            Assert.That(_creature.IsConfigured, Is.False);

            Assert.That(() => _creature.Tick(0.1f), Throws.Nothing);
            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Idle));
        }

        [Test]
        public void ASuddenDisturbanceInsideTheStartleRadiusStartsFlight()
        {
            ActivateWithSpecies();

            _creature.NotifySuddenDisturbance(_creatureObject.transform.position + new Vector3(10f, 0f, 0f));

            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Flee));
            Assert.That(_creature.State.DesiredSpeed, Is.EqualTo(_species.FleeSpeedMetresPerSecond));
        }

        [Test]
        public void ASuddenDisturbanceOutsideTheStartleRadiusChangesNothing()
        {
            ActivateWithSpecies();

            _creature.NotifySuddenDisturbance(_creatureObject.transform.position + new Vector3(20f, 0f, 0f));

            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Idle));
        }

        [Test]
        public void ADisturbanceIsIgnoredWhenTheCreatureIsNotConfigured()
        {
            _creatureObject.SetActive(true);

            Assert.That(() => _creature.NotifySuddenDisturbance(Vector3.zero), Throws.Nothing);
            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Idle));
        }

        [Test]
        public void ItNoticesAnActorThatMovesInsideTheNoticeRadius()
        {
            ActivateWithSpecies();
            SetPerceivedActors(_actorObject.transform);

            // The first sample only records where the actor was; speed needs two samples.
            _creature.Tick(0.2f);
            _actorObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 0f, 8f);
            Physics.SyncTransforms();
            _creature.Tick(0.2f);

            Assert.That(_creature.State.HasStimulus, Is.True);
            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Alert));
        }

        [Test]
        public void ItIgnoresAnActorThatStandsStill()
        {
            ActivateWithSpecies();
            SetPerceivedActors(_actorObject.transform);

            _creature.Tick(0.2f);
            _creature.Tick(0.2f);
            _creature.Tick(0.2f);

            Assert.That(_creature.State.HasStimulus, Is.False);
            Assert.That(_creature.Behavior, Is.Not.EqualTo(CreatureBehavior.Alert));
        }

        [Test]
        public void ItIgnoresAnActorBeyondTheNoticeRadius()
        {
            ActivateWithSpecies();
            _actorObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 0f, 20f);
            SetPerceivedActors(_actorObject.transform);

            _creature.Tick(0.2f);
            _actorObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 0f, 24f);
            Physics.SyncTransforms();
            _creature.Tick(0.2f);

            Assert.That(_creature.State.HasStimulus, Is.False);
        }

        [Test]
        public void TerrainBetweenTheCreatureAndTheActorHidesIt()
        {
            CreateOccluderBetweenCreatureAndActor();
            ActivateWithSpecies();
            SetPerceivedActors(_actorObject.transform);

            _creature.Tick(0.2f);
            _actorObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 0f, 8f);
            Physics.SyncTransforms();
            _creature.Tick(0.2f);

            Assert.That(_creature.State.HasStimulus, Is.False,
                "Cover must work: an actor behind solid terrain is not noticed.");
        }

        [Test]
        public void ASpeciesThatSensesThroughObstaclesIgnoresCover()
        {
            CreateOccluderBetweenCreatureAndActor();
            SetBool(_species, "_requiresLineOfSight", false);
            ActivateWithSpecies();
            SetPerceivedActors(_actorObject.transform);

            _creature.Tick(0.2f);
            _actorObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 0f, 8f);
            Physics.SyncTransforms();
            _creature.Tick(0.2f);

            Assert.That(_creature.State.HasStimulus, Is.True);
        }

        [Test]
        public void ItWandersAtTheSpeedTheSpeciesAuthors()
        {
            ActivateWithSpecies();

            // Idle maximum is 4 s, so after 4.2 s the creature has started a wander leg.
            _creature.Tick(4.2f);

            Assert.That(_creature.Behavior, Is.EqualTo(CreatureBehavior.Wander));
            Assert.That(_creature.State.DesiredSpeed, Is.EqualTo(_species.WanderSpeedMetresPerSecond));

            SetFloat(_species, "_wanderSpeedMetresPerSecond", 1.25f);
            Assert.That(_creature.State.DesiredSpeed, Is.EqualTo(1.25f),
                "The instance must read its speed from the species asset, not from a copy.");
        }

        [Test]
        public void ACreatureWithoutActorsToWatchNeverBecomesAlert()
        {
            ActivateWithSpecies();

            for (int i = 0; i < 10; i++)
            {
                _creature.Tick(0.2f);
            }

            Assert.That(_creature.State.HasStimulus, Is.False);
            Assert.That(_creature.Behavior, Is.Not.EqualTo(CreatureBehavior.Alert));
        }

        private void ActivateWithSpecies()
        {
            SerializedObject serialized = new SerializedObject(_creature);
            serialized.FindProperty("_species").objectReferenceValue = _species;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            _creatureObject.SetActive(true);
            Physics.SyncTransforms();
        }

        private void SetPerceivedActors(params Transform[] actors)
        {
            SerializedObject serialized = new SerializedObject(_creature);
            SerializedProperty array = serialized.FindProperty("_perceivedActors");
            array.arraySize = actors.Length;
            for (int i = 0; i < actors.Length; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = actors[i];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void CreateOccluderBetweenCreatureAndActor()
        {
            _occluderObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _occluderObject.name = "Perception Occluder Test";
            _occluderObject.transform.position = _creatureObject.transform.position + new Vector3(0f, 1f, 4f);
            _occluderObject.transform.localScale = new Vector3(6f, 4f, 1f);
            Physics.SyncTransforms();
        }

        private static void SetString(ScriptableObject target, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(ScriptableObject target, string propertyName, float value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(ScriptableObject target, string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(target);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
