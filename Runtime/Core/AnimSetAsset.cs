using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayablesAnimatorController.Core
{
    /// <summary>
    /// Serializable entry in an <see cref="AnimSetAsset"/> mapping a string key to an <see cref="AnimationClip"/> and default playback settings.
    /// </summary>
    [Serializable]
    public sealed class AnimClipEntry
    {
        /// <summary>
        /// Case-sensitive lookup key used with <see cref="IAnimationDriver.PlayByKey"/>.
        /// </summary>
        public string key = "Idle";

        /// <summary>
        /// Animation clip played when this entry is requested.
        /// </summary>
        public AnimationClip clip;

        /// <summary>
        /// Default cross-fade duration in seconds when transitioning into this clip.
        /// </summary>
        [Min(0f)] public float fadeDuration = 0.12f;

        /// <summary>
        /// Default playback speed multiplier for this clip.
        /// </summary>
        public float speed = 1f;

        /// <summary>
        /// Default zero-based animation layer index for this clip.
        /// </summary>
        [Min(0)] public int layer = 0;

        /// <summary>
        /// Default priority lock level held while this clip is active and incomplete.
        /// </summary>
        public int priority = 0;

        /// <summary>
        /// When true, restarts the clip if it is already playing on the target layer.
        /// </summary>
        public bool restartIfSame = false;

        /// <summary>
        /// Optional key played automatically when a non-looping clip completes.
        /// </summary>
        public string nextKeyOnComplete = string.Empty;
    }

    /// <summary>
    /// Optional <see cref="ScriptableObject"/> mapping string keys to <see cref="AnimationClip"/> entries and default transition settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimSet", menuName = "PlayablesAnimatorController/Animation Set", order = 100)]
    public class AnimSetAsset : ScriptableObject
    {
        /// <summary>
        /// Configured list of keyed animation entries. Duplicate keys keep the first valid entry during lookup.
        /// </summary>
        public List<AnimClipEntry> entries = new List<AnimClipEntry>();

        private Dictionary<string, AnimClipEntry> _lookup;

        /// <summary>
        /// Looks up an <see cref="AnimClipEntry"/> by exact ordinal key, lazily rebuilding the internal cache when needed.
        /// </summary>
        /// <param name="key">Case-sensitive entry key to search for.</param>
        /// <param name="entry">Receives the matched entry if found; otherwise null.</param>
        /// <returns>True when a non-null entry exists for <paramref name="key"/>; otherwise false.</returns>
        public bool TryGetEntry(string key, out AnimClipEntry entry)
        {
            if (string.IsNullOrEmpty(key))
            {
                entry = null;
                return false;
            }

            if (_lookup == null || _lookup.Count != entries.Count)
            {
                RebuildLookup();
            }

            return _lookup.TryGetValue(key, out entry) && entry != null;
        }

        private void OnValidate()
        {
            _lookup = null;
        }

        /// <summary>
        /// Reconstructs the ordinal dictionary cache from <see cref="entries"/>, ignoring null or empty keys and duplicate entries.
        /// </summary>
        private void RebuildLookup()
        {
            _lookup = new Dictionary<string, AnimClipEntry>(entries.Count, StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e != null && !string.IsNullOrEmpty(e.key) && !_lookup.ContainsKey(e.key))
                {
                    _lookup.Add(e.key, e);
                }
            }
        }
    }
}
