namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// Authoring rules for species stable IDs. A species ID is the identity of a species in logs,
    /// events, and any future save data, so it must be explicit, non-blank, and free of whitespace.
    /// These rules only check the text; nothing in the project generates or derives an ID, and no
    /// GameObject, prefab, or asset name is ever used as an ID.
    /// </summary>
    /// <remarks>
    /// The rules match <c>Wildshift.World.Regions.WorldRegionIdRules</c> on purpose, so authored IDs
    /// across the project read the same way: lowercase words separated by '-', levels separated by '/'.
    /// </remarks>
    public static class CreatureSpeciesIdRules
    {
        /// <summary>Example of the recommended style: planet, then <c>species</c>, then the species name.</summary>
        public const string ExampleId = "nacre/species/siltveil-grazer";

        /// <summary>
        /// Returns true when <paramref name="stableId"/> is non-blank and contains no whitespace or control
        /// characters. Otherwise returns false with a message that tells the author what to change.
        /// </summary>
        public static bool TryValidate(string stableId, out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "The species stable ID is empty. Enter a unique authored ID such as '" + ExampleId +
                        "' in the Stable Id field. Species IDs are never generated automatically.";
                return false;
            }

            for (int i = 0; i < stableId.Length; i++)
            {
                char character = stableId[i];
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    error = "The species stable ID '" + stableId + "' contains a space, tab, or other " +
                            "whitespace/control character. Remove it; separate words with '-' and levels with " +
                            "', such as '" + ExampleId + "'.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
