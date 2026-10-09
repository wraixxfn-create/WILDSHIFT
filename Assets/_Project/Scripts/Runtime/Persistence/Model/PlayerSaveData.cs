using System;
using UnityEngine;

namespace Wildshift.Persistence.Model
{
    /// <summary>
    /// Durable record of where the player was standing and which way they faced.
    /// It stores plain numbers only — never a <see cref="Transform"/>, GameObject, or scene path — so
    /// the data stays readable after scenes, prefabs, and hierarchies change.
    /// </summary>
    /// <remarks>
    /// Orientation is a single yaw angle because the prototype character only turns around the world
    /// up axis (see <c>ThirdPersonPlayerMovement</c>); the camera owns its own pitch and is not saved
    /// yet. Add pitch/roll as new, defaulted fields when a system actually owns them.
    /// </remarks>
    [Serializable]
    public sealed class PlayerSaveData
    {
        [SerializeField] private float positionX;
        [SerializeField] private float positionY;
        [SerializeField] private float positionZ;
        [SerializeField] private float yawDegrees;

        /// <summary>Creates an empty record; used by the JSON deserializer and by default state.</summary>
        public PlayerSaveData()
        {
        }

        /// <summary>Creates a record from world-space numbers.</summary>
        public PlayerSaveData(float positionX, float positionY, float positionZ, float yawDegrees)
        {
            this.positionX = positionX;
            this.positionY = positionY;
            this.positionZ = positionZ;
            this.yawDegrees = yawDegrees;
        }

        /// <summary>World-space X position in metres.</summary>
        public float PositionX => positionX;

        /// <summary>World-space Y position in metres.</summary>
        public float PositionY => positionY;

        /// <summary>World-space Z position in metres.</summary>
        public float PositionZ => positionZ;

        /// <summary>Heading around the world up axis, in degrees.</summary>
        public float YawDegrees => yawDegrees;

        /// <summary>World-space position rebuilt as a vector; convenience only, the fields are the format.</summary>
        public Vector3 Position => new Vector3(positionX, positionY, positionZ);

        /// <summary>Yaw-only rotation rebuilt as a quaternion; convenience only, the fields are the format.</summary>
        public Quaternion Rotation => Quaternion.Euler(0f, yawDegrees, 0f);
    }
}
