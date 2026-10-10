using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.World.Events
{
    /// <summary>
    /// Scene component that owns the authoritative <see cref="PlayerActionEventRecorder"/> for the
    /// loaded world, analogous to <see cref="Clock.WorldClockHost"/>. The recorder is created in
    /// Awake; other systems resolve the host through <see cref="FindFirstAvailable"/> or an
    /// Inspector reference and read <see cref="Recorder"/> from Start or later.
    /// </summary>
    /// <remarks>
    /// Only one host may be live at a time. A second active host logs an error and destroys itself,
    /// mirroring the single-authoritative-host pattern used by <see cref="Clock.WorldClockHost"/>.
    /// When the scene unloads, the host and its recorder are destroyed, which resets the event log
    /// for the next play session as required by the no-persistence-yet restriction.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerActionEventRecorderHost : MonoBehaviour
    {
        [SerializeField, Min(1), Tooltip("Maximum number of events retained in memory; oldest events drop first.")]
        private int _historyLimit = PlayerActionEventRecorder.DefaultHistoryLimit;

        private PlayerActionEventRecorder _recorder;
        private bool _isAuthoritative;

        /// <summary>
        /// The authoritative recorder for this scene, created in Awake. Null on a duplicate host
        /// that was destroyed. Read it from Start or later rather than from other objects' Awake calls.
        /// </summary>
        public PlayerActionEventRecorder Recorder => _recorder;

        /// <summary>
        /// Finds the first live, authoritative recorder host in the loaded scenes. Returns null
        /// when none is present; callers should handle that gracefully (no events recorded).
        /// </summary>
        public static PlayerActionEventRecorderHost FindFirstAvailable()
        {
            PlayerActionEventRecorderHost[] hosts = Object.FindObjectsByType<PlayerActionEventRecorderHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayerActionEventRecorderHost host in hosts)
            {
                if (host != null && host._isAuthoritative && host._recorder != null)
                {
                    return host;
                }
            }

            return null;
        }

        /// <summary>
        /// Replaces the authoritative recorder. Used only when a save is restored into this session: the
        /// previous log is discarded rather than merged, so the replacement must already hold a validated
        /// history. Build it on a staged recorder through <c>GameSaveMapper.TryApply</c> first, so a failed
        /// restore never touches the live log.
        /// </summary>
        /// <exception cref="System.ArgumentNullException">Thrown when replacement is null.</exception>
        /// <exception cref="System.InvalidOperationException">Thrown when this host is not the authoritative one.</exception>
        public void ReplaceRecorder(PlayerActionEventRecorder replacement)
        {
            if (replacement == null)
            {
                throw new System.ArgumentNullException(nameof(replacement));
            }

            if (!_isAuthoritative)
            {
                throw new System.InvalidOperationException(
                    "Only the authoritative PlayerActionEventRecorderHost can replace its recorder.");
            }

            _recorder = replacement;
        }

        private void Awake()
        {
            if (HasAuthoritativeHostAlive())
            {
                WildshiftLog.Error("A second PlayerActionEventRecorderHost was found while another is already " +
                                   "active in the loaded scenes. Only one recorder may exist; this duplicate " +
                                   "has been destroyed.", this);
                Destroy(this);
                return;
            }

            _recorder = new PlayerActionEventRecorder(Mathf.Max(1, _historyLimit));
            _isAuthoritative = true;
        }

        private bool HasAuthoritativeHostAlive()
        {
            PlayerActionEventRecorderHost[] hosts = Object.FindObjectsByType<PlayerActionEventRecorderHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (PlayerActionEventRecorderHost host in hosts)
            {
                if (host != this && host._isAuthoritative)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
