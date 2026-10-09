namespace Wildshift.World.Regions
{
    /// <summary>
    /// Authoring rules for region stable IDs. A stable ID is the identity of a region in saves and
    /// gameplay, so it must be explicit, non-blank, and free of whitespace. These rules only check
    /// the text; the registry checks uniqueness, and nothing in the project generates IDs.
    /// </summary>
    public static class WorldRegionIdRules
    {
        /// <summary>Example of the recommended style: lowercase words, levels separated by '/'.</summary>
        public const string ExampleId = "nacre/coast/north";

        /// <summary>
        /// Returns true when <paramref name="stableId"/> is non-blank and contains no whitespace or control
        /// characters. Otherwise returns false with a message that tells the author what to change.
        /// </summary>
        public static bool TryValidate(string stableId, out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "The stable ID is empty. Enter a unique authored ID such as '" + ExampleId +
                        "' in the Stable Id field. IDs are never generated automatically.";
                return false;
            }

            for (int i = 0; i < stableId.Length; i++)
            {
                char character = stableId[i];
                if (char.IsWhiteSpace(character) || char.IsControl(character))
                {
                    error = "The stable ID '" + stableId + "' contains a space, tab, or other whitespace/control " +
                            "character. Remove it; separate words with '-' and levels with '/', such as '" +
                            ExampleId + "'.";
                    return false;
                }
            }

            error = null;
            return true;
        }
    }
}
