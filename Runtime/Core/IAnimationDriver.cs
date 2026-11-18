using System;
using UnityEngine;

namespace PlayablesAnimatorController.Core
{
    /// <summary>
    /// Unified code-driven animation playback contract implemented by <see cref="Components.CustomAnimController"/>
    /// and mock drivers for deterministic unit testing.
    /// </summary>
    public interface IAnimationDriver
    {
        /// <summary>
        /// Number of animation layers currently allocated by this driver.
        /// </summary>
        int LayerCount { get; }

        /// <summary>
        /// Raised when a non-looping clip reaches the end of its duration, passing the layer index and completed clip.
        /// </summary>
        event Action<int, AnimationClip> OnClipCompleted;

        /// <summary>
        /// Raised when a clip begins playing, passing the layer index, resolved state ID, and started clip.
        /// </summary>
        event Action<int, string, AnimationClip> OnClipStarted;

        /// <summary>
        /// Plays a clip using a full <see cref="AnimPlayRequest"/> specification.
        /// </summary>
        /// <param name="request">Playback parameters including clip, layer, fade duration, priority, and callbacks.</param>
        /// <returns>True if the request was accepted; false if rejected due to null clip, invalid graph, or priority lock.</returns>
        bool Play(in AnimPlayRequest request);

        /// <summary>
        /// Convenience overload that plays <paramref name="clip"/> with common transition parameters.
        /// </summary>
        /// <param name="clip">Animation clip to play.</param>
        /// <param name="fadeDuration">Cross-fade duration in seconds; 0 switches immediately.</param>
        /// <param name="layer">Zero-based target layer index.</param>
        /// <param name="speed">Playback speed multiplier.</param>
        /// <param name="priority">Priority lock level while the clip is active and incomplete.</param>
        /// <returns>True if the clip started or continued playing; false if rejected.</returns>
        bool Play(AnimationClip clip, float fadeDuration = 0.15f, int layer = 0, float speed = 1f, int priority = 0);

        /// <summary>
        /// Plays a clip configured in the driver's animation set by string key.
        /// </summary>
        /// <param name="key">Case-sensitive entry key to resolve.</param>
        /// <param name="fadeOverride">Optional cross-fade duration in seconds overriding the entry default.</param>
        /// <param name="layerOverride">Optional zero-based layer index overriding the entry default.</param>
        /// <returns>True if the key resolved to a valid clip and playback was accepted; otherwise false.</returns>
        bool PlayByKey(string key, float? fadeOverride = null, int? layerOverride = null);

        /// <summary>
        /// Stops playback on <paramref name="layer"/>, optionally fading the layer weight down to 0 first.
        /// </summary>
        /// <param name="layer">Zero-based layer index to stop.</param>
        /// <param name="fadeOutDuration">Fade-out duration in seconds; 0 clears playback immediately.</param>
        void StopLayer(int layer, float fadeOutDuration = 0f);

        /// <summary>
        /// Sets the blend weight for <paramref name="layer"/>, clamped to [0, 1].
        /// </summary>
        /// <param name="layer">Zero-based layer index.</param>
        /// <param name="weight">Target layer weight in [0, 1].</param>
        void SetLayerWeight(int layer, float weight);

        /// <summary>
        /// Updates the playback speed multiplier for the active clip on <paramref name="layer"/>.
        /// </summary>
        /// <param name="speed">Playback speed multiplier.</param>
        /// <param name="layer">Zero-based layer index.</param>
        void SetSpeed(float speed, int layer = 0);

        /// <summary>
        /// Returns an observable <see cref="AnimPlaybackState"/> snapshot for <paramref name="layer"/>.
        /// </summary>
        /// <param name="layer">Zero-based layer index.</param>
        /// <returns>Playback snapshot for the layer, or default if the layer index is out of range.</returns>
        AnimPlaybackState GetLayerState(int layer = 0);
    }
}
