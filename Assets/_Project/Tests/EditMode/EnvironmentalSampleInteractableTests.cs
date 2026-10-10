using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.Environment.Samples;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies single-session collection semantics, idempotency, event recording, and
    /// the prompt/description authored-data separation without relying on scene hierarchy names.
    /// </summary>
    public sealed class EnvironmentalSampleInteractableTests
    {
        private GameObject _interactor;
        private GameObject _sampleObject;
        private EnvironmentalSampleInteractable _interactable;
        private EnvironmentalSampleDefinition _definition;
        private PlayerActionEventRecorderHost _host;
        private GameObject _hostObject;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnvironmentalSampleDefinition>();
            SetDefinitionField(_definition, "_stableId", "test/sample/soil-a");
            SetDefinitionField(_definition, "_displayName", "Test soil scraping");
            SetDefinitionField(_definition, "_interactPrompt", "Press E to collect sample");
            SetDefinitionField(_definition, "_collectedDescription", "A faint pearlescent scraping.");

            _interactor = new GameObject("Sample Interactor Test");

            _sampleObject = new GameObject("Sample Target Test");
            _sampleObject.transform.position = new Vector3(100f, 0f, 100f);
            _sampleObject.AddComponent<BoxCollider>();
            _interactable = _sampleObject.AddComponent<EnvironmentalSampleInteractable>();
            SetObjectReference(_interactable, "_definition", _definition);

            _hostObject = new GameObject("Event Recorder Host Test");
            _host = _hostObject.AddComponent<PlayerActionEventRecorderHost>();
            SetObjectReference(_interactable, "_eventRecorderHost", _host);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sampleObject);
            Object.DestroyImmediate(_interactor);
            Object.DestroyImmediate(_hostObject);
            Object.DestroyImmediate(_definition);
        }

        [Test]
        public void CanInteractIsTrueBeforeCollection()
        {
            Assert.That(_interactable.CanInteract(_interactor), Is.True);
            Assert.That(_interactable.IsCollected, Is.False);
        }

        [Test]
        public void PromptComesFromTheDefinition()
        {
            Assert.That(_interactable.GetInteractionPrompt(), Is.EqualTo("Press E to collect sample"));
        }

        [Test]
        public void InteractMarksTheSampleCollectedForTheSession()
        {
            _interactable.Interact(_interactor);

            Assert.That(_interactable.IsCollected, Is.True);
            Assert.That(_interactable.CanInteract(_interactor), Is.False,
                "A collected sample must reject further interaction.");
        }

        [Test]
        public void InteractIsIdempotent_DuplicateCallsDoNotRecordDuplicateEvents()
        {
            _interactable.Interact(_interactor);
            _interactable.Interact(_interactor);
            _interactable.Interact(_interactor);

            IReadOnlyList<PlayerActionEvent> events = _host.Recorder.GetRecentEvents(10);
            Assert.That(events.Count, Is.EqualTo(1),
                "Only one collection event must be recorded for a single session.");

            PlayerActionEvent evt = events[0];
            Assert.That(evt.EventType, Is.EqualTo(PlayerActionEventType.ResourceExtraction));
            Assert.That(evt.TargetId, Is.EqualTo("test/sample/soil-a"));
            Assert.That(evt.HasMagnitude, Is.True);
            Assert.That(evt.Magnitude, Is.EqualTo(1f));
            bool collectedParamFound = false;
            foreach (PlayerActionEventParameter p in evt.Parameters)
            {
                if (p.Id == EnvironmentalSampleInteractable.CollectedParameterId && p.Value == 1f)
                {
                    collectedParamFound = true;
                }
            }

            Assert.That(collectedParamFound, Is.True,
                "Collection event must carry a sample-collected=1 parameter.");
        }

        [Test]
        public void PromptReturnsNullAfterCollection()
        {
            _interactable.Interact(_interactor);
            Assert.That(_interactable.GetInteractionPrompt(), Is.Null,
                "A spent sample must not offer a prompt to aim at.");
        }

        [Test]
        public void CanInteractRejectsANullInteractorOrDisabledComponent()
        {
            Assert.That(_interactable.CanInteract(null), Is.False);
            _interactable.enabled = false;
            Assert.That(_interactable.CanInteract(_interactor), Is.False);
        }

        [Test]
        public void CanInteractIsFalseWhenDefinitionHasNoStableId()
        {
            SetDefinitionField(_definition, "_stableId", "   ");
            Assert.That(_interactable.CanInteract(_interactor), Is.False,
                "Samples without a stable authored ID must not be collectable.");
        }

        private static void SetObjectReference(Object component, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetDefinitionField(ScriptableObject definition, string propertyName, string value)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty(propertyName).stringValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
