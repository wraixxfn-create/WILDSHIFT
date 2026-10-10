using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wildshift.World;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies position queries, boundary and overlap rules, transform handling, and the rule that invalid
    /// setups make the locator unavailable with actionable messages.
    /// </summary>
    public sealed class WorldRegionLocatorTests
    {
        private const string NorthId = "nacre/dev/test-north";
        private const string SouthId = "nacre/dev/test-south";

        private WorldRegionTestObjects _objects;
        private RegionDefinition _north;
        private RegionDefinition _south;
        private WorldRegionCatalog _catalog;
        private bool _previousIgnoreFailingMessages;

        // Default layout: north covers x in [-5, 5), south covers x in [5, 15); both cover y in [-2, 2) and z in [-5, 5).
        // They share the face at x = 5, which must belong to south only.
        [SetUp]
        public void SetUp()
        {
            // Invalid setups intentionally log errors from the locator and catalog. Those messages are checked through the
            // validation results, so they are not unexpected test failures.
            _previousIgnoreFailingMessages = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;

            _objects = new WorldRegionTestObjects();
            _north = _objects.CreateDefinition(NorthId, "Test North");
            _south = _objects.CreateDefinition(SouthId, "Test South");
            _catalog = _objects.CreateCatalog(_north, _south);
        }

        [TearDown]
        public void TearDown()
        {
            _objects.DestroyAll();
            LogAssert.ignoreFailingMessages = _previousIgnoreFailingMessages;
        }

        [Test]
        public void PositionInsideOneRegionReturnsThatRegion()
        {
            WorldRegionLocator locator = CreateDefaultLocator();
            Assert.That(locator.IsAvailable, Is.True, string.Join("\n", locator.ValidationErrors));

            WorldRegionQueryResult result = locator.FindRegionAt(new Vector3(-2f, 0f, 1f));

            Assert.That(result.Status, Is.EqualTo(WorldRegionQueryStatus.Found));
            Assert.That(result.HasRegion, Is.True);
            Assert.That(result.RegionId, Is.EqualTo(NorthId));
            Assert.That(result.Definition, Is.SameAs(_north));
            Assert.That(result.OverlappingRegionCount, Is.EqualTo(1));
            Assert.That(result.IsOverlapping, Is.False);
        }

        [Test]
        public void PositionOutsideEveryRegionIsOutsideAllRegionsNotAnError()
        {
            WorldRegionLocator locator = CreateDefaultLocator();

            WorldRegionQueryResult result = locator.FindRegionAt(new Vector3(0f, 50f, 0f));

            Assert.That(result.Status, Is.EqualTo(WorldRegionQueryStatus.OutsideAllRegions));
            Assert.That(result.HasRegion, Is.False);
            Assert.That(result.Volume, Is.Null);
            Assert.That(result.RegionId, Is.Null);
            Assert.That(result.Definition, Is.Null);
            Assert.That(locator.IsAvailable, Is.True, "Being outside every region is a valid answer, not a setup problem.");
        }

        [Test]
        public void SharedFaceBelongsToExactlyOneRegionAndMinimumFacesAreInside()
        {
            WorldRegionLocator locator = CreateDefaultLocator();

            Assert.That(locator.FindRegionAt(new Vector3(4.999f, 0f, 0f)).RegionId, Is.EqualTo(NorthId));
            Assert.That(locator.FindRegionAt(new Vector3(5f, 0f, 0f)).RegionId, Is.EqualTo(SouthId),
                "The shared face at x = 5 is the minimum face of the south region.");
            Assert.That(locator.FindRegionAt(new Vector3(-5f, 0f, 0f)).RegionId, Is.EqualTo(NorthId),
                "The minimum face of the north region is inside it.");
            Assert.That(locator.FindRegionAt(new Vector3(-5.001f, 0f, 0f)).Status,
                Is.EqualTo(WorldRegionQueryStatus.OutsideAllRegions));
            Assert.That(locator.FindRegionAt(new Vector3(15f, 0f, 0f)).Status,
                Is.EqualTo(WorldRegionQueryStatus.OutsideAllRegions), "The maximum face of the south region is outside it.");
            Assert.That(locator.FindRegionAt(new Vector3(0f, 2f, 0f)).Status,
                Is.EqualTo(WorldRegionQueryStatus.OutsideAllRegions), "The maximum Y face is outside.");
        }

        [Test]
        public void OverlapResolvesToTheHigherPriorityRegionRegardlessOfListOrder()
        {
            WorldRegionLocator locator = CreateLocator(_catalog,
                (_north, Vector3.zero, Vector3.one * 10f, 0),
                (_south, Vector3.zero, Vector3.one * 4f, 5));
            Assert.That(locator.IsAvailable, Is.True, string.Join("\n", locator.ValidationErrors));

            WorldRegionQueryResult overlapped = locator.FindRegionAt(Vector3.zero);
            Assert.That(overlapped.RegionId, Is.EqualTo(SouthId), "Priority 5 beats priority 0.");
            Assert.That(overlapped.OverlappingRegionCount, Is.EqualTo(2));
            Assert.That(overlapped.IsOverlapping, Is.True);

            WorldRegionQueryResult outsideSmall = locator.FindRegionAt(new Vector3(4f, 0f, 0f));
            Assert.That(outsideSmall.RegionId, Is.EqualTo(NorthId), "Only the larger north volume contains this point.");
            Assert.That(outsideSmall.IsOverlapping, Is.False);

            WorldRegionLocator reversed = CreateLocator(_catalog,
                (_south, Vector3.zero, Vector3.one * 4f, 5),
                (_north, Vector3.zero, Vector3.one * 10f, 0));
            Assert.That(reversed.FindRegionAt(Vector3.zero).RegionId, Is.EqualTo(SouthId),
                "The result must not depend on the order of the Volumes list.");
        }

        [Test]
        public void EqualPriorityOverlapResolvesByLowerStableIdAndWarns()
        {
            WorldRegionLocator locator = CreateLocator(_catalog,
                (_south, Vector3.zero, Vector3.one * 10f, 0),
                (_north, Vector3.zero, Vector3.one * 10f, 0));

            Assert.That(locator.IsAvailable, Is.True, "An equal-priority overlap is a warning, not a blocking error.");
            Assert.That(locator.ValidationWarnings, Is.Not.Empty, "Equal-priority overlap between regions should be reported.");
            Assert.That(string.Join("\n", locator.ValidationWarnings), Does.Contain("overlap"));

            WorldRegionQueryResult result = locator.FindRegionAt(Vector3.zero);
            Assert.That(result.RegionId, Is.EqualTo(NorthId), "'nacre/dev/test-north' sorts before 'nacre/dev/test-south'.");
            Assert.That(result.OverlappingRegionCount, Is.EqualTo(2));
        }

        [Test]
        public void SeveralVolumesOfOneRegionCountAsOneRegion()
        {
            WorldRegionLocator locator = CreateLocator(_catalog,
                (_north, Vector3.zero, Vector3.one * 10f, 0),
                (_north, new Vector3(1f, 0f, 0f), Vector3.one * 10f, 0));

            WorldRegionQueryResult result = locator.FindRegionAt(new Vector3(0.5f, 0f, 0f));
            Assert.That(result.RegionId, Is.EqualTo(NorthId));
            Assert.That(result.OverlappingRegionCount, Is.EqualTo(1));
            Assert.That(result.IsOverlapping, Is.False);
            Assert.That(locator.ValidationWarnings, Is.Empty, "Volumes of the same region do not overlap in a meaningful way.");
        }

        [Test]
        public void DisabledVolumeIsIgnoredByQueries()
        {
            WorldRegionLocator locator = CreateDefaultLocator();
            WorldRegionVolume south = FindVolume(locator, _south);
            Assert.That(locator.FindRegionAt(new Vector3(10f, 0f, 0f)).RegionId, Is.EqualTo(SouthId));

            south.gameObject.SetActive(false);

            Assert.That(locator.FindRegionAt(new Vector3(10f, 0f, 0f)).Status,
                Is.EqualTo(WorldRegionQueryStatus.OutsideAllRegions));
            Assert.That(locator.FindRegionAt(new Vector3(0f, 0f, 0f)).RegionId, Is.EqualTo(NorthId),
                "Other volumes are unaffected.");
        }

        [Test]
        public void VolumeFollowsItsTransformPositionRotationAndScale()
        {
            WorldRegionLocator locator = CreateLocator(_catalog, (_north, Vector3.zero, Vector3.one, 0));
            WorldRegionVolume volume = FindVolume(locator, _north);

            // A unit box scaled to 10 has half-extents of 5 along its rotated axes, centred at (100, 0, 0).
            volume.transform.position = new Vector3(100f, 0f, 0f);
            volume.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
            volume.transform.localScale = Vector3.one * 10f;

            Assert.That(locator.FindRegionAt(new Vector3(106f, 0f, 0f)).HasRegion, Is.True,
                "Offset (6, 0, 0) is inside the rotated box: its projection on the rotated X axis is about 4.2.");
            Assert.That(locator.FindRegionAt(new Vector3(104.5f, 4.5f, 0f)).HasRegion, Is.False,
                "Offset (4.5, 4.5, 0) is outside the rotated box, although it is inside the unrotated one.");
            Assert.That(locator.FindRegionAt(Vector3.zero).HasRegion, Is.False, "The volume has moved away from the origin.");
        }

        [Test]
        public void LocatorResolvesStableIdsThroughItsCatalog()
        {
            WorldRegionLocator locator = CreateDefaultLocator();

            Assert.That(locator.TryGetDefinition(SouthId, out RegionDefinition south, out string error), Is.True, error);
            Assert.That(south, Is.SameAs(_south));
            Assert.That(locator.TryGetDefinition("nacre/dev/unknown", out RegionDefinition unknown, out string unknownError),
                Is.False);
            Assert.That(unknown, Is.Null);
            Assert.That(unknownError, Does.Contain("nacre/dev/unknown"));
        }

        [Test]
        public void MissingCatalogMakesEveryQueryUnavailable()
        {
            WorldRegionLocator locator = CreateLocator(null, (_north, Vector3.zero, Vector3.one, 0));

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(locator.FindRegionAt(Vector3.zero).Status, Is.EqualTo(WorldRegionQueryStatus.Unavailable),
                "Unavailable must not look like 'outside all regions'.");
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("No Region Catalog"));
            Assert.That(locator.TryGetDefinition(NorthId, out _, out string error), Is.False);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void VolumeWithoutDefinitionMakesTheLocatorUnavailable()
        {
            WorldRegionLocator locator = CreateLocator(_catalog, ((RegionDefinition)null, Vector3.zero, Vector3.one, 0));

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("no Region Definition"));
            Assert.That(locator.FindRegionAt(Vector3.zero).Status, Is.EqualTo(WorldRegionQueryStatus.Unavailable));
        }

        [Test]
        public void VolumeReferencingAnUncataloguedAssetWithTheSameIdIsRejected()
        {
            RegionDefinition imposter = _objects.CreateDefinition(NorthId, "Imposter with the same ID");
            WorldRegionLocator locator = CreateLocator(_catalog, (imposter, Vector3.zero, Vector3.one, 0));

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("not in the Region Catalog"),
                "Identity is the registered asset, not just the ID text.");
        }

        [Test]
        public void VolumeWithInvalidStableIdIsRejected()
        {
            RegionDefinition empty = _objects.CreateDefinition(string.Empty, "No ID yet");
            WorldRegionLocator locator = CreateLocator(_catalog, (empty, Vector3.zero, Vector3.one, 0));

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("invalid stable ID"));
        }

        [Test]
        public void DuplicateIdsInTheCatalogMakeTheLocatorUnavailable()
        {
            RegionDefinition copy = _objects.CreateDefinition(NorthId, "North copy");
            WorldRegionCatalog duplicateCatalog = _objects.CreateCatalog(_north, copy);
            List<string> catalogErrors = new List<string>();

            Assert.That(duplicateCatalog.TryCreateRegistry(catalogErrors, out WorldRegionRegistry registry), Is.False);
            Assert.That(registry, Is.Null);
            Assert.That(string.Join("\n", catalogErrors), Does.Contain("Region catalog '" + duplicateCatalog.name + "': ")
                .And.Contain("Duplicate stable ID"));

            WorldRegionLocator locator = CreateLocator(duplicateCatalog, (_north, Vector3.zero, Vector3.one, 0));
            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(locator.FindRegionAt(Vector3.zero).Status, Is.EqualTo(WorldRegionQueryStatus.Unavailable));
        }

        [Test]
        public void NullCatalogEntryMakesTheLocatorUnavailable()
        {
            WorldRegionCatalog catalog = _objects.CreateCatalog(_north, null);
            WorldRegionLocator locator = CreateLocator(catalog, (_north, Vector3.zero, Vector3.one, 0));

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("Entry 1 is empty"));
        }

        [Test]
        public void VolumeUnderTheLocatorButNotListedIsReportedInsteadOfIgnored()
        {
            GameObject root = _objects.CreateObject("Unlisted Volume Locator");
            root.SetActive(false);
            WorldRegionLocator locator = root.AddComponent<WorldRegionLocator>();
            WorldRegionTestObjects.SetLocatorCatalog(locator, _catalog);
            _objects.CreateVolume(root.transform, _north, Vector3.zero, Vector3.one, 0, "Forgotten Volume");
            WorldRegionTestObjects.SetLocatorVolumes(locator, new List<WorldRegionVolume>());
            root.SetActive(true);

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("not in its Volumes list"));
        }

        [Test]
        public void ZeroSizeVolumeIsRejectedAndNeverContainsPoints()
        {
            WorldRegionLocator locator = CreateLocator(_catalog, (_north, Vector3.zero, Vector3.zero, 0));

            Assert.That(locator.IsAvailable, Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("Local Size"));
        }

        [Test]
        public void VolumeListedTwiceIsRejected()
        {
            WorldRegionLocator locator = CreateDefaultLocator();
            WorldRegionVolume north = FindVolume(locator, _north);
            WorldRegionTestObjects.SetLocatorVolumes(locator, new List<WorldRegionVolume> { north, north });

            Assert.That(locator.Initialize(), Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("listed more than once"));
            Assert.That(locator.FindRegionAt(Vector3.zero).Status, Is.EqualTo(WorldRegionQueryStatus.Unavailable));
        }

        [Test]
        public void EmptyEntryInTheVolumesListIsIgnoredAndReportedAsAWarning()
        {
            WorldRegionLocator locator = CreateDefaultLocator();
            WorldRegionVolume north = FindVolume(locator, _north);
            WorldRegionVolume south = FindVolume(locator, _south);
            WorldRegionTestObjects.SetLocatorVolumes(locator, new List<WorldRegionVolume> { north, null, south });

            Assert.That(locator.Initialize(), Is.True, string.Join("\n", locator.ValidationErrors));
            Assert.That(locator.IsAvailable, Is.True, "A stale empty row must not switch region lookup off.");
            Assert.That(locator.ValidationErrors, Is.Empty);
            Assert.That(locator.ValidationWarnings, Has.Count.EqualTo(1));
            Assert.That(locator.ValidationWarnings[0],
                Does.Contain("Volumes entry 1 is empty").And.Contain("Element 1").And.Contain("Remove Empty Volume Entries"));
            Assert.That(locator.FindRegionAt(new Vector3(-2f, 0f, 0f)).RegionId, Is.EqualTo(NorthId));
            Assert.That(locator.FindRegionAt(new Vector3(12f, 0f, 0f)).RegionId, Is.EqualTo(SouthId));
        }

        [Test]
        public void EmptyEntryDoesNotHideAVolumeThatIsMissingFromTheList()
        {
            WorldRegionLocator locator = CreateDefaultLocator();
            WorldRegionVolume north = FindVolume(locator, _north);

            // The south volume still sits under the locator, but its list entry has become an empty row.
            WorldRegionTestObjects.SetLocatorVolumes(locator, new List<WorldRegionVolume> { north, null });

            Assert.That(locator.Initialize(), Is.False);
            Assert.That(string.Join("\n", locator.ValidationErrors), Does.Contain("not in its Volumes list"));
            Assert.That(string.Join("\n", locator.ValidationWarnings), Does.Contain("Volumes entry 1 is empty"));
            Assert.That(locator.FindRegionAt(Vector3.zero).Status, Is.EqualTo(WorldRegionQueryStatus.Unavailable));
        }

        [Test]
        public void RemoveEmptyVolumeEntriesDropsOnlyTheEmptyRowsAndKeepsTheOrder()
        {
            WorldRegionLocator locator = CreateDefaultLocator();
            WorldRegionVolume north = FindVolume(locator, _north);
            WorldRegionVolume south = FindVolume(locator, _south);
            WorldRegionTestObjects.SetLocatorVolumes(locator,
                new List<WorldRegionVolume> { null, south, null, north, null });
            Assert.That(locator.Initialize(), Is.True, string.Join("\n", locator.ValidationErrors));
            Assert.That(locator.ValidationWarnings, Has.Count.EqualTo(3));

            Assert.That(locator.RemoveEmptyVolumeEntries(), Is.EqualTo(3));

            List<WorldRegionVolume> remaining = WorldRegionTestObjects.GetLocatorVolumes(locator);
            Assert.That(remaining, Has.Count.EqualTo(2));
            Assert.That(remaining[0], Is.SameAs(south));
            Assert.That(remaining[1], Is.SameAs(north));
            Assert.That(locator.Initialize(), Is.True, string.Join("\n", locator.ValidationErrors));
            Assert.That(locator.ValidationWarnings, Is.Empty);
            Assert.That(locator.FindRegionAt(new Vector3(-2f, 0f, 0f)).RegionId, Is.EqualTo(NorthId));
            Assert.That(locator.RemoveEmptyVolumeEntries(), Is.EqualTo(0), "A second call has nothing left to remove.");
        }

        [Test]
        public void ValidSetupInitializesWithoutErrorsAndCanBeRevalidated()
        {
            WorldRegionLocator locator = CreateDefaultLocator();

            Assert.That(locator.Initialize(), Is.True, string.Join("\n", locator.ValidationErrors));
            Assert.That(locator.ValidationErrors, Is.Empty);
            Assert.That(locator.ValidationWarnings, Is.Empty);
            Assert.That(locator.FindRegionAt(new Vector3(-2f, 0f, 0f)).RegionId, Is.EqualTo(NorthId));
        }

        private WorldRegionLocator CreateDefaultLocator()
        {
            return CreateLocator(_catalog,
                (_north, Vector3.zero, new Vector3(10f, 4f, 10f), 0),
                (_south, new Vector3(10f, 0f, 0f), new Vector3(10f, 4f, 10f), 0));
        }

        /// <summary>
        /// Builds a locator with one child volume per entry, registers those volumes in its list, and activates it so
        /// Awake validates the setup, as it does in a running scene.
        /// </summary>
        private WorldRegionLocator CreateLocator(WorldRegionCatalog catalog,
            params (RegionDefinition Definition, Vector3 Center, Vector3 Size, int Priority)[] volumes)
        {
            GameObject root = _objects.CreateObject("Region Locator Test");
            root.SetActive(false);
            WorldRegionLocator locator = root.AddComponent<WorldRegionLocator>();
            WorldRegionTestObjects.SetLocatorCatalog(locator, catalog);

            List<WorldRegionVolume> created = new List<WorldRegionVolume>();
            for (int i = 0; i < volumes.Length; i++)
            {
                created.Add(_objects.CreateVolume(root.transform, volumes[i].Definition, volumes[i].Center,
                    volumes[i].Size, volumes[i].Priority, "Volume " + i));
            }

            WorldRegionTestObjects.SetLocatorVolumes(locator, created);
            root.SetActive(true);
            return locator;
        }

        private static WorldRegionVolume FindVolume(WorldRegionLocator locator, RegionDefinition definition)
        {
            foreach (WorldRegionVolume volume in locator.GetComponentsInChildren<WorldRegionVolume>(true))
            {
                if (volume.Definition == definition)
                {
                    return volume;
                }
            }

            Assert.Fail("No volume for " + definition.name + " was found under the locator.");
            return null;
        }
    }
}
