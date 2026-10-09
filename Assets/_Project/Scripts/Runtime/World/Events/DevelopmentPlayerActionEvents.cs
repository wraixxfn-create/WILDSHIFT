namespace Wildshift.World.Events
{
    /// <summary>
    /// Development-only helpers that build a few well-formed example events so the event log can be
    /// exercised before real gameplay exists. The helpers perform no gameplay: entering the test
    /// region, interacting with the test object, and the simulated resource extraction only produce
    /// records for the recorder. No resource extraction, ecology, or faction behaviour is implemented
    /// here, and these helpers must not be wired into shipping gameplay paths.
    /// </summary>
    public static class DevelopmentPlayerActionEvents
    {
        /// <summary>Stable ID of the development-only test region; a placeholder, not a real region.</summary>
        public const string TestRegionId = "nacre/dev/test-region";

        /// <summary>Stable ID of the development-only test object; a placeholder, not a real object.</summary>
        public const string TestObjectId = "nacre/dev/test-object";

        /// <summary>Parameter ID carrying the simulated number of extracted units.</summary>
        public const string ExtractedUnitsParameterId = "units-extracted";

        /// <summary>Builds a development-only "entered the test region" event.</summary>
        public static PlayerActionEvent CreateTestRegionEntry(double elapsedWorldTime)
        {
            return new PlayerActionEvent(
                PlayerActionEvent.NewId(),
                PlayerActionEventType.RegionEntered,
                elapsedWorldTime,
                regionId: TestRegionId);
        }

        /// <summary>Builds a development-only "interacted with the test object" event.</summary>
        public static PlayerActionEvent CreateTestObjectInteraction(double elapsedWorldTime)
        {
            return new PlayerActionEvent(
                PlayerActionEvent.NewId(),
                PlayerActionEventType.ObjectInteraction,
                elapsedWorldTime,
                targetId: TestObjectId);
        }

        /// <summary>
        /// Builds a development-only simulated resource extraction event with an optional magnitude
        /// and a matching <see cref="ExtractedUnitsParameterId"/> parameter. This is a test record
        /// only; no extraction gameplay exists behind it.
        /// </summary>
        public static PlayerActionEvent CreateSimulatedResourceExtraction(double elapsedWorldTime, float extractedUnits = 3f)
        {
            return new PlayerActionEvent(
                PlayerActionEvent.NewId(),
                PlayerActionEventType.ResourceExtraction,
                elapsedWorldTime,
                regionId: TestRegionId,
                targetId: TestObjectId,
                magnitude: extractedUnits,
                parameters: new[]
                {
                    new PlayerActionEventParameter(ExtractedUnitsParameterId, extractedUnits),
                });
        }
    }
}
