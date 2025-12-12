using System;
using PlayablesAnimatorController.Core;

namespace PlayablesAnimatorController.States
{
    /// <summary>
    /// Pluggable predicate rule for code-driven animation state transitions.
    /// Use "*" for FromStateId to register an any-state transition.
    /// </summary>
    public interface IAnimTransitionRule
    {
        string FromStateId { get; }
        string ToStateId { get; }
        int Priority { get; }
        bool Evaluate(in AnimPlaybackState currentState);
    }

    /// <summary>
    /// Delegate-backed IAnimTransitionRule convenience implementation.
    /// </summary>
    public sealed class FuncAnimTransitionRule : IAnimTransitionRule
    {
        public string FromStateId { get; }
        public string ToStateId { get; }
        public int Priority { get; }
        private readonly Func<AnimPlaybackState, bool> _predicate;

        public FuncAnimTransitionRule(
            string fromStateId,
            string toStateId,
            Func<AnimPlaybackState, bool> predicate,
            int priority = 0)
        {
            FromStateId = string.IsNullOrEmpty(fromStateId) ? "*" : fromStateId;
            ToStateId = toStateId ?? throw new ArgumentNullException(nameof(toStateId));
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            Priority = priority;
        }

        public bool Evaluate(in AnimPlaybackState currentState) => _predicate(currentState);
    }
}
