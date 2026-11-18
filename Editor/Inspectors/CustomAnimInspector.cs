using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.Core;
using PlayablesAnimatorController.Editor.UI;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlayablesAnimatorController.Editor.Inspectors
{
    /// <summary>
    /// UI Toolkit custom inspector for <see cref="CustomAnimController"/> displaying default serialized fields
    /// alongside a live Play Mode status panel of active layers, cross-fade blends, and progress bars.
    /// </summary>
    [CustomEditor(typeof(CustomAnimController))]
    public sealed class CustomAnimInspector : UnityEditor.Editor
    {
        private VisualElement _layersContainer;

        /// <summary>
        /// Builds the UI Toolkit inspector hierarchy and schedules a 100 ms refresh loop for live playback telemetry.
        /// </summary>
        /// <returns>The root <see cref="VisualElement"/> for the inspector.</returns>
        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            AttachStyles(root);

            InspectorElement.FillDefaultInspector(root, serializedObject, this);

            var card = new VisualElement();
            card.AddToClassList("cah-card");
            card.style.marginTop = 8f;

            var title = new Label("Live Playables State");
            title.AddToClassList("cah-section-title");
            card.Add(title);

            _layersContainer = new VisualElement();
            card.Add(_layersContainer);

            var openBtn = new Button(AnimDebuggerWindow.ShowWindow) { text = "Open Animation Debugger" };
            openBtn.style.marginTop = 4f;
            card.Add(openBtn);
            root.Add(card);

            RefreshLayers();
            root.schedule.Execute(RefreshLayers).Every(100);
            return root;
        }

        /// <summary>
        /// Rebuilds the per-layer readout and normalized progress bars from the inspected <see cref="CustomAnimController"/> during Play Mode.
        /// </summary>
        private void RefreshLayers()
        {
            if (target == null || _layersContainer == null) return;
            _layersContainer.Clear();

            if (!Application.isPlaying)
            {
                var hint = new Label("Enter Play Mode to monitor live layer playback, cross-fade weights, and clip progress.");
                hint.AddToClassList("cah-muted");
                hint.style.whiteSpace = WhiteSpace.Normal;
                _layersContainer.Add(hint);
                return;
            }

            var controller = (CustomAnimController)target;
            for (int i = 0; i < controller.LayerCount; i++)
            {
                AnimPlaybackState s = controller.GetLayerState(i);
                string clipName = s.CurrentClip != null ? s.CurrentClip.name : "(None)";
                string blendText = s.IsBlending && s.PreviousClip != null
                    ? $"  [Blend {s.PreviousClip.name} -> {clipName}: {s.BlendWeight * 100f:F0}%]"
                    : string.Empty;

                var lbl = new Label($"Layer {i} (W:{s.LayerWeight:F2} P:{s.Priority}): {s.StateId ?? clipName} ({s.NormalizedTime * 100f:F0}%){blendText}");
                lbl.AddToClassList("cah-mono");
                _layersContainer.Add(lbl);

                var track = new VisualElement();
                track.AddToClassList("cah-bar-track");
                var fill = new VisualElement();
                fill.AddToClassList("cah-bar-fill");
                if (s.IsBlending) fill.AddToClassList("cah-bar-fill--blend");
                fill.style.width = Length.Percent(Mathf.Clamp01(s.NormalizedTime) * 100f);
                track.Add(fill);
                _layersContainer.Add(track);
            }
        }

        /// <summary>
        /// Loads <c>CustomAnimStyles.uss</c> from the package path (falling back to an AssetDatabase search) and attaches it to <paramref name="target"/>.
        /// </summary>
        /// <param name="target">Visual element receiving the stylesheet.</param>
        internal static void AttachStyles(VisualElement target)
        {
            const string pkgPath = "Packages/com.haruchanz64.playables-animator-controller/Editor/UI/CustomAnimStyles.uss";
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(pkgPath);
            if (sheet == null)
            {
                string[] guids = AssetDatabase.FindAssets("CustomAnimStyles t:StyleSheet");
                if (guids.Length > 0)
                {
                    sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath(guids[0]));
                }
            }
            if (sheet != null) target.styleSheets.Add(sheet);
        }
    }
}
