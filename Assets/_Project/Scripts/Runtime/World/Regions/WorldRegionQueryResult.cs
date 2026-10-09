namespace Wildshift.World.Regions
{
    /// <summary>Outcome of a position query against a <see cref="WorldRegionLocator"/>.</summary>
    public enum WorldRegionQueryStatus
    {
        /// <summary>
        /// No answer: the locator's setup is invalid or it has not been initialized. This is the default value, so a
        /// default-constructed result means "no answer", never "in a region". Check the locator's ValidationErrors.
        /// </summary>
        Unavailable = 0,

        /// <summary>The setup is valid and the position is inside no active region volume.</summary>
        OutsideAllRegions = 1,

        /// <summary>The position is inside at least one active region volume and one region was chosen.</summary>
        Found = 2,
    }

    /// <summary>
    /// Immutable result of a position query. It is a value type, so queries do not allocate.
    /// </summary>
    public readonly struct WorldRegionQueryResult
    {
        /// <summary>Creates a result. Use the locator's query methods rather than constructing results directly.</summary>
        public WorldRegionQueryResult(WorldRegionQueryStatus status, WorldRegionVolume volume, int overlappingRegionCount)
        {
            Status = status;
            Volume = volume;
            OverlappingRegionCount = overlappingRegionCount;
        }

        /// <summary>Outcome of the query; see <see cref="WorldRegionQueryStatus"/>.</summary>
        public WorldRegionQueryStatus Status { get; }

        /// <summary>The chosen volume when <see cref="Status"/> is Found; otherwise null.</summary>
        public WorldRegionVolume Volume { get; }

        /// <summary>
        /// Number of distinct regions whose volumes contain the position, including the chosen region.
        /// Zero when outside all regions or unavailable, one in the normal case, and two or more when regions overlap.
        /// </summary>
        public int OverlappingRegionCount { get; }

        /// <summary>True when the position is inside a region.</summary>
        public bool HasRegion => Status == WorldRegionQueryStatus.Found;

        /// <summary>True when more than one region contains the position. The chosen region is still deterministic.</summary>
        public bool IsOverlapping => Status == WorldRegionQueryStatus.Found && OverlappingRegionCount > 1;

        /// <summary>The chosen region's authored definition, or null when there is no region.</summary>
        public RegionDefinition Definition => HasRegion ? Volume.Definition : null;

        /// <summary>The chosen region's stable ID, or null when there is no region.</summary>
        public string RegionId => HasRegion ? Volume.StableId : null;
    }
}
