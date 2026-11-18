using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.States;
using UnityEngine;

namespace PlayablesAnimatorController.Samples
{
    /// <summary>
    /// Example 2D sprite animation controller driven entirely in C# with zero AnimatorController graph.
    /// Uses fadeDuration = 0 for crisp frame-by-frame 2D sprite switching.
    /// </summary>
    [RequireComponent(typeof(CustomAnimController))]
    public sealed class Sample2DCodeAnimator : MonoBehaviour
    {
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip runClip;
        [SerializeField] private AnimationClip jumpClip;
        [SerializeField] private AnimationClip attackClip;

        private CustomAnimController _anim;
        private AnimStateMachine _fsm;

        /// <summary>
        /// Current horizontal movement speed used by Idle/Run transition predicates (threshold 0.1).
        /// </summary>
        public float HorizontalSpeed { get; set; }

        /// <summary>
        /// Whether the character is grounded; transitions to Jump when false.
        /// </summary>
        public bool IsGrounded { get; set; } = true;

        /// <summary>
        /// Builds the layer-0 <see cref="AnimStateMachine"/> with Idle, Run, Jump, and auto-returning Attack states and enters Idle.
        /// </summary>
        private void Awake()
        {
            _anim = GetComponent<CustomAnimController>();
            _fsm = new AnimStateMachine(_anim, layer: 0);

            _fsm.RegisterState("Idle", idleClip, fadeDuration: 0f)
                .RegisterState("Run", runClip, fadeDuration: 0f)
                .RegisterState("Jump", jumpClip, fadeDuration: 0f, priority: 5)
                .RegisterState("Attack", attackClip, fadeDuration: 0f, priority: 10, autoReturnStateId: "Idle");

            _fsm.AddAnyTransition("Jump", _ => !IsGrounded, priority: 5)
                .AddTransition("Jump", "Idle", _ => IsGrounded && Mathf.Abs(HorizontalSpeed) < 0.1f)
                .AddTransition("Jump", "Run", _ => IsGrounded && Mathf.Abs(HorizontalSpeed) >= 0.1f)
                .AddTransition("Idle", "Run", _ => Mathf.Abs(HorizontalSpeed) >= 0.1f)
                .AddTransition("Run", "Idle", _ => Mathf.Abs(HorizontalSpeed) < 0.1f);

            _fsm.TrySetState("Idle");
        }

        private void Update()
        {
            _fsm.Tick();
        }

        /// <summary>
        /// Requests the priority-10 Attack state, which locks out lower-priority locomotion states and returns to Idle on completion.
        /// </summary>
        public void TriggerAttack()
        {
            _fsm.TrySetState("Attack");
        }
    }
}
