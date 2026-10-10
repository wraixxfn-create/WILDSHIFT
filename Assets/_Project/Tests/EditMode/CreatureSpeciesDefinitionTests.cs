using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.Ecology.Creatures;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies that a species definition exposes its authored values and that the shared validator
    /// accepts a coherent species while reporting every unusable or contradictory setting with an
    /// actionable message.
    /// </summary>
    public sealed class CreatureSpeciesDefinitionTests
    {
        private CreatureSpeciesDefinition _species;
        private List<string> _errors;
        private List<string> _warnings;

        [SetUp]
        public void SetUp()
        {
            _species = ScriptableObject.CreateInstance<CreatureSpeciesDefinition>();
            _errors = new List<string>();
            _warnings = new List<string>();

            // A coherent baseline, mirroring the authored Nacre Siltveil Grazer asset.
            SetString("_stableId", "nacre/species/test-grazer");
            SetString("_displayName", "Test Grazer");
            SetString("_loreDescription", "A low grazer used by tests. It notices movement and bolts.");
            SetEnum("_sizeClass", (int)CreatureSizeClass.Small);
            SetFloat("_wanderSpeedMetresPerSecond", 0.7f);
            SetFloat("_fleeSpeedMetresPerSecond", 3.4f);
            SetFloat("_accelerationMetresPerSecondSquared", 9f);
            SetFloat("_turnRateDegreesPerSecond", 260f);
            SetFloat("_maximumSlopeDegrees", 40f);
            SetFloat("_wanderRadiusMetres", 4f);
            SetFloat("_noticeRadiusMetres", 9f);
            SetFloat("_fleeRadiusMetres", 3.5f);
            SetFloat("_startleRadiusMetres", 12f);
            SetFloat("_minimumActorSpeedMetresPerSecond", 0.6f);
            SetFloat("_perceptionHeightOffsetMetres", 0.3f);
            SetBool("_requiresLineOfSight", true);
            SetFloat("_perceptionIntervalSeconds", 0.15f);
            SetFloat("_idleMinSeconds", 2.5f);
            SetFloat("_idleMaxSeconds", 7f);
            SetFloat("_alertHoldSeconds", 1.2f);
            SetFloat("_calmDownSeconds", 3.5f);
            SetFloat("_fleeDurationSeconds", 1.6f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_species);
        }

        [Test]
        public void ExposesEveryAuthoredValue()
        {
            Assert.That(_species.StableId, Is.EqualTo("nacre/species/test-grazer"));
            Assert.That(_species.DisplayName, Is.EqualTo("Test Grazer"));
            Assert.That(_species.LoreDescription, Does.StartWith("A low grazer"));
            Assert.That(_species.SizeClass, Is.EqualTo(CreatureSizeClass.Small));
            Assert.That(_species.WanderSpeedMetresPerSecond, Is.EqualTo(0.7f));
            Assert.That(_species.FleeSpeedMetresPerSecond, Is.EqualTo(3.4f));
            Assert.That(_species.AccelerationMetresPerSecondSquared, Is.EqualTo(9f));
            Assert.That(_species.TurnRateDegreesPerSecond, Is.EqualTo(260f));
            Assert.That(_species.MaximumSlopeDegrees, Is.EqualTo(40f));
            Assert.That(_species.WanderRadiusMetres, Is.EqualTo(4f));
            Assert.That(_species.NoticeRadiusMetres, Is.EqualTo(9f));
            Assert.That(_species.FleeRadiusMetres, Is.EqualTo(3.5f));
            Assert.That(_species.StartleRadiusMetres, Is.EqualTo(12f));
            Assert.That(_species.MinimumActorSpeedMetresPerSecond, Is.EqualTo(0.6f));
            Assert.That(_species.PerceptionHeightOffsetMetres, Is.EqualTo(0.3f));
            Assert.That(_species.RequiresLineOfSight, Is.True);
            Assert.That(_species.PerceptionIntervalSeconds, Is.EqualTo(0.15f));
            Assert.That(_species.IdleMinSeconds, Is.EqualTo(2.5f));
            Assert.That(_species.IdleMaxSeconds, Is.EqualTo(7f));
            Assert.That(_species.AlertHoldSeconds, Is.EqualTo(1.2f));
            Assert.That(_species.CalmDownSeconds, Is.EqualTo(3.5f));
            Assert.That(_species.FleeDurationSeconds, Is.EqualTo(1.6f));
        }

        [Test]
        public void ACoherentSpeciesPassesWithNoErrorsAndNoWarnings()
        {
            bool valid = CreatureSpeciesValidator.Validate(_species, _errors, _warnings);

            Assert.That(valid, Is.True, string.Join(" | ", _errors));
            Assert.That(_errors, Is.Empty);
            Assert.That(_warnings, Is.Empty);
        }

        [Test]
        public void AMissingSpeciesIsAnError()
        {
            bool valid = CreatureSpeciesValidator.Validate(null, _errors, _warnings);

            Assert.That(valid, Is.False);
            Assert.That(_errors, Has.Count.EqualTo(1));
            Assert.That(_errors[0], Does.Contain("No Species Definition is assigned"));
        }

        [TestCase("", "species stable ID is empty")]
        [TestCase("   ", "species stable ID is empty")]
        [TestCase("nacre/species/test grazer", "whitespace/control")]
        [TestCase("nacre/species/test\tgrazer", "whitespace/control")]
        public void AnUnusableStableIdIsAnError(string stableId, string expectedMessagePart)
        {
            SetString("_stableId", stableId);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain(expectedMessagePart));
        }

        [TestCase("", "Display Name is empty")]
        [TestCase("  ", "Display Name is empty")]
        public void ABlankDisplayNameIsAnError(string displayName, string expectedMessagePart)
        {
            SetString("_displayName", displayName);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain(expectedMessagePart));
        }

        [TestCase("_wanderSpeedMetresPerSecond", "Wander Speed")]
        [TestCase("_fleeSpeedMetresPerSecond", "Flee Speed")]
        [TestCase("_accelerationMetresPerSecondSquared", "Acceleration")]
        [TestCase("_turnRateDegreesPerSecond", "Turn Rate")]
        [TestCase("_wanderRadiusMetres", "Wander Radius")]
        [TestCase("_noticeRadiusMetres", "Notice Radius")]
        [TestCase("_fleeRadiusMetres", "Flee Radius")]
        [TestCase("_startleRadiusMetres", "Startle Radius")]
        [TestCase("_perceptionIntervalSeconds", "Perception Interval")]
        [TestCase("_idleMinSeconds", "Idle Min Seconds")]
        [TestCase("_calmDownSeconds", "Calm Down Seconds")]
        [TestCase("_fleeDurationSeconds", "Flee Duration Seconds")]
        public void ANonPositiveRequiredValueIsAnError(string propertyName, string label)
        {
            SetFloat(propertyName, 0f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain(label + " must be a finite number greater than zero"));
        }

        [TestCase("_minimumActorSpeedMetresPerSecond", "Minimum Actor Speed")]
        [TestCase("_perceptionHeightOffsetMetres", "Perception Height Offset")]
        public void ANegativeOptionalValueIsAnError(string propertyName, string label)
        {
            SetFloat(propertyName, -1f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain(label + " must be a finite number of zero or more"));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ANonFiniteValueIsAnError(float value)
        {
            SetFloat("_noticeRadiusMetres", value);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain("Notice Radius must be a finite number"));
        }

        [TestCase(90f)]
        [TestCase(120f)]
        public void ASlopeLimitAtOrAboveNinetyDegreesIsAnError(float slopeDegrees)
        {
            SetFloat("_maximumSlopeDegrees", slopeDegrees);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain("Maximum Slope Degrees must stay below 90"));
        }

        [Test]
        public void AFleeSpeedThatDoesNotBeatTheWanderSpeedIsAContradiction()
        {
            SetFloat("_wanderSpeedMetresPerSecond", 3.4f);
            SetFloat("_fleeSpeedMetresPerSecond", 3.4f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain("Flee Speed"));
            Assert.That(string.Join(" | ", _errors), Does.Contain("must be greater than Wander Speed"));
        }

        [Test]
        public void AFleeRadiusBeyondTheNoticeRadiusIsAContradiction()
        {
            SetFloat("_noticeRadiusMetres", 3f);
            SetFloat("_fleeRadiusMetres", 3.5f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain("must not be greater than Notice Radius"));
        }

        [Test]
        public void AnIdleMaximumBelowTheIdleMinimumIsAContradiction()
        {
            SetFloat("_idleMinSeconds", 7f);
            SetFloat("_idleMaxSeconds", 2.5f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(string.Join(" | ", _errors), Does.Contain("must not be less than Idle Min Seconds"));
        }

        [Test]
        public void EqualIdleBoundsAreLegalSoASpeciesMayGrazeForAFixedTime()
        {
            SetFloat("_idleMinSeconds", 4f);
            SetFloat("_idleMaxSeconds", 4f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.True);
            Assert.That(_errors, Is.Empty);
        }

        [Test]
        public void MissingLoreIsAWarningNotAnError()
        {
            SetString("_loreDescription", "");

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.True);
            Assert.That(_errors, Is.Empty);
            Assert.That(string.Join(" | ", _warnings), Does.Contain("Lore Description is empty"));
        }

        [Test]
        public void AStartleRadiusInsideTheFleeRadiusIsAWarning()
        {
            SetFloat("_startleRadiusMetres", 2f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.True);
            Assert.That(string.Join(" | ", _warnings), Does.Contain("Startle Radius"));
        }

        [Test]
        public void ASlowPerceptionIntervalIsAWarning()
        {
            SetFloat("_perceptionIntervalSeconds", 0.8f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.True);
            Assert.That(string.Join(" | ", _warnings), Does.Contain("Perception Interval"));
        }

        [Test]
        public void ValidationAppendsToExistingMessagesWithoutClearingThem()
        {
            _errors.Add("pre-existing");
            SetFloat("_wanderRadiusMetres", -1f);

            Assert.That(CreatureSpeciesValidator.Validate(_species, _errors, _warnings), Is.False);
            Assert.That(_errors[0], Is.EqualTo("pre-existing"));
            Assert.That(_errors, Has.Count.EqualTo(2));
        }

        [Test]
        public void ValidationRequiresBothMessageLists()
        {
            Assert.That(() => CreatureSpeciesValidator.Validate(_species, null, _warnings),
                Throws.ArgumentNullException);
            Assert.That(() => CreatureSpeciesValidator.Validate(_species, _errors, null),
                Throws.ArgumentNullException);
        }

        [Test]
        public void TheShippedNacreSpeciesAssetIsValid()
        {
            const string assetPath = "Assets/_Project/Data/Ecology/Species/NacreSiltveilGrazer.asset";
            CreatureSpeciesDefinition asset = AssetDatabase.LoadAssetAtPath<CreatureSpeciesDefinition>(assetPath);
            Assert.That(asset, Is.Not.Null, $"Could not load CreatureSpeciesDefinition at {assetPath}.");

            Assert.That(CreatureSpeciesValidator.Validate(asset, _errors, _warnings), Is.True,
                string.Join(" | ", _errors));
            Assert.That(_errors, Is.Empty);
            Assert.That(_warnings, Is.Empty);
            Assert.That(asset.StableId, Is.EqualTo("nacre/species/siltveil-grazer"));
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

        private void SetBool(string propertyName, bool value)
        {
            SerializedObject serialized = new SerializedObject(_species);
            serialized.FindProperty(propertyName).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private void SetEnum(string propertyName, int value)
        {
            SerializedObject serialized = new SerializedObject(_species);
            serialized.FindProperty(propertyName).enumValueIndex = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
