using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Wildshift.Editor.Environment
{
    /// <summary>
    /// Optional editor window that runs <see cref="NacrePropPlacementAuditor"/> against the selected Nacre region root
    /// and lists what it found. It is a development aid, not part of the game: it lives in the editor-only assembly,
    /// it never edits the scene, and nothing in a build depends on it.
    /// </summary>
    /// <remarks>
    /// Open it with <b>Window → WILDSHIFT → Nacre Prop Placement</b>, then select <c>NACRE_GRAYBOX_REGION_ROOT</c> in
    /// the Hierarchy (or any of its <c>NACRE_*_Props_*</c> children) and press <b>Audit Placement</b>. Press
    /// <b>Frame First Error</b> to jump the Scene view to the first prop that failed a rule. See
    /// <c>docs/nacre-graybox-props.md</c> for the rules it checks and how to place props by hand without it.
    /// </remarks>
    public sealed class NacrePropPlacementWindow : EditorWindow
    {
        private const string MenuPath = "Window/WILDSHIFT/Nacre Prop Placement";

        private Vector2 _scroll;
        private readonly List<string> _errors = new List<string>();
        private readonly List<string> _warnings = new List<string>();
        private Transform _lastAudited;
        private int _lastPropCount;

        [MenuItem(MenuPath)]
        private static void Open()
        {
            NacrePropPlacementWindow window = GetWindow<NacrePropPlacementWindow>("Nacre Props");
            window.minSize = new Vector2(360f, 240f);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Select NACRE_GRAYBOX_REGION_ROOT (or one of its NACRE_*_Props_* groups) and press Audit Placement. " +
                "The audit is read-only: it never moves or creates objects.", MessageType.Info);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Audit Placement", GUILayout.Height(26f)))
                {
                    RunAudit();
                }

                using (new EditorGUI.DisabledScope(_errors.Count == 0))
                {
                    if (GUILayout.Button("Frame First Error", GUILayout.Height(26f), GUILayout.Width(140f)))
                    {
                        FrameFirstError();
                    }
                }
            }

            if (_lastAudited == null)
            {
                EditorGUILayout.LabelField("No audit has run yet.");
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audited: " + _lastAudited.name + " (" + _lastPropCount + " props)",
                EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_errors.Count + " errors, " + _warnings.Count + " warnings");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            DrawSection("Errors", _errors, MessageType.Error);
            DrawSection("Warnings", _warnings, MessageType.Warning);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string title, List<string> messages, MessageType type)
        {
            if (messages.Count == 0)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(title + " (" + messages.Count + ")", EditorStyles.boldLabel);
            for (int i = 0; i < messages.Count; i++)
            {
                EditorGUILayout.HelpBox(messages[i], type);
            }
        }

        private void RunAudit()
        {
            _errors.Clear();
            _warnings.Clear();

            Transform target = ResolveTarget(Selection.activeTransform);
            _lastAudited = target;
            _lastPropCount = CountProps(target);
            NacrePropPlacementAuditor.Audit(target, _errors, _warnings);
        }

        /// <summary>
        /// Walks up from the selection to the region root, so selecting a single prop or a group still audits the whole
        /// layout. Falls back to the selection itself.
        /// </summary>
        private static Transform ResolveTarget(Transform selected)
        {
            if (selected == null)
            {
                return null;
            }

            for (Transform current = selected; current != null; current = current.parent)
            {
                if (current.name.IndexOf("NACRE_GRAYBOX_REGION_ROOT", System.StringComparison.Ordinal) >= 0)
                {
                    return current;
                }
            }

            return selected;
        }

        private static int CountProps(Transform root)
        {
            if (root == null)
            {
                return 0;
            }

            int count = 0;
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.parent != null &&
                    child.parent.name.IndexOf("_Props_", System.StringComparison.Ordinal) >= 0 &&
                    child.GetComponent<Renderer>() != null)
                {
                    count++;
                }
            }

            return count;
        }

        private void FrameFirstError()
        {
            if (_errors.Count == 0 || _lastAudited == null)
            {
                return;
            }

            // The message starts with the prop name in single quotes.
            string message = _errors[0];
            int open = message.IndexOf('\'');
            int close = open >= 0 ? message.IndexOf('\'', open + 1) : -1;
            if (open < 0 || close <= open)
            {
                return;
            }

            string name = message.Substring(open + 1, close - open - 1);
            foreach (Transform child in _lastAudited.GetComponentsInChildren<Transform>(true))
            {
                if (child.name != name)
                {
                    continue;
                }

                Selection.activeTransform = child;
                SceneView view = SceneView.lastActiveSceneView;
                if (view != null)
                {
                    view.Frame(GetFrameBounds(child), true);
                    view.Repaint();
                }

                return;
            }
        }

        /// <summary>
        /// World-space bounds used to frame a prop in the Scene view. Prefers the renderer bounds the audit rules
        /// judge the prop by, then the collider, and finally a small box at the transform, so an object without
        /// either still gets framed. <see cref="SceneView.Frame(Bounds, bool)"/> takes bounds, not a GameObject.
        /// </summary>
        private static Bounds GetFrameBounds(Transform child)
        {
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                return renderer.bounds;
            }

            Collider collider = child.GetComponent<Collider>();
            if (collider != null)
            {
                return collider.bounds;
            }

            return new Bounds(child.position, Vector3.one);
        }
    }
}
