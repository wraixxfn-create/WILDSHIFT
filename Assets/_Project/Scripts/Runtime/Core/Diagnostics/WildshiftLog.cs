using UnityEngine;

namespace Wildshift.Core.Diagnostics
{
    /// <summary>
    /// Small adapter over Unity's logger. It intentionally keeps Unity's native log entries,
    /// context objects, and stack-trace behavior.
    /// </summary>
    public static class WildshiftLog
    {
        private const string Prefix = "[Wildshift]";

        /// <summary>Writes an informational message to the Unity Console.</summary>
        public static void Info(object message, UnityEngine.Object context = null)
        {
            Debug.Log($"{Prefix} {message}", context);
        }

        /// <summary>Writes a warning to the Unity Console.</summary>
        public static void Warning(object message, UnityEngine.Object context = null)
        {
            Debug.LogWarning($"{Prefix} {message}", context);
        }

        /// <summary>Writes an error to the Unity Console.</summary>
        public static void Error(object message, UnityEngine.Object context = null)
        {
            Debug.LogError($"{Prefix} {message}", context);
        }

        /// <summary>
        /// Writes a verbose message only when WILDSHIFT_VERBOSE_LOGGING is defined for the
        /// calling assembly. The call and its arguments are removed from builds without the symbol.
        /// </summary>
        [System.Diagnostics.Conditional("WILDSHIFT_VERBOSE_LOGGING")]
        public static void Verbose(object message, UnityEngine.Object context = null)
        {
            Debug.Log($"{Prefix} {message}", context);
        }
    }
}
