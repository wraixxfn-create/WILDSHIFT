using UnityEngine;
using Wildshift.Player.Interaction;

namespace Wildshift.UI
{
    /// <summary>
    /// Temporary, IMGUI-based interaction prompt. It watches the player's
    /// <see cref="PlayerInteractionDetector"/> and, when the current target implements
    /// <see cref="Interaction.IInteractionPromptProvider"/>, draws the prompt near the
    /// bottom centre of the screen. This is the simplest UI the project currently supports —
    /// the project has no Canvas or TextMeshPro package yet — and it is intentionally
    /// easy to replace once a proper HUD is in place.
    /// </summary>
    /// <remarks>
    /// The prompt is shown only while a valid target is aimed at and in range; looking away
    /// or moving out of range hides it on the next frame. It never draws anything while the
    /// gameplay input is disabled.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Player interaction detector whose CurrentTarget drives the prompt.")]
        private PlayerInteractionDetector _detector;

        [Header("Presentation")]
        [SerializeField, Tooltip("Vertical distance in pixels from the bottom edge of the screen.")]
        private float _bottomInset = 80f;
        [SerializeField, Tooltip("Width of the prompt box in pixels.")]
        private float _boxWidth = 360f;
        [SerializeField, Tooltip("Height of the prompt box in pixels.")]
        private float _boxHeight = 44f;

        private GUIStyle _labelStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        private void Reset()
        {
            AutoResolveDetector();
        }

        private void Awake()
        {
            if (_detector == null)
            {
                AutoResolveDetector();
            }
        }

        private void OnGUI()
        {
            if (_detector == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            Interaction.IInteractable target = _detector.CurrentTarget;
            string text = null;
            if (target is Interaction.IInteractionPromptProvider provider)
            {
                text = provider.GetInteractionPrompt();
            }

            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            EnsureStyles();

            Rect rect = new Rect(
                (Screen.width - _boxWidth) * 0.5f,
                Screen.height - _bottomInset - _boxHeight,
                _boxWidth,
                _boxHeight);

            GUI.Box(rect, GUIContent.none, _boxStyle);

            GUIContent content = new GUIContent(text);
            GUI.Label(rect, content, _labelStyle);
        }

        private void AutoResolveDetector()
        {
            if (_detector != null)
            {
                return;
            }

            _detector = FindFirstObjectByType<PlayerInteractionDetector>();
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                padding = new RectOffset(12, 12, 8, 8),
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };

            _stylesInitialized = true;
        }
    }
}
