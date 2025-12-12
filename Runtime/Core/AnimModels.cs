using System;
using UnityEngine;

namespace PlayablesAnimatorController.Core
{
    /// <summary>
    /// Normalized-time callback triggered when playback crosses the specified clip progress [0..1].
    /// </summary>
    public readonly struct AnimClipEvent
    {
        public readonly float NormalizedTime;
        public readonly Action Callback;

        public AnimClipEvent(float normalizedTime, Action callback)
        {
            NormalizedTime = Mathf.Clamp01(normalizedTime);
            Callback = callback;
        }
    }

    /// <summary>
    /// Parameters for playing an AnimationClip on a layer via PlayablesAnimatorController.
    /// </summary>
    public struct AnimPlayRequest
    {
        public AnimationClip Clip;
        public string StateId;
        public float FadeDuration;
        public float Speed;
        public float StartNormalizedTime;
        public int Layer;
        public int Priority;
        public bool RestartIfSame;
        public bool ForceOverridePriority;
        public Action OnComplete;
        public AnimClipEvent[] Events;

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
    /// Observable playback snapshot for a single animation layer.
    /// </summary>
    [Serializable]
    public struct AnimPlaybackState
    {
        public int Layer;
        public string StateId;
        public AnimationClip CurrentClip;
        public AnimationClip PreviousClip;
        public float ElapsedTime;
        public float Duration;
        public float NormalizedTime;
        public float Speed;
        public float LayerWeight;
        public float BlendWeight;
        public bool IsPlaying;
        public bool IsBlending;
        public bool IsLooping;
        public bool HasCompleted;
        public int LoopCount;
        public int Priority;
    }

    /// <summary>
    /// Inspector configuration for an animation layer (supports 3D AvatarMasks and additive layers).
    /// </summary>
    [Serializable]
    public sealed class AnimLayerConfig
    {
        public string layerName = "Base";
        [Range(0f, 1f)] public float defaultWeight = 1f;
        public AvatarMask avatarMask;
        public bool isAdditive;
    }
}
