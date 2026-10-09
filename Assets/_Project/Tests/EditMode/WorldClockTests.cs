using System;
using System.Collections.Generic;
using NUnit.Framework;
using Wildshift.World.Clock;

namespace Wildshift.Tests
{
    /// <summary>Verifies world-time arithmetic, pause/scale control, events, determinism, and argument validation.</summary>
    public sealed class WorldClockTests
    {
        private const long OneSecondTicks = WorldClock.TicksPerSecond;

        [Test]
        public void NewClockStartsAtZeroRunningAtNormalScale()
        {
            WorldClock clock = new WorldClock();

            Assert.That(clock.ElapsedTicks, Is.EqualTo(0L));
            Assert.That(clock.ElapsedTime, Is.EqualTo(0d));
            Assert.That(clock.TimeScale, Is.EqualTo(1d));
            Assert.That(clock.IsPaused, Is.False);
        }

        [Test]
        public void StartPausedClockDoesNotAdvanceUntilResumed()
        {
            WorldClock clock = new WorldClock(startPaused: true);

            Assert.That(clock.IsPaused, Is.True);
            clock.AdvanceRealTime(5d);
            Assert.That(clock.ElapsedTicks, Is.EqualTo(0L));

            clock.Resume();
            clock.AdvanceRealTime(1d);
            Assert.That(clock.ElapsedTime, Is.EqualTo(1d));
        }

        [Test]
        public void RealTimeAdvanceAtNormalScaleAccumulatesExactly()
        {
            WorldClock clock = new WorldClock();

            clock.AdvanceRealTime(0.5d);
            Assert.That(clock.ElapsedTicks, Is.EqualTo(500000L));
            clock.AdvanceRealTime(0.25d);

            Assert.That(clock.ElapsedTicks, Is.EqualTo(750000L));
            Assert.That(clock.ElapsedTime, Is.EqualTo(0.75d));
        }

        [Test]
        public void TimeScaleMultipliesRealTimeAdvanceAndCanChangeAtRuntime()
        {
            WorldClock clock = new WorldClock();

            clock.SetTimeScale(2d);
            clock.AdvanceRealTime(0.25d);
            Assert.That(clock.ElapsedTime, Is.EqualTo(0.5d));

            clock.SetTimeScale(0.5d);
            Assert.That(clock.TimeScale, Is.EqualTo(0.5d));
            clock.AdvanceRealTime(1d);
            Assert.That(clock.ElapsedTime, Is.EqualTo(1d));
        }

        [Test]
        public void ZeroTimeScaleFreezesTimeWithoutPausingTheClock()
        {
            WorldClock clock = new WorldClock();
            int events = 0;
            clock.TimeAdvanced += _ => events++;

            clock.SetTimeScale(0d);
            clock.AdvanceRealTime(10d);

            Assert.That(clock.ElapsedTicks, Is.EqualTo(0L));
            Assert.That(clock.IsPaused, Is.False);
            Assert.That(events, Is.EqualTo(0));
        }

        [Test]
        public void PauseBlocksRealTimeAdvanceAndResumeRestoresIt()
        {
            WorldClock clock = new WorldClock();
            clock.AdvanceRealTime(1d);

            clock.Pause();
            clock.Pause();
            Assert.That(clock.IsPaused, Is.True);
            clock.AdvanceRealTime(5d);
            Assert.That(clock.ElapsedTime, Is.EqualTo(1d), "Paused real time must not move world time.");

            clock.Resume();
            clock.Resume();
            Assert.That(clock.IsPaused, Is.False);
            clock.AdvanceRealTime(2d);
            Assert.That(clock.ElapsedTime, Is.EqualTo(3d));
        }

        [Test]
        public void ManualAdvanceIgnoresPauseAndTimeScaleAndReportsItsSource()
        {
            WorldClock clock = new WorldClock(timeScale: 0d, startPaused: true);
            List<WorldTimeAdvance> advances = new List<WorldTimeAdvance>();
            clock.TimeAdvanced += advance => advances.Add(advance);

            clock.AdvanceManually(2d);

            Assert.That(clock.ElapsedTime, Is.EqualTo(2d));
            Assert.That(clock.IsPaused, Is.True, "Manual advancement must not change the pause state.");
            Assert.That(advances, Has.Count.EqualTo(1));
            Assert.That(advances[0].Source, Is.EqualTo(WorldTimeAdvanceSource.Manual));
        }

        [Test]
        public void TimeAdvancedReportsEachEffectiveAdvanceAfterTheClockMoves()
        {
            WorldClock clock = new WorldClock(timeScale: 2d);
            List<WorldTimeAdvance> advances = new List<WorldTimeAdvance>();
            clock.TimeAdvanced += advance => advances.Add(advance);

            clock.AdvanceRealTime(0.5d);
            clock.Pause();
            clock.AdvanceRealTime(3d);
            clock.Resume();
            clock.AdvanceManually(0.25d);

            Assert.That(advances, Has.Count.EqualTo(2));

            WorldTimeAdvance first = advances[0];
            Assert.That(first.PreviousElapsedTicks, Is.EqualTo(0L));
            Assert.That(first.ElapsedTicks, Is.EqualTo(OneSecondTicks));
            Assert.That(first.DeltaTicks, Is.EqualTo(OneSecondTicks));
            Assert.That(first.ElapsedTime, Is.EqualTo(1d));
            Assert.That(first.DeltaTime, Is.EqualTo(1d));
            Assert.That(first.Source, Is.EqualTo(WorldTimeAdvanceSource.RealTime));

            WorldTimeAdvance second = advances[1];
            Assert.That(second.PreviousElapsedTicks, Is.EqualTo(OneSecondTicks));
            Assert.That(second.ElapsedTime, Is.EqualTo(1.25d));
            Assert.That(second.DeltaTime, Is.EqualTo(0.25d));
            Assert.That(second.Source, Is.EqualTo(WorldTimeAdvanceSource.Manual));
        }

        [Test]
        public void NoEventIsRaisedForZeroLengthOrPausedAdvances()
        {
            WorldClock clock = new WorldClock();
            int events = 0;
            clock.TimeAdvanced += _ => events++;

            clock.AdvanceRealTime(0d);
            clock.AdvanceManually(0d);
            clock.Pause();
            clock.AdvanceRealTime(1d);

            Assert.That(events, Is.EqualTo(0));
            Assert.That(clock.ElapsedTicks, Is.EqualTo(0L));
        }

        [Test]
        public void SubTickAdvancesAreCarriedForwardRatherThanLost()
        {
            WorldClock clock = new WorldClock();
            List<WorldTimeAdvance> advances = new List<WorldTimeAdvance>();
            clock.TimeAdvanced += advance => advances.Add(advance);

            // Each step is one thousandth of a tick; 1100 steps total 1.1 ticks of world time.
            for (int step = 0; step < 1100; step++)
            {
                clock.AdvanceManually(1e-9d);
            }

            Assert.That(clock.ElapsedTicks, Is.EqualTo(1L));
            Assert.That(advances, Has.Count.EqualTo(1), "The single whole tick should be published exactly once.");
        }

        [Test]
        public void FrameSizedAdvancesDoNotDriftOverAnHour()
        {
            WorldClock clock = new WorldClock();
            const int frames = 3600 * 60;

            for (int frame = 0; frame < frames; frame++)
            {
                clock.AdvanceRealTime(1d / 60d);
            }

            // One hour of 60 Hz frames is 3600 world seconds. Rounding each frame to whole ticks would drift by about 72,000 ticks (72 ms) per hour.
            Assert.That(Math.Abs(clock.ElapsedTicks - 3600L * OneSecondTicks), Is.LessThanOrEqualTo(1L),
                "Frame-sized advances must not accumulate rounding drift.");
        }

        [Test]
        public void IdenticalCallSequencesProduceIdenticalTimeAndEvents()
        {
            WorldClock first = new WorldClock();
            WorldClock second = new WorldClock();
            List<WorldTimeAdvance> firstLog = new List<WorldTimeAdvance>();
            List<WorldTimeAdvance> secondLog = new List<WorldTimeAdvance>();
            first.TimeAdvanced += advance => firstLog.Add(advance);
            second.TimeAdvanced += advance => secondLog.Add(advance);

            RunScriptedSequence(first);
            RunScriptedSequence(second);

            Assert.That(second.ElapsedTicks, Is.EqualTo(first.ElapsedTicks));
            Assert.That(secondLog.Count, Is.EqualTo(firstLog.Count));
            for (int index = 0; index < firstLog.Count; index++)
            {
                Assert.That(secondLog[index].ElapsedTicks, Is.EqualTo(firstLog[index].ElapsedTicks));
                Assert.That(secondLog[index].Source, Is.EqualTo(firstLog[index].Source));
            }
        }

        [Test]
        public void ScriptedSequenceMatchesHandCalculatedTime()
        {
            WorldClock clock = new WorldClock();

            RunScriptedSequence(clock);

            // 1.0 s real at 1x (1.0) + 2.0 s real at 3x (6.0) + paused 5 s (0) + manual 0.5 s (0.5) + 0.25 s at 0x (0)
            Assert.That(clock.ElapsedTime, Is.EqualTo(7.5d).Within(1e-6));
            Assert.That(clock.TimeScale, Is.EqualTo(0d));
            Assert.That(clock.IsPaused, Is.False);
        }

        [Test]
        public void InvalidArgumentsAreRejectedAndLeaveTheClockUnchanged()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldClock(timeScale: -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldClock(timeScale: double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldClock(timeScale: double.PositiveInfinity));

            WorldClock clock = new WorldClock();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetTimeScale(-0.5d));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetTimeScale(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceRealTime(-1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceRealTime(double.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceRealTime(double.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceManually(-0.5d));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceManually(double.NaN));

            Assert.That(clock.ElapsedTicks, Is.EqualTo(0L));
            Assert.That(clock.TimeScale, Is.EqualTo(1d));
        }

        [Test]
        public void AdvancingBeyondTheMaximumElapsedTimeThrowsWithoutChangingTheClock()
        {
            WorldClock clock = new WorldClock();
            clock.AdvanceManually(1d);

            Assert.Throws<InvalidOperationException>(() => clock.AdvanceManually(1e13d));

            Assert.That(clock.ElapsedTicks, Is.EqualTo(OneSecondTicks));
        }

        [Test]
        public void SubscriberCannotAdvanceTheClockReentrantly()
        {
            WorldClock clock = new WorldClock();
            int blockedAttempts = 0;
            clock.TimeAdvanced += _ =>
            {
                try
                {
                    clock.AdvanceManually(1d);
                }
                catch (InvalidOperationException)
                {
                    blockedAttempts++;
                }
            };

            clock.AdvanceRealTime(1d);
            Assert.That(blockedAttempts, Is.EqualTo(1));
            Assert.That(clock.ElapsedTime, Is.EqualTo(1d), "The outer advance must complete normally.");

            clock.AdvanceManually(1d);
            Assert.That(clock.ElapsedTime, Is.EqualTo(2d), "The guard must reset after the event finishes.");
        }

        [Test]
        public void UnsubscribedListenersAreNoLongerNotified()
        {
            WorldClock clock = new WorldClock();
            int notifications = 0;
            Action<WorldTimeAdvance> listener = _ => notifications++;

            clock.TimeAdvanced += listener;
            clock.AdvanceManually(1d);
            clock.TimeAdvanced -= listener;
            clock.AdvanceManually(1d);

            Assert.That(notifications, Is.EqualTo(1));
        }

        private static void RunScriptedSequence(WorldClock clock)
        {
            clock.AdvanceRealTime(1d);
            clock.SetTimeScale(3d);
            clock.AdvanceRealTime(2d);
            clock.Pause();
            clock.AdvanceRealTime(5d);
            clock.AdvanceManually(0.5d);
            clock.Resume();
            clock.SetTimeScale(0d);
            clock.AdvanceRealTime(0.25d);
        }
    }
}
