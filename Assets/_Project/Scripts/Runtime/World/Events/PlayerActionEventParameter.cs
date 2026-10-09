using System;
using UnityEngine;

namespace Wildshift.World.Events
{
    /// <summary>
    /// One strongly typed, named numeric value attached to a <see cref="PlayerActionEvent"/>.
    /// Instances are immutable and hold only serializable data; free-form string payloads are
    /// deliberately not supported. <see cref="PlayerActionEventRecorder"/> validates the ID and
    /// value when the carrying event is recorded.
    /// </summary>
    [Serializable]
    public sealed class PlayerActionEventParameter
    {
        [SerializeField] private string _id;
        [SerializeField] private float _value;

        /// <summary>Creates a parameter; validation happens when the owning event is recorded.</summary>
        public PlayerActionEventParameter(string id, float value)
        {
            _id = id;
            _value = value;
        }

        /// <summary>Parameterless constructor for Unity serialization only; do not call directly.</summary>
        internal PlayerActionEventParameter()
        {
        }

        /// <summary>Stable parameter ID, for example <c>units-extracted</c>; never blank on a recorded event.</summary>
        public string Id => _id;

        /// <summary>Finite numeric value carried by the parameter.</summary>
        public float Value => _value;

        /// <summary>Returns a short debug description such as <c>units-extracted=3</c>.</summary>
        public override string ToString()
        {
            return $"{_id}={_value}";
        }
    }
}
