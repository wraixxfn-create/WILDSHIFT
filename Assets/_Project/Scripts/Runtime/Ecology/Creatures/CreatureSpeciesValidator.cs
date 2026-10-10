using System;
using System.Collections.Generic;
using System.Globalization;

namespace Wildshift.Ecology.Creatures
{
    /// <summary>
    /// Validates an authored <see cref="CreatureSpeciesDefinition"/> and returns actionable messages.
    /// It is pure logic with no console output and no Unity state, so the same rules run from the asset's
    /// context menu, from a creature's initialization, and from Edit Mode tests.
    /// </summary>
    /// <remarks>
    /// <para><b>Errors</b> make the species unusable and stop a <see cref="CreatureBehaviorController"/>
    /// from running. They cover: a missing asset; an invalid or blank stable ID or display name; any
    /// non-finite number; a non-positive speed, radius, duration, or interval; a negative slope limit,
    /// perception height, actor speed, or idle minimum; an idle maximum below the idle minimum; a flee
    /// speed that is not faster than the wander speed; a slope limit at or above 90 degrees; and a flee
    /// radius larger than the notice radius, which would make the creature flee from things it has not
    /// noticed.</para>
    /// <para><b>Warnings</b> do not stop the creature from running. They report authoring that is legal
    /// but usually a mistake: no lore text, a startle radius smaller than the flee radius (so a sudden
    /// disturbance carries less far than an approaching actor), and a perception interval long enough
    /// that fast actors are noticed late.</para>
    /// <para>Rules are deliberately about <i>relationships</i> between values as well as individual
    /// values, because a species whose numbers are each legal but mutually contradictory — fleeing
    /// slower than it grazes, or never leaving Alert — is exactly the authoring mistake a range check
    /// alone would miss.</para>
    /// </remarks>
    public static class CreatureSpeciesValidator
    {
        /// <summary>Perception intervals above this are reported, because a fast actor is then noticed late.</summary>
        public const float LatePerceptionWarningSeconds = 0.5f;

        /// <summary>
        /// Validates one species definition. Messages are appended to <paramref name="errors"/> and
        /// <paramref name="warnings"/>; both lists are required and neither is cleared here, so a caller can
        /// aggregate several species.
        /// </summary>
        /// <returns>True when no errors were added by this call.</returns>
        public static bool Validate(CreatureSpeciesDefinition species, List<string> errors, List<string> warnings)
        {
            if (errors == null)
            {
                throw new ArgumentNullException(nameof(errors));
            }

            if (warnings == null)
            {
                throw new ArgumentNullException(nameof(warnings));
            }

            int errorsBefore = errors.Count;

            if (species == null)
            {
                errors.Add("No Species Definition is assigned. Assign a CreatureSpeciesDefinition asset, or create " +
                           "one with Assets > Create > Wildshift > Ecology > Creature Species Definition.");
                return false;
            }

            ValidateIdentity(species, errors, warnings);
            ValidateMovement(species, errors);
            ValidateDetection(species, errors, warnings);
            ValidateTiming(species, errors);

            return errors.Count == errorsBefore;
        }

        private static void ValidateIdentity(CreatureSpeciesDefinition species, List<string> errors, List<string> warnings)
        {
            if (!CreatureSpeciesIdRules.TryValidate(species.StableId, out string idError))
            {
                errors.Add(idError);
            }

            if (string.IsNullOrWhiteSpace(species.DisplayName))
            {
                errors.Add("The Display Name is empty. Enter the short label the species is known by, for example " +
                           "'Siltveil Grazer'. Logs and UI use it; the Stable Id stays the identity.");
            }

            if (string.IsNullOrWhiteSpace(species.LoreDescription))
            {
                warnings.Add("The Lore Description is empty. Two or three sentences about what the creature is and how " +
                             "it behaves keep the species understandable to the next person tuning it.");
            }

            if (!Enum.IsDefined(typeof(CreatureSizeClass), species.SizeClass))
            {
                errors.Add("Size Class '" + (int)species.SizeClass + "' is not a defined CreatureSizeClass value.");
            }
        }

        private static void ValidateMovement(CreatureSpeciesDefinition species, List<string> errors)
        {
            RequireFiniteAndPositive(species.WanderSpeedMetresPerSecond, "Wander Speed", errors);
            RequireFiniteAndPositive(species.FleeSpeedMetresPerSecond, "Flee Speed", errors);
            RequireFiniteAndPositive(species.AccelerationMetresPerSecondSquared, "Acceleration", errors);
            RequireFiniteAndPositive(species.TurnRateDegreesPerSecond, "Turn Rate", errors);
            RequireFiniteAndPositive(species.WanderRadiusMetres, "Wander Radius", errors);

            if (!float.IsFinite(species.MaximumSlopeDegrees) || species.MaximumSlopeDegrees <= 0f)
            {
                errors.Add("Maximum Slope Degrees must be a finite number greater than zero; it is currently " +
                           Describe(species.MaximumSlopeDegrees) + ".");
            }
            else if (species.MaximumSlopeDegrees >= 90f)
            {
                errors.Add("Maximum Slope Degrees must stay below 90 so the CharacterController remains valid; it is " +
                           "currently " + Describe(species.MaximumSlopeDegrees) + ".");
            }

            // Contradictory pair: a species that cannot outrun its own graze speed can never escape a threat.
            if (float.IsFinite(species.WanderSpeedMetresPerSecond) &&
                float.IsFinite(species.FleeSpeedMetresPerSecond) &&
                species.WanderSpeedMetresPerSecond > 0f &&
                species.FleeSpeedMetresPerSecond <= species.WanderSpeedMetresPerSecond)
            {
                errors.Add("Flee Speed (" + Describe(species.FleeSpeedMetresPerSecond) + " m/s) must be greater than " +
                           "Wander Speed (" + Describe(species.WanderSpeedMetresPerSecond) + " m/s), otherwise fleeing " +
                           "makes no distance and the creature is caught immediately.");
            }
        }

        private static void ValidateDetection(CreatureSpeciesDefinition species, List<string> errors, List<string> warnings)
        {
            RequireFiniteAndPositive(species.NoticeRadiusMetres, "Notice Radius", errors);
            RequireFiniteAndPositive(species.FleeRadiusMetres, "Flee Radius", errors);
            RequireFiniteAndPositive(species.StartleRadiusMetres, "Startle Radius", errors);
            RequireFiniteAndPositive(species.PerceptionIntervalSeconds, "Perception Interval", errors);

            if (!float.IsFinite(species.MinimumActorSpeedMetresPerSecond) || species.MinimumActorSpeedMetresPerSecond < 0f)
            {
                errors.Add("Minimum Actor Speed must be a finite number of zero or more; it is currently " +
                           Describe(species.MinimumActorSpeedMetresPerSecond) + " m/s.");
            }

            if (!float.IsFinite(species.PerceptionHeightOffsetMetres) || species.PerceptionHeightOffsetMetres < 0f)
            {
                errors.Add("Perception Height Offset must be a finite number of zero or more; it is currently " +
                           Describe(species.PerceptionHeightOffsetMetres) + " m.");
            }

            // Contradictory pair: fleeing from something it has not noticed has no readable cause for the player.
            if (float.IsFinite(species.FleeRadiusMetres) &&
                float.IsFinite(species.NoticeRadiusMetres) &&
                species.NoticeRadiusMetres > 0f &&
                species.FleeRadiusMetres > species.NoticeRadiusMetres)
            {
                errors.Add("Flee Radius (" + Describe(species.FleeRadiusMetres) + " m) must not be greater than Notice " +
                           "Radius (" + Describe(species.NoticeRadiusMetres) + " m), otherwise the creature flees from " +
                           "stimuli it never noticed. Raise the Notice Radius or lower the Flee Radius.");
            }

            if (float.IsFinite(species.StartleRadiusMetres) &&
                float.IsFinite(species.FleeRadiusMetres) &&
                species.FleeRadiusMetres > 0f &&
                species.StartleRadiusMetres < species.FleeRadiusMetres)
            {
                warnings.Add("Startle Radius (" + Describe(species.StartleRadiusMetres) + " m) is smaller than Flee " +
                             "Radius (" + Describe(species.FleeRadiusMetres) + " m), so a sudden disturbance carries " +
                             "less far than an approaching actor. That is legal, but it usually reads as the creature " +
                             "ignoring a loud event.");
            }

            if (float.IsFinite(species.PerceptionIntervalSeconds) &&
                species.PerceptionIntervalSeconds > LatePerceptionWarningSeconds)
            {
                warnings.Add("Perception Interval is " + Describe(species.PerceptionIntervalSeconds) + " s, so a fast " +
                             "actor can be up to that long inside the Notice Radius before it is noticed. Use " +
                             Describe(LatePerceptionWarningSeconds) + " s or less unless the species is meant to react slowly.");
            }
        }

        private static void ValidateTiming(CreatureSpeciesDefinition species, List<string> errors)
        {
            RequireFiniteAndPositive(species.IdleMinSeconds, "Idle Min Seconds", errors);
            RequireFiniteAndPositive(species.CalmDownSeconds, "Calm Down Seconds", errors);
            RequireFiniteAndPositive(species.FleeDurationSeconds, "Flee Duration Seconds", errors);

            if (!float.IsFinite(species.AlertHoldSeconds) || species.AlertHoldSeconds < 0f)
            {
                errors.Add("Alert Hold Seconds must be a finite number of zero or more; it is currently " +
                           Describe(species.AlertHoldSeconds) + " s.");
            }

            if (!float.IsFinite(species.IdleMaxSeconds))
            {
                errors.Add("Idle Max Seconds must be a finite number; it is currently " +
                           Describe(species.IdleMaxSeconds) + " s.");
            }
            else if (float.IsFinite(species.IdleMinSeconds) &&
                     species.IdleMinSeconds > 0f &&
                     species.IdleMaxSeconds < species.IdleMinSeconds)
            {
                // Contradictory pair: the random idle interval would be an empty range.
                errors.Add("Idle Max Seconds (" + Describe(species.IdleMaxSeconds) + " s) must not be less than Idle Min " +
                           "Seconds (" + Describe(species.IdleMinSeconds) + " s). The idle interval is drawn between the two.");
            }
        }

        private static void RequireFiniteAndPositive(float value, string label, List<string> errors)
        {
            if (!float.IsFinite(value) || value <= 0f)
            {
                errors.Add(label + " must be a finite number greater than zero; it is currently " + Describe(value) + ".");
            }
        }

        private static string Describe(float value)
        {
            if (float.IsNaN(value))
            {
                return "NaN";
            }

            if (float.IsPositiveInfinity(value))
            {
                return "Infinity";
            }

            if (float.IsNegativeInfinity(value))
            {
                return "-Infinity";
            }

            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
