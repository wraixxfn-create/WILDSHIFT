using UnityEngine;
using Wildshift.Interaction;

namespace Wildshift.Tests
{
    /// <summary>Test double that records interaction calls for interaction framework tests.</summary>
    public sealed class RecordingInteractable : MonoBehaviour, IInteractable
    {
        public bool Available = true;
        public int InteractCount;
        public GameObject LastInteractor;

        public bool CanInteract(GameObject interactor)
        {
            return Available && interactor != null;
        }

        public void Interact(GameObject interactor)
        {
            InteractCount++;
            LastInteractor = interactor;
        }
    }
}
