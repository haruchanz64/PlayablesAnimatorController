using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace PlayablesAnimatorController.Core
{
    /// <summary>
    /// Pure C# Playables graph driver supporting 2D and 3D AnimationClips without an AnimatorController.
    /// Provides per-layer cross-fade mixing, AvatarMask layers, priority locks, and normalized-time events.
    /// </summary>
    public sealed class PlayableAnimGraph : IDisposable
    {
        private sealed class LayerSlot
        {
            public int Index;
            public AnimationMixerPlayable Mixer;
            public AnimationClipPlayable ActivePlayable;
            public AnimationClipPlayable PreviousPlayable;

            public AnimationClip ActiveClip;
            public AnimationClip PreviousClip;
            public string StateId;
            public float Speed = 1f;
            public float LayerWeight = 1f;
            public float BlendElapsed;
            public float BlendDuration;
            public bool IsBlending;
            public bool IsPlaying;
            public bool HasCompleted;
            public int Priority;
            public int LoopCount;
            public float PrevNormalized;
            public Action OnComplete;
            public AnimClipEvent[] Events;
            public bool[] FiredEvents;
            public bool FadingOutLayer;
            public float LayerFadeElapsed;
            public float LayerFadeDuration;
            public float LayerFadeStartWeight;
        }

        private PlayableGraph _graph;
        private AnimationPlayableOutput _output;
        private AnimationLayerMixerPlayable _layerMixer;
        private readonly List<LayerSlot> _layers = new List<LayerSlot>(4);
        private bool _disposed;
        private bool _manualMode;

        /// <summary>
        /// True when this instance has not been disposed and its underlying <see cref="PlayableGraph"/> is valid.
        /// </summary>
        public bool IsValid => !_disposed && _graph.IsValid();

        /// <summary>
        /// Number of animation layers currently allocated in the layer mixer.
        /// </summary>
        public int LayerCount => _layers.Count;

        /// <summary>
        /// Raised when a non-looping clip reaches its duration, passing the layer index and completed clip.
        /// </summary>
        public event Action<int, AnimationClip> OnClipCompleted;

        /// <summary>
        /// Raised when a clip begins playing on a layer, passing the layer index, resolved state ID, and started clip.
        /// </summary>
        public event Action<int, string, AnimationClip> OnClipStarted;

        /// <summary>
        /// Creates and starts a <see cref="PlayableGraph"/> targeting <paramref name="animator"/> with at least one mixer layer.
        /// </summary>
        /// <param name="animator">Target <see cref="Animator"/> receiving playable output; must not be null.</param>
        /// <param name="graphName">Display name shown in Unity's PlayableGraph Visualizer.</param>
        /// <param name="layerConfigs">Optional per-layer weights, avatar masks, and additive flags; defaults to a single base layer at weight 1.</param>
        /// <param name="manualUpdateMode">When true, uses <see cref="DirectorUpdateMode.Manual"/> so <see cref="Tick"/> drives graph evaluation deterministically.</param>
        public PlayableAnimGraph(
            Animator animator,
            string graphName = "PlayablesAnimatorController",
            IReadOnlyList<AnimLayerConfig> layerConfigs = null,
            bool manualUpdateMode = false)
        {
            if (animator == null) throw new ArgumentNullException(nameof(animator));

            _manualMode = manualUpdateMode;
            _graph = PlayableGraph.Create(graphName);
            _graph.SetTimeUpdateMode(manualUpdateMode ? DirectorUpdateMode.Manual : DirectorUpdateMode.GameTime);

            int count = layerConfigs != null && layerConfigs.Count > 0 ? layerConfigs.Count : 1;
            _layerMixer = AnimationLayerMixerPlayable.Create(_graph, count);
            _output = AnimationPlayableOutput.Create(_graph, "Animation", animator);
            _output.SetSourcePlayable(_layerMixer);

            for (int i = 0; i < count; i++)
            {
                var cfg = layerConfigs != null && i < layerConfigs.Count ? layerConfigs[i] : null;
                CreateLayerInternal(i, cfg);
            }

            _graph.Play();
        }

        /// <summary>
        /// Starts or cross-fades a clip on the requested layer, expanding the layer mixer if needed.
        /// Restores layer weight to 1 when playing onto a muted layer and cancels any active layer fade-out.
        /// </summary>
        /// <param name="request">Playback specification including clip, fade duration, speed, priority, and callbacks.</param>
        /// <returns>True if playback started or updated speed on the same clip; false if the graph is invalid, clip is null, or a higher-priority clip is active.</returns>
        public bool Play(in AnimPlayRequest request)
        {
            if (!IsValid || request.Clip == null) return false;

            int layerIndex = Mathf.Max(0, request.Layer);
            EnsureLayerCapacity(layerIndex + 1);
            LayerSlot slot = _layers[layerIndex];

            // Priority check: block lower-priority requests while an active non-completed clip holds priority
            if (!request.ForceOverridePriority &&
                slot.IsPlaying &&
                !slot.HasCompleted &&
                request.Priority < slot.Priority)
            {
                return false;
            }

            // Skip restart if already playing the same clip unless requested
            if (!request.RestartIfSame &&
                slot.IsPlaying &&
                slot.ActiveClip == request.Clip &&
                !slot.HasCompleted)
            {
                slot.Speed = request.Speed;
                if (slot.ActivePlayable.IsValid())
                {
                    slot.ActivePlayable.SetSpeed(request.Speed);
                }
                return true;
            }

            // Cancel any pending layer fade-out and restore full layer weight if layer was muted
            slot.FadingOutLayer = false;
            if (slot.LayerWeight <= 0.001f)
            {
                SetLayerWeight(layerIndex, 1f);
            }

            // Release any old previous clip playable
            DestroyPreviousPlayable(slot);

            float fade = Mathf.Max(0f, request.FadeDuration);
            bool canBlend = fade > 0.0001f && slot.ActivePlayable.IsValid();

            if (canBlend)
            {
                // Move current active (port 0) to previous (port 1)
                _graph.Disconnect(slot.Mixer, 0);
                _graph.Connect(slot.ActivePlayable, 0, slot.Mixer, 1);
                slot.PreviousPlayable = slot.ActivePlayable;
                slot.PreviousClip = slot.ActiveClip;
                slot.ActivePlayable = default;
            }
            else
            {
                DestroyActivePlayable(slot);
            }

            var newPlayable = AnimationClipPlayable.Create(_graph, request.Clip);
            newPlayable.SetSpeed(request.Speed);
            newPlayable.SetApplyFootIK(false);
            newPlayable.SetApplyPlayableIK(false);

            double startTime = Mathf.Clamp01(request.StartNormalizedTime) * request.Clip.length;
            if (startTime > 0d)
            {
                newPlayable.SetTime(startTime);
            }

            _graph.Connect(newPlayable, 0, slot.Mixer, 0);
            slot.ActivePlayable = newPlayable;
            slot.ActiveClip = request.Clip;
            slot.StateId = string.IsNullOrEmpty(request.StateId) ? request.Clip.name : request.StateId;
            slot.Speed = request.Speed;
            slot.Priority = request.Priority;
            slot.IsPlaying = true;
            slot.HasCompleted = false;
            slot.LoopCount = 0;
            slot.PrevNormalized = Mathf.Clamp01(request.StartNormalizedTime);
            slot.OnComplete = request.OnComplete;
            slot.Events = request.Events;
            slot.FiredEvents = request.Events != null && request.Events.Length > 0
                ? new bool[request.Events.Length]
                : null;

            if (canBlend)
            {
                slot.IsBlending = true;
                slot.BlendElapsed = 0f;
                slot.BlendDuration = fade;
                slot.Mixer.SetInputWeight(0, 0f);
                slot.Mixer.SetInputWeight(1, 1f);
            }
            else
            {
                slot.IsBlending = false;
                slot.BlendElapsed = 0f;
                slot.BlendDuration = 0f;
                slot.Mixer.SetInputWeight(0, 1f);
                slot.Mixer.SetInputWeight(1, 0f);
            }

            OnClipStarted?.Invoke(layerIndex, slot.StateId, request.Clip);
            return true;
        }

        /// <summary>
        /// Stops playback on <paramref name="layerIndex"/>, immediately clearing its playables or fading the layer weight to 0 over <paramref name="fadeOutDuration"/>.
        /// Overlay layers (index greater than 0) have their weight reset to 0 on immediate stop.
        /// </summary>
        /// <param name="layerIndex">Zero-based layer index to stop.</param>
        /// <param name="fadeOutDuration">Fade-out duration in seconds; values at or below 0.0001 stop immediately.</param>
        public void StopLayer(int layerIndex, float fadeOutDuration = 0f)
        {
            if (!IsValid || layerIndex < 0 || layerIndex >= _layers.Count) return;
            LayerSlot slot = _layers[layerIndex];

            if (fadeOutDuration > 0.0001f && slot.LayerWeight > 0.001f)
            {
                slot.FadingOutLayer = true;
                slot.LayerFadeElapsed = 0f;
                slot.LayerFadeDuration = fadeOutDuration;
                slot.LayerFadeStartWeight = slot.LayerWeight;
                return;
            }

            ClearLayerPlayback(slot);
            if (layerIndex > 0)
            {
                SetLayerWeight(layerIndex, 0f);
            }
        }

        /// <summary>
        /// Sets the blend weight for <paramref name="layerIndex"/> on the underlying <see cref="AnimationLayerMixerPlayable"/>.
        /// </summary>
        /// <param name="layerIndex">Zero-based layer index.</param>
        /// <param name="weight">Target layer weight, clamped to [0, 1].</param>
        public void SetLayerWeight(int layerIndex, float weight)
        {
            if (!IsValid || layerIndex < 0 || layerIndex >= _layers.Count) return;
            float clamped = Mathf.Clamp01(weight);
            _layers[layerIndex].LayerWeight = clamped;
            _layerMixer.SetInputWeight(layerIndex, clamped);
        }

        /// <summary>
        /// Updates the playback speed multiplier for the currently active clip on <paramref name="layerIndex"/>.
        /// </summary>
        /// <param name="speed">Playback speed multiplier.</param>
        /// <param name="layerIndex">Zero-based layer index.</param>
        public void SetSpeed(float speed, int layerIndex = 0)
        {
            if (!IsValid || layerIndex < 0 || layerIndex >= _layers.Count) return;
            LayerSlot slot = _layers[layerIndex];
            slot.Speed = speed;
            if (slot.ActivePlayable.IsValid())
            {
                slot.ActivePlayable.SetSpeed(speed);
            }
        }

        /// <summary>
        /// Advances cross-fade weights, layer fade-outs, normalized-time clip events, and completion callbacks across all layers.
        /// Evaluates the underlying <see cref="PlayableGraph"/> directly when constructed in manual update mode.
        /// </summary>
        /// <param name="deltaTime">Elapsed step time in seconds; negative values are clamped to 0.</param>
        public void Tick(float deltaTime)
        {
            if (!IsValid) return;

            float dt = Mathf.Max(0f, deltaTime);
            if (_manualMode && dt > 0f)
            {
                _graph.Evaluate(dt);
            }

            for (int i = 0; i < _layers.Count; i++)
            {
                TickLayer(_layers[i], dt);
            }
        }

        /// <summary>
        /// Captures an <see cref="AnimPlaybackState"/> snapshot for <paramref name="layerIndex"/>.
        /// </summary>
        /// <param name="layerIndex">Zero-based layer index.</param>
        /// <returns>Current playback metrics for the layer, or default if the graph is invalid or the index is out of range.</returns>
        public AnimPlaybackState GetLayerState(int layerIndex = 0)
        {
            if (!IsValid || layerIndex < 0 || layerIndex >= _layers.Count)
            {
                return default;
            }

            LayerSlot slot = _layers[layerIndex];
            float duration = slot.ActiveClip != null ? slot.ActiveClip.length : 0f;
            float elapsed = slot.ActivePlayable.IsValid() ? (float)slot.ActivePlayable.GetTime() : 0f;
            bool isLooping = slot.ActiveClip != null && slot.ActiveClip.isLooping;

            float normalized = 0f;
            if (duration > 1e-5f)
            {
                normalized = isLooping
                    ? Mathf.Repeat(elapsed / duration, 1f)
                    : Mathf.Clamp01(elapsed / duration);
            }

            float blendWeight = slot.IsBlending && slot.BlendDuration > 1e-5f
                ? Mathf.Clamp01(slot.BlendElapsed / slot.BlendDuration)
                : 1f;

            return new AnimPlaybackState
            {
                Layer = layerIndex,
                StateId = slot.StateId,
                CurrentClip = slot.ActiveClip,
                PreviousClip = slot.PreviousClip,
                ElapsedTime = elapsed,
                Duration = duration,
                NormalizedTime = normalized,
                Speed = slot.Speed,
                LayerWeight = slot.LayerWeight,
                BlendWeight = blendWeight,
                IsPlaying = slot.IsPlaying,
                IsBlending = slot.IsBlending,
                IsLooping = isLooping,
                HasCompleted = slot.HasCompleted,
                LoopCount = slot.LoopCount,
                Priority = slot.Priority
            };
        }

        /// <summary>
        /// Steps layer fade-out, cross-fade blending, normalized-time event dispatch, and non-looping completion detection for a single layer.
        /// </summary>
        private void TickLayer(LayerSlot slot, float dt)
        {
            if (slot.FadingOutLayer)
            {
                slot.LayerFadeElapsed += dt;
                float t = Mathf.Clamp01(slot.LayerFadeElapsed / Mathf.Max(0.0001f, slot.LayerFadeDuration));
                SetLayerWeight(slot.Index, Mathf.Lerp(slot.LayerFadeStartWeight, 0f, t));
                if (t >= 1f)
                {
                    slot.FadingOutLayer = false;
                    ClearLayerPlayback(slot);
                }
            }

            if (slot.IsBlending)
            {
                slot.BlendElapsed += dt;
                float t = Mathf.Clamp01(slot.BlendElapsed / Mathf.Max(0.0001f, slot.BlendDuration));
                slot.Mixer.SetInputWeight(0, t);
                slot.Mixer.SetInputWeight(1, 1f - t);

                if (t >= 1f)
                {
                    slot.IsBlending = false;
                    DestroyPreviousPlayable(slot);
                }
            }

            if (!slot.IsPlaying || slot.ActiveClip == null || !slot.ActivePlayable.IsValid())
            {
                return;
            }

            float length = slot.ActiveClip.length;
            if (length <= 1e-5f) return;

            float time = (float)slot.ActivePlayable.GetTime();
            if (slot.ActiveClip.isLooping)
            {
                int loops = Mathf.FloorToInt(time / length);
                float currNorm = Mathf.Repeat(time / length, 1f);
                if (loops > slot.LoopCount)
                {
                    // Fire tail events before loop wrap, reset fired flags, then fire head events of the new cycle
                    FireClipEventsInRange(slot, slot.PrevNormalized, 1f);
                    ResetFiredEvents(slot);
                    slot.LoopCount = loops;
                    FireClipEventsInRange(slot, 0f, currNorm);
                }
                else
                {
                    FireClipEventsInRange(slot, slot.PrevNormalized, currNorm);
                }
                slot.PrevNormalized = currNorm;
            }
            else
            {
                float currNorm = Mathf.Clamp01(time / length);
                FireClipEventsInRange(slot, slot.PrevNormalized, currNorm);
                slot.PrevNormalized = currNorm;

                if (!slot.HasCompleted && time >= length)
                {
                    slot.HasCompleted = true;
                    slot.Priority = 0; // Release priority lock upon completion
                    Action cb = slot.OnComplete;
                    slot.OnComplete = null;
                    cb?.Invoke();
                    OnClipCompleted?.Invoke(slot.Index, slot.ActiveClip);
                }
            }
        }

        /// <summary>
        /// Invokes any unfired <see cref="AnimClipEvent"/> whose normalized marker falls within [<paramref name="prevNorm"/>, <paramref name="currNorm"/>].
        /// </summary>
        private static void FireClipEventsInRange(LayerSlot slot, float prevNorm, float currNorm)
        {
            if (slot.Events == null || slot.FiredEvents == null) return;
            for (int i = 0; i < slot.Events.Length; i++)
            {
                if (slot.FiredEvents[i]) continue;
                float marker = slot.Events[i].NormalizedTime;
                if (marker >= prevNorm && marker <= currNorm)
                {
                    slot.FiredEvents[i] = true;
                    slot.Events[i].Callback?.Invoke();
                }
            }
        }

        /// <summary>
        /// Clears per-event fired flags so normalized events can trigger again on the next loop cycle.
        /// </summary>
        private static void ResetFiredEvents(LayerSlot slot)
        {
            if (slot.FiredEvents == null) return;
            Array.Clear(slot.FiredEvents, 0, slot.FiredEvents.Length);
        }

        /// <summary>
        /// Expands <see cref="_layerMixer"/> and creates default layer slots when a request targets a higher layer index.
        /// </summary>
        private void EnsureLayerCapacity(int requiredCount)
        {
            if (requiredCount <= _layers.Count) return;
            _layerMixer.SetInputCount(requiredCount);
            for (int i = _layers.Count; i < requiredCount; i++)
            {
                CreateLayerInternal(i, null);
            }
        }

        /// <summary>
        /// Allocates a 2-input <see cref="AnimationMixerPlayable"/> for a layer, connects it to <see cref="_layerMixer"/>, and applies optional mask and additive flags.
        /// </summary>
        private void CreateLayerInternal(int index, AnimLayerConfig cfg)
        {
            var mixer = AnimationMixerPlayable.Create(_graph, 2);
            _graph.Connect(mixer, 0, _layerMixer, index);

            float weight = cfg != null ? Mathf.Clamp01(cfg.defaultWeight) : (index == 0 ? 1f : 0f);
            _layerMixer.SetInputWeight(index, weight);

            if (cfg != null)
            {
                if (cfg.avatarMask != null)
                {
                    _layerMixer.SetLayerMaskFromAvatarMask((uint)index, cfg.avatarMask);
                }
                _layerMixer.SetLayerAdditive((uint)index, cfg.isAdditive);
            }

            _layers.Add(new LayerSlot
            {
                Index = index,
                Mixer = mixer,
                LayerWeight = weight
            });
        }

        /// <summary>
        /// Destroys active and outgoing playables on <paramref name="slot"/> and resets its playback, blend, priority, and callback state.
        /// </summary>
        private void ClearLayerPlayback(LayerSlot slot)
        {
            DestroyActivePlayable(slot);
            DestroyPreviousPlayable(slot);
            slot.IsPlaying = false;
            slot.IsBlending = false;
            slot.HasCompleted = false;
            slot.Priority = 0;
            slot.StateId = null;
            slot.OnComplete = null;
        }

        /// <summary>
        /// Disconnects and destroys the active clip playable bound to mixer port 0 on <paramref name="slot"/>.
        /// </summary>
        private void DestroyActivePlayable(LayerSlot slot)
        {
            if (slot.ActivePlayable.IsValid())
            {
                _graph.Disconnect(slot.Mixer, 0);
                slot.ActivePlayable.Destroy();
            }
            slot.ActivePlayable = default;
            slot.ActiveClip = null;
        }

        /// <summary>
        /// Disconnects and destroys the outgoing cross-fade clip playable bound to mixer port 1 on <paramref name="slot"/>.
        /// </summary>
        private void DestroyPreviousPlayable(LayerSlot slot)
        {
            if (slot.PreviousPlayable.IsValid())
            {
                _graph.Disconnect(slot.Mixer, 1);
                slot.PreviousPlayable.Destroy();
            }
            slot.PreviousPlayable = default;
            slot.PreviousClip = null;
        }

        /// <summary>
        /// Destroys the underlying <see cref="PlayableGraph"/> and clears layer slots. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _layers.Clear();
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
