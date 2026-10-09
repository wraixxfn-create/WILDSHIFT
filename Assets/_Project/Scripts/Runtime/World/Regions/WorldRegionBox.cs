using UnityEngine;

namespace Wildshift.World.Regions
{
    /// <summary>
    /// Oriented box in world space used for region bounds. It is plain data: a centre, three orthonormal axes, and
    /// half-extents along those axes. It is independent of physics and of the scene.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Containment is half-open.</b> A point is inside when, along every axis, the offset from the centre is
    /// at least <c>-halfExtent</c> and strictly less than <c>+halfExtent</c>. The minimum face belongs to the box and
    /// the maximum face does not. Two boxes that share a face therefore tile space with no point in both.
    /// </para>
    /// <para>
    /// <b>Overlap</b> uses the separating axis theorem over the 15 candidate axes. Boxes that only touch
    /// (share a face, edge, or corner) do not overlap, which matches half-open containment.
    /// </para>
    /// </remarks>
    public readonly struct WorldRegionBox
    {
        private const float SeparationTolerance = 1e-5f;
        private const float MinimumCrossLengthSquared = 1e-6f;

        /// <summary>Creates a box. Axes should be unit length and mutually perpendicular; half-extents should be non-negative.</summary>
        public WorldRegionBox(Vector3 center, Vector3 axisX, Vector3 axisY, Vector3 axisZ, Vector3 halfExtents)
        {
            Center = center;
            AxisX = axisX;
            AxisY = axisY;
            AxisZ = axisZ;
            HalfExtents = halfExtents;
        }

        /// <summary>World-space centre of the box.</summary>
        public Vector3 Center { get; }

        /// <summary>Unit world-space direction of the box's local X axis.</summary>
        public Vector3 AxisX { get; }

        /// <summary>Unit world-space direction of the box's local Y axis.</summary>
        public Vector3 AxisY { get; }

        /// <summary>Unit world-space direction of the box's local Z axis.</summary>
        public Vector3 AxisZ { get; }

        /// <summary>Half of the box's size along its own X, Y, and Z axes, in world units.</summary>
        public Vector3 HalfExtents { get; }

        /// <summary>True when the point lies in the half-open box (minimum faces inclusive, maximum faces exclusive).</summary>
        public bool Contains(Vector3 worldPoint)
        {
            Vector3 offset = worldPoint - Center;
            float x = Vector3.Dot(offset, AxisX);
            float y = Vector3.Dot(offset, AxisY);
            float z = Vector3.Dot(offset, AxisZ);

            return x >= -HalfExtents.x && x < HalfExtents.x &&
                   y >= -HalfExtents.y && y < HalfExtents.y &&
                   z >= -HalfExtents.z && z < HalfExtents.z;
        }

        /// <summary>
        /// True when the two boxes share at least some volume. Boxes that only touch are not overlapping.
        /// Intended for authoring checks and tests; it is not needed per frame.
        /// </summary>
        public bool Overlaps(WorldRegionBox other)
        {
            Vector3[] ownAxes = { AxisX, AxisY, AxisZ };
            Vector3[] otherAxes = { other.AxisX, other.AxisY, other.AxisZ };
            float[] ownHalf = { HalfExtents.x, HalfExtents.y, HalfExtents.z };
            float[] otherHalf = { other.HalfExtents.x, other.HalfExtents.y, other.HalfExtents.z };
            Vector3 offset = other.Center - Center;

            // Face normals of this box.
            for (int i = 0; i < 3; i++)
            {
                if (IsSeparatedOnAxis(ownAxes[i], offset, ownAxes, ownHalf, otherAxes, otherHalf))
                {
                    return false;
                }
            }

            // Face normals of the other box.
            for (int i = 0; i < 3; i++)
            {
                if (IsSeparatedOnAxis(otherAxes[i], offset, ownAxes, ownHalf, otherAxes, otherHalf))
                {
                    return false;
                }
            }

            // Edge-edge cross products. Nearly parallel edges give no useful axis and are covered by the face normals.
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    Vector3 axis = Vector3.Cross(ownAxes[i], otherAxes[j]);
                    if (axis.sqrMagnitude < MinimumCrossLengthSquared)
                    {
                        continue;
                    }

                    if (IsSeparatedOnAxis(axis.normalized, offset, ownAxes, ownHalf, otherAxes, otherHalf))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsSeparatedOnAxis(Vector3 axis, Vector3 offset,
            Vector3[] ownAxes, float[] ownHalf, Vector3[] otherAxes, float[] otherHalf)
        {
            float ownRadius = 0f;
            float otherRadius = 0f;
            for (int k = 0; k < 3; k++)
            {
                ownRadius += ownHalf[k] * Mathf.Abs(Vector3.Dot(ownAxes[k], axis));
                otherRadius += otherHalf[k] * Mathf.Abs(Vector3.Dot(otherAxes[k], axis));
            }

            float centerDistance = Mathf.Abs(Vector3.Dot(offset, axis));
            return centerDistance >= ownRadius + otherRadius - SeparationTolerance;
        }
    }
}
