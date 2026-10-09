using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace Wildshift.World.Events
{
    /// <summary>
    /// Immutable, serializable record of one meaningful player action. It contains only stable IDs,
    /// an event type enum, and numbers — never Unity object references — so any future system can
    /// read it and a future persistence layer can save it. Callers create instances;
    /// <see cref="PlayerActionEventRecorder"/> validates them and is the authority for what enters
    /// the log.
    /// </summary>
    [Serializable]
    public sealed class PlayerActionEvent
    {
        [SerializeField] private string _id;
        [SerializeField] private PlayerActionEventType _eventType;
        [SerializeField] private double _elapsedWorldTime;
        [SerializeField] private string _regionId;
        [SerializeField] private string _targetId;
        [SerializeField] private bool _hasMagnitude;
        [SerializeField] private float _magnitude;
        [SerializeField] private PlayerActionEventParameter[] _parameters;

        [NonSerialized] private ReadOnlyCollection<PlayerActionEventParameter> _parameterView;

        /// <summary>
        /// Creates an event record. Field validation happens when the event is recorded; optional
        /// details are simply left out by passing null. The parameter collection is copied, so later
        /// changes to the caller's list cannot alter this record.
        /// </summary>
        public PlayerActionEvent(
            string id,
            PlayerActionEventType eventType,
            double elapsedWorldTime,
            string regionId = null,
            string targetId = null,
            float? magnitude = null,
            IReadOnlyList<PlayerActionEventParameter> parameters = null)
        {
            _id = id;
            _eventType = eventType;
            _elapsedWorldTime = elapsedWorldTime;
            _regionId = regionId;
            _targetId = targetId;
            _hasMagnitude = magnitude.HasValue;
            _magnitude = magnitude.GetValueOrDefault();

            if (parameters == null || parameters.Count == 0)
            {
                _parameters = Array.Empty<PlayerActionEventParameter>();
            }
            else
            {
                // Copy so later changes to the caller's collection cannot alter this record.
                _parameters = new PlayerActionEventParameter[parameters.Count];
                for (int index = 0; index < parameters.Count; index++)
                {
                    _parameters[index] = parameters[index];
                }
            }
        }

        /// <summary>Parameterless constructor for Unity serialization only; do not call directly.</summary>
        internal PlayerActionEvent()
        {
        }

        /// <summary>
        /// Returns a new stable event ID suitable for a fresh record. Event IDs only need to be
        /// unique among the events a recorder currently retains; a globally unique GUID satisfies
        /// that and keeps working if records are persisted later.
        /// </summary>
        public static string NewId()
        {
            return Guid.NewGuid().ToString("N");
        }

        /// <summary>Stable ID assigned once when the record is created; required, never blank on a recorded event.</summary>
        public string Id => _id;

        /// <summary>Strongly typed kind of action; required and must be a defined value other than <see cref="PlayerActionEventType.None"/>.</summary>
        public PlayerActionEventType EventType => _eventType;

        /// <summary>
        /// Elapsed world time in seconds at which the action happened, measured by the caller's world
        /// clock. Required, finite, and non-negative; a recorder only accepts times that do not move
        /// backwards relative to its own log.
        /// </summary>
        public double ElapsedWorldTime => _elapsedWorldTime;

        /// <summary>Stable region ID where the action happened, or null when the action is not tied to one region.</summary>
        public string RegionId => _regionId;

        /// <summary>True when the record names a region.</summary>
        public bool HasRegionId => !string.IsNullOrEmpty(_regionId);

        /// <summary>
        /// Stable ID of the affected creature, object, structure, or settlement, or null when the
        /// action has no single target.
        /// </summary>
        public string TargetId => _targetId;

        /// <summary>True when the record names a target.</summary>
        public bool HasTargetId => !string.IsNullOrEmpty(_targetId);

        /// <summary>True when the record carries a magnitude.</summary>
        public bool HasMagnitude => _hasMagnitude;

        /// <summary>
        /// Non-negative amount related to the action, for example damage dealt or units extracted.
        /// Meaningful only when <see cref="HasMagnitude"/> is true; what a magnitude means for one
        /// event type is decided by the system that reacts to that type, not here.
        /// </summary>
        public float Magnitude => _magnitude;

        /// <summary>Read-only, possibly empty set of structured parameters; never null.</summary>
        public IReadOnlyList<PlayerActionEventParameter> Parameters
        {
            get
            {
                if (_parameters == null || _parameters.Length == 0)
                {
                    return Array.Empty<PlayerActionEventParameter>();
                }

                if (_parameterView == null)
                {
                    _parameterView = Array.AsReadOnly(_parameters);
                }

                return _parameterView;
            }
        }

        /// <summary>Returns a short debug description of the record.</summary>
        public override string ToString()
        {
            return $"{_eventType} event '{_id}' at {_elapsedWorldTime:0.###}s";
        }
    }
}
