using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Wildshift.Core.Diagnostics;

namespace Wildshift.Core.Bootstrap
{
    /// <summary>
    /// Application entry point. Validates the bootstrap configuration and hands control
    /// to the target scene. Dependencies are assigned in the Inspector; nothing is looked up globally.
    /// </summary>
    public sealed class BootstrapController : MonoBehaviour
    {
        [SerializeField]
        private BootstrapSettings _settings;

        private BootstrapPhase _phase = BootstrapPhase.NotStarted;
        private float _loadProgress;
        private string _failureReason = string.Empty;

        /// <summary>Current phase of the bootstrap sequence.</summary>
        public BootstrapPhase Phase => _phase;

        /// <summary>Progress of the target scene load, in the range [0, 1].</summary>
        public float LoadProgress => _loadProgress;

        /// <summary>Human-readable reason when <see cref="Phase"/> is <see cref="BootstrapPhase.Failed"/>.</summary>
        public string FailureReason => _failureReason;

        private void Start()
        {
            StartCoroutine(RunBootstrapSequence());
        }

        private IEnumerator RunBootstrapSequence()
        {
            if (!TryResolveTargetSceneName(out string sceneName, out string error))
            {
                Fail(error);
                yield break;
            }

            _phase = BootstrapPhase.LoadingTargetScene;
            AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (loadOperation == null)
            {
                Fail($"Unity refused to start loading scene '{sceneName}'.");
                yield break;
            }

            // Single-mode load: this scene, and this component, are unloaded when the target finishes loading.
            while (!loadOperation.isDone)
            {
                _loadProgress = loadOperation.progress;
                yield return null;
            }

            _loadProgress = 1f;
        }

        private bool TryResolveTargetSceneName(out string sceneName, out string error)
        {
            sceneName = null;
            error = null;

            if (_settings == null)
            {
                error = "BootstrapController has no BootstrapSettings assigned.";
                return false;
            }

            sceneName = _settings.TargetSceneName;
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                error = "BootstrapSettings.TargetSceneName is empty.";
                return false;
            }

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                error = $"Scene '{sceneName}' is not enabled in Build Settings.";
                return false;
            }

            return true;
        }

        private void Fail(string reason)
        {
            _phase = BootstrapPhase.Failed;
            _failureReason = reason;
            WildshiftLog.Error($"[Bootstrap] {reason}", this);
        }
    }
}
