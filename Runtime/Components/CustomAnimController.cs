using System;
using System.Collections.Generic;
using PlayablesAnimatorController.Core;
using UnityEngine;

namespace PlayablesAnimatorController.Components
{
    /// <summary>
    /// MonoBehaviour driver attaching a code-driven PlayableAnimGraph to any 2D or 3D Animator
    /// without requiring an AnimatorController state graph.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Animator))]
    public sealed class CustomAnimController : MonoBehaviour, IAnimationDriver
    {
        private static readonly List<CustomAnimController> ActiveRegistry = new List<CustomAnimController>(16);

        public static IReadOnlyList<CustomAnimController> ActiveControllers => ActiveRegistry;

        [Header("Optional Configuration")]
        [SerializeField] private AnimSetAsset animationSet;
        [SerializeField] private List<AnimLayerConfig> layers = new List<AnimLayerConfig>
        {
            new AnimLayerConfig { layerName = "Base", defaultWeight = 1f }
        };
        [SerializeField] private string initialKey;

        private Animator _animator;
        private PlayableAnimGraph _graph;
        private readonly List<string> _recentHistory = new List<string>(24);

        public Animator Animator => _animator;
        public AnimSetAsset AnimationSet
        {
            get => animationSet;
            set => animationSet = value;
        }
        public int LayerCount => _graph != null ? _graph.LayerCount : Mathf.Max(1, layers.Count);
        public IReadOnlyList<string> RecentHistory => _recentHistory;

        public event Action<int, AnimationClip> OnClipCompleted;
        public event Action<int, string, AnimationClip> OnClipStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            ActiveRegistry.Clear();
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            InitializeGraph();
        }

        private void OnEnable()
        {
            if (!ActiveRegistry.Contains(this))
            {
                ActiveRegistry.Add(this);
            }

            if (_graph == null || !_graph.IsValid)
            {
                InitializeGraph();
            }

            if (!string.IsNullOrEmpty(initialKey) && animationSet != null)
            {
                PlayByKey(initialKey);
            }
        }

        private void OnDisable()
        {
            ActiveRegistry.Remove(this);
        }

        private void Update()
        {
            _graph?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            ActiveRegistry.Remove(this);
            DisposeGraph();
        }

        public bool Play(in AnimPlayRequest request)
        {
            if (_graph == null || !_graph.IsValid)
            {
                InitializeGraph();
            }

            return _graph != null && _graph.Play(in request);
        }

        public bool Play(
            AnimationClip clip,
            float fadeDuration = 0.15f,
            int layer = 0,
            float speed = 1f,
            int priority = 0)
        {
            var req = AnimPlayRequest.Create(clip, fadeDuration, layer, speed, priority);
            return Play(in req);
        }

        public bool PlayByKey(string key, float? fadeOverride = null, int? layerOverride = null)
        {
            if (animationSet == null || !animationSet.TryGetEntry(key, out AnimClipEntry entry) || entry.clip == null)
            {
                return false;
            }

            string nextKey = entry.nextKeyOnComplete;
            var req = new AnimPlayRequest
            {
                Clip = entry.clip,
                StateId = entry.key,
                FadeDuration = fadeOverride ?? entry.fadeDuration,
                Speed = entry.speed,
                Layer = layerOverride ?? entry.layer,
                Priority = entry.priority,
                RestartIfSame = entry.restartIfSame,
                OnComplete = !string.IsNullOrEmpty(nextKey) ? () => PlayByKey(nextKey) : null
            };
            return Play(in req);
        }

        public void StopLayer(int layer, float fadeOutDuration = 0f)
        {
            _graph?.StopLayer(layer, fadeOutDuration);
        }

        public void SetLayerWeight(int layer, float weight)
        {
            _graph?.SetLayerWeight(layer, weight);
        }

        public void SetSpeed(float speed, int layer = 0)
        {
            _graph?.SetSpeed(speed, layer);
        }

        public AnimPlaybackState GetLayerState(int layer = 0)
        {
            return _graph != null ? _graph.GetLayerState(layer) : default;
        }

        private void InitializeGraph()
        {
            DisposeGraph();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator == null) return;

            _graph = new PlayableAnimGraph(_animator, $"PlayablesAnimatorController:{name}", layers, manualUpdateMode: false);
            _graph.OnClipStarted += HandleClipStarted;
            _graph.OnClipCompleted += HandleClipCompleted;
        }

        private void DisposeGraph()
        {
            if (_graph == null) return;
            _graph.OnClipStarted -= HandleClipStarted;
            _graph.OnClipCompleted -= HandleClipCompleted;
            _graph.Dispose();
            _graph = null;
        }

        private void HandleClipStarted(int layer, string stateId, AnimationClip clip)
        {
            string entry = $"[L{layer}] {stateId} ({clip?.name})";
            _recentHistory.Add(entry);
            if (_recentHistory.Count > 24)
            {
                _recentHistory.RemoveAt(0);
            }

            OnClipStarted?.Invoke(layer, stateId, clip);
        }

        private void HandleClipCompleted(int layer, AnimationClip clip)
        {
            OnClipCompleted?.Invoke(layer, clip);
        }
    }
}
