namespace Wildshift.Interaction
{
    /// <summary>
    /// Contract for a world object the environmental scanner can inspect. Implementations own
    /// authored identity and scan text; aiming, range, line of sight, and input live in the player
    /// scanner. Scanning is independent of <see cref="IInteractable"/> so a collected sample can
    /// still be inspected.
    /// </summary>
    public interface IScanTarget
    {
        /// <summary>Stable authored ID recorded on scan events. Never derived from the scene hierarchy.</summary>
        string TargetId { get; }

        /// <summary>Short authored label shown in scan feedback. Not an identity key.</summary>
        string DisplayName { get; }

        /// <summary>Short authored description of what the scanner is looking at.</summary>
        string Description { get; }

        /// <summary>Simple authored scan result. Not a scientific classification system.</summary>
        string ScanResult { get; }

        /// <summary>True when this object may be scanned right now.</summary>
        bool IsScanAvailable { get; }
    }
}
