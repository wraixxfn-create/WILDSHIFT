using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wildshift.World;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>Verifies stable-ID lookup, authoring validation, and the dev test catalog asset.</summary>
    public sealed class WorldRegionRegistryTests
    {
        private const string DevCatalogPath = "Assets/_Project/Data/WorldRegions/DevWorldRegionCatalog.asset";

        private WorldRegionTestObjects _objects;

        [SetUp]
        public void SetUp()
        {
            _objects = new WorldRegionTestObjects();
        }

        [TearDown]
        public void TearDown()
        {
            _objects.DestroyAll();
        }

        [Test]
        public void ResolvesDefinitionsByStableIdIndependentOfAssetNameDisplayNameAndListOrder()
        {
            RegionDefinition north = _objects.CreateDefinition("nacre/check/north", "Display label A", "AssetNameOne");
            RegionDefinition south = _objects.CreateDefinition("nacre/check/south", "Display label B", "AssetNameTwo");
            List<string> errors = new List<string>();

            bool created = WorldRegionRegistry.TryCreate(new List<RegionDefinition> { south, north }, errors,
                out WorldRegionRegistry registry);

            Assert.That(created, Is.True, string.Join("; ", errors));
            Assert.That(registry.Count, Is.EqualTo(2));
            Assert.That(registry.TryGetDefinition("nacre/check/north", out RegionDefinition foundNorth, out string northError),
                Is.True, northError);
            Assert.That(foundNorth, Is.SameAs(north));
            Assert.That(registry.TryGetDefinition("nacre/check/south", out RegionDefinition foundSouth, out string southError),
                Is.True, southError);
            Assert.That(foundSouth, Is.SameAs(south));

            IReadOnlyList<RegionDefinition> ordered = registry.GetAllDefinitions();
            Assert.That(ordered[0], Is.SameAs(north), "Snapshot is ordered by stable ID, not by list order.");
            Assert.That(ordered[1], Is.SameAs(south));
        }

        [Test]
        public void DevCatalogAssetDefinesTwoRegionsWithDistinctStableIds()
        {
            WorldRegionCatalog catalog = AssetDatabase.LoadAssetAtPath<WorldRegionCatalog>(DevCatalogPath);
            Assert.That(catalog, Is.Not.Null, $"Could not load the dev region catalog at {DevCatalogPath}.");

            List<string> errors = new List<string>();
            Assert.That(catalog.TryCreateRegistry(errors, out WorldRegionRegistry registry), Is.True, string.Join("; ", errors));
            Assert.That(registry.Count, Is.EqualTo(2));

            Assert.That(registry.TryGetDefinition("nacre/dev/test-north", out RegionDefinition north, out string northError),
                Is.True, northError);
            Assert.That(registry.TryGetDefinition("nacre/dev/test-south", out RegionDefinition south, out string southError),
                Is.True, southError);
            Assert.That(north.StableId, Is.Not.EqualTo(south.StableId));
            Assert.That(north.Description, Is.Not.Empty, "Authored descriptions are part of the region definition.");
        }

        [Test]
        public void EmptyOrWhitespaceStableIdIsRejectedAndNamesTheAsset()
        {
            RegionDefinition empty = _objects.CreateDefinition(string.Empty, assetName: "NoIdAsset");
            RegionDefinition blank = _objects.CreateDefinition("   ", assetName: "BlankIdAsset");
            List<string> errors = new List<string>();

            Assert.That(WorldRegionRegistry.TryCreate(new List<RegionDefinition> { empty, blank }, errors, out WorldRegionRegistry registry),
                Is.False);
            Assert.That(registry, Is.Null);
            Assert.That(errors, Has.Count.EqualTo(2));
            Assert.That(errors[0], Does.Contain("NoIdAsset").And.Contain("empty"));
            Assert.That(errors[1], Does.Contain("BlankIdAsset").And.Contain("empty"));
        }

        [Test]
        public void StableIdWithWhitespaceIsRejected()
        {
            RegionDefinition spaced = _objects.CreateDefinition("nacre/coast north", assetName: "SpacedIdAsset");
            List<string> errors = new List<string>();

            Assert.That(WorldRegionRegistry.TryCreate(new List<RegionDefinition> { spaced }, errors, out WorldRegionRegistry registry),
                Is.False);
            Assert.That(registry, Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("nacre/coast north").And.Contain("whitespace"));
        }

        [Test]
        public void DuplicateStableIdFailsTheWholeRegistryAndNamesBothAssets()
        {
            RegionDefinition first = _objects.CreateDefinition("nacre/check/shared", assetName: "FirstShared");
            RegionDefinition second = _objects.CreateDefinition("nacre/check/shared", assetName: "SecondShared");
            RegionDefinition unrelated = _objects.CreateDefinition("nacre/check/other", assetName: "Unrelated");
            List<string> errors = new List<string>();

            bool created = WorldRegionRegistry.TryCreate(new List<RegionDefinition> { first, second, unrelated }, errors,
                out WorldRegionRegistry registry);

            Assert.That(created, Is.False, "A catalog with a duplicate ID must not produce a partial registry.");
            Assert.That(registry, Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("nacre/check/shared").And.Contain("FirstShared").And.Contain("SecondShared"));
        }

        [Test]
        public void SameDefinitionListedTwiceIsRejected()
        {
            RegionDefinition only = _objects.CreateDefinition("nacre/check/once", assetName: "ListedTwice");
            List<string> errors = new List<string>();

            Assert.That(WorldRegionRegistry.TryCreate(new List<RegionDefinition> { only, only }, errors, out WorldRegionRegistry registry),
                Is.False);
            Assert.That(registry, Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("more than once"));
        }

        [Test]
        public void NullEntryIsReportedWithItsPosition()
        {
            RegionDefinition valid = _objects.CreateDefinition("nacre/check/valid", assetName: "ValidOne");
            List<string> errors = new List<string>();

            Assert.That(WorldRegionRegistry.TryCreate(new List<RegionDefinition> { valid, null }, errors, out WorldRegionRegistry registry),
                Is.False);
            Assert.That(registry, Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(errors[0], Does.Contain("Entry 1").And.Contain("no Region Definition"));
        }

        [Test]
        public void MissingListIsReportedAsAnError()
        {
            List<string> errors = new List<string>();

            Assert.That(WorldRegionRegistry.TryCreate(null, errors, out WorldRegionRegistry registry), Is.False);
            Assert.That(registry, Is.Null);
            Assert.That(errors, Has.Count.EqualTo(1));
        }

        [Test]
        public void UnknownAndBlankLookupsReturnErrorsWithoutThrowing()
        {
            RegionDefinition north = _objects.CreateDefinition("nacre/check/north");
            List<string> errors = new List<string>();
            Assert.That(WorldRegionRegistry.TryCreate(new List<RegionDefinition> { north }, errors, out WorldRegionRegistry registry),
                Is.True, string.Join("; ", errors));

            bool found = registry.TryGetDefinition("nacre/check/missing", out RegionDefinition missing, out string missingError);
            Assert.That(found, Is.False);
            Assert.That(missing, Is.Null);
            Assert.That(missingError, Does.Contain("nacre/check/missing"));

            Assert.That(registry.TryGetDefinition("  ", out _, out string blankError), Is.False);
            Assert.That(blankError, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void IsRegisteredRequiresTheExactRegisteredAsset()
        {
            RegionDefinition registered = _objects.CreateDefinition("nacre/check/registered");
            RegionDefinition copyWithSameId = _objects.CreateDefinition("nacre/check/registered");
            RegionDefinition unrelated = _objects.CreateDefinition("nacre/check/unrelated");
            List<string> errors = new List<string>();
            Assert.That(WorldRegionRegistry.TryCreate(new List<RegionDefinition> { registered, unrelated }, errors,
                out WorldRegionRegistry registry), Is.True, string.Join("; ", errors));

            Assert.That(registry.IsRegistered(registered), Is.True);
            Assert.That(registry.IsRegistered(copyWithSameId), Is.False,
                "A different asset that only shares the ID is not the registered region.");
            Assert.That(registry.IsRegistered(null), Is.False);
        }
    }
}
