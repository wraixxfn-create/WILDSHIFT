namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// The four behaviours a creature instance can currently be in. This is runtime state, not
    /// authored data: it lives on <see cref="CreatureInstanceState"/>, never on a species asset.
    /// </summary>
    /// <remarks>
    /// The set is deliberately the minimum needed for a cautious grazer. Add a value here only when
    /// a species definition also gains the parameters that drive it, so no behaviour exists that
    /// nothing is configured for.
    /// </remarks>
    public enum CreatureBehavior
    {
        /// <summary>Standing and grazing in place. Waiting out the authored idle interval.</summary>
        Idle = 0,

        /// <summary>Moving at graze speed toward a point inside the species' wander radius.</summary>
        Wander = 1,

        /// <summary>Stopped and turned toward a noticed stimulus, deciding whether it is a threat.</summary>
        Alert = 2,

        /// <summary>Moving away from a threat at flee speed for the authored flee duration.</summary>
        Flee = 3,
    }
}
