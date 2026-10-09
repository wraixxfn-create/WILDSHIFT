using System;
using NUnit.Framework;
using Wildshift.Persistence;

namespace Wildshift.Tests
{
    /// <summary>
    /// Verifies the write-throttling policy that keeps periodic saving away from the per-frame path.
    /// </summary>
    public sealed class SaveWriteThrottleTests
    {
        [Test]
        public void ANewThrottleHasNothingToWrite()
        {
            SaveWriteThrottle throttle = new SaveWriteThrottle(10d);

            Assert.That(throttle.MinimumIntervalSeconds, Is.EqualTo(10d));
            Assert.That(throttle.IsDirty, Is.False);
            Assert.That(throttle.ShouldWrite(0d), Is.False);
            Assert.That(throttle.ShouldWrite(1000d), Is.False);
        }

        [Test]
        public void DirtyStateMayBeWrittenImmediatelyTheFirstTime()
        {
            SaveWriteThrottle throttle = new SaveWriteThrottle(10d);
            throttle.MarkDirty();

            Assert.That(throttle.IsDirty, Is.True);
            Assert.That(throttle.ShouldWrite(0d), Is.True, "The first save of a session must not wait for the interval.");
        }

        [Test]
        public void WritesAreSpacedByTheMinimumInterval()
        {
            SaveWriteThrottle throttle = new SaveWriteThrottle(10d);
            throttle.MarkDirty();
            Assert.That(throttle.ShouldWrite(100d), Is.True);

            throttle.MarkWritten(100d);
            throttle.MarkDirty();

            Assert.That(throttle.ShouldWrite(105d), Is.False, "Writing again inside the interval must be refused.");
            Assert.That(throttle.ShouldWrite(109.5d), Is.False);
            Assert.That(throttle.ShouldWrite(110d), Is.True, "Once the interval has passed the write is due again.");
        }

        [Test]
        public void StateStaysDirtyUntilAWriteIsReportedSoAFailedSaveIsRetried()
        {
            SaveWriteThrottle throttle = new SaveWriteThrottle(1d);
            throttle.MarkDirty();

            // No MarkWritten call: the save attempt failed, so the request must survive.
            Assert.That(throttle.ShouldWrite(0d), Is.True);
            Assert.That(throttle.IsDirty, Is.True);
            Assert.That(throttle.ShouldWrite(5d), Is.True);

            throttle.MarkWritten(5d);
            Assert.That(throttle.IsDirty, Is.False);
            Assert.That(throttle.ShouldWrite(50d), Is.False, "With nothing changed there is nothing to write.");
        }

        [Test]
        public void ANonFiniteClockNeverTriggersAWrite()
        {
            SaveWriteThrottle throttle = new SaveWriteThrottle(1d);
            throttle.MarkDirty();

            Assert.That(throttle.ShouldWrite(double.NaN), Is.False);
        }

        [Test]
        public void NonPositiveOrNonFiniteIntervalsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveWriteThrottle(0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveWriteThrottle(-1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveWriteThrottle(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SaveWriteThrottle(double.PositiveInfinity));
        }

        [Test]
        public void TheDefaultIntervalIsOneWritePerHalfMinute()
        {
            SaveWriteThrottle throttle = new SaveWriteThrottle();

            Assert.That(throttle.MinimumIntervalSeconds, Is.EqualTo(SaveWriteThrottle.DefaultMinimumIntervalSeconds));
            Assert.That(throttle.MinimumIntervalSeconds, Is.EqualTo(30d));
        }
    }
}
