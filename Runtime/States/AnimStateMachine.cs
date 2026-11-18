using System;
using System.Collections.Generic;
using PlayablesAnimatorController.Core;
using UnityEngine;

namespace PlayablesAnimatorController.States
{
    /// <summary>
    /// Configuration and lifecycle callbacks for a named state registered in <see cref="AnimStateMachine"/>.
    /// </summary>
    public sealed class AnimStateDefinition
    {
        /// <summary>
        /// Unique case-sensitive identifier for this state.
        /// </summary>
        public string StateId;

        /// <summary>
        /// Animation clip played when this state becomes active.
        /// </summary>
        public AnimationClip Clip;

        /// <summary>
        /// Cross-fade duration in seconds when entering this state.
        /// </summary>
        public float FadeDuration = 0.12f;

        /// <summary>
        /// Playback speed multiplier applied when entering this state.
        /// </summary>
        public float Speed = 1f;

        /// <summary>
        /// Priority lock level passed to <see cref="AnimPlayRequest.Priority"/> while this state is active and incomplete.
        /// </summary>
        public int Priority;

        /// <summary>
        /// When true, calling <see cref="AnimStateMachine.TrySetState"/> with this state restarts playback even if already active.
        /// </summary>
        public bool RestartIfSame;

        /// <summary>
        /// Optional state identifier transitioned to automatically on <see cref="AnimStateMachine.Tick"/> once a non-looping clip completes.
        /// </summary>
        public string AutoReturnStateId;

        /// <summary>
        /// Optional callback invoked immediately after transitioning into this state.
        /// </summary>
        public Action OnEnter;

        /// <summary>
        /// Optional callback invoked immediately before leaving this state for another state.
        /// </summary>
        public Action OnExit;
    }

    /// <summary>
    /// Optional pure C# code-driven animation state machine driving any <see cref="IAnimationDriver"/> layer.
    /// Evaluates prioritized transition rules and optional auto-return on non-looping clip completion.
    /// </summary>
    public sealed class AnimStateMachine
    {
        private readonly IAnimationDriver _driver;
        private readonly int _layer;
        private readonly Dictionary<string, AnimStateDefinition> _states = new Dictionary<string, AnimStateDefinition>(StringComparer.Ordinal);
        private readonly List<IAnimTransitionRule> _transitions = new List<IAnimTransitionRule>(16);
        private bool _transitionsDirty;

        /// <summary>
        /// Identifier of the currently active state, or null before any state is set.
        /// </summary>
        public string CurrentStateId { get; private set; }

        /// <summary>
        /// Zero-based animation layer index driven by this state machine.
        /// </summary>
        public int Layer => _layer;

        /// <summary>
        /// Raised after a state transition succeeds, passing the previous state ID (or null) and the new state ID.
        /// </summary>
        public event Action<string, string> OnStateChanged;

        /// <summary>
        /// Initializes a state machine targeting a specific layer on <paramref name="driver"/>.
        /// </summary>
        /// <param name="driver">Animation driver used to play clips and read layer state; must not be null.</param>
        /// <param name="layer">Zero-based target layer index; negative values are clamped to 0.</param>
        public AnimStateMachine(IAnimationDriver driver, int layer = 0)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _layer = Mathf.Max(0, layer);
        }

        /// <summary>
        /// Registers or replaces a state definition keyed by <see cref="AnimStateDefinition.StateId"/>.
        /// </summary>
        /// <param name="definition">State definition with a non-empty <see cref="AnimStateDefinition.StateId"/>.</param>
        /// <returns>This state machine instance for fluent chaining.</returns>
        public AnimStateMachine RegisterState(AnimStateDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StateId))
            {
                throw new ArgumentException("State definition requires a non-empty StateId.", nameof(definition));
            }

            _states[definition.StateId] = definition;
            return this;
        }

        /// <summary>
        /// Convenience overload that builds and registers an <see cref="AnimStateDefinition"/>.
        /// </summary>
        /// <param name="stateId">Unique case-sensitive state identifier.</param>
        /// <param name="clip">Animation clip played when entering the state.</param>
        /// <param name="fadeDuration">Cross-fade duration in seconds.</param>
        /// <param name="speed">Playback speed multiplier.</param>
        /// <param name="priority">Priority lock level while the clip is active and incomplete.</param>
        /// <param name="autoReturnStateId">Optional state ID to enter automatically once a non-looping clip completes.</param>
        /// <returns>This state machine instance for fluent chaining.</returns>
        public AnimStateMachine RegisterState(
            string stateId,
            AnimationClip clip,
            float fadeDuration = 0.12f,
            float speed = 1f,
            int priority = 0,
            string autoReturnStateId = null)
        {
            return RegisterState(new AnimStateDefinition
            {
                StateId = stateId,
                Clip = clip,
                FadeDuration = fadeDuration,
                Speed = speed,
                Priority = priority,
                AutoReturnStateId = autoReturnStateId
            });
        }

        /// <summary>
        /// Adds a custom <see cref="IAnimTransitionRule"/> and marks the transition list for descending-priority sorting on the next tick.
        /// </summary>
        /// <param name="rule">Transition rule to register; must not be null.</param>
        /// <returns>This state machine instance for fluent chaining.</returns>
        public AnimStateMachine AddTransition(IAnimTransitionRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _transitions.Add(rule);
            _transitionsDirty = true;
            return this;
        }

        /// <summary>
        /// Adds a delegate-backed transition rule between <paramref name="fromStateId"/> and <paramref name="toStateId"/>.
        /// </summary>
        /// <param name="fromStateId">Source state ID, or <c>"*"</c> to match any active state.</param>
        /// <param name="toStateId">Target state ID to enter when <paramref name="predicate"/> returns true.</param>
        /// <param name="predicate">Condition evaluated against the current layer <see cref="AnimPlaybackState"/>.</param>
        /// <param name="priority">Rule evaluation priority; higher values are checked first.</param>
        /// <returns>This state machine instance for fluent chaining.</returns>
        public AnimStateMachine AddTransition(
            string fromStateId,
            string toStateId,
            Func<AnimPlaybackState, bool> predicate,
            int priority = 0)
        {
            return AddTransition(new FuncAnimTransitionRule(fromStateId, toStateId, predicate, priority));
        }

        /// <summary>
        /// Adds a delegate-backed any-state transition rule (<c>FromStateId = "*"</c>) targeting <paramref name="toStateId"/>.
        /// </summary>
        /// <param name="toStateId">Target state ID to enter when <paramref name="predicate"/> returns true.</param>
        /// <param name="predicate">Condition evaluated against the current layer <see cref="AnimPlaybackState"/>.</param>
        /// <param name="priority">Rule evaluation priority; defaults to 10 so any-state rules run ahead of standard priority 0 rules.</param>
        /// <returns>This state machine instance for fluent chaining.</returns>
        public AnimStateMachine AddAnyTransition(
            string toStateId,
            Func<AnimPlaybackState, bool> predicate,
            int priority = 10)
        {
            return AddTransition("*", toStateId, predicate, priority);
        }

        /// <summary>
        /// Attempts to transition to <paramref name="stateId"/> by playing its clip on the configured driver layer.
        /// Invokes <see cref="AnimStateDefinition.OnExit"/>, <see cref="AnimStateDefinition.OnEnter"/>, and <see cref="OnStateChanged"/> only when playback succeeds.
        /// </summary>
        /// <param name="stateId">Registered target state identifier.</param>
        /// <param name="forceOverridePriority">When true, bypasses any active priority lock on the driver layer.</param>
        /// <returns>True if already in <paramref name="stateId"/> (without restart) or if the driver accepted playback; false if unregistered or blocked by priority.</returns>
        public bool TrySetState(string stateId, bool forceOverridePriority = false)
        {
            if (string.IsNullOrEmpty(stateId) || !_states.TryGetValue(stateId, out AnimStateDefinition next))
            {
                return false;
            }

            if (CurrentStateId == stateId && !next.RestartIfSame)
            {
                return true;
            }

            var req = new AnimPlayRequest
            {
                Clip = next.Clip,
                StateId = next.StateId,
                FadeDuration = next.FadeDuration,
                Speed = next.Speed,
                Layer = _layer,
                Priority = next.Priority,
                RestartIfSame = next.RestartIfSame,
                ForceOverridePriority = forceOverridePriority
            };

            if (!_driver.Play(in req))
            {
                return false;
            }

            string prevId = CurrentStateId;
            if (!string.IsNullOrEmpty(prevId) && _states.TryGetValue(prevId, out AnimStateDefinition prev))
            {
                prev.OnExit?.Invoke();
            }

            CurrentStateId = next.StateId;
            next.OnEnter?.Invoke();
            OnStateChanged?.Invoke(prevId, CurrentStateId);
            return true;
        }

        /// <summary>
        /// Evaluates registered transition rules in descending priority order and executes the first matching transition;
        /// otherwise triggers <see cref="AnimStateDefinition.AutoReturnStateId"/> when the active non-looping clip has completed.
        /// </summary>
        public void Tick()
        {
            EnsureSorted();
            AnimPlaybackState playback = _driver.GetLayerState(_layer);

            // 1. Evaluate transition rules in descending priority order
            for (int i = 0; i < _transitions.Count; i++)
            {
                IAnimTransitionRule rule = _transitions[i];
                bool matchesFrom = rule.FromStateId == "*" ||
                                   string.Equals(rule.FromStateId, CurrentStateId, StringComparison.Ordinal);
                if (!matchesFrom) continue;
                if (string.Equals(rule.ToStateId, CurrentStateId, StringComparison.Ordinal)) continue;

                if (rule.Evaluate(in playback))
                {
                    if (TrySetState(rule.ToStateId))
                    {
                        return;
                    }
                }
            }

            // 2. Handle auto-return when a non-looping state finishes
            if (!string.IsNullOrEmpty(CurrentStateId) &&
                playback.HasCompleted &&
                _states.TryGetValue(CurrentStateId, out AnimStateDefinition current) &&
                !string.IsNullOrEmpty(current.AutoReturnStateId))
            {
                TrySetState(current.AutoReturnStateId, forceOverridePriority: true);
            }
        }

        /// <summary>
        /// Lazily sorts <see cref="_transitions"/> in descending <see cref="IAnimTransitionRule.Priority"/> order when new rules were added.
        /// </summary>
        private void EnsureSorted()
        {
            if (!_transitionsDirty) return;
            _transitionsDirty = false;
            _transitions.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }
}
