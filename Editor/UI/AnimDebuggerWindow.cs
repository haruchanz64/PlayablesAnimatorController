using System.Collections.Generic;
using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.Core;
using PlayablesAnimatorController.Editor.Inspectors;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PlayablesAnimatorController.Editor.UI
{
    /// <summary>
    /// UI Toolkit live Playables debugger showing active layers, cross-fade progress,
    /// recent transition history, and manual clip playback controls.
    /// </summary>
    public sealed class AnimDebuggerWindow : EditorWindow
    {
        private DropdownField _controllerDropdown;
        private VisualElement _layersContainer;
        private VisualElement _historyContainer;
        private ObjectField _testClipField;
        private FloatField _testFadeField;
        private IntegerField _testLayerField;
        private Slider _speedSlider;
        private int _selectedIndex;

        [MenuItem("Tools/PlayablesAnimatorController/Animation Debugger", false, 10)]
        public static void ShowWindow()
        {
            var window = GetWindow<AnimDebuggerWindow>("Anim Debugger");
            window.minSize = new Vector2(320f, 280f);
            window.Show();
        }

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("cah-root");
            CustomAnimInspector.AttachStyles(rootVisualElement);

            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;
            scroll.style.minHeight = 0f;

            // Target selector
            var targetCard = new VisualElement();
            targetCard.AddToClassList("cah-card");
            targetCard.Add(MakeTitle("Active CustomAnimController"));

            _controllerDropdown = new DropdownField("Controller", new List<string> { "(None Active)" }, 0);
            _controllerDropdown.RegisterValueChangedCallback(_ =>
            {
                _selectedIndex = Mathf.Max(0, _controllerDropdown.index);
                RefreshView();
            });
            targetCard.Add(_controllerDropdown);
            scroll.Add(targetCard);

            // Live layers
            var layersCard = new VisualElement();
            layersCard.AddToClassList("cah-card");
            layersCard.Add(MakeTitle("Layer Playback & Blend Weights"));
            _layersContainer = new VisualElement();
            layersCard.Add(_layersContainer);
            scroll.Add(layersCard);

            // Quick test playback
            var testCard = new VisualElement();
            testCard.AddToClassList("cah-card");
            testCard.Add(MakeTitle("Manual Test Playback"));

            _testClipField = new ObjectField("AnimationClip") { objectType = typeof(AnimationClip) };
            _testFadeField = new FloatField("Fade Duration (s)") { value = 0.15f };
            _testLayerField = new IntegerField("Layer Index") { value = 0 };
            _speedSlider = new Slider("Playback Speed", 0f, 3f) { value = 1f, showInputField = true };
            _speedSlider.RegisterValueChangedCallback(evt =>
            {
                var ctrl = GetSelectedController();
                ctrl?.SetSpeed(evt.newValue, Mathf.Max(0, _testLayerField.value));
            });

            var playRow = new VisualElement();
            playRow.AddToClassList("cah-row");
            playRow.Add(new Button(PlayTestClip) { text = "Play Clip" });
            playRow.Add(new Button(() => GetSelectedController()?.StopLayer(Mathf.Max(0, _testLayerField.value), 0.1f)) { text = "Stop Layer" });

            testCard.Add(_testClipField);
            testCard.Add(_testFadeField);
            testCard.Add(_testLayerField);
            testCard.Add(_speedSlider);
            testCard.Add(playRow);
            scroll.Add(testCard);

            // Transition history
            var historyCard = new VisualElement();
            historyCard.AddToClassList("cah-card");
            historyCard.Add(MakeTitle("Recent Clip History"));
            _historyContainer = new VisualElement();
            historyCard.Add(_historyContainer);
            scroll.Add(historyCard);

            rootVisualElement.Add(scroll);
            rootVisualElement.schedule.Execute(RefreshView).Every(100);
            RefreshView();
        }

        private CustomAnimController GetSelectedController()
        {
            var active = CustomAnimController.ActiveControllers;
            if (active.Count == 0) return null;
            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, active.Count - 1);
            return active[_selectedIndex];
        }

        private void PlayTestClip()
        {
            var ctrl = GetSelectedController();
            var clip = _testClipField.value as AnimationClip;
            if (ctrl == null || clip == null) return;

            var req = AnimPlayRequest.Create(
                clip,
                fadeDuration: Mathf.Max(0f, _testFadeField.value),
                layer: Mathf.Max(0, _testLayerField.value),
                speed: _speedSlider.value,
                restartIfSame: true);
            req.ForceOverridePriority = true;
            ctrl.Play(in req);
        }

        private void RefreshView()
        {
            if (_controllerDropdown == null) return;

            var active = CustomAnimController.ActiveControllers;
            if (active.Count == 0)
            {
                _controllerDropdown.choices = new List<string> { "(None Active)" };
                _controllerDropdown.SetValueWithoutNotify("(None Active)");
                _layersContainer.Clear();
                var empty = new Label("Enter Play Mode with a CustomAnimController in the scene.");
                empty.AddToClassList("cah-muted");
                _layersContainer.Add(empty);
                _historyContainer.Clear();
                return;
            }

            var names = new List<string>(active.Count);
            for (int i = 0; i < active.Count; i++)
            {
                names.Add(active[i].name);
            }

            _selectedIndex = Mathf.Clamp(_selectedIndex, 0, active.Count - 1);
            _controllerDropdown.choices = names;
            _controllerDropdown.SetValueWithoutNotify(names[_selectedIndex]);

            var ctrl = active[_selectedIndex];
            _layersContainer.Clear();
            for (int i = 0; i < ctrl.LayerCount; i++)
            {
                AnimPlaybackState s = ctrl.GetLayerState(i);
                string clipName = s.CurrentClip != null ? s.CurrentClip.name : "(Idle)";
                string blendInfo = s.IsBlending && s.PreviousClip != null
                    ? $" | Blending from {s.PreviousClip.name} ({s.BlendWeight * 100f:F0}%)"
                    : string.Empty;

                var lbl = new Label(
                    $"L{i} [{s.StateId ?? clipName}]  {s.ElapsedTime:F2}s / {s.Duration:F2}s ({s.NormalizedTime * 100f:F0}%)  W:{s.LayerWeight:F2}  P:{s.Priority}{blendInfo}");
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

            _historyContainer.Clear();
            var hist = ctrl.RecentHistory;
            for (int i = hist.Count - 1; i >= 0; i--)
            {
                var line = new Label(hist[i]);
                line.AddToClassList("cah-mono");
                _historyContainer.Add(line);
            }
        }

        private static Label MakeTitle(string text)
        {
            var l = new Label(text);
            l.AddToClassList("cah-section-title");
            return l;
        }
    }
}
