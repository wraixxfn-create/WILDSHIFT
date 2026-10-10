using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.Player.Regions;
using Wildshift.World;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies player association transitions, completion-time action lookup, outside-region
    /// behaviour, and delegation to the locator's existing half-open boundary rules.
    /// </summary>
    public sealed class PlayerRegionAssociationTests
    {
        private const string WestId = "nacre/test/west";
        private const string EastId = "nacre/test/east";

        private WorldRegionTestObjects _objects;
        private GameObject _playerObject;
        private PlayerRegionAssociation _association;

        // West covers x [-10, 0); east covers x [0, 10). The shared x=0 face belongs to east.
        [SetUp]
        public void SetUp()
        {
            _objects = new WorldRegionTestObjects();
            RegionDefinition west = _objects.CreateDefinition(WestId, "West Display Name");
            RegionDefinition east = _objects.CreateDefinition(EastId, "East Display Name");
            WorldRegionCatalog catalog = _objects.CreateCatalog(west, east);

            GameObject locatorRoot = _objects.CreateObject("Player Region Test Locator");
            locatorRoot.SetActive(false);
            WorldRegionLocator locator = locatorRoot.AddComponent<WorldRegionLocator>();
            WorldRegionTestObjects.SetLocatorCatalog(locator, catalog);
            WorldRegionVolume westVolume = _objects.CreateVolume(
                locatorRoot.transform, west, new Vector3(-5f, 0f, 0f), new Vector3(10f, 10f, 10f), 0, "West Volume");
            WorldRegionVolume eastVolume = _objects.CreateVolume(
                locatorRoot.transform, east, new Vector3(5f, 0f, 0f), new Vector3(10f, 10f, 10f), 0, "East Volume");
            WorldRegionTestObjects.SetLocatorVolumes(locator, new[] { westVolume, eastVolume });
            locatorRoot.SetActive(true);
            Assert.That(locator.IsAvailable, Is.True, string.Join("\n", locator.ValidationErrors));

            _playerObject = _objects.CreateObject("Player Region Test Actor");
            _playerObject.SetActive(false);
            _playerObject.transform.position = new Vector3(-5f, 0f, 0f);
            _association = _playerObject.AddComponent<PlayerRegionAssociation>();
            SetObjectReference(_association, "_regionLocator", locator);
            _playerObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            _objects.DestroyAll();
        }

        [Test]
        public void InitialPositionResolvesToStableIdNotDisplayName()
        {
            Assert.That(_association.CurrentRegionStatus, Is.EqualTo(WorldRegionQueryStatus.Found));
            Assert.That(_association.CurrentRegionId, Is.EqualTo(WestId));
            Assert.That(_association.CurrentRegionId, Is.Not.EqualTo("West Display Name"));
            Assert.That(_association.HasRegion, Is.True);
        }

        [Test]
        public void RemainingInSameRegionDoesNotPublishRepeatedUpdates()
        {
            int transitions = 0;
            _association.RegionAssociationChanged += _ => transitions++;

            Assert.That(_association.RefreshRegionAssociation(), Is.False);
            _playerObject.transform.position = new Vector3(-4f, 1f, 2f);
            Assert.That(_association.RefreshRegionAssociation(), Is.False);
            Assert.That(_association.RefreshRegionAssociation(), Is.False);

            Assert.That(transitions, Is.EqualTo(0));
            Assert.That(_association.CurrentRegionId, Is.EqualTo(WestId));
        }

        [Test]
        public void MovingBetweenRegionsAndOutsidePublishesOneTransitionPerAssociationChange()
        {
            List<PlayerRegionAssociationChange> changes = new List<PlayerRegionAssociationChange>();
            _association.RegionAssociationChanged += changes.Add;

            _playerObject.transform.position = new Vector3(5f, 0f, 0f);
            Assert.That(_association.RefreshRegionAssociation(), Is.True);
            Assert.That(_association.RefreshRegionAssociation(), Is.False,
                "An unchanged position must not publish the same region again.");

            _playerObject.transform.position = new Vector3(20f, 0f, 0f);
            Assert.That(_association.RefreshRegionAssociation(), Is.True);
            _playerObject.transform.position = new Vector3(25f, 0f, 0f);
            Assert.That(_association.RefreshRegionAssociation(), Is.False,
                "Moving while still outside must not publish a repeated outside association.");

            Assert.That(changes.Count, Is.EqualTo(2));
            Assert.That(changes[0].PreviousRegionId, Is.EqualTo(WestId));
            Assert.That(changes[0].CurrentRegionId, Is.EqualTo(EastId));
            Assert.That(changes[0].MovedBetweenRegions, Is.True);
            Assert.That(changes[1].PreviousRegionId, Is.EqualTo(EastId));
            Assert.That(changes[1].CurrentRegionId, Is.Null);
            Assert.That(changes[1].CurrentStatus, Is.EqualTo(WorldRegionQueryStatus.OutsideAllRegions));
            Assert.That(changes[1].LeftRegion, Is.True);
        }

        [Test]
        public void SharedBoundaryUsesTheLocatorsExistingHalfOpenRule()
        {
            _playerObject.transform.position = Vector3.zero;

            _association.RefreshRegionAssociation();

            Assert.That(_association.CurrentRegionId, Is.EqualTo(EastId),
                "The association must delegate x=0 to the locator, whose maximum face is excluded and minimum face included.");
        }

        [Test]
        public void ActionResolutionRefreshesAtCommitTimeAfterPlayerMoves()
        {
            // The cached association still says west. Move without a normal refresh to model an
            // interaction that began in west and completed after crossing into east.
            Assert.That(_association.CurrentRegionId, Is.EqualTo(WestId));
            _playerObject.transform.position = new Vector3(5f, 0f, 0f);

            bool found = _association.TryResolveRegionForAction(out string regionId);

            Assert.That(found, Is.True);
            Assert.That(regionId, Is.EqualTo(EastId));
            Assert.That(_association.CurrentRegionId, Is.EqualTo(EastId));
        }

        [Test]
        public void ActionsOutsideAllRegionsReturnNullAndWarnOnlyOncePerOutsideStay()
        {
            _playerObject.transform.position = new Vector3(20f, 0f, 0f);
            _association.RefreshRegionAssociation();
            LogAssert.Expect(LogType.Warning, new Regex("player is outside all registered region volumes"));

            Assert.That(_association.TryResolveRegionForAction(out string firstRegionId), Is.False);
            Assert.That(firstRegionId, Is.Null);
            Assert.That(_association.TryResolveRegionForAction(out string secondRegionId), Is.False);
            Assert.That(secondRegionId, Is.Null);
        }

        [Test]
        public void MissingLocatorIsDiagnosedOnceAndNeverCreatesARegionId()
        {
            GameObject unconfiguredPlayer = new GameObject("Unconfigured Player Region Test");
            unconfiguredPlayer.SetActive(false);
            PlayerRegionAssociation unconfigured = unconfiguredPlayer.AddComponent<PlayerRegionAssociation>();
            LogAssert.Expect(LogType.Error, new Regex("has no World Region Locator assigned"));

            unconfiguredPlayer.SetActive(true);
            Assert.That(unconfigured.CurrentRegionStatus, Is.EqualTo(WorldRegionQueryStatus.Unavailable));
            Assert.That(unconfigured.CurrentRegionId, Is.Null);
            Assert.That(unconfigured.RefreshRegionAssociation(), Is.False,
                "Repeated refreshes must not repeat the same missing association or its error log.");

            Object.DestroyImmediate(unconfiguredPlayer);
        }

        private static void SetObjectReference(Object component, string propertyName, Object value)
        {
            SerializedObject serialized = new SerializedObject(component);
            serialized.FindProperty(propertyName).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
