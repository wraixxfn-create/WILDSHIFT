using System;
using UnityEngine;

namespace Wildshift.Persistence
{
    /// <summary>
    /// Durable position and orientation of the player. It stores plain values rather than a
    /// transform, <see cref="GameObject"/>, or hierarchy path, so the player can be restored in any
    /// scene that provides a body to place. Only data that already exists in the prototype is
    /// stored: no health, inventory, or progression model is saved yet.
    /// </summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private Quaternion _orientation;

        /// <summary>Creates a player block; validation happens when the owning save is validated.</summary>
        internal PlayerSaveData(Vector3 position, Quaternion orientation)
        {
            _position = position;
            _orientation = orientation;
        }

        /// <summary>Parameterless constructor for Unity serialization only; do not call directly.</summary>
        internal PlayerSaveData()
        {
        }

        /// <summary>World-space position the player was saved at.</summary>
        public Vector3 Position => _position;

        /// <summary>World-space orientation the player was saved with.</summary>
        public Quaternion Orientation => _orientation;

        /// <summary>
        /// Checks that the stored transform values are usable. Non-finite components are rejected, and
        /// an orientation that is not a real rotation (all components zero, which is what unset data
        /// deserializes to) is rejected instead of being silently applied as identity.
        /// </summary>
        public bool TryValidate(out string error)
        {
            if (!IsFinite(_position))
            {
                error = $"the saved player position {_position} is not a finite number";
                return false;
            }

            if (!IsFinite(_orientation))
            {
                error = $"the saved player orientation {_orientation} is not a finite number";
                return false;
            }

            float squaredMagnitude =
                (_orientation.x * _orientation.x) +
                (_orientation.y * _orientation.y) +
                (_orientation.z * _orientation.z) +
                (_orientation.w * _orientation.w);
            if (squaredMagnitude < 1e-6f)
            {
                error = "the saved player orientation is not a usable rotation";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(Quaternion value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
