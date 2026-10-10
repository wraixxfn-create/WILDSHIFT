using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Environment.Samples;
using Wildshift.Player.Regions;
using Wildshift.World;
using Wildshift.World.Events;
using Wildshift.World.Regions;

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
        private WorldRegionTestObjects _regionObjects;
        private PlayerRegionAssociation _regionAssociation;
        private WorldRegionLocator _regionLocator;

        [SetUp]
        public void SetUp()
        {
            _definition = ScriptableObject.CreateInstance<EnvironmentalSampleDefinition>();
            SetDefinitionField(_definition, "_stableId", "test/sample/soil-a");
            SetDefinitionField(_definition, "_displayName", "Test soil scraping");
            SetDefinitionField(_definition, "_interactPrompt", "Press E to collect sample");
            SetDefinitionField(_definition, "_collectedDescription", "A faint pearlescent scraping.");

            _regionObjects = new WorldRegionTestObjects();
            RegionDefinition west = _regionObjects.CreateDefinition("nacre/test/sample-west", "Sample West");
            RegionDefinition east = _regionObjects.CreateDefinition("nacre/test/sample-east", "Sample East");
            WorldRegionCatalog catalog = _regionObjects.CreateCatalog(west, east);
            GameObject locatorRoot = _regionObjects.CreateObject("Sample Region Locator");
            locatorRoot.SetActive(false);
            _regionLocator = locatorRoot.AddComponent<WorldRegionLocator>();
            WorldRegionTestObjects.SetLocatorCatalog(_regionLocator, catalog);
            WorldRegionVolume westVolume = _regionObjects.CreateVolume(
                locatorRoot.transform, west, new Vector3(-5f, 0f, 0f), new Vector3(10f, 10f, 10f), 0);
            WorldRegionVolume eastVolume = _regionObjects.CreateVolume(
                locatorRoot.transform, east, new Vector3(5f, 0f, 0f), new Vector3(10f, 10f, 10f), 0);
            WorldRegionTestObjects.SetLocatorVolumes(_regionLocator, new[] { westVolume, eastVolume });
            locatorRoot.SetActive(true);

            _interactor = new GameObject("Sample Interactor Test");
            _interactor.SetActive(false);
            _interactor.transform.position = new Vector3(-5f, 0f, 0f);
            _regionAssociation = _interactor.AddComponent<PlayerRegionAssociation>();
            SetObjectReference(_regionAssociation, "_regionLocator", _regionLocator);
            _interactor.SetActive(true);

            // The target sits in east while the player starts in west. Events must follow the player,
            // proving that target position and display names are not used as region identity.
            _sampleObject = new GameObject("Sample Target Test");
            _sampleObject.SetActive(false);
            _sampleObject.transform.position = new Vector3(5f, 0f, 0f);
            _sampleObject.AddComponent<BoxCollider>();
            _interactable = _sampleObject.AddComponent<EnvironmentalSampleInteractable>();
            SetObjectReference(_interactable, "_definition", _definition);

            _hostObject = new GameObject("Event Recorder Host Test");
            _host = _hostObject.AddComponent<PlayerActionEventRecorderHost>();
            SetObjectReference(_interactable, "_eventRecorderHost", _host);
            _sampleObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_sampleObject);
            Object.DestroyImmediate(_interactor);
            Object.DestroyImmediate(_hostObject);
            Object.DestroyImmediate(_definition);
            _regionObjects.DestroyAll();
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
            Assert.That(evt.RegionId, Is.EqualTo("nacre/test/sample-west"),
                "Collection region follows the player, not the sample target in east.");
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
        public void CollectionUsesPlayersRegionAtSuccessfulCommitTime()
        {
            // The association was initialized in west. Move directly to east without manually refreshing,
            // modelling an interaction that began before the boundary crossing and completed afterwards.
            Assert.That(_regionAssociation.CurrentRegionId, Is.EqualTo("nacre/test/sample-west"));
            _interactor.transform.position = new Vector3(5f, 0f, 0f);

            _interactable.Interact(_interactor);

            PlayerActionEvent evt = _host.Recorder.GetRecentEvents(1)[0];
            Assert.That(evt.RegionId, Is.EqualTo("nacre/test/sample-east"));
            Assert.That(evt.RegionId, Is.Not.EqualTo("Sample East"), "Display names are never event identifiers.");
        }

        [Test]
        public void CollectionOutsideRegisteredRegionsRecordsNoRegionId()
        {
            _interactor.transform.position = new Vector3(20f, 0f, 0f);
            LogAssert.Expect(LogType.Warning, new Regex("player is outside all registered region volumes"));

            _interactable.Interact(_interactor);

            PlayerActionEvent evt = _host.Recorder.GetRecentEvents(1)[0];
            Assert.That(evt.HasRegionId, Is.False);
            Assert.That(evt.RegionId, Is.Null, "Outside actions must not receive a fabricated region ID.");
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
