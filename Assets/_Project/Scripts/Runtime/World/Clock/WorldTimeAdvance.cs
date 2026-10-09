namespace Wildshift.World.Clock
{
    /// <summary>
    /// Immutable description of one effective <see cref="WorldClock"/> advance, delivered to
    /// <see cref="WorldClock.TimeAdvanced"/> subscribers after the clock has already moved forward.
    /// </summary>
    public readonly struct WorldTimeAdvance
    {
        internal WorldTimeAdvance(long previousElapsedTicks, long elapsedTicks, WorldTimeAdvanceSource source)
        {
            PreviousElapsedTicks = previousElapsedTicks;
            ElapsedTicks = elapsedTicks;
            Source = source;
        }

        /// <summary>Elapsed world time before this advance, in ticks (see <see cref="WorldClock.TicksPerSecond"/>).</summary>
        public long PreviousElapsedTicks { get; }

        /// <summary>Elapsed world time after this advance, in ticks.</summary>
        public long ElapsedTicks { get; }

        /// <summary>Number of ticks advanced; always greater than zero for a delivered event.</summary>
        public long DeltaTicks => ElapsedTicks - PreviousElapsedTicks;

        /// <summary>Elapsed world time after this advance, in seconds.</summary>
        public double ElapsedTime => ElapsedTicks / (double)WorldClock.TicksPerSecond;

        /// <summary>World seconds advanced by this event.</summary>
        public double DeltaTime => DeltaTicks / (double)WorldClock.TicksPerSecond;

        /// <summary>Whether this advance came from real-time updates or a manual test advance.</summary>
        public WorldTimeAdvanceSource Source { get; }
    }
}
