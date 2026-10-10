using System;

namespace Wildshift.World.Clock
{
    /// <summary>
    /// Authoritative elapsed world time for one world/session. Time is stored as integer ticks
    /// (microseconds) with a carried sub-tick remainder, so repeated frame-sized advances do not
    /// accumulate floating-point drift. The clock never reads the system calendar or the wall clock:
    /// its state depends only on the sequence of calls made to it. Create one per world; it is a plain
    /// C# object, not a singleton, and it has no Unity lifetime of its own (see <see cref="WorldClockHost"/>).
    /// </summary>
    /// <remarks>
    /// Subscribers to <see cref="TimeAdvanced"/> must not advance the clock from inside the callback;
    /// such a call throws <see cref="InvalidOperationException"/> so event order stays chronological.
    /// </remarks>
    public sealed class WorldClock
    {
        /// <summary>Resolution of <see cref="ElapsedTicks"/>: one tick is one microsecond of world time.</summary>
        public const long TicksPerSecond = 1000000L;

        /// <summary>Largest elapsed value the clock accepts (about 146,000 world years); advances beyond it throw.</summary>
        public const long MaxElapsedTicks = long.MaxValue / 2;

        private long _elapsedTicks;
        private double _fractionalTicks;
        private double _timeScale;
        private bool _isPaused;
        private bool _isRaisingTimeAdvanced;

        /// <summary>
        /// Creates a clock at world time zero.
        /// </summary>
        /// <param name="timeScale">World seconds per real second. Must be finite and non-negative; zero freezes time without pausing.</param>
        /// <param name="startPaused">When true, <see cref="AdvanceRealTime"/> has no effect until <see cref="Resume"/> is called.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when timeScale is negative, NaN, or infinite.</exception>
        public WorldClock(double timeScale = 1d, bool startPaused = false)
        {
            ValidateTimeScale(timeScale);
            _timeScale = timeScale;
            _isPaused = startPaused;
        }

        /// <summary>Elapsed world time in ticks, where one tick is 1/<see cref="TicksPerSecond"/> of a second.</summary>
        public long ElapsedTicks => _elapsedTicks;

        /// <summary>Elapsed world time in seconds. Derived from <see cref="ElapsedTicks"/>; not accumulated separately.</summary>
        public double ElapsedTime => _elapsedTicks / (double)TicksPerSecond;

        /// <summary>Current world seconds per real second applied by <see cref="AdvanceRealTime"/>.</summary>
        public double TimeScale => _timeScale;

        /// <summary>True while real-time advancement is suspended. Manual advancement still works while paused.</summary>
        public bool IsPaused => _isPaused;

        /// <summary>
        /// Raised after each advance that moves elapsed time forward by at least one tick. Calls that add no
        /// whole tick, paused real-time updates, and zero-length advances raise nothing. Subscribers receive
        /// the state after the advance; the publisher remains the only authority on time.
        /// </summary>
        public event Action<WorldTimeAdvance> TimeAdvanced;

        /// <summary>
        /// Changes how quickly real time becomes world time. Affects later <see cref="AdvanceRealTime"/> calls only.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when timeScale is negative, NaN, or infinite.</exception>
        public void SetTimeScale(double timeScale)
        {
            ValidateTimeScale(timeScale);
            _timeScale = timeScale;
        }

        /// <summary>Suspends real-time advancement. Calling it while already paused has no effect.</summary>
        public void Pause()
        {
            _isPaused = true;
        }

        /// <summary>Resumes real-time advancement. Calling it while already running has no effect.</summary>
        public void Resume()
        {
            _isPaused = false;
        }

        /// <summary>
        /// Sets elapsed world time to a value taken from a save. Unlike the Advance methods, this may move
        /// time backwards, because loading a save replaces the session's timeline. It ignores pause and time
        /// scale, raises no <see cref="TimeAdvanced"/> event, and clears any sub-tick remainder.
        /// </summary>
        /// <param name="elapsedTicks">Elapsed time in ticks, from zero up to <see cref="MaxElapsedTicks"/>.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when elapsedTicks is negative or above the maximum.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called from a <see cref="TimeAdvanced"/> subscriber.</exception>
        public void RestoreElapsedTicks(long elapsedTicks)
        {
            if (elapsedTicks < 0L || elapsedTicks > MaxElapsedTicks)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedTicks), elapsedTicks,
                    "Restored world time must be between zero and the clock's maximum elapsed ticks.");
            }

            ThrowIfRaisingTimeAdvanced();
            _elapsedTicks = elapsedTicks;
            _fractionalTicks = 0d;
        }

        /// <summary>
        /// Advances world time by <paramref name="realSeconds"/> multiplied by <see cref="TimeScale"/>.
        /// Does nothing while paused. Intended to be called once per frame by the owning host.
        /// </summary>
        /// <param name="realSeconds">Real seconds elapsed since the previous call. Must be finite and non-negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when realSeconds is negative, NaN, or infinite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called from a <see cref="TimeAdvanced"/> subscriber, or when the advance would exceed <see cref="MaxElapsedTicks"/>.</exception>
        public void AdvanceRealTime(double realSeconds)
        {
            ValidateDuration(realSeconds, nameof(realSeconds));
            ThrowIfRaisingTimeAdvanced();
            if (_isPaused)
            {
                return;
            }

            Advance(realSeconds * _timeScale, WorldTimeAdvanceSource.RealTime);
        }

        /// <summary>
        /// Advances world time by exactly <paramref name="worldSeconds"/>, ignoring pause and time scale.
        /// Intended for tests and development validation; it works while paused.
        /// </summary>
        /// <param name="worldSeconds">World seconds to add. Must be finite and non-negative.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when worldSeconds is negative, NaN, or infinite.</exception>
        /// <exception cref="InvalidOperationException">Thrown when called from a <see cref="TimeAdvanced"/> subscriber, or when the advance would exceed <see cref="MaxElapsedTicks"/>.</exception>
        public void AdvanceManually(double worldSeconds)
        {
            ValidateDuration(worldSeconds, nameof(worldSeconds));
            ThrowIfRaisingTimeAdvanced();
            Advance(worldSeconds, WorldTimeAdvanceSource.Manual);
        }

        private void Advance(double worldSeconds, WorldTimeAdvanceSource source)
        {
            // The remainder below one tick is carried forward, so rounding error never accumulates and
            // sub-tick advances are not lost. The result depends only on the call sequence.
            double exactTicks = worldSeconds * TicksPerSecond + _fractionalTicks;
            double wholeTicks = Math.Floor(exactTicks);
            if (wholeTicks > (double)(MaxElapsedTicks - _elapsedTicks))
            {
                throw new InvalidOperationException(
                    $"Advancing the world clock by {worldSeconds} seconds would exceed the maximum elapsed time " +
                    $"of {MaxElapsedTicks} ticks. The clock was left unchanged.");
            }

            _fractionalTicks = exactTicks - wholeTicks;
            if (wholeTicks < 1d)
            {
                return;
            }

            long previousElapsedTicks = _elapsedTicks;
            _elapsedTicks = previousElapsedTicks + (long)wholeTicks;
            RaiseTimeAdvanced(new WorldTimeAdvance(previousElapsedTicks, _elapsedTicks, source));
        }

        private void RaiseTimeAdvanced(WorldTimeAdvance advance)
        {
            Action<WorldTimeAdvance> handler = TimeAdvanced;
            if (handler == null)
            {
                return;
            }

            _isRaisingTimeAdvanced = true;
            try
            {
                handler(advance);
            }
            finally
            {
                _isRaisingTimeAdvanced = false;
            }
        }

        private void ThrowIfRaisingTimeAdvanced()
        {
            if (_isRaisingTimeAdvanced)
            {
                throw new InvalidOperationException(
                    "The world clock cannot be advanced from inside a TimeAdvanced subscriber. " +
                    "Defer the advance until the current event has finished.");
            }
        }

        private static void ValidateTimeScale(double timeScale)
        {
            if (double.IsNaN(timeScale) || double.IsInfinity(timeScale) || timeScale < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(timeScale), timeScale,
                    "The world clock time scale must be a finite, non-negative number.");
            }
        }

        private static void ValidateDuration(double seconds, string parameterName)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
            {
                throw new ArgumentOutOfRangeException(parameterName, seconds,
                    "A world clock advance must be a finite, non-negative number of seconds.");
            }
        }
    }
}
