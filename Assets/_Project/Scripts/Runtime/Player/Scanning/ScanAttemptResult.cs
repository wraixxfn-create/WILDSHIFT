namespace Wildshift.Player.Scanning
{
    /// <summary>Outcome of one scanner activation. Visual presentation maps these values; it does not decide them.</summary>
    public enum ScanAttemptOutcome
    {
        /// <summary>The aimed object is a valid scan target inside range with clear line of sight.</summary>
        Success = 0,

        /// <summary>The aimed object is not a scan target, the ray hit nothing, or a solid obstacle blocked the aim.</summary>
        InvalidTarget = 1,

        /// <summary>A scan target is along the aim ray but farther than the configured scan range.</summary>
        OutOfRange = 2,

        /// <summary>The target was already scanned this session and repeat scans are disabled.</summary>
        RepeatRejected = 3,
    }

    /// <summary>
    /// Immutable result of one scan attempt. Contains only serializable values and authored text —
    /// never Unity object references — so presentation can be replaced without changing scan logic.
    /// </summary>
    public sealed class ScanAttemptResult
    {
        private ScanAttemptResult(
            ScanAttemptOutcome outcome,
            string targetId,
            string displayName,
            string description,
            string scanResultText,
            string regionId,
            bool eventRecorded,
            bool isRepeat)
        {
            Outcome = outcome;
            TargetId = targetId;
            DisplayName = displayName;
            Description = description;
            ScanResultText = scanResultText;
            RegionId = regionId;
            EventRecorded = eventRecorded;
            IsRepeat = isRepeat;
        }

        /// <summary>Classified outcome of this attempt.</summary>
        public ScanAttemptOutcome Outcome { get; }

        /// <summary>Stable target ID when a scan target was identified; otherwise null.</summary>
        public string TargetId { get; }

        /// <summary>Authored display name when a scan target was identified; otherwise null.</summary>
        public string DisplayName { get; }

        /// <summary>Authored description on a successful or repeat-rejected scan; otherwise null.</summary>
        public string Description { get; }

        /// <summary>Authored scan result text on a successful scan; otherwise null.</summary>
        public string ScanResultText { get; }

        /// <summary>Stable region ID recorded with a successful scan, or null when none was resolved.</summary>
        public string RegionId { get; }

        /// <summary>True when this attempt appended a record to the player action event log.</summary>
        public bool EventRecorded { get; }

        /// <summary>True when this target ID was already scanned earlier in the session.</summary>
        public bool IsRepeat { get; }

        /// <summary>Creates a successful scan result, including optional re-scans that were not logged.</summary>
        public static ScanAttemptResult Success(
            string targetId,
            string displayName,
            string description,
            string scanResultText,
            string regionId,
            bool eventRecorded,
            bool isRepeat)
        {
            return new ScanAttemptResult(
                ScanAttemptOutcome.Success,
                targetId,
                displayName,
                description,
                scanResultText,
                regionId,
                eventRecorded,
                isRepeat);
        }

        /// <summary>Creates a result for empty air, a non-scan object, or a blocked line of sight.</summary>
        public static ScanAttemptResult InvalidTarget()
        {
            return new ScanAttemptResult(
                ScanAttemptOutcome.InvalidTarget,
                null,
                null,
                null,
                null,
                null,
                eventRecorded: false,
                isRepeat: false);
        }

        /// <summary>Creates a result for a scan target that is along the aim ray but outside range.</summary>
        public static ScanAttemptResult OutOfRange(string targetId, string displayName)
        {
            return new ScanAttemptResult(
                ScanAttemptOutcome.OutOfRange,
                targetId,
                displayName,
                null,
                null,
                null,
                eventRecorded: false,
                isRepeat: false);
        }

        /// <summary>Creates a result for a valid in-range target that this session already scanned.</summary>
        public static ScanAttemptResult RepeatRejected(
            string targetId,
            string displayName,
            string description,
            string scanResultText,
            string regionId)
        {
            return new ScanAttemptResult(
                ScanAttemptOutcome.RepeatRejected,
                targetId,
                displayName,
                description,
                scanResultText,
                regionId,
                eventRecorded: false,
                isRepeat: true);
        }
    }
}
