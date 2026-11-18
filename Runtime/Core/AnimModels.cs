using System;
using UnityEngine;

namespace PlayablesAnimatorController.Core
{
    /// <summary>
    /// Normalized-time callback triggered when playback crosses the specified clip progress in [0, 1].
    /// </summary>
    public readonly struct AnimClipEvent
    {
        /// <summary>
        /// Normalized clip progress in [0, 1] at which <see cref="Callback"/> is invoked.
        /// </summary>
        public readonly float NormalizedTime;

        /// <summary>
        /// Delegate invoked when playback reaches or crosses <see cref="NormalizedTime"/>.
        /// </summary>
        public readonly Action Callback;

        /// <summary>
        /// Initializes a normalized-time clip event, clamping <paramref name="normalizedTime"/> to [0, 1].
        /// </summary>
        /// <param name="normalizedTime">Clip progress fraction where 0 is the start and 1 is the end.</param>
        /// <param name="callback">Action invoked when the marker is crossed.</param>
        public AnimClipEvent(float normalizedTime, Action callback)
        {
            NormalizedTime = Mathf.Clamp01(normalizedTime);
            Callback = callback;
        }
    }

    /// <summary>
    /// Parameters for playing an <see cref="AnimationClip"/> on a layer through <see cref="IAnimationDriver"/> or <see cref="PlayableAnimGraph"/>.
    /// </summary>
    public struct AnimPlayRequest
    {
        /// <summary>
        /// Animation clip to play. Requests with a null clip are rejected.
        /// </summary>
        public AnimationClip Clip;

        /// <summary>
        /// Optional logical state identifier used for diagnostics and events; defaults to the clip name when null or empty.
        /// </summary>
        public string StateId;

        /// <summary>
        /// Cross-fade duration in seconds from the currently active clip on the target layer. Use 0 for an instant switch.
        /// </summary>
        public float FadeDuration;

        /// <summary>
        /// Playback speed multiplier applied to the clip playable (1 is normal speed).
        /// </summary>
        public float Speed;

        /// <summary>
        /// Initial clip progress in [0, 1] applied when playback begins.
        /// </summary>
        public float StartNormalizedTime;

        /// <summary>
        /// Zero-based target layer index. Negative values are clamped to 0 and layers expand on demand.
        /// </summary>
        public int Layer;

        /// <summary>
        /// Priority value held while the clip is active and incomplete; lower-priority requests on the same layer are rejected unless overridden.
        /// </summary>
        public int Priority;

        /// <summary>
        /// When true, restarts playback from <see cref="StartNormalizedTime"/> even if <see cref="Clip"/> is already playing on the layer.
        /// </summary>
        public bool RestartIfSame;

        /// <summary>
        /// When true, bypasses the active clip's <see cref="Priority"/> lock on the target layer.
        /// </summary>
        public bool ForceOverridePriority;

        /// <summary>
        /// Optional one-shot callback invoked when a non-looping clip reaches its full duration.
        /// </summary>
        public Action OnComplete;

        /// <summary>
        /// Optional array of normalized-time callbacks evaluated as playback advances through the clip.
        /// </summary>
        public AnimClipEvent[] Events;

        /// <summary>
        /// Builds an <see cref="AnimPlayRequest"/> with clamped non-negative <paramref name="fadeDuration"/> and <paramref name="layer"/> values.
        /// </summary>
        /// <param name="clip">Animation clip to play.</param>
        /// <param name="fadeDuration">Cross-fade duration in seconds; clamped to at least 0.</param>
        /// <param name="layer">Zero-based layer index; clamped to at least 0.</param>
        /// <param name="speed">Playback speed multiplier.</param>
        /// <param name="priority">Priority lock level while the clip is active.</param>
        /// <param name="restartIfSame">True to restart if the same clip is already active and incomplete.</param>
        /// <param name="onComplete">Optional callback fired when a non-looping clip finishes.</param>
        /// <param name="stateId">Optional logical state name; falls back to the clip name when omitted.</param>
        /// <returns>A configured play request ready to pass to <see cref="IAnimationDriver.Play(in AnimPlayRequest)"/>.</returns>
        public static AnimPlayRequest Create(
            AnimationClip clip,
            float fadeDuration = 0.15f,
            int layer = 0,
            float speed = 1f,
            int priority = 0,
            bool restartIfSame = false,
            Action onComplete = null,
            string stateId = null)
        {
            return new AnimPlayRequest
            {
                Clip = clip,
                StateId = stateId,
                FadeDuration = Mathf.Max(0f, fadeDuration),
                Speed = speed,
                StartNormalizedTime = 0f,
                Layer = Mathf.Max(0, layer),
                Priority = priority,
                RestartIfSame = restartIfSame,
                ForceOverridePriority = false,
                OnComplete = onComplete
            };
        }
    }

    /// <summary>
    /// Observable playback snapshot for a single animation layer at the time of query.
    /// </summary>
    [Serializable]
    public struct AnimPlaybackState
    {
        /// <summary>
        /// Zero-based index of the queried layer.
        /// </summary>
        public int Layer;

        /// <summary>
        /// Active state identifier, or the active clip name when no explicit state ID was supplied.
        /// </summary>
        public string StateId;

        /// <summary>
        /// Clip currently playing on port 0 of the layer mixer, or null if stopped.
        /// </summary>
        public AnimationClip CurrentClip;

        /// <summary>
        /// Outgoing clip on port 1 during a cross-fade, or null when not blending.
        /// </summary>
        public AnimationClip PreviousClip;

        /// <summary>
        /// Elapsed playback time in seconds reported by the active playable.
        /// </summary>
        public float ElapsedTime;

        /// <summary>
        /// Total length of <see cref="CurrentClip"/> in seconds, or 0 when no clip is active.
        /// </summary>
        public float Duration;

        /// <summary>
        /// Current playback progress in [0, 1], wrapped for looping clips and clamped for non-looping clips.
        /// </summary>
        public float NormalizedTime;

        /// <summary>
        /// Current playback speed multiplier on this layer.
        /// </summary>
        public float Speed;

        /// <summary>
        /// Current layer mixer weight in [0, 1].
        /// </summary>
        public float LayerWeight;

        /// <summary>
        /// Incoming clip cross-fade weight in [0, 1]; equals 1 when no blend is active.
        /// </summary>
        public float BlendWeight;

        /// <summary>
        /// True while the layer has an active clip playing.
        /// </summary>
        public bool IsPlaying;

        /// <summary>
        /// True while cross-fading between <see cref="PreviousClip"/> and <see cref="CurrentClip"/>.
        /// </summary>
        public bool IsBlending;

        /// <summary>
        /// True when <see cref="CurrentClip"/> has <see cref="AnimationClip.isLooping"/> enabled.
        /// </summary>
        public bool IsLooping;

        /// <summary>
        /// True once a non-looping clip has reached or passed its duration.
        /// </summary>
        public bool HasCompleted;

        /// <summary>
        /// Number of full loop iterations completed by the current looping clip.
        /// </summary>
        public int LoopCount;

        /// <summary>
        /// Active priority lock level on this layer; resets to 0 when a non-looping clip completes or the layer stops.
        /// </summary>
        public int Priority;
    }

    /// <summary>
    /// Inspector configuration for an animation layer, supporting 3D <see cref="AvatarMask"/> assets and additive blending.
    /// </summary>
    [Serializable]
    public sealed class AnimLayerConfig
    {
        /// <summary>
        /// Display name identifying this layer in the Inspector.
        /// </summary>
        public string layerName = "Base";

        /// <summary>
        /// Initial blend weight in [0, 1] assigned to this layer when the graph is created.
        /// </summary>
        [Range(0f, 1f)] public float defaultWeight = 1f;

        /// <summary>
        /// Optional avatar mask restricting which bones or transforms this layer affects.
        /// </summary>
        public AvatarMask avatarMask;

        /// <summary>
        /// When true, configures the layer to blend additively onto lower layers.
        /// </summary>
        public bool isAdditive;
    }
}
