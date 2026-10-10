using System.Collections;
using UnityEngine;
using Wildshift.Core.Diagnostics;
using Wildshift.Environment.Samples;

namespace Wildshift.UI
{
    /// <summary>
    /// Temporary, IMGUI-based feedback panel displayed when the player collects an
    /// environmental sample. It is shown for a short duration, then auto-dismisses. Only
    /// one message is shown at a time; collecting another sample while a message is up
    /// replaces it.
    /// </summary>
    /// <remarks>
    /// This is a lightweight, self-contained UI built on IMGUI so no Canvas, TMP, or UI
    /// package is required. It is intended to be replaced with a proper HUD element when
    /// the project's UI approach is formalized.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class SampleCollectionFeedback : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField, Tooltip("Duration in seconds the feedback stays visible after it appears.")]
        private float _displayDurationSeconds = 4f;
        [SerializeField, Tooltip("Vertical distance in pixels from the top edge of the screen.")]
        private float _topInset = 80f;
        [SerializeField, Tooltip("Width of the feedback box in pixels.")]
        private float _boxWidth = 460f;

        private static SampleCollectionFeedback _instance;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        private EnvironmentalSampleDefinition _currentDefinition;
        private Coroutine _dismissRoutine;

        /// <summary>
        /// The currently live feedback instance, if any. <see cref="EnvironmentalSampleInteractable"/>
        /// resolves it at collection time; null when no feedback object exists in the scene.
        /// </summary>
        public static SampleCollectionFeedback Instance => _instance;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                WildshiftLog.Warning("A second SampleCollectionFeedback was found while another is already " +
                                     "active; the duplicate will be destroyed.", this);
                Destroy(this);
                return;
            }

            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        /// <summary>
        /// Shows collection feedback for the given sample definition. If feedback is already
        /// showing it is replaced and the timer restarts.
        /// </summary>
        public void Show(EnvironmentalSampleDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            _currentDefinition = definition;
            if (_dismissRoutine != null)
            {
                StopCoroutine(_dismissRoutine);
            }

            _dismissRoutine = StartCoroutine(DismissAfterDelay(_displayDurationSeconds));
        }

        private void OnGUI()
        {
            if (_currentDefinition == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();

            string title = string.IsNullOrWhiteSpace(_currentDefinition.DisplayName)
                ? "Sample collected"
                : $"Collected: {_currentDefinition.DisplayName}";
            string body = _currentDefinition.CollectedDescription ?? string.Empty;

            float estimatedHeight = Mathf.Max(100f, 60f + body.Length * 0.9f);
            Rect rect = new Rect(
                (Screen.width - _boxWidth) * 0.5f,
                _topInset,
                _boxWidth,
                estimatedHeight);

            GUI.Box(rect, GUIContent.none, _boxStyle);

            Rect titleRect = new Rect(rect.x + 16f, rect.y + 12f, rect.width - 32f, 28f);
            GUI.Label(titleRect, title, _titleStyle);

            Rect bodyRect = new Rect(rect.x + 16f, rect.y + 44f, rect.width - 32f, rect.height - 56f);
            GUI.Label(bodyRect, body, _bodyStyle);
        }

        private IEnumerator DismissAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            _currentDefinition = null;
            _dismissRoutine = null;
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized)
            {
                return;
            }

            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                padding = new RectOffset(16, 16, 12, 12),
            };

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                wordWrap = true,
            };

            _bodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 14,
                wordWrap = true,
            };

            _stylesInitialized = true;
        }
    }
}
