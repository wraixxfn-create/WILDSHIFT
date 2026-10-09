namespace Wildshift.World.Clock
{
    /// <summary>Identifies what caused a <see cref="WorldClock"/> advance.</summary>
    public enum WorldTimeAdvanceSource
    {
        /// <summary>Advanced by <see cref="WorldClock.AdvanceRealTime"/>; scaled by the time scale and blocked while paused.</summary>
        RealTime = 1,

        /// <summary>Advanced by <see cref="WorldClock.AdvanceManually"/>; exact, and unaffected by pause or time scale.</summary>
        Manual = 2
    }
}
