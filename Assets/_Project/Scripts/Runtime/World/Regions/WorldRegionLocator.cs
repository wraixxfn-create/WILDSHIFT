using System;
using System.Collections.Generic;
using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.World.Regions
{
    /// <summary>
    /// Scene component that answers "which authored region contains this world position?" It owns the list of
    /// <see cref="WorldRegionVolume"/> components for its part of the world and checks them against a
    /// <see cref="WorldRegionCatalog"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>Query rules</b> (see <c>docs/world-regions.md</c>):</para>
    /// <list type="bullet">
    /// <item><description>A position inside no active volume returns <see cref="WorldRegionQueryStatus.OutsideAllRegions"/>.
    /// This is a normal result, not an error, and it does not log.</description></item>
    /// <item><description>A position inside volumes of one region returns that region.</description></item>
    /// <item><description>A position inside volumes of several regions returns the volume with the highest
    /// <c>Priority</c>. Equal priorities are broken by the lower stable ID in ordinal order. The result is the same for any
    /// hierarchy or list order. Overlap is reported through <see cref="WorldRegionQueryResult.IsOverlapping"/>.</description></item>
    /// <item><description>Several volumes of the same region count as one region.</description></item>
    /// <item><description>Disabled volumes and volumes on inactive GameObjects are ignored.</description></item>
    /// <item><description>If the setup is invalid, every query returns <see cref="WorldRegionQueryStatus.Unavailable"/>. The
    /// setup is validated once when initialized (Awake), and the errors are logged to the Console. Queries never log.</description></item>
    /// </list>
    /// <para>
    /// The locator is scene-owned and is not a global service. It holds no runtime state for regions; it only answers
    /// spatial queries. Ecological or other changing values belong in <see cref="WorldStateService"/>.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class WorldRegionLocator : MonoBehaviour
    {
        [SerializeField, Tooltip("The catalog that lists every region this locator may report. Required.")]
        private WorldRegionCatalog _catalog;

        [SerializeField, Tooltip("Every WorldRegionVolume that belongs to this locator. Volumes placed under this object " +
                                 "but missing from this list are reported as errors.")]
        private List<WorldRegionVolume> _volumes = new List<WorldRegionVolume>();

        private readonly List<string> _validationErrors = new List<string>();
        private readonly List<string> _validationWarnings = new List<string>();
        private readonly List<WorldRegionVolume> _matches = new List<WorldRegionVolume>();
        private WorldRegionRegistry _registry;
        private bool _initialized;

        /// <summary>True when the last validation found no errors, so queries return answers rather than Unavailable.</summary>
        public bool IsAvailable => _initialized && _registry != null;

        /// <summary>Errors from the last validation. Empty when the setup is valid.</summary>
        public IReadOnlyList<string> ValidationErrors => _validationErrors;

        /// <summary>Warnings from the last validation, such as equal-priority overlaps. They do not block queries.</summary>
        public IReadOnlyList<string> ValidationWarnings => _validationWarnings;

        /// <summary>
        /// Validates the setup, builds the lookup index, and logs any errors and warnings to the Console. Safe to call
        /// again after the setup changes; it rebuilds the index each time. Returns true when the setup is valid.
        /// </summary>
        public bool Initialize()
        {
            _validationErrors.Clear();
            _validationWarnings.Clear();
            bool valid = WorldRegionSetupValidator.Validate(_catalog, _volumes, transform,
                _validationErrors, _validationWarnings, out WorldRegionRegistry registry);

            _registry = valid ? registry : null;
            _initialized = true;

            LogValidationMessages();
            return valid;
        }

        /// <summary>
        /// Resolves a stable ID to its authored definition through the catalog. Returns false with an error when the
        /// locator is unavailable or the ID is unknown.
        /// </summary>
        public bool TryGetDefinition(string stableId, out RegionDefinition definition, out string error)
        {
            EnsureInitialized();
            if (!IsAvailable)
            {
                definition = null;
                error = "The region locator is unavailable because its setup is invalid. See the Console and ValidationErrors.";
                return false;
            }

            return _registry.TryGetDefinition(stableId, out definition, out error);
        }

        /// <summary>
        /// Returns the region that contains <paramref name="worldPosition"/>, applying the rules in the class remarks.
        /// This method does not allocate. It is intended for player and simulation code to call from Update or later.
        /// </summary>
        public WorldRegionQueryResult FindRegionAt(Vector3 worldPosition)
        {
            EnsureInitialized();
            if (!IsAvailable)
            {
                return new WorldRegionQueryResult(WorldRegionQueryStatus.Unavailable, null, 0);
            }

            _matches.Clear();
            WorldRegionVolume best = null;
            for (int i = 0; i < _volumes.Count; i++)
            {
                WorldRegionVolume volume = _volumes[i];
                if (volume == null || !volume.isActiveAndEnabled || !volume.GetWorldBox().Contains(worldPosition))
                {
                    continue;
                }

                _matches.Add(volume);
                if (best == null || IsPreferred(volume, best))
                {
                    best = volume;
                }
            }

            if (best == null)
            {
                return new WorldRegionQueryResult(WorldRegionQueryStatus.OutsideAllRegions, null, 0);
            }

            return new WorldRegionQueryResult(WorldRegionQueryStatus.Found, best, CountDistinctRegions(_matches));
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnValidate()
        {
            // Editor feedback only: validate the current inspector values without changing the runtime index.
            List<string> errors = new List<string>();
            List<string> warnings = new List<string>();
            WorldRegionSetupValidator.Validate(_catalog, _volumes, transform, errors, warnings, out _);
            Log(errors, warnings);
        }

        [ContextMenu("Validate Region Setup")]
        private void ValidateFromContextMenu()
        {
            bool valid = Initialize();
            if (valid)
            {
                WildshiftLog.Info($"World region setup '{name}' is valid.", this);
            }
        }

        private void EnsureInitialized()
        {
            if (!_initialized)
            {
                Initialize();
            }
        }

        private void LogValidationMessages()
        {
            Log(_validationErrors, _validationWarnings);
        }

        private void Log(IReadOnlyList<string> errors, IReadOnlyList<string> warnings)
        {
            foreach (string error in errors)
            {
                WildshiftLog.Error("World region locator '" + name + "': " + error, this);
            }

            foreach (string warning in warnings)
            {
                WildshiftLog.Warning("World region locator '" + name + "': " + warning, this);
            }
        }

        private static bool IsPreferred(WorldRegionVolume candidate, WorldRegionVolume current)
        {
            if (candidate.Priority != current.Priority)
            {
                return candidate.Priority > current.Priority;
            }

            return string.CompareOrdinal(candidate.StableId, current.StableId) < 0;
        }

        private static int CountDistinctRegions(List<WorldRegionVolume> matches)
        {
            int distinct = 0;
            for (int i = 0; i < matches.Count; i++)
            {
                bool seenEarlier = false;
                for (int j = 0; j < i; j++)
                {
                    if (string.Equals(matches[j].StableId, matches[i].StableId, StringComparison.Ordinal))
                    {
                        seenEarlier = true;
                        break;
                    }
                }

                if (!seenEarlier)
                {
                    distinct++;
                }
            }

            return distinct;
        }
    }
}
