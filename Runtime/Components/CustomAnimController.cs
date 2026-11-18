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

        /// <summary>
        /// Enabled <see cref="CustomAnimController"/> instances currently registered for runtime inspection and debugging.
        /// </summary>
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

        /// <summary>
        /// Cached <see cref="UnityEngine.Animator"/> component on this GameObject driven by the playable graph.
        /// </summary>
        public Animator Animator => _animator;

        /// <summary>
        /// Optional <see cref="AnimSetAsset"/> used to resolve string keys passed to <see cref="PlayByKey"/>.
        /// </summary>
        public AnimSetAsset AnimationSet
        {
            get => animationSet;
            set => animationSet = value;
        }

        /// <inheritdoc/>
        public int LayerCount => _graph != null ? _graph.LayerCount : Mathf.Max(1, layers.Count);

        /// <summary>
        /// Rolling log of up to 24 recent clip start entries formatted as <c>[L&lt;layer&gt;] &lt;stateId&gt; (&lt;clipName&gt;)</c>.
        /// </summary>
        public IReadOnlyList<string> RecentHistory => _recentHistory;

        /// <inheritdoc/>
        public event Action<int, AnimationClip> OnClipCompleted;

        /// <inheritdoc/>
        public event Action<int, string, AnimationClip> OnClipStarted;

        /// <summary>
        /// Clears the static controller registry on subsystem registration to support Enter Play Mode without domain reload.
        /// </summary>
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

        /// <summary>
        /// Registers this controller in <see cref="ActiveControllers"/>, ensures the playable graph is initialized, and triggers <c>initialKey</c> if configured.
        /// </summary>
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

        /// <summary>
        /// Plays a clip request on the underlying <see cref="PlayableAnimGraph"/>, initializing the graph lazily if needed.
        /// </summary>
        /// <param name="request">Playback parameters including clip, fade duration, layer, speed, priority, and callbacks.</param>
        /// <returns>True if the request was accepted; false if the graph could not initialize, the clip is null, or a higher-priority clip is active.</returns>
        public bool Play(in AnimPlayRequest request)
        {
            if (_graph == null || !_graph.IsValid)
            {
                InitializeGraph();
            }

            return _graph != null && _graph.Play(in request);
        }

        /// <inheritdoc/>
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

        /// <summary>
        /// Resolves <paramref name="key"/> in <see cref="AnimationSet"/> and plays its clip with optional fade and layer overrides.
        /// Automatically chains <see cref="AnimClipEntry.nextKeyOnComplete"/> when set on a non-looping entry.
        /// </summary>
        /// <param name="key">Case-sensitive entry key in <see cref="AnimationSet"/>.</param>
        /// <param name="fadeOverride">Optional cross-fade duration in seconds overriding the entry default.</param>
        /// <param name="layerOverride">Optional zero-based layer index overriding the entry default.</param>
        /// <returns>True if the key resolved to a clip and playback was accepted; otherwise false.</returns>
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

        /// <inheritdoc/>
        public void StopLayer(int layer, float fadeOutDuration = 0f)
        {
            _graph?.StopLayer(layer, fadeOutDuration);
        }

        /// <inheritdoc/>
        public void SetLayerWeight(int layer, float weight)
        {
            _graph?.SetLayerWeight(layer, weight);
        }

        /// <inheritdoc/>
        public void SetSpeed(float speed, int layer = 0)
        {
            _graph?.SetSpeed(speed, layer);
        }

        /// <inheritdoc/>
        public AnimPlaybackState GetLayerState(int layer = 0)
        {
            return _graph != null ? _graph.GetLayerState(layer) : default;
        }

        /// <summary>
        /// Disposes any prior graph and creates a fresh <see cref="PlayableAnimGraph"/> bound to <see cref="_animator"/> using serialized layer configs.
        /// </summary>
        private void InitializeGraph()
        {
            DisposeGraph();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_animator == null) return;

            _graph = new PlayableAnimGraph(_animator, $"PlayablesAnimatorController:{name}", layers, manualUpdateMode: false);
            _graph.OnClipStarted += HandleClipStarted;
            _graph.OnClipCompleted += HandleClipCompleted;
        }

        /// <summary>
        /// Unsubscribes graph callbacks and disposes the active <see cref="PlayableAnimGraph"/> if present.
        /// </summary>
        private void DisposeGraph()
        {
            if (_graph == null) return;
            _graph.OnClipStarted -= HandleClipStarted;
            _graph.OnClipCompleted -= HandleClipCompleted;
            _graph.Dispose();
            _graph = null;
        }

        /// <summary>
        /// Records a started clip in the bounded <see cref="_recentHistory"/> ring and forwards <see cref="OnClipStarted"/>.
        /// </summary>
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

        /// <summary>
        /// Forwards clip completion notifications from the underlying graph to <see cref="OnClipCompleted"/>.
        /// </summary>
        private void HandleClipCompleted(int layer, AnimationClip clip)
        {
            OnClipCompleted?.Invoke(layer, clip);
        }
    }
}
