using System;
using UnityEngine;
using Wildshift.Persistence.Model;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Converts <see cref="SaveGameData"/> to and from the JSON text stored on disk.
    /// This is the only place that knows the file is JSON; swapping the text format later means
    /// changing this type and bumping <see cref="SaveSchema.CurrentVersion"/>, nothing else.
    /// </summary>
    /// <remarks>
    /// <c>JsonUtility</c> reads only the serialized fields of the save model, so a file can never
    /// resurrect scenes, components, or arbitrary object graphs: unknown JSON members are ignored and
    /// missing members stay at their defaults. Because defaults are silent,
    /// <see cref="SaveDataValidator"/> must still run on everything that parses.
    /// </remarks>
    internal static class SaveSerializer
    {
        /// <summary>Serializes a payload to pretty-printed JSON so saves stay diff- and eyeball-friendly.</summary>
        internal static string Serialize(SaveGameData data)
        {
            return JsonUtility.ToJson(data, true);
        }

        /// <summary>
        /// Parses save JSON. Returns false with a developer-readable reason for any text that is not
        /// a JSON object this model can read; malformed input must never escape as an exception.
        /// </summary>
        internal static bool TryDeserialize(string json, out SaveGameData data, out string error)
        {
            data = null;

            if (string.IsNullOrWhiteSpace(json))
            {
                error = "The save file is empty.";
                return false;
            }

            try
            {
                data = JsonUtility.FromJson<SaveGameData>(json);
            }
            catch (Exception parseException)
            {
                // The file is untrusted input; any parser failure is reported, never rethrown.
                error = $"The save file is not valid save JSON ({parseException.GetType().Name}: " +
                        $"{parseException.Message}).";
                return false;
            }

            if (data == null)
            {
                error = "The save file does not contain a save object.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
