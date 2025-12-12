using PlayablesAnimatorController.Core;
using NUnit.Framework;
using UnityEngine;

namespace PlayablesAnimatorController.Tests
{
    public sealed class PlayableAnimGraphTests
    {
        private static AnimationClip CreateDummyClip(string name, float durationSeconds, bool loop)
        {
            var clip = new AnimationClip { name = name, wrapMode = loop ? WrapMode.Loop : WrapMode.Once };
            var curve = AnimationCurve.Linear(0f, 0f, durationSeconds, 1f);
            clip.SetCurve(string.Empty, typeof(Transform), "localPosition.x", curve);
            return clip;
        }

        [Test]
        public void PlayableGraph_CrossFadesFiresNormalizedEventAndCompletes()
        {
            var go = new GameObject("AnimTestHost");
            var animator = go.AddComponent<Animator>();
            var clipA = CreateDummyClip("ClipA", 1.0f, loop: false);
            var clipB = CreateDummyClip("ClipB", 0.5f, loop: false);

            PlayableAnimGraph graph = null;
            try
            {
                graph = new PlayableAnimGraph(animator, "TestGraph", manualUpdateMode: true);

                Assert.IsTrue(graph.Play(AnimPlayRequest.Create(clipA, fadeDuration: 0f, priority: 0)));
                graph.Tick(0.1f);
                Assert.AreEqual(clipA, graph.GetLayerState(0).CurrentClip);

                bool eventFired = false;
                bool completedFired = false;

                var reqB = AnimPlayRequest.Create(
                    clipB,
                    fadeDuration: 0.2f,
                    layer: 0,
                    priority: 5,
                    onComplete: () => completedFired = true,
                    stateId: "Attack");
                reqB.Events = new[] { new AnimClipEvent(0.5f, () => eventFired = true) };

                Assert.IsTrue(graph.Play(in reqB));
                Assert.IsTrue(graph.GetLayerState(0).IsBlending);

                // Priority 0 request blocked while priority 5 clip is active
                Assert.IsFalse(graph.Play(AnimPlayRequest.Create(clipA, fadeDuration: 0.1f, priority: 0)));

                // Step past blend (0.2s) and normalized event (0.25s)
                graph.Tick(0.3f);
                Assert.IsFalse(graph.GetLayerState(0).IsBlending);
                Assert.IsTrue(eventFired);
                Assert.IsFalse(completedFired);

                // Step past clipB length (0.5s total)
                graph.Tick(0.3f);
                Assert.IsTrue(completedFired);
                Assert.IsTrue(graph.GetLayerState(0).HasCompleted);

                // Now lower-priority clipA is allowed again
                Assert.IsTrue(graph.Play(AnimPlayRequest.Create(clipA, fadeDuration: 0f, priority: 0)));
            }
            finally
            {
                graph?.Dispose();
                Object.DestroyImmediate(clipA);
                Object.DestroyImmediate(clipB);
                Object.DestroyImmediate(go);
            }
        }
    }
}
