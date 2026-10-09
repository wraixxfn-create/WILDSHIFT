namespace Wildshift.Persistence
{
    /// <summary>
    /// Counts describing what a restore actually applied. Callers log or display these so a partial
    /// match between a save and the current content (for example a region that no longer exists)
    /// is visible instead of silent.
    /// </summary>
    public readonly struct SaveRestoreSummary
    {
        internal SaveRestoreSummary(int appliedRegionCount, int skippedRegionCount, int restoredEventCount)
        {
            AppliedRegionCount = appliedRegionCount;
            SkippedRegionCount = skippedRegionCount;
            RestoredEventCount = restoredEventCount;
        }

        /// <summary>Saved regions whose state was written back onto a registered region.</summary>
        public int AppliedRegionCount { get; }

        /// <summary>
        /// Saved regions that no longer match a registered region ID. Their data is left alone rather
        /// than being guessed onto another region.
        /// </summary>
        public int SkippedRegionCount { get; }

        /// <summary>World events re-recorded into the event log, oldest first.</summary>
        public int RestoredEventCount { get; }

        /// <summary>Short developer-readable description; contains no filesystem path.</summary>
        public override string ToString()
        {
            return $"{AppliedRegionCount} regions applied, {SkippedRegionCount} regions skipped, " +
                   $"{RestoredEventCount} events restored";
        }
    }
}
