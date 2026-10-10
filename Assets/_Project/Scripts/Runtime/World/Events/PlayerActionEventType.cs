namespace Wildshift.World.Events
{
    /// <summary>
    /// Strongly typed kind of one recorded player action. A future world can respond to patterns of
    /// these actions, so the set is deliberately small and defined: add a value only when a reacting
    /// system defines the term, never as a free-form string. <see cref="None"/> is the unset default
    /// and is rejected by the recorder. Current kinds: region entry, object interaction, violence,
    /// wildlife disturbance, resource extraction, sabotage, settlement assistance, and environment scan.
    /// </summary>
    public enum PlayerActionEventType
    {
        /// <summary>Placeholder for unset or uninitialized event data; never recorded.</summary>
        None = 0,

        /// <summary>The player entered a region.</summary>
        RegionEntered = 1,

        /// <summary>The player activated a world object.</summary>
        ObjectInteraction = 2,

        /// <summary>The player harmed or destroyed a creature, structure, or other target.</summary>
        Violence = 3,

        /// <summary>The player disturbed wildlife without directly harming it.</summary>
        WildlifeDisturbance = 4,

        /// <summary>The player extracted a resource from the world.</summary>
        ResourceExtraction = 5,

        /// <summary>The player damaged, undermined, or disabled a target.</summary>
        Sabotage = 6,

        /// <summary>The player helped a settlement or its members.</summary>
        SettlementAssistance = 7,

        /// <summary>The player inspected a world object with the environmental scanner.</summary>
        EnvironmentScan = 8,
    }
}
