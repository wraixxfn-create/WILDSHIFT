using System;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Small policy object that keeps periodic saving off the disk-write-per-frame path. A caller
    /// marks its state dirty when something worth saving changes and asks whether a write is due;
    /// writes happen at most once per configured interval and only when something actually changed.
    /// It owns no clock (the caller passes its own world time, like
    /// <c>PlayerActionEventRecorder</c> does) and it performs no I/O: <see cref="LocalSaveService"/>
    /// still does the writing, so an explicit save is always available.
    /// </summary>
    public sealed class SaveWriteThrottle
    {
        /// <summary>Interval used when a caller does not configure one: one write per half minute at most.</summary>
        public const double DefaultMinimumIntervalSeconds = 30d;

        private readonly double _minimumIntervalSeconds;
        private double _lastWriteTime = double.NegativeInfinity;
        private bool _isDirty;

        /// <summary>Creates a throttle with a minimum interval between writes.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when the interval is zero or negative.</exception>
        public SaveWriteThrottle(double minimumIntervalSeconds = DefaultMinimumIntervalSeconds)
        {
            if (minimumIntervalSeconds <= 0d || double.IsNaN(minimumIntervalSeconds) || double.IsInfinity(minimumIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(minimumIntervalSeconds), minimumIntervalSeconds,
                    "The minimum interval between save writes must be a finite positive number of seconds.");
            }

            _minimumIntervalSeconds = minimumIntervalSeconds;
        }

        /// <summary>Minimum number of seconds between two writes.</summary>
        public double MinimumIntervalSeconds => _minimumIntervalSeconds;

        /// <summary>True when state changed since the last write (or since the throttle was created).</summary>
        public bool IsDirty => _isDirty;

        /// <summary>
        /// Records that owned state changed and is worth saving at the next allowed moment.
        /// Call this from the system that changed the state, not from a per-frame update.
        /// </summary>
        public void MarkDirty()
        {
            _isDirty = true;
        }

        /// <summary>
        /// True when a write is worth doing now: something changed, and either nothing has been
        /// written yet or at least <see cref="MinimumIntervalSeconds"/> has passed since the last
        /// write. Safe to call every frame; it allocates nothing and touches no disk.
        /// </summary>
        public bool ShouldWrite(double currentWorldTime)
        {
            if (!_isDirty || double.IsNaN(currentWorldTime))
            {
                return false;
            }

            return double.IsNegativeInfinity(_lastWriteTime) ||
                   currentWorldTime - _lastWriteTime >= _minimumIntervalSeconds;
        }

        /// <summary>
        /// Records that a write happened at the given world time and clears the dirty flag. Call it
        /// only after <see cref="LocalSaveService.TrySave"/> succeeded, so a failed save is retried
        /// at the next opportunity instead of being silently dropped.
        /// </summary>
        public void MarkWritten(double currentWorldTime)
        {
            _lastWriteTime = currentWorldTime;
            _isDirty = false;
        }
    }
}
