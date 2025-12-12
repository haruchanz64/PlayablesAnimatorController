using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlayablesAnimatorController.Core
{
    [Serializable]
    public sealed class AnimClipEntry
    {
        public string key = "Idle";
        public AnimationClip clip;
        [Min(0f)] public float fadeDuration = 0.12f;
        public float speed = 1f;
        [Min(0)] public int layer = 0;
        public int priority = 0;
        public bool restartIfSame = false;
        public string nextKeyOnComplete = string.Empty;
    }

    /// <summary>
    /// Optional ScriptableObject mapping string keys to AnimationClips and default transition settings.
    /// </summary>
    [CreateAssetMenu(fileName = "AnimSet", menuName = "PlayablesAnimatorController/Animation Set", order = 100)]
    public class AnimSetAsset : ScriptableObject
    {
        public List<AnimClipEntry> entries = new List<AnimClipEntry>();

        private Dictionary<string, AnimClipEntry> _lookup;

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
