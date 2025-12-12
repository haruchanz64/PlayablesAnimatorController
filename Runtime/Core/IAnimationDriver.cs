using System;
using UnityEngine;

namespace PlayablesAnimatorController.Core
{
    /// <summary>
    /// Unified code-driven animation playback contract implemented by CustomAnimController
    /// and mock drivers for deterministic unit testing.
    /// </summary>
    public interface IAnimationDriver
    {
        int LayerCount { get; }
        event Action<int, AnimationClip> OnClipCompleted;
        event Action<int, string, AnimationClip> OnClipStarted;

        bool Play(in AnimPlayRequest request);
        bool Play(AnimationClip clip, float fadeDuration = 0.15f, int layer = 0, float speed = 1f, int priority = 0);
        bool PlayByKey(string key, float? fadeOverride = null, int? layerOverride = null);
        void StopLayer(int layer, float fadeOutDuration = 0f);
        void SetLayerWeight(int layer, float weight);
        void SetSpeed(float speed, int layer = 0);
        AnimPlaybackState GetLayerState(int layer = 0);
    }
}
