using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wildshift.Persistence;
using Wildshift.World;
using Wildshift.World.Events;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the schema-v1 fields added for prototype save/load: the optional player region ID, the elapsed
    /// world time, and the collected-sample list. Also verifies that a version-1 file written before these fields
    /// existed still reads with safe defaults, so no migration step is needed.
    /// </summary>
    public sealed class GameSaveSessionFieldsTests
    {
        private const string SurveySite = "nacre/frontier/survey-site";
        private const string SampleId = "nacre/sample/disturbed-soil-a";

        [Test]
        public void CaptureStoresTheRegionClockAndSortedSampleIds()
        {
            GameSaveData save = CaptureWith(SurveySite, 42.5d, new List<string> { "nacre/sample/b", "nacre/sample/a" });

            Assert.That(save.Player.RegionId, Is.EqualTo(SurveySite));
            Assert.That(save.ElapsedWorldTime, Is.EqualTo(42.5d));
            Assert.That(save.CollectedSampleIds, Is.EqualTo(new[] { "nacre/sample/a", "nacre/sample/b" }),
                "Sample IDs are stored sorted so identical state produces identical files.");
            Assert.That(save.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void CaptureWithoutOptionalStateStoresOutsideNoTimeAndNoSamples()
        {
            GameSaveData save = GameSaveMapper.Capture(Vector3.zero, Quaternion.identity,
                new WorldStateService(), new PlayerActionEventRecorder());

            Assert.That(save.Player.RegionId, Is.Null, "Outside all regions is stored as an absent region.");
            Assert.That(save.ElapsedWorldTime, Is.EqualTo(0d));
            Assert.That(save.CollectedSampleIds, Is.Empty);
            Assert.That(save.TryValidate(out string error), Is.True, error);
        }

        [Test]
        public void NewFieldsRoundTripThroughJsonAndStillValidate()
        {
            GameSaveData save = CaptureWith(SurveySite, 12.25d, new List<string> { SampleId });

            GameSaveData loaded = JsonUtility.FromJson<GameSaveData>(JsonUtility.ToJson(save));

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.TryValidate(out string error), Is.True, error);
            Assert.That(loaded.Player.RegionId, Is.EqualTo(SurveySite));
            Assert.That(loaded.ElapsedWorldTime, Is.EqualTo(12.25d));
            Assert.That(loaded.CollectedSampleIds, Is.EqualTo(new[] { SampleId }));
        }

        [Test]
        public void AVersionOneFileWrittenBeforeTheFieldsExistedStillLoadsWithSafeDefaults()
        {
            // Hand-written in the shape the game wrote before region, time, and sample fields were added.
            const string legacy =
                "{\"_schemaVersion\":1,\"_savedAtUtc\":\"2026-01-01T00:00:00.0000000Z\",\"_gameVersion\":\"legacy\"," +
                "\"_player\":{\"_position\":{\"x\":1.0,\"y\":2.0,\"z\":3.0},\"_orientation\":{\"x\":0.0,\"y\":0.0,\"z\":0.0,\"w\":1.0}}," +
                "\"_regions\":[],\"_worldEvents\":[]}";

            GameSaveData save = JsonUtility.FromJson<GameSaveData>(legacy);

            Assert.That(save, Is.Not.Null);
            Assert.That(save.TryValidate(out string error), Is.True, error);
            Assert.That(save.Player.RegionId, Is.Null, "An absent region reads as outside all regions.");
            Assert.That(save.ElapsedWorldTime, Is.EqualTo(0d), "An absent time reads as zero.");
            Assert.That(save.CollectedSampleIds, Is.Empty, "An absent collection reads as no samples.");
        }

        [Test]
        public void AMalformedRegionIdInThePlayerBlockIsRejected()
        {
            GameSaveData save = SaveWithPlayerRegion("nacre bad region");

            Assert.That(save.TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("region ID"));
        }

        [Test]
        public void AnEmptyRegionIdInThePlayerBlockIsRejected()
        {
            GameSaveData save = SaveWithPlayerRegion(string.Empty);

            Assert.That(save.TryValidate(out _), Is.False, "Outside must be an absent value, never an empty string.");
        }

        [Test]
        public void NonFiniteOrNegativeOrOversizedElapsedWorldTimeIsRejected()
        {
            Assert.That(WithElapsed(double.NaN).TryValidate(out string nanError), Is.False);
            Assert.That(nanError, Does.Contain("world time"));
            Assert.That(WithElapsed(double.PositiveInfinity).TryValidate(out _), Is.False);
            Assert.That(WithElapsed(-0.5d).TryValidate(out _), Is.False);
            Assert.That(WithElapsed(GameSaveData.MaxSupportedElapsedWorldTime + 1d).TryValidate(out _), Is.False);
            Assert.That(WithElapsed(GameSaveData.MaxSupportedElapsedWorldTime).TryValidate(out _), Is.True);
        }

        [Test]
        public void DuplicateBlankAndMalformedCollectedSampleIdsAreRejected()
        {
            Assert.That(CaptureWith(SurveySite, 0d, new List<string> { SampleId, SampleId }).TryValidate(out string duplicate), Is.False);
            Assert.That(duplicate, Does.Contain("more than once"));

            Assert.That(CaptureWith(SurveySite, 0d, new List<string> { "bad id" }).TryValidate(out _), Is.False);
            Assert.That(CaptureWith(SurveySite, 0d, new List<string> { "   " }).TryValidate(out _), Is.False);
            Assert.That(CaptureWith(SurveySite, 0d, new List<string> { null }).TryValidate(out _), Is.False);
        }

        [Test]
        public void TooManyCollectedSampleIdsAreRejected()
        {
            List<string> ids = new List<string>();
            for (int index = 0; index <= GameSaveData.MaxSupportedCollectedSamples; index++)
            {
                ids.Add("nacre/sample/s" + index.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            Assert.That(CaptureWith(null, 0d, ids).TryValidate(out string error), Is.False);
            Assert.That(error, Does.Contain("collected samples"));
        }

        private static GameSaveData CaptureWith(string regionId, double elapsedWorldTime, List<string> sampleIds)
        {
            return GameSaveMapper.Capture(
                new Vector3(1f, 2f, 3f),
                Quaternion.identity,
                new WorldStateService(),
                new PlayerActionEventRecorder(),
                GameSaveData.DefaultMaxWorldEvents,
                regionId,
                elapsedWorldTime,
                sampleIds);
        }

        private static GameSaveData SaveWithPlayerRegion(string regionId)
        {
            return new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                "test",
                new PlayerSaveData(Vector3.zero, Quaternion.identity, regionId),
                new List<RegionSaveData>(),
                new List<PlayerActionEvent>());
        }

        private static GameSaveData WithElapsed(double elapsedWorldTime)
        {
            return new GameSaveData(
                GameSaveData.CurrentSchemaVersion,
                DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                "test",
                new PlayerSaveData(Vector3.zero, Quaternion.identity),
                new List<RegionSaveData>(),
                new List<PlayerActionEvent>(),
                elapsedWorldTime);
        }
    }
}
