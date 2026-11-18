using System;
using PlayablesAnimatorController.Core;

namespace PlayablesAnimatorController.States
{
    /// <summary>
    /// Pluggable predicate rule for code-driven animation state transitions evaluated by <see cref="AnimStateMachine"/>.
    /// Use <c>"*"</c> for <see cref="FromStateId"/> to register an any-state transition.
    /// </summary>
    public interface IAnimTransitionRule
    {
        /// <summary>
        /// Source state identifier required for this rule to match, or <c>"*"</c> to match any active state.
        /// </summary>
        string FromStateId { get; }

        /// <summary>
        /// Target state identifier entered when <see cref="Evaluate"/> returns true.
        /// </summary>
        string ToStateId { get; }

        /// <summary>
        /// Rule evaluation priority; higher values are evaluated first by <see cref="AnimStateMachine.Tick"/>.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Evaluates whether the state machine should transition from <see cref="FromStateId"/> to <see cref="ToStateId"/>.
        /// </summary>
        /// <param name="currentState">Current playback snapshot of the state machine's target layer.</param>
        /// <returns>True to request transitioning to <see cref="ToStateId"/>; otherwise false.</returns>
        bool Evaluate(in AnimPlaybackState currentState);
    }

    /// <summary>
    /// Delegate-backed <see cref="IAnimTransitionRule"/> convenience implementation.
    /// </summary>
    public sealed class FuncAnimTransitionRule : IAnimTransitionRule
    {
        /// <inheritdoc/>
        public string FromStateId { get; }

        /// <inheritdoc/>
        public string ToStateId { get; }

        /// <inheritdoc/>
        public int Priority { get; }

        private readonly Func<AnimPlaybackState, bool> _predicate;

        /// <summary>
        /// Creates a transition rule backed by a delegate predicate.
        /// </summary>
        /// <param name="fromStateId">Source state identifier; null or empty values normalize to <c>"*"</c> (any state).</param>
        /// <param name="toStateId">Destination state identifier; must not be null.</param>
        /// <param name="predicate">Condition evaluated against the current layer playback snapshot; must not be null.</param>
        /// <param name="priority">Evaluation priority where higher values are checked earlier.</param>
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

        /// <inheritdoc/>
        public bool Evaluate(in AnimPlaybackState currentState) => _predicate(currentState);
    }
}
