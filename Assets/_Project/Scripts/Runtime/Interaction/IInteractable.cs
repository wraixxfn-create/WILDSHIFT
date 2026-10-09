using UnityEngine;

namespace Wildshift.Interaction
{
    /// <summary>
    /// Common contract for world objects that an interactor, such as the player, can activate.
    /// Implementations own their behaviour and state; aiming, range, and input live in the detector.
    /// </summary>
    /// <remarks>
    /// Implement this on a MonoBehaviour on the interactable's GameObject. The detector uses the component
    /// to reject destroyed or disabled targets. Interactions are not required to be repeatable;
    /// <see cref="CanInteract"/> decides availability at the moment of input.
    /// </remarks>
    public interface IInteractable
    {
        /// <summary>Returns whether <paramref name="interactor"/> may interact with this object right now.</summary>
        bool CanInteract(GameObject interactor);

        /// <summary>Executes the interaction. Callers are expected to have checked <see cref="CanInteract"/>.</summary>
        void Interact(GameObject interactor);
    }
}
