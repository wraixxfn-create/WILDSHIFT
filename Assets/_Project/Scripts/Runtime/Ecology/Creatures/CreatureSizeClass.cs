namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// Approximate body-size band for a species. This is an authored, coarse label — it exists so
    /// designers, UI text, and future rules (footprint, silhouette, how startling a creature is)
    /// can compare species without inventing a numeric body-mass system.
    /// </summary>
    /// <remarks>
    /// The metre bands in the remarks are guidance for authors, not enforced limits: the visible
    /// size of a creature comes from its prefab, and nothing at runtime converts this enum into a
    /// length. If a species needs an exact measurement, add a field for it rather than stretching
    /// these bands.
    /// </remarks>
    public enum CreatureSizeClass
    {
        /// <summary>Under about 0.3 m. Roughly hand-sized.</summary>
        Tiny = 0,

        /// <summary>About 0.3 m to 1 m. Roughly cat-sized to hare-sized.</summary>
        Small = 1,

        /// <summary>About 1 m to 2 m. Roughly dog-sized to human-sized.</summary>
        Medium = 2,

        /// <summary>About 2 m to 5 m. Larger than a human, still traversable terrain-scale.</summary>
        Large = 3,

        /// <summary>Over about 5 m. A landmark rather than an animal.</summary>
        Enormous = 4,
    }
}
