using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.Core;
using UnityEngine;

namespace PlayablesAnimatorController.Samples
{
    /// <summary>
    /// Example 3D layered controller demonstrating smooth cross-fades on layer 0,
    /// masked upper-body attacks on layer 1, and normalized-time hit-window callbacks.
    /// </summary>
    [RequireComponent(typeof(CustomAnimController))]
    public sealed class Sample3DLayeredAnimator : MonoBehaviour
    {
        [SerializeField] private AnimationClip idleClip;
        [SerializeField] private AnimationClip runClip;
        [SerializeField] private AnimationClip upperBodySlashClip;

        private CustomAnimController _anim;

        private void Awake()
        {
            _anim = GetComponent<CustomAnimController>();
        }

        /// <summary>
        /// Cross-fades base layer 0 between the run and idle clips based on movement state.
        /// </summary>
        /// <param name="isMoving">True to play the run clip; false to play the idle clip.</param>
        public void UpdateLocomotion(bool isMoving)
        {
            _anim.Play(isMoving ? runClip : idleClip, fadeDuration: 0.15f, layer: 0);
        }

        /// <summary>
        /// Plays the upper-body slash clip on layer 1 with a hit-frame callback at 45% progress and fades layer 1 out on completion.
        /// </summary>
        public void PlayUpperBodySlash()
        {
            var req = AnimPlayRequest.Create(
                upperBodySlashClip,
                fadeDuration: 0.08f,
                layer: 1,
                priority: 10,
                restartIfSame: true,
                onComplete: () => _anim.StopLayer(1, fadeOutDuration: 0.12f),
                stateId: "UpperSlash");

            req.Events = new[]
            {
                new AnimClipEvent(0.45f, () => Debug.Log("[Sample3DLayeredAnimator] Hitbox active at 45% normalized time"))
            };

            _anim.Play(in req);
        }
    }
}
