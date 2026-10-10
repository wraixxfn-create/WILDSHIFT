using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wildshift.World.Regions
{
    /// <summary>
    /// Validates a region setup (a catalog, the volumes listed by a locator, and the volumes under the locator)
    /// and returns actionable messages. It is pure logic with no console output, so the same rules run in the
    /// Inspector, at runtime initialization, and in tests.
    /// </summary>
    /// <remarks>
    /// <para><b>Errors</b> make the setup invalid and the locator unavailable. They cover: a missing catalog; an
    /// invalid or duplicate catalog entry; a volume listed twice; a missing, invalid, or uncatalogued region
    /// definition; invalid volume bounds; and a volume under the locator that is not in its list (it would otherwise
    /// be silently ignored).</para>
    /// <para><b>Warnings</b> do not make the setup invalid. They report an empty entry in the Volumes list, and two
    /// volumes of different regions that overlap with equal priority. Those positions are still resolved
    /// deterministically by stable ID order, but that is usually not what the author intended.</para>
    /// <para>An empty entry is only a warning because it ignores nothing that exists: queries skip it, and a volume
    /// that is still under the locator but missing from the list is reported as an error. A stale row (left behind by
    /// deleting a volume, or by adding a row in the Inspector and never filling it in) is clutter to remove, not a
    /// reason to switch region lookup off for the whole scene.</para>
    /// </remarks>
    public static class WorldRegionSetupValidator
    {
        /// <summary>
        /// Validates the setup. Messages are appended to <paramref name="errors"/> and <paramref name="warnings"/>.
        /// Returns true when there are no errors, and then sets <paramref name="registry"/>; otherwise sets it to null.
        /// </summary>
        /// <param name="catalog">The region catalog. Null is an error.</param>
        /// <param name="volumes">The volumes listed for the locator. Null is treated as an error.</param>
        /// <param name="owner">The locator's transform, used to find volumes under it. May be null to skip that check.</param>
        public static bool Validate(WorldRegionCatalog catalog, IReadOnlyList<WorldRegionVolume> volumes, Transform owner,
            List<string> errors, List<string> warnings, out WorldRegionRegistry registry)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            registry = null;
            int errorsBefore = errors.Count;

            WorldRegionRegistry catalogRegistry = null;
            if (catalog == null)
            {
                errors.Add("No Region Catalog is assigned. Assign a WorldRegionCatalog asset so region IDs can be resolved.");
            }
            else
            {
                catalog.TryCreateRegistry(errors, out catalogRegistry);
            }

            if (volumes == null)
            {
                errors.Add("The Volumes list is missing. Add the WorldRegionVolume components that belong to this locator.");
                return false;
            }

            for (int i = 0; i < volumes.Count; i++)
            {
                ValidateListedVolume(volumes, i, catalogRegistry, errors, warnings);
            }

            if (owner != null)
            {
                WorldRegionVolume[] volumesUnderOwner = owner.GetComponentsInChildren<WorldRegionVolume>(true);
                foreach (WorldRegionVolume child in volumesUnderOwner)
                {
                    if (IndexOf(volumes, child) < 0)
                    {
                        errors.Add("Volume '" + child.name + "' is under '" + owner.name + "' but is not in its Volumes list, " +
                                   "so it would be ignored. Add it to the list or move it out of this locator.");
                    }
                }
            }

            AddOverlapWarnings(volumes, warnings);

            if (errors.Count != errorsBefore)
            {
                return false;
            }

            registry = catalogRegistry;
            return registry != null;
        }

        private static void ValidateListedVolume(IReadOnlyList<WorldRegionVolume> volumes, int index,
            WorldRegionRegistry catalogRegistry, List<string> errors, List<string> warnings)
        {
            WorldRegionVolume volume = volumes[index];
            if (volume == null)
            {
                // Entry numbers are zero-based and match the "Element N" labels in the Inspector list.
                warnings.Add("Volumes entry " + index + " is empty (Element " + index + " in the Inspector) and is ignored. " +
                             "Assign a WorldRegionVolume or remove the entry; the component menu command " +
                             "'Remove Empty Volume Entries' removes every empty entry.");
                return;
            }

            string label = "Volume '" + volume.name + "' (entry " + index + ")";
            if (IndexOf(volumes, volume) != index)
            {
                errors.Add(label + " is listed more than once. Remove the duplicate entry.");
            }

            if (!volume.TryValidateBounds(out string boundsError))
            {
                errors.Add(label + ": " + boundsError);
            }

            RegionDefinition definition = volume.Definition;
            if (definition == null)
            {
                errors.Add(label + " has no Region Definition assigned. Assign a RegionDefinition asset to the volume.");
                return;
            }

            if (!WorldRegionIdRules.TryValidate(definition.StableId, out string idError))
            {
                errors.Add(label + " references region definition '" + definition.name + "', which has an invalid stable ID: " +
                           idError);
                return;
            }

            // When the catalog itself is invalid there is no registry to check against; its own errors are already reported.
            if (catalogRegistry != null && !catalogRegistry.IsRegistered(definition))
            {
                errors.Add(label + " references region '" + definition.StableId + "' (asset '" + definition.name +
                           "'), which is not in the Region Catalog. Add that exact asset to the catalog, or point the volume " +
                           "at a region that is listed there.");
            }
        }

        private static void AddOverlapWarnings(IReadOnlyList<WorldRegionVolume> volumes, List<string> warnings)
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                WorldRegionVolume first = volumes[i];
                if (!IsUsableForOverlapCheck(first))
                {
                    continue;
                }

                for (int j = i + 1; j < volumes.Count; j++)
                {
                    WorldRegionVolume second = volumes[j];
                    if (!IsUsableForOverlapCheck(second) ||
                        first.Priority != second.Priority ||
                        string.Equals(first.StableId, second.StableId, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (!first.GetWorldBox().Overlaps(second.GetWorldBox()))
                    {
                        continue;
                    }

                    string winner = string.CompareOrdinal(first.StableId, second.StableId) <= 0
                        ? first.StableId
                        : second.StableId;
                    warnings.Add("Volumes '" + first.name + "' (region '" + first.StableId + "') and '" + second.name +
                                 "' (region '" + second.StableId + "') overlap at priority " + first.Priority +
                                 ". Positions inside both resolve to '" + winner + "' by stable ID order. Give one volume a " +
                                 "higher priority, or shrink the volumes so they do not overlap.");
                }
            }
        }

        private static bool IsUsableForOverlapCheck(WorldRegionVolume volume)
        {
            return volume != null && volume.Definition != null && volume.TryValidateBounds(out _);
        }

        private static int IndexOf(IReadOnlyList<WorldRegionVolume> volumes, WorldRegionVolume target)
        {
            for (int i = 0; i < volumes.Count; i++)
            {
                if (ReferenceEquals(volumes[i], target))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
