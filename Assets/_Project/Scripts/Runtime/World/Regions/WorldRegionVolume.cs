using UnityEngine;

namespace Wildshift.World.Regions
{
    /// <summary>
    /// Scene component that marks a box of world space as belonging to one authored region. It is a spatial
    /// description only: it stores a reference to a <see cref="RegionDefinition"/>, not runtime state, and it does
    /// not register itself anywhere. The <see cref="WorldRegionLocator"/> that lists it answers position queries.
    /// </summary>
    /// <remarks>
    /// The box is defined in this object's local space by <see cref="LocalCenter"/> and <see cref="LocalSize"/>, so
    /// position, rotation, and scale on the transform all apply. Non-uniform scale is handled; skew inherited from a
    /// rotated, non-uniformly scaled parent is not supported. One region may be covered by several volumes, and
    /// several volumes may overlap only if they resolve by priority (see <see cref="WorldRegionLocator"/>).
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WorldRegionVolume : MonoBehaviour
    {
        [SerializeField, Tooltip("The authored region this volume belongs to. Use a RegionDefinition asset that is listed " +
                                 "in the locator's Region Catalog.")]
        private RegionDefinition _definition;

        [SerializeField, Tooltip("Centre of the box in this object's local space.")]
        private Vector3 _localCenter = Vector3.zero;

        [SerializeField, Tooltip("Size of the box in this object's local space. Every axis must be greater than zero.")]
        private Vector3 _localSize = Vector3.one;

        [SerializeField, Tooltip("Used only when this volume overlaps a volume of a different region. Higher priority wins. " +
                                 "Equal priorities are resolved by stable ID order and reported as a warning.")]
        private int _priority;

        /// <summary>The authored region this volume belongs to; may be null on an unfinished volume.</summary>
        public RegionDefinition Definition => _definition;

        /// <summary>Stable ID of <see cref="Definition"/>, or null when no definition is assigned.</summary>
        public string StableId => _definition != null ? _definition.StableId : null;

        /// <summary>Overlap priority. Higher values win when volumes of different regions overlap.</summary>
        public int Priority => _priority;

        /// <summary>Centre of the box in local space.</summary>
        public Vector3 LocalCenter => _localCenter;

        /// <summary>Size of the box in local space.</summary>
        public Vector3 LocalSize => _localSize;

        /// <summary>
        /// Returns the current world-space box from the transform's position, rotation, and scale.
        /// Reading it does not allocate.
        /// </summary>
        public WorldRegionBox GetWorldBox()
        {
            Transform cachedTransform = transform;
            Vector3 scale = cachedTransform.lossyScale;
            Vector3 halfExtents = new Vector3(
                Mathf.Abs(_localSize.x * scale.x),
                Mathf.Abs(_localSize.y * scale.y),
                Mathf.Abs(_localSize.z * scale.z)) * 0.5f;

            return new WorldRegionBox(
                cachedTransform.TransformPoint(_localCenter),
                cachedTransform.right,
                cachedTransform.up,
                cachedTransform.forward,
                halfExtents);
        }

        /// <summary>
        /// Checks the box settings without touching the scene. Returns false with a description when the centre or size
        /// is not finite, or when any size axis is zero or negative. An invalid volume is never considered to contain points.
        /// </summary>
        public bool TryValidateBounds(out string error)
        {
            if (!IsFinite(_localCenter))
            {
                error = "Local Center must contain finite numbers.";
                return false;
            }

            if (!IsFinite(_localSize) || _localSize.x <= 0f || _localSize.y <= 0f || _localSize.z <= 0f)
            {
                error = "Local Size must be finite and greater than zero on every axis; it is currently (" +
                        _localSize.x + ", " + _localSize.y + ", " + _localSize.z + ").";
                return false;
            }

            error = null;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void OnDrawGizmosSelected()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(_localCenter, _localSize);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
