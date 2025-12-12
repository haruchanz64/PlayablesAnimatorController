using System;
using System.Collections.Generic;
using PlayablesAnimatorController.Core;
using PlayablesAnimatorController.States;
using NUnit.Framework;
using UnityEngine;

namespace PlayablesAnimatorController.Tests
{
    public sealed class AnimStateMachineTests
    {
        private sealed class FakeDriver : IAnimationDriver
        {
            public int LayerCount => 1;
            public AnimPlaybackState State;
            public readonly List<string> PlayedIds = new List<string>();

            public event Action<int, AnimationClip> OnClipCompleted;
            public event Action<int, string, AnimationClip> OnClipStarted;

            public bool Play(in AnimPlayRequest request)
            {
                if (!request.ForceOverridePriority && State.IsPlaying && !State.HasCompleted && request.Priority < State.Priority)
                {
                    return false;
                }

                State = new AnimPlaybackState
                {
                    Layer = request.Layer,
                    StateId = request.StateId,
                    CurrentClip = request.Clip,
                    Priority = request.Priority,
                    IsPlaying = true,
                    HasCompleted = false
                };
                PlayedIds.Add(request.StateId);
                OnClipStarted?.Invoke(request.Layer, request.StateId, request.Clip);
                return true;
            }

            public bool Play(AnimationClip clip, float fadeDuration = 0.15f, int layer = 0, float speed = 1f, int priority = 0)
            {
                var req = AnimPlayRequest.Create(clip, fadeDuration, layer, speed, priority);
                return Play(in req);
            }

            public bool PlayByKey(string key, float? fadeOverride = null, int? layerOverride = null) => false;
            public void StopLayer(int layer, float fadeOutDuration = 0f) => State.IsPlaying = false;
            public void SetLayerWeight(int layer, float weight) => State.LayerWeight = weight;
            public void SetSpeed(float speed, int layer = 0) => State.Speed = speed;
            public AnimPlaybackState GetLayerState(int layer = 0) => State;

            public void MarkCompleted()
            {
                State.HasCompleted = true;
                State.Priority = 0;
                OnClipCompleted?.Invoke(0, State.CurrentClip);
            }
        }

        [Test]
        public void StateMachine_TransitionsByPriorityAndAutoReturnsOnCompletion()
        {
            var driver = new FakeDriver();
            var fsm = new AnimStateMachine(driver);

            var idleClip = new AnimationClip { name = "Idle" };
            var runClip = new AnimationClip { name = "Run" };
            var attackClip = new AnimationClip { name = "Attack" };

            try
            {
                bool moving = false;
                fsm.RegisterState("Idle", idleClip, priority: 0)
                   .RegisterState("Run", runClip, priority: 0)
                   .RegisterState("Attack", attackClip, priority: 10, autoReturnStateId: "Idle");

                fsm.AddTransition("Idle", "Run", _ => moving)
                   .AddTransition("Run", "Idle", _ => !moving);

                Assert.IsTrue(fsm.TrySetState("Idle"));
                Assert.AreEqual("Idle", fsm.CurrentStateId);

                moving = true;
                fsm.Tick();
                Assert.AreEqual("Run", fsm.CurrentStateId);

                // Higher-priority Attack locks out lower-priority transitions until complete
                Assert.IsTrue(fsm.TrySetState("Attack"));
                Assert.AreEqual("Attack", fsm.CurrentStateId);
                Assert.IsFalse(fsm.TrySetState("Run"));

                // Once Attack finishes, Tick auto-returns to Idle
                driver.MarkCompleted();
                fsm.Tick();
                Assert.AreEqual("Idle", fsm.CurrentStateId);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(idleClip);
                UnityEngine.Object.DestroyImmediate(runClip);
                UnityEngine.Object.DestroyImmediate(attackClip);
            }
        }
    }
}
