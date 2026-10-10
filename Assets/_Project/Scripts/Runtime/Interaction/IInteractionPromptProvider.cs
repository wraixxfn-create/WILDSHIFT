namespace Wildshift.Interaction
{
    /// <summary>
    /// Optional contract for an <see cref="IInteractable"/> that can supply the short
    /// interaction prompt text shown while the player aims at it (for example
    /// "Press E to collect sample"). Implement this on the same MonoBehaviour that
    /// implements <see cref="IInteractable"/>; the prompt UI looks for the interface on
    /// the detector's current target. The existing interaction contract is unchanged.
    /// </summary>
    public interface IInteractionPromptProvider
    {
        /// <summary>
        /// Returns the prompt text to display while this interactable is the player's
        /// valid target. Return null or empty to hide the prompt.
        /// </summary>
        string GetInteractionPrompt();
    }
}
