using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.World.Clock
{
    /// <summary>
    /// Scene component that owns the authoritative <see cref="WorldClock"/> for the loaded world and feeds it
    /// real frame time each update. It is the only place where Unity frame time enters the world clock; the
    /// clock itself stays deterministic for any given sequence of advance calls.
    /// </summary>
    /// <remarks>
    /// The clock is scene-scoped: when the scene unloads, the host and its clock are destroyed, and a reload
    /// creates a fresh clock at zero. Only one host may be live at a time. If a second host is awake while
    /// another is still alive, the second logs an error and destroys itself, so duplicates cannot run in parallel.
    /// Other systems should read <see cref="Clock"/> from Start or later, subscribe to
    /// <see cref="WorldClock.TimeAdvanced"/>, and never create their own clock.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WorldClockHost : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("When enabled, world time stays still until the clock is resumed from code.")]
        private bool _startPaused;

        [SerializeField, Min(0f)]
        [Tooltip("World seconds per real second. Zero freezes world time without pausing.")]
        private float _initialTimeScale = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Largest real frame time (seconds) forwarded to the clock, so a long hitch does not jump world time.")]
        private float _maxFrameDeltaSeconds = 0.25f;

        private WorldClock _clock;
        private bool _isAuthoritative;

        /// <summary>
        /// The authoritative clock for this scene, created in Awake. Null on a duplicate host that was destroyed.
        /// Read it from Start or later rather than from other objects' Awake calls.
        /// </summary>
        public WorldClock Clock => _clock;

        private void Awake()
        {
            if (HasAuthoritativeHostAlive())
            {
                WildshiftLog.Error("A second WorldClockHost was found while another is already active in the loaded " +
                                   "scenes. Only one world clock may exist; this duplicate has been destroyed.", this);
                Destroy(this);
                return;
            }

            _clock = new WorldClock(Mathf.Max(0f, _initialTimeScale), _startPaused);
            _isAuthoritative = true;
        }

        private void Update()
        {
            if (_clock == null)
            {
                return;
            }

            float frameSeconds = Mathf.Clamp(Time.deltaTime, 0f, Mathf.Max(0f, _maxFrameDeltaSeconds));
            _clock.AdvanceRealTime(frameSeconds);
        }

        private bool HasAuthoritativeHostAlive()
        {
            WorldClockHost[] hosts = UnityEngine.Object.FindObjectsByType<WorldClockHost>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (WorldClockHost host in hosts)
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
