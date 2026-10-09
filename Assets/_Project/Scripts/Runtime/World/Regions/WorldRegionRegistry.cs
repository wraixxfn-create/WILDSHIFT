using System;
using System.Collections.Generic;

namespace Wildshift.World.Regions
{
    /// <summary>
    /// Read-only index from stable region ID to its authored <see cref="RegionDefinition"/>. Lookup by ID is a
    /// dictionary lookup, so it does not depend on asset names, display names, scene hierarchy, or list order.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The registry holds authored definitions only. It never creates runtime state (that is
    /// <see cref="WorldStateService"/>'s job) and never generates IDs.
    /// </para>
    /// <para>
    /// <see cref="TryCreate"/> is all-or-nothing: if any entry is null, has an invalid ID, or duplicates an ID, no
    /// registry is produced. Every problem is reported in one pass so an author can fix them together. A partially
    /// valid catalog is never used, because a silently dropped region would be an invisible gap in the world.
    /// </para>
    /// </remarks>
    public sealed class WorldRegionRegistry
    {
        private readonly Dictionary<string, RegionDefinition> _definitionsById;
        private readonly List<RegionDefinition> _definitionsOrdered;

        private WorldRegionRegistry(Dictionary<string, RegionDefinition> definitionsById)
        {
            _definitionsById = definitionsById;
            _definitionsOrdered = new List<RegionDefinition>(definitionsById.Values);
            _definitionsOrdered.Sort((left, right) => string.CompareOrdinal(left.StableId, right.StableId));
        }

        /// <summary>Number of registered region definitions.</summary>
        public int Count => _definitionsById.Count;

        /// <summary>
        /// Validates <paramref name="definitions"/> and builds a registry when every entry is usable.
        /// Problems are appended to <paramref name="errors"/> as actionable messages; on failure
        /// <paramref name="registry"/> is null. The list passed in is not modified except for appended errors.
        /// </summary>
        public static bool TryCreate(IReadOnlyList<RegionDefinition> definitions, List<string> errors,
            out WorldRegionRegistry registry)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            registry = null;
            if (definitions == null)
            {
                errors.Add("The region list is missing. Create a region catalog list before building a registry.");
                return false;
            }

            int errorsBefore = errors.Count;
            Dictionary<string, RegionDefinition> definitionsById =
                new Dictionary<string, RegionDefinition>(definitions.Count, StringComparer.Ordinal);

            for (int i = 0; i < definitions.Count; i++)
            {
                RegionDefinition definition = definitions[i];
                if (definition == null)
                {
                    errors.Add("Entry " + i + " is empty: no Region Definition is assigned. " +
                               "Assign a Region Definition asset to that entry or remove the entry.");
                    continue;
                }

                if (!WorldRegionIdRules.TryValidate(definition.StableId, out string idError))
                {
                    errors.Add("Region definition '" + definition.name + "' (entry " + i + "): " + idError);
                    continue;
                }

                if (definitionsById.TryGetValue(definition.StableId, out RegionDefinition existing))
                {
                    if (ReferenceEquals(existing, definition))
                    {
                        errors.Add("Region definition '" + definition.name + "' (entry " + i +
                                   ") is listed more than once. Remove the duplicate entry.");
                    }
                    else
                    {
                        errors.Add("Duplicate stable ID '" + definition.StableId + "': '" + existing.name +
                                   "' and '" + definition.name + "' (entry " + i + ") both use it. " +
                                   "Each region needs a unique ID, so change one of them. Renaming an asset does not change its ID.");
                    }

                    continue;
                }

                definitionsById.Add(definition.StableId, definition);
            }

            if (errors.Count != errorsBefore)
            {
                return false;
            }

            registry = new WorldRegionRegistry(definitionsById);
            return true;
        }

        /// <summary>
        /// Resolves a stable ID to its authored definition. Returns false with a descriptive error for a blank ID
        /// or an unknown ID; it never throws for an ordinary lookup failure.
        /// </summary>
        public bool TryGetDefinition(string stableId, out RegionDefinition definition, out string error)
        {
            definition = null;
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Cannot resolve a region with an empty or whitespace stable ID.";
                return false;
            }

            if (_definitionsById.TryGetValue(stableId, out definition))
            {
                error = null;
                return true;
            }

            error = "No region with stable ID '" + stableId + "' is in the region catalog.";
            return false;
        }

        /// <summary>
        /// True only when this exact definition object is the one registered under its stable ID. A different asset
        /// that happens to share the ID is not considered registered, so a stale or copied reference is detected.
        /// </summary>
        public bool IsRegistered(RegionDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return _definitionsById.TryGetValue(definition.StableId, out RegionDefinition registered) &&
                   ReferenceEquals(registered, definition);
        }

        /// <summary>Snapshot of every registered definition, ordered by stable ID (ordinal). The list is a copy.</summary>
        public IReadOnlyList<RegionDefinition> GetAllDefinitions()
        {
            return new List<RegionDefinition>(_definitionsOrdered);
        }
    }
}
