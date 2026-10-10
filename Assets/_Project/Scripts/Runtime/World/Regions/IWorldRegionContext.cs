namespace Wildshift.World.Regions
{
    /// <summary>
    /// Read-only spatial context for an actor whose actions can be attributed to a world region.
    /// Implementations resolve through the authoritative <see cref="WorldRegionLocator"/>; consumers
    /// must not infer a region from display names, scene names, or hierarchy paths.
    /// </summary>
    public interface IWorldRegionContext
    {
        /// <summary>The latest query status for the actor's current world position.</summary>
        WorldRegionQueryStatus CurrentRegionStatus { get; }

        /// <summary>The current stable region ID, or null while outside all regions or unavailable.</summary>
        string CurrentRegionId { get; }

        /// <summary>
        /// Re-resolves the actor's position at the moment an action is committed. This completion-time
        /// rule means that if an interaction starts in one region and completes in another, the latter
        /// region is returned. Returns false with a null ID outside registered regions or when lookup is
        /// unavailable; callers should still record the action without a region ID rather than invent one.
        /// </summary>
        bool TryResolveRegionForAction(out string regionId);
    }
}
