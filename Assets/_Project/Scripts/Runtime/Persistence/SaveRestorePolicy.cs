using System;
using System.Collections.Generic;
using UnityEngine;
using Wildshift.World.Clock;
using Wildshift.World.Events;
using Wildshift.World.Regions;

namespace Wildshift.Persistence
{
    /// <summary>How a save's region claim relates to the region registry of this build.</summary>
    public enum SavedRegionResolution
    {
        /// <summary>The save says the player was outside every region, so there is nothing to check.</summary>
        None = 0,

        /// <summary>The saved region ID is registered in this build's region catalog.</summary>
        Registered = 1,

        /// <summary>
        /// The saved region ID is well formed but not registered in this build (for example, a region that was
        /// renamed or removed). It is ignored, and the region is taken from the restored position instead.
        /// </summary>
        Unregistered = 2,
    }

    /// <summary>
    /// Where the player is placed after a load, and why. When <see cref="IsRecovered"/> is true, the saved
    /// position was not used and <see cref="Position"/> and <see cref="Orientation"/> hold the safe spawn point.
    /// </summary>
    public readonly struct SavedPlayerPlacement
    {
        /// <summary>Creates a placement. A null recovery reason means the saved pose was used unchanged.</summary>
        public SavedPlayerPlacement(Vector3 position, Quaternion orientation, string recoveryReason)
        {
            Position = position;
            Orientation = orientation;
            RecoveryReason = recoveryReason;
        }

        /// <summary>World position to place the player at.</summary>
        public Vector3 Position { get; }

        /// <summary>World orientation to place the player with.</summary>
        public Quaternion Orientation { get; }

        /// <summary>Why the saved pose was replaced, or null when it was used.</summary>
        public string RecoveryReason { get; }

        /// <summary>True when the saved pose was replaced by the safe spawn point.</summary>
        public bool IsRecovered => RecoveryReason != null;
    }

    /// <summary>
    /// Pure decisions made while restoring a save into a live session. Nothing here touches Unity objects,
    /// the disk, or the physics scene. The caller supplies any scene knowledge (such as an obstruction test),
    /// which keeps these rules testable in Edit Mode.
    /// </summary>
    public static class SaveRestorePolicy
    {
        /// <summary>
        /// Checks the saved region claim against the registry of this build. The position stays authoritative;
        /// this only says whether the claim names a region the build knows about.
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when registry is null.</exception>
        public static SavedRegionResolution ResolveRegion(string savedRegionId, WorldRegionRegistry registry, out string error)
        {
            if (registry == null)
            {
                throw new ArgumentNullException(nameof(registry));
            }

            if (savedRegionId == null)
            {
                error = null;
                return SavedRegionResolution.None;
            }

            if (registry.TryGetDefinition(savedRegionId, out _, out string lookupError))
            {
                error = null;
                return SavedRegionResolution.Registered;
            }

            error = lookupError;
            return SavedRegionResolution.Unregistered;
        }

        /// <summary>
        /// Decides where the player goes. A saved pose is replaced by the safe spawn point when it is not a
        /// valid save value, when it is below <paramref name="minimumValidHeight"/> (the player fell out of the
        /// world), or when <paramref name="isObstructed"/> reports that the player's body would overlap solid
        /// geometry there. Otherwise the saved pose is used unchanged, apart from normalising the orientation.
        /// </summary>
        /// <param name="saved">The player block from the save.</param>
        /// <param name="safePosition">Position of the safe spawn point in the prototype region.</param>
        /// <param name="safeOrientation">Orientation of the safe spawn point.</param>
        /// <param name="minimumValidHeight">Lowest world height that counts as inside the playable world.</param>
        /// <param name="isObstructed">Returns true when the player's body would overlap solid geometry at a position.</param>
        /// <exception cref="ArgumentNullException">Thrown when saved or isObstructed is null.</exception>
        public static SavedPlayerPlacement ResolvePlayerPlacement(
            PlayerSaveData saved,
            Vector3 safePosition,
            Quaternion safeOrientation,
            float minimumValidHeight,
            Func<Vector3, bool> isObstructed)
        {
            if (saved == null)
            {
                throw new ArgumentNullException(nameof(saved));
            }

            if (isObstructed == null)
            {
                throw new ArgumentNullException(nameof(isObstructed));
            }

            if (!saved.TryValidate(out string validationError))
            {
                return Safe(safePosition, safeOrientation, "the saved pose is not valid (" + validationError + ")");
            }

            if (saved.Position.y < minimumValidHeight)
            {
                return Safe(safePosition, safeOrientation,
                    "the saved position is below the playable world (height " +
                    saved.Position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) + " m)");
            }

            if (isObstructed(saved.Position))
            {
                return Safe(safePosition, safeOrientation, "the saved position overlaps solid geometry");
            }

            return new SavedPlayerPlacement(saved.Position, Quaternion.Normalize(saved.Orientation), null);
        }

        /// <summary>
        /// Chooses the world clock time to restore. It is the saved clock time, but never earlier than the newest
        /// saved event. That way, the first event recorded after a load cannot predate the restored history, even
        /// if the saved clock value was rounded when it was written.
        /// </summary>
        public static long ResolveElapsedWorldTicks(GameSaveData save)
        {
            if (save == null)
            {
                throw new ArgumentNullException(nameof(save));
            }

            long ticks = ToClampedTicks(save.ElapsedWorldTime);
            IReadOnlyList<PlayerActionEvent> events = save.WorldEvents;
            if (events.Count > 0)
            {
                long newestEventTicks = ToClampedTicks(events[events.Count - 1].ElapsedWorldTime);
                if (newestEventTicks > ticks)
                {
                    ticks = newestEventTicks;
                }
            }

            return ticks;
        }

        private static SavedPlayerPlacement Safe(Vector3 safePosition, Quaternion safeOrientation, string reason)
        {
            return new SavedPlayerPlacement(safePosition, Quaternion.Normalize(safeOrientation), reason);
        }

        private static long ToClampedTicks(double seconds)
        {
            if (double.IsNaN(seconds) || seconds <= 0d)
            {
                return 0L;
            }

            // Compare as doubles first: converting an out-of-range double to long is not meaningful.
            double ticks = Math.Round(seconds * WorldClock.TicksPerSecond);
            if (ticks >= (double)WorldClock.MaxElapsedTicks)
            {
                return WorldClock.MaxElapsedTicks;
            }

            return (long)ticks;
        }
    }
}
