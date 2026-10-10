using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildshift.Persistence;
using Wildshift.World;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the pure restore decisions: how a saved region claim is checked against the registry, when a saved
    /// pose is replaced by the safe spawn, and which clock time a load restores. These run without scene objects.
    /// </summary>
    public sealed class SaveRestorePolicyTests
    {
        private static readonly Vector3 SafePosition = new Vector3(-6f, 1.05f, 2f);
        private static readonly Quaternion SafeOrientation = Quaternion.identity;
        private const float MinimumHeight = -20f;

        private WorldRegionTestObjects _regionObjects;

        [SetUp]
        public void SetUp()
        {
            _regionObjects = new WorldRegionTestObjects();
        }

        [TearDown]
        public void TearDown()
        {
            _regionObjects.DestroyAll();
        }

        [Test]
        public void AnAbsentRegionClaimNeedsNoCheck()
        {
            WorldRegionRegistry registry = CreateRegistry("nacre/test/policy-a");

            SavedRegionResolution resolution = SaveRestorePolicy.ResolveRegion(null, registry, out string error);

            Assert.That(resolution, Is.EqualTo(SavedRegionResolution.None));
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ARegisteredRegionClaimIsAccepted()
        {
            WorldRegionRegistry registry = CreateRegistry("nacre/test/policy-a");

            SavedRegionResolution resolution = SaveRestorePolicy.ResolveRegion("nacre/test/policy-a", registry, out string error);

            Assert.That(resolution, Is.EqualTo(SavedRegionResolution.Registered));
            Assert.That(error, Is.Null);
        }

        [Test]
        public void AWellFormedButUnknownRegionClaimIsReportedAsUnregistered()
        {
            WorldRegionRegistry registry = CreateRegistry("nacre/test/policy-a");

            SavedRegionResolution resolution = SaveRestorePolicy.ResolveRegion("nacre/test/removed", registry, out string error);

            Assert.That(resolution, Is.EqualTo(SavedRegionResolution.Unregistered));
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void ResolveRegionRejectsAMissingRegistry()
        {
            Assert.Throws<ArgumentNullException>(() => SaveRestorePolicy.ResolveRegion("nacre/test/policy-a", null, out _));
        }

        [Test]
        public void AValidSavedPoseIsUsedUnchangedExceptForNormalisation()
        {
            Vector3 position = new Vector3(1f, 2f, 3f);
            Quaternion orientation = Quaternion.Euler(0f, 90f, 0f);
            PlayerSaveData saved = new PlayerSaveData(position, orientation);

            SavedPlayerPlacement placement = SaveRestorePolicy.ResolvePlayerPlacement(
                saved, SafePosition, SafeOrientation, MinimumHeight, _ => false);

            Assert.That(placement.IsRecovered, Is.False);
            Assert.That(placement.RecoveryReason, Is.Null);
            Assert.That(placement.Position, Is.EqualTo(position));
            Assert.That(Quaternion.Angle(placement.Orientation, orientation), Is.LessThan(0.001f));
        }

        [Test]
        public void AnUnnormalisedSavedOrientationIsNormalised()
        {
            PlayerSaveData saved = new PlayerSaveData(Vector3.zero, new Quaternion(0f, 0f, 0f, 2f));

            SavedPlayerPlacement placement = SaveRestorePolicy.ResolvePlayerPlacement(
                saved, SafePosition, SafeOrientation, MinimumHeight, _ => false);

            Assert.That(placement.IsRecovered, Is.False);
            Assert.That(placement.Orientation.magnitude, Is.EqualTo(1f).Within(1e-5f));
        }

        [Test]
        public void APositionBelowTheWorldIsReplacedBySafeSpawn()
        {
            PlayerSaveData saved = new PlayerSaveData(new Vector3(0f, -50f, 0f), Quaternion.identity);

            SavedPlayerPlacement placement = SaveRestorePolicy.ResolvePlayerPlacement(
                saved, SafePosition, SafeOrientation, MinimumHeight, _ => false);

            Assert.That(placement.IsRecovered, Is.True);
            Assert.That(placement.RecoveryReason, Does.Contain("below the playable world"));
            Assert.That(placement.Position, Is.EqualTo(SafePosition));
        }

        [Test]
        public void AnObstructedPositionIsReplacedBySafeSpawn()
        {
            Vector3 blocked = new Vector3(4f, 1f, 4f);
            PlayerSaveData saved = new PlayerSaveData(blocked, Quaternion.identity);

            SavedPlayerPlacement placement = SaveRestorePolicy.ResolvePlayerPlacement(
                saved, SafePosition, SafeOrientation, MinimumHeight, position => position == blocked);

            Assert.That(placement.IsRecovered, Is.True);
            Assert.That(placement.RecoveryReason, Does.Contain("solid geometry"));
            Assert.That(placement.Position, Is.EqualTo(SafePosition));
        }

        [Test]
        public void ANonFinitePositionIsReplacedWithoutAskingTheSceneAboutIt()
        {
            bool asked = false;
            PlayerSaveData saved = new PlayerSaveData(new Vector3(float.NaN, 1f, 0f), Quaternion.identity);

            SavedPlayerPlacement placement = SaveRestorePolicy.ResolvePlayerPlacement(
                saved, SafePosition, SafeOrientation, MinimumHeight, _ => { asked = true; return false; });

            Assert.That(placement.IsRecovered, Is.True);
            Assert.That(placement.RecoveryReason, Does.Contain("not valid"));
            Assert.That(asked, Is.False, "An invalid pose must not be sent to the physics check.");
            Assert.That(placement.Position, Is.EqualTo(SafePosition));
        }

        [Test]
        public void ResolvePlayerPlacementRejectsMissingInputs()
        {
            PlayerSaveData saved = new PlayerSaveData(Vector3.zero, Quaternion.identity);

            Assert.Throws<ArgumentNullException>(() => SaveRestorePolicy.ResolvePlayerPlacement(
                null, SafePosition, SafeOrientation, MinimumHeight, _ => false));
            Assert.Throws<ArgumentNullException>(() => SaveRestorePolicy.ResolvePlayerPlacement(
                saved, SafePosition, SafeOrientation, MinimumHeight, null));
        }

        [Test]
        public void TheRestoredClockIsTheSavedTimeWhenNoEventIsNewer()
        {
            GameSaveData save = SaveWith(5.5d, new List<PlayerActionEvent>
            {
                new PlayerActionEvent("ev-early", PlayerActionEventType.RegionEntered, 2d),
            });

            Assert.That(SaveRestorePolicy.ResolveElapsedWorldTicks(save), Is.EqualTo(5500000L));
        }

        [Test]
        public void TheRestoredClockIsNeverEarlierThanTheNewestSavedEvent()
        {
            GameSaveData save = SaveWith(2d, new List<PlayerActionEvent>
            {
                new PlayerActionEvent("ev-late", PlayerActionEventType.RegionEntered, 9.25d),
            });

            Assert.That(SaveRestorePolicy.ResolveElapsedWorldTicks(save), Is.EqualTo(9250000L),
                "A rounded clock value must never let the next event predate the restored history.");
        }

        [Test]
        public void AnAbsentSavedClockWithNoEventsRestoresZero()
        {
            GameSaveData save = SaveWith(0d, new List<PlayerActionEvent>());

            Assert.That(SaveRestorePolicy.ResolveElapsedWorldTicks(save), Is.EqualTo(0L));
        }

        private WorldRegionRegistry CreateRegistry(params string[] stableIds)
        {
            List<RegionDefinition> definitions = new List<RegionDefinition>();
            foreach (string stableId in stableIds)
            {
                definitions.Add(_regionObjects.CreateDefinition(stableId));
            }

            List<string> errors = new List<string>();
            Assert.That(WorldRegionRegistry.TryCreate(definitions, errors, out WorldRegionRegistry registry), Is.True,
                string.Join(" ", errors));
            return registry;
        }

        private static GameSaveData SaveWith(double elapsedWorldTime, List<PlayerActionEvent> events)
        {
            return new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                "test",
                new PlayerSaveData(Vector3.zero, Quaternion.identity),
                new List<RegionSaveData>(),
                events,
                elapsedWorldTime);
        }
    }
}
