using System;
using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.World.Regions;

namespace Wildshift.Player.Regions
{
    /// <summary>
    /// One change to the player's spatial region association. Region values are stable authored IDs;
    /// null means that the player is not associated with a registered region.
    /// </summary>
    public readonly struct PlayerRegionAssociationChange
    {
        public PlayerRegionAssociationChange(
            WorldRegionQueryStatus previousStatus,
            string previousRegionId,
            WorldRegionQueryStatus currentStatus,
            string currentRegionId)
        {
            PreviousStatus = previousStatus;
            PreviousRegionId = previousRegionId;
            CurrentStatus = currentStatus;
            CurrentRegionId = currentRegionId;
        }

        /// <summary>Lookup status before the change.</summary>
        public WorldRegionQueryStatus PreviousStatus { get; }

        /// <summary>Previous stable region ID, or null.</summary>
        public string PreviousRegionId { get; }

        /// <summary>Lookup status after the change.</summary>
        public WorldRegionQueryStatus CurrentStatus { get; }

        /// <summary>Current stable region ID, or null.</summary>
        public string CurrentRegionId { get; }

        /// <summary>True when the change entered a registered region from outside or unavailable space.</summary>
        public bool EnteredRegion => PreviousRegionId == null && CurrentRegionId != null;

        /// <summary>True when the change left a registered region without entering another one.</summary>
        public bool LeftRegion => PreviousRegionId != null && CurrentRegionId == null;

        /// <summary>True when the change moved directly between two different registered regions.</summary>
        public bool MovedBetweenRegions => PreviousRegionId != null && CurrentRegionId != null &&
                                           !string.Equals(PreviousRegionId, CurrentRegionId, StringComparison.Ordinal);
    }

    /// <summary>
    /// Player-to-world integration service that keeps a stable region association for the player's
    /// current position. Spatial decisions are delegated entirely to <see cref="WorldRegionLocator"/>,
    /// so this component inherits the existing boundary, overlap, priority, and disabled-volume rules.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The locator is queried in <c>LateUpdate</c>, after normal player movement. A query does not count
    /// as an association update: state and <see cref="RegionAssociationChanged"/> change only when the
    /// query status or stable region ID changes, so remaining in one region produces no repeated work
    /// for subscribers and no routine logs.
    /// </para>
    /// <para>
    /// <see cref="TryResolveRegionForAction"/> always performs a fresh query. Therefore a multi-frame
    /// interaction is attributed to the player's position when the successful action is committed, not
    /// where it began. Outside all volumes and unavailable lookup both return null; no fallback or
    /// fabricated ID is used.
    /// </para>
    /// <para>
    /// This service owns only the player's association. It publishes a read-only transition event and
    /// never writes to world state, ecology, factions, or the player-action log.
    /// </para>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PlayerRegionAssociation : MonoBehaviour, IWorldRegionContext
    {
        [SerializeField, Tooltip("Authoritative scene region locator. Required. Boundary and overlap decisions are delegated to it.")]
        private WorldRegionLocator _regionLocator;

        private bool _hasResolved;
        private string _currentRegionId;
        private WorldRegionQueryStatus _currentRegionStatus = WorldRegionQueryStatus.Unavailable;
        private bool _missingLocatorLogged;
        private bool _unavailableLocatorLogged;
        private bool _invalidRegionLogged;
        private bool _actionWithoutRegionLogged;

        /// <summary>
        /// Raised once after each actual status or stable-ID transition. It is not raised on unchanged
        /// per-frame queries. Subscribers are observers only; this service does not mutate their state.
        /// </summary>
        public event Action<PlayerRegionAssociationChange> RegionAssociationChanged;

        /// <inheritdoc />
        public WorldRegionQueryStatus CurrentRegionStatus => _currentRegionStatus;

        /// <inheritdoc />
        public string CurrentRegionId => _currentRegionId;

        /// <summary>True only while the current position resolves to a registered region.</summary>
        public bool HasRegion => _currentRegionStatus == WorldRegionQueryStatus.Found && _currentRegionId != null;

        private void Awake()
        {
            RefreshRegionAssociation();
        }

        private void LateUpdate()
        {
            RefreshRegionAssociation();
        }

        /// <summary>
        /// Resolves the current transform position and updates the cached association only if it changed.
        /// Returns true when this call produced an initial association or a transition. This public seam
        /// also supports explicit refreshes after teleports and deterministic tests.
        /// </summary>
        public bool RefreshRegionAssociation()
        {
            if (_regionLocator == null)
            {
                if (!_missingLocatorLogged)
                {
                    WildshiftLog.Error(
                        $"{nameof(PlayerRegionAssociation)} on '{name}' has no World Region Locator assigned. " +
                        "The player will have no region association until the scene reference is configured.",
                        this);
                    _missingLocatorLogged = true;
                }

                return ApplyAssociation(WorldRegionQueryStatus.Unavailable, null);
            }

            WorldRegionQueryResult result = _regionLocator.FindRegionAt(transform.position);
            if (result.Status == WorldRegionQueryStatus.Unavailable)
            {
                if (!_unavailableLocatorLogged)
                {
                    WildshiftLog.Warning(
                        $"{nameof(PlayerRegionAssociation)} on '{name}' cannot resolve a player region because " +
                        "the assigned World Region Locator is unavailable. Actions will omit region IDs until it is valid.",
                        this);
                    _unavailableLocatorLogged = true;
                }

                _invalidRegionLogged = false;
                return ApplyAssociation(WorldRegionQueryStatus.Unavailable, null);
            }

            _unavailableLocatorLogged = false;
            if (result.Status == WorldRegionQueryStatus.OutsideAllRegions)
            {
                _invalidRegionLogged = false;
                return ApplyAssociation(WorldRegionQueryStatus.OutsideAllRegions, null);
            }

            string stableId = result.RegionId;
            if (!WorldRegionIdRules.TryValidate(stableId, out string idError))
            {
                if (!_invalidRegionLogged)
                {
                    WildshiftLog.Error(
                        $"The World Region Locator returned an invalid stable region ID for player '{name}': {idError} " +
                        "The association has been rejected rather than recording an invalid ID.",
                        this);
                    _invalidRegionLogged = true;
                }

                return ApplyAssociation(WorldRegionQueryStatus.Unavailable, null);
            }

            _invalidRegionLogged = false;
            return ApplyAssociation(WorldRegionQueryStatus.Found, stableId);
        }

        /// <inheritdoc />
        public bool TryResolveRegionForAction(out string regionId)
        {
            RefreshRegionAssociation();
            if (HasRegion)
            {
                regionId = _currentRegionId;
                return true;
            }

            regionId = null;
            if (!_actionWithoutRegionLogged)
            {
                string reason = _currentRegionStatus == WorldRegionQueryStatus.OutsideAllRegions
                    ? "the player is outside all registered region volumes"
                    : "region lookup is unavailable";
                WildshiftLog.Warning(
                    $"A player action on '{name}' has no region association because {reason}. " +
                    "Any event for this action must omit the region ID.",
                    this);
                _actionWithoutRegionLogged = true;
            }

            return false;
        }

        private bool ApplyAssociation(WorldRegionQueryStatus status, string stableId)
        {
            bool changed = !_hasResolved || status != _currentRegionStatus ||
                           !string.Equals(stableId, _currentRegionId, StringComparison.Ordinal);
            if (!changed)
            {
                return false;
            }

            WorldRegionQueryStatus previousStatus = _currentRegionStatus;
            string previousRegionId = _currentRegionId;

            _hasResolved = true;
            _currentRegionStatus = status;
            _currentRegionId = stableId;
            _actionWithoutRegionLogged = false;

            PlayerRegionAssociationChange change = new PlayerRegionAssociationChange(
                previousStatus,
                previousRegionId,
                status,
                stableId);
            RegionAssociationChanged?.Invoke(change);

            WildshiftLog.Verbose(DescribeChange(change), this);
            return true;
        }

        private string DescribeChange(PlayerRegionAssociationChange change)
        {
            if (change.MovedBetweenRegions)
            {
                return $"Player '{name}' moved from region '{change.PreviousRegionId}' to '{change.CurrentRegionId}'.";
            }

            if (change.EnteredRegion)
            {
                return $"Player '{name}' entered region '{change.CurrentRegionId}'.";
            }

            if (change.LeftRegion)
            {
                return $"Player '{name}' left region '{change.PreviousRegionId}'.";
            }

            return $"Player '{name}' region lookup status changed from {change.PreviousStatus} to {change.CurrentStatus}.";
        }
    }
}
