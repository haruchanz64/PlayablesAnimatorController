using System;
using System.Collections.Generic;
using PlayablesAnimatorController.Core;
using UnityEngine;

namespace PlayablesAnimatorController.States
{
    public sealed class AnimStateDefinition
    {
        public string StateId;
        public AnimationClip Clip;
        public float FadeDuration = 0.12f;
        public float Speed = 1f;
        public int Priority;
        public bool RestartIfSame;
        public string AutoReturnStateId;
        public Action OnEnter;
        public Action OnExit;
    }

    /// <summary>
    /// Optional pure C# code-driven animation state machine driving any IAnimationDriver layer.
    /// Evaluates prioritized transition rules and optional auto-return on non-looping clip completion.
    /// </summary>
    public sealed class AnimStateMachine
    {
        private readonly IAnimationDriver _driver;
        private readonly int _layer;
        private readonly Dictionary<string, AnimStateDefinition> _states = new Dictionary<string, AnimStateDefinition>(StringComparer.Ordinal);
        private readonly List<IAnimTransitionRule> _transitions = new List<IAnimTransitionRule>(16);
        private bool _transitionsDirty;

        public string CurrentStateId { get; private set; }
        public int Layer => _layer;

        public event Action<string, string> OnStateChanged;

        public AnimStateMachine(IAnimationDriver driver, int layer = 0)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _layer = Mathf.Max(0, layer);
        }

        public AnimStateMachine RegisterState(AnimStateDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.StateId))
            {
                throw new ArgumentException("State definition requires a non-empty StateId.", nameof(definition));
            }

            _states[definition.StateId] = definition;
            return this;
        }

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

        public AnimStateMachine AddTransition(IAnimTransitionRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            _transitions.Add(rule);
            _transitionsDirty = true;
            return this;
        }

        public AnimStateMachine AddTransition(
            string fromStateId,
            string toStateId,
            Func<AnimPlaybackState, bool> predicate,
            int priority = 0)
        {
            return AddTransition(new FuncAnimTransitionRule(fromStateId, toStateId, predicate, priority));
        }

        public AnimStateMachine AddAnyTransition(
            string toStateId,
            Func<AnimPlaybackState, bool> predicate,
            int priority = 10)
        {
            return AddTransition("*", toStateId, predicate, priority);
        }

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

        private void EnsureSorted()
        {
            if (!_transitionsDirty) return;
            _transitionsDirty = false;
            _transitions.Sort((a, b) => b.Priority.CompareTo(a.Priority));
        }
    }
}
