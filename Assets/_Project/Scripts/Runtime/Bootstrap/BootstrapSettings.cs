using UnityEngine;

namespace Wildshift.Bootstrap
{
    /// <summary>
    /// Designer-authored configuration for the bootstrap sequence.
    /// Holds configuration only; runtime state lives in <see cref="BootstrapController"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "BootstrapSettings", menuName = "Wildshift/Configuration/Bootstrap Settings")]
    public sealed class BootstrapSettings : ScriptableObject
    {
        [SerializeField]
        private string _targetSceneName = "Prototype";

        /// <summary>
        /// Name of the scene loaded after the bootstrap sequence. The scene must be enabled in Build Settings.
        /// </summary>
        public string TargetSceneName => _targetSceneName;
    }
}
