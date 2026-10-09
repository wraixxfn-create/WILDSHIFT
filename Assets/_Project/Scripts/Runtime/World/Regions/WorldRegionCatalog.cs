using System.Collections.Generic;
using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.World.Regions
{
    /// <summary>
    /// Designer-authored list of the region definitions that make up one world (or one prototype test world).
    /// It is configuration only: it holds references to <see cref="RegionDefinition"/> assets and no runtime state.
    /// Validation runs when the asset is edited and can be run from the context menu; the
    /// <see cref="WorldRegionLocator"/> validates it again at runtime.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldRegionCatalog", menuName = "Wildshift/World/Region Catalog")]
    public sealed class WorldRegionCatalog : ScriptableObject
    {
        [SerializeField, Tooltip("Every region definition that belongs to this world. Each stable ID must be unique " +
                                 "within this list. Entries must not be empty.")]
        private List<RegionDefinition> _regions = new List<RegionDefinition>();

        /// <summary>The authored entries exactly as listed, including any invalid ones. Read-only to callers.</summary>
        public IReadOnlyList<RegionDefinition> Regions => _regions;

        /// <summary>
        /// Builds a registry from this catalog. On failure, every problem is appended to <paramref name="errors"/>
        /// with this catalog's name and <paramref name="registry"/> is null.
        /// </summary>
        public bool TryCreateRegistry(List<string> errors, out WorldRegionRegistry registry)
        {
            if (errors == null)
            {
                throw new System.ArgumentNullException(nameof(errors));
            }

            List<string> catalogErrors = new List<string>();
            bool valid = WorldRegionRegistry.TryCreate(_regions, catalogErrors, out registry);
            foreach (string error in catalogErrors)
            {
                errors.Add("Region catalog '" + name + "': " + error);
            }

            return valid;
        }

        private void OnValidate()
        {
            List<string> errors = new List<string>();
            TryCreateRegistry(errors, out _);
            foreach (string error in errors)
            {
                WildshiftLog.Error(error, this);
            }
        }

        [ContextMenu("Validate Region Catalog")]
        private void ValidateFromContextMenu()
        {
            List<string> errors = new List<string>();
            if (!TryCreateRegistry(errors, out WorldRegionRegistry registry))
            {
                foreach (string error in errors)
                {
                    WildshiftLog.Error(error, this);
                }

                return;
            }

            WildshiftLog.Info($"Region catalog '{name}' is valid with {registry.Count} region(s).", this);
        }
    }
}
