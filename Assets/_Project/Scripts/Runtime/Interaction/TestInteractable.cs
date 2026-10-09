using UnityEngine;
using Wildshift.Core.Diagnostics;

namespace Wildshift.Interaction
{
    /// <summary>
    /// Placeholder that exists only to validate the interaction framework end to end. It logs when
    /// successfully interacted with and carries no gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TestInteractable : MonoBehaviour, IInteractable
    {
        [SerializeField, Tooltip("When enabled, the object accepts only its first successful interaction.")]
        private bool _singleUse;

        private int _interactionCount;

        /// <summary>Number of successful interactions since this component was created.</summary>
        public int InteractionCount => _interactionCount;

        /// <inheritdoc />
        public bool CanInteract(GameObject interactor)
        {
            return isActiveAndEnabled && interactor != null && !(_singleUse && _interactionCount > 0);
        }

        /// <inheritdoc />
        public void Interact(GameObject interactor)
        {
            if (!CanInteract(interactor))
            {
                return;
            }

            _interactionCount++;
            WildshiftLog.Info(
                $"Test interaction on '{name}' succeeded from '{interactor.name}' " +
                $"(interaction {_interactionCount}, single use: {_singleUse}).",
                this);
        }
    }
}
