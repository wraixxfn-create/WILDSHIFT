using System.Collections;
using UnityEngine;
using Wildshift.Player.Scanning;

namespace Wildshift.UI
{
    /// <summary>
    /// Temporary IMGUI panel that presents environmental scanner results. It only displays
    /// <see cref="ScanAttemptResult"/> values produced by <see cref="PlayerEnvironmentalScanner"/>;
    /// it never decides whether a scan succeeded, recorded an event, or was in range.
    /// </summary>
    /// <remarks>
    /// Replace this component with a Canvas HUD later without changing scan behaviour: subscribe
    /// to <see cref="PlayerEnvironmentalScanner.ScanAttempted"/> and map the same result object.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EnvironmentalScannerFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField, Tooltip("Player scanner whose ScanAttempted event drives this panel.")]
        private PlayerEnvironmentalScanner _scanner;

        [Header("Presentation")]
        [SerializeField, Tooltip("Duration in seconds the feedback stays visible after a scan attempt.")]
        private float _displayDurationSeconds = 3.5f;
        [SerializeField, Tooltip("Vertical distance in pixels from the top edge of the screen.")]
        private float _topInset = 160f;
        [SerializeField, Tooltip("Width of the feedback box in pixels.")]
        private float _boxWidth = 480f;
        [SerializeField, TextArea(1, 2), Tooltip("Shown when the aimed object is not a scan target or line of sight is blocked.")]
        private string _invalidTargetMessage = "Nothing to scan.";
        [SerializeField, TextArea(1, 2), Tooltip("Shown when a scan target is along the aim ray but farther than the scan range.")]
        private string _outOfRangeMessage = "Target is out of range.";
        [SerializeField, TextArea(1, 2), Tooltip("Shown when repeat scans are disabled and this target was already scanned.")]
        private string _alreadyScannedMessage = "Already scanned.";

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _boxStyle;
        private bool _stylesInitialized;

        private ScanAttemptResult _current;
        private Coroutine _dismissRoutine;

        private void Reset()
        {
            AutoResolveScanner();
        }

        private void Awake()
        {
            if (_scanner == null)
            {
                AutoResolveScanner();
            }
        }

        private void OnEnable()
        {
            if (_scanner != null)
            {
                _scanner.ScanAttempted += Present;
            }
        }

        private void OnDisable()
        {
            if (_scanner != null)
            {
                _scanner.ScanAttempted -= Present;
            }
        }

        /// <summary>Displays <paramref name="result"/>. A later attempt replaces the current message.</summary>
        public void Present(ScanAttemptResult result)
        {
            if (result == null)
            {
                return;
            }

            _current = result;
            if (_dismissRoutine != null)
            {
                StopCoroutine(_dismissRoutine);
            }

            _dismissRoutine = StartCoroutine(DismissAfterDelay(_displayDurationSeconds));
        }

        private void OnGUI()
        {
            if (_current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();
            BuildCopy(_current, out string title, out string body);

            float estimatedHeight = Mathf.Max(88f, 56f + body.Length * 0.85f);
            Rect rect = new Rect(
                (Screen.width - _boxWidth) * 0.5f,
                _topInset,
                _boxWidth,
                estimatedHeight);

            GUI.Box(rect, GUIContent.none, _boxStyle);

            Rect titleRect = new Rect(rect.x + 16f, rect.y + 10f, rect.width - 32f, 26f);
            GUI.Label(titleRect, title, _titleStyle);

            Rect bodyRect = new Rect(rect.x + 16f, rect.y + 38f, rect.width - 32f, rect.height - 50f);
            GUI.Label(bodyRect, body, _bodyStyle);
        }

        private void BuildCopy(ScanAttemptResult result, out string title, out string body)
        {
            switch (result.Outcome)
            {
                case ScanAttemptOutcome.Success:
                    title = string.IsNullOrWhiteSpace(result.DisplayName)
                        ? "Scan complete"
                        : $"Scan: {result.DisplayName}";
                    body = JoinNonEmpty(result.Description, result.ScanResultText);
                    if (string.IsNullOrEmpty(body))
                    {
                        body = "Scan complete.";
                    }

                    return;

                case ScanAttemptOutcome.OutOfRange:
                    title = "Scanner";
                    body = string.IsNullOrWhiteSpace(result.DisplayName)
                        ? _outOfRangeMessage
                        : $"{result.DisplayName} is out of range.";
                    return;

                case ScanAttemptOutcome.RepeatRejected:
                    title = "Scanner";
                    body = string.IsNullOrWhiteSpace(result.DisplayName)
                        ? _alreadyScannedMessage
                        : $"{_alreadyScannedMessage} {result.DisplayName}.";
                    return;

                default:
                    title = "Scanner";
                    body = _invalidTargetMessage;
                    return;
            }
        }

        private static string JoinNonEmpty(string first, string second)
        {
            bool hasFirst = !string.IsNullOrWhiteSpace(first);
            bool hasSecond = !string.IsNullOrWhiteSpace(second);
            if (hasFirst && hasSecond)
            {
                return first.Trim() + "\n" + second.Trim();
            }

            if (hasFirst)
            {
                return first.Trim();
            }

            return hasSecond ? second.Trim() : string.Empty;
        }

        private IEnumerator DismissAfterDelay(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            _current = null;
            _dismissRoutine = null;
        }

        private void AutoResolveScanner()
        {
            if (_scanner != null)
            {
                return;
            }

            _scanner = FindFirstObjectByType<PlayerEnvironmentalScanner>();
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
