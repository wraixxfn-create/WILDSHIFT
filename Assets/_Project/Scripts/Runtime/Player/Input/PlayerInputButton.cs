namespace Wildshift.Player.Input
{
    /// <summary>
    /// Discrete gameplay actions exposed by <see cref="PlayerInputReader"/>.
    /// The enum names intentionally match the button action names in PlayerInputActions.inputactions.
    /// </summary>
    public enum PlayerInputButton
    {
        Sprint = 0,
        Jump = 1,
        Interact = 2,
        LightAttack = 3,
        HeavyAttack = 4,
        Dodge = 5,
        AbilityPrimary = 6,
        AbilitySecondary = 7,
        AbilityTertiary = 8,
        Pause = 9
    }
}
