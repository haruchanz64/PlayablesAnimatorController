# PlayablesAnimatorController (Unity 6)

Code-driven 2D and 3D animation controller for Unity 6 built on the `Playables` API. It plays 2D Sprite `AnimationClip`s and 3D skeletal rigs directly on an `Animator` component without requiring an `AnimatorController` state graph.

## Features

- **No AnimatorController state graph:** Drives `PlayableGraph`, `AnimationLayerMixerPlayable`, and per-layer `AnimationMixerPlayable` instances directly from C#.
- **2D and 3D support:** Use `fadeDuration = 0` for instant 2D sprite clip switching or positive fade durations for smooth 3D skeletal cross-fades.
- **Masked and additive layers:** Configure `AnimLayerConfig` entries with `AvatarMask` assets and per-layer weights, or fade layers out with `StopLayer(layer, fadeOutDuration)`.
- **Priority locks and normalized-time events:** Prevent lower-priority locomotion clips from interrupting higher-priority attacks or hitstun, and trigger callbacks at exact `[0..1]` normalized clip progress via `AnimClipEvent`.
- **Optional code state machine (`AnimStateMachine`):** Register named states, prioritized `IAnimTransitionRule` predicates, any-state transitions, and automatic return states on clip completion.
- **UI Toolkit inspection:** Includes `CustomAnimInspector` and `AnimDebuggerWindow` (`Tools > HaruChanZ64 > Animation > PlayablesAnimatorController > Animation Debugger`) displaying live per-layer progress bars, blend weights, clip history, and manual test playback.

## Installation

1. Open **Window > Package Manager** in Unity 6 (`6000.0+`).
2. Click **+** (top-left) > **Install package from git URL...**
3. Enter:
   ```text
   https://github.com/haruchanz64/PlayablesAnimatorController.git
   ```

## Quick start

1. Add `CustomAnimController` to any GameObject with an `Animator` (leave the `Animator`'s Controller slot empty).
2. Play clips directly in code:

```csharp
using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.Core;
using UnityEngine;

public sealed class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private CustomAnimController anim;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip attackClip;

    public void PlayIdle() => anim.Play(idleClip, fadeDuration: 0.15f);

    public void PlayAttack()
    {
        var req = AnimPlayRequest.Create(
            attackClip,
            fadeDuration: 0.08f,
            priority: 10,
            restartIfSame: true,
            onComplete: PlayIdle);

        req.Events = new[]
        {
            new AnimClipEvent(0.4f, () => Debug.Log("Hit frame reached"))
        };

        anim.Play(in req);
    }
}
```

3. For state-based transitions, construct `new AnimStateMachine(anim, layer: 0)`, register states with `RegisterState(...)`, add predicates with `AddTransition(...)`, and call `Tick()` in `Update()`.

## License

[MIT](LICENSE.md)
