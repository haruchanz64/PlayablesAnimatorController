# PlayablesAnimatorController

Code-driven 2D and 3D animation controller for Unity 6 built directly on the `Playables` API. It plays 2D Sprite `AnimationClip` assets and 3D skeletal rigs on a standard `Animator` component without requiring an `AnimatorController` state graph.

## Features

- **Direct Playables pipeline (`PlayableAnimGraph`):** Drives `PlayableGraph`, `AnimationLayerMixerPlayable`, and per-layer 2-input `AnimationMixerPlayable` slots from pure C#, with optional `manualUpdateMode` for deterministic stepping or EditMode tests.
- **2D and 3D playback support:** Pass `fadeDuration: 0f` for instant 2D frame-by-frame clip switching, or positive durations for linear 3D skeletal cross-fades.
- **Masked and additive layers (`AnimLayerConfig`):** Configure per-layer `defaultWeight`, `AvatarMask`, and `isAdditive` flags on `CustomAnimController`, or fade overlay layers back out via `StopLayer(layer, fadeOutDuration)`.
- **Priority locks and normalized-time events (`AnimPlayRequest`, `AnimClipEvent`):** Keep lower-priority clips from interrupting higher-priority actions until a non-looping clip finishes, and invoke C# callbacks when playback crosses `[0, 1]` clip markers (including across loop wrap-arounds).
- **Keyed clip sets (`AnimSetAsset`):** Optional `ScriptableObject` mapping case-sensitive string keys to clips, default fade times, speeds, layers, priorities, and optional `nextKeyOnComplete` chaining via `PlayByKey`.
- **Optional code state machine (`AnimStateMachine`):** Register named `AnimStateDefinition` entries, prioritized `IAnimTransitionRule` predicates, `"*"` any-state rules, enter/exit hooks, and automatic return states on clip completion.
- **UI Toolkit inspection and debugging:** Ships with `CustomAnimInspector` and `AnimDebuggerWindow` (`Tools > HaruChanZ64 > Animation > PlayablesAnimatorController > Animation Debugger`) showing live layer progress bars, cross-fade weights, recent clip history, and manual test playback controls.

## Requirements

- Unity `6000.0` or newer
- Zero required external dependencies

## Installation

### Unity Package Manager (Git URL)

1. Open **Window > Package Manager** in Unity 6 (`6000.0+`).
2. Click **+** (top-left) and choose **Install package from git URL...**.
3. Enter:

```text
https://github.com/haruchanz64/PlayablesAnimatorController.git
```

### Local package

Add the package path to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.haruchanz64.playables-animator-controller": "file:../Plugins/PlayablesAnimatorController"
  }
}
```

## Quick start

1. Add `CustomAnimController` to any GameObject with an `Animator` component (it enforces `[RequireComponent(typeof(Animator))]`). Leave the `Animator` component's **Controller** field empty.
2. *(Optional)* Configure additional entries in the **Layers** list on `CustomAnimController` (for example, layer `1` with an upper-body `AvatarMask` and `defaultWeight = 0`).
3. *(Optional)* Create a keyed clip library via **Assets > Create > PlayablesAnimatorController > Animation Set**, populate its `entries` list, assign it to `Animation Set` on `CustomAnimController`, and set `Initial Key` to play a starting clip automatically on `OnEnable`.
4. Drive clips from your gameplay scripts using `CustomAnimController.Play(...)`, `CustomAnimController.PlayByKey(...)`, or an `AnimStateMachine`.
5. Enter Play Mode and open **Tools > HaruChanZ64 > Animation > PlayablesAnimatorController > Animation Debugger** (or click **Open Animation Debugger** in the `CustomAnimController` Inspector) to monitor layer progress, cross-fade weights, and clip history, or trigger manual test clips.

## Core concepts

### Playables graph and per-layer cross-fading

`PlayableAnimGraph` connects a single `AnimationLayerMixerPlayable` to an `AnimationPlayableOutput` bound to the target `Animator`. Each layer owns a 2-input `AnimationMixerPlayable`:

1. **Port 0 (active clip):** Receives the newly created `AnimationClipPlayable`.
2. **Port 1 (outgoing clip):** Holds the previous clip when `FadeDuration > 0.0001f` and a valid active playable already exists. During `Tick(deltaTime)`, port 0 ramps linearly from `0` to `1` while port 1 ramps from `1` to `0`, and port 1 is disconnected and destroyed once blending finishes.
3. **Instant switches:** When `FadeDuration <= 0.0001f` (typical for 2D sprites), the previous active playable is destroyed immediately and port 0 starts at weight `1`.
4. **On-demand layer expansion:** Playing on a layer index greater than or equal to `LayerCount` expands `_layerMixer` automatically so code can target higher layers without throwing out-of-range errors.

### Priority locks, completion, and normalized-time events

- **Priority gating:** While a layer has an active, incomplete clip (`IsPlaying && !HasCompleted`), requests with `request.Priority < activePriority` return `false` unless `request.ForceOverridePriority` is `true`. Once a non-looping clip reaches `time >= clip.length`, `HasCompleted` becomes `true`, `Priority` resets to `0`, `request.OnComplete` fires, and `OnClipCompleted` is raised.
- **Same-clip guard:** Re-requesting the currently active, incomplete clip with `RestartIfSame == false` keeps playback running without restarting and updates the playable's speed to `request.Speed`.
- **Normalized-time events (`AnimClipEvent`):** Each `Tick` checks markers in `[prevNorm, currNorm]` so events fire once per pass without frame-rate dependence. On looping clips, crossing a loop boundary fires remaining tail markers up to `1.0`, resets fired flags, increments `LoopCount`, and fires early markers in `[0.0, currNorm]`.

### Code-driven state machine (`AnimStateMachine`)

`AnimStateMachine` operates on any `IAnimationDriver` and a single target layer (`layer = 0` by default). On each `Tick()`:

1. Registered `IAnimTransitionRule` instances are sorted lazily in descending `Priority` order.
2. The first matching rule (`FromStateId == "*"` or ordinal match with `CurrentStateId`, excluding self-transitions) whose `Evaluate(in AnimPlaybackState)` returns `true` calls `TrySetState(rule.ToStateId)`.
3. If no transition triggers and the active state's clip has `HasCompleted == true` with a non-empty `AutoReturnStateId`, the machine transitions to `AutoReturnStateId` with `forceOverridePriority: true`.

## Code examples

### 1. Direct playback with priority lock, hit-frame events, and overlay layers

```csharp
using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.Core;
using UnityEngine;

public sealed class PlayerAnimator : MonoBehaviour
{
    [SerializeField] private CustomAnimController anim;
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip runClip;
    [SerializeField] private AnimationClip upperBodySlashClip;

    public void UpdateLocomotion(bool isMoving)
    {
        anim.Play(isMoving ? runClip : idleClip, fadeDuration: 0.15f, layer: 0);
    }

    public void PlayUpperBodySlash()
    {
        var req = AnimPlayRequest.Create(
            upperBodySlashClip,
            fadeDuration: 0.08f,
            layer: 1,
            priority: 10,
            restartIfSame: true,
            onComplete: () => anim.StopLayer(1, fadeOutDuration: 0.12f),
            stateId: "UpperSlash");

        req.Events = new[]
        {
            new AnimClipEvent(0.45f, () => Debug.Log("Hitbox active"))
        };

        anim.Play(in req);
    }
}
```

### 2. Keyed playback with `AnimSetAsset`

Assign an `AnimSetAsset` to `CustomAnimController.AnimationSet` in the Inspector (or via code) to play clips by string key and optionally override fade duration or target layer:

```csharp
using PlayablesAnimatorController.Components;
using UnityEngine;

public sealed class KeyedCharacterAnimator : MonoBehaviour
{
    [SerializeField] private CustomAnimController anim;

    public void TriggerDodge()
    {
        // Uses entry defaults from AnimSetAsset and chains nextKeyOnComplete automatically.
        anim.PlayByKey("Dodge", fadeOverride: 0.05f);
    }

    public void TriggerEmoteOnOverlay()
    {
        anim.PlayByKey("Wave", layerOverride: 1);
    }
}
```

### 3. State machine with a custom `IAnimTransitionRule`

Implement `IAnimTransitionRule` for reusable transition logic (such as exit-time gates or shared character checks) and register it alongside states on `AnimStateMachine`:

```csharp
using System;
using PlayablesAnimatorController.Components;
using PlayablesAnimatorController.Core;
using PlayablesAnimatorController.States;
using UnityEngine;

public sealed class ExitTimeTransitionRule : IAnimTransitionRule
{
    private readonly float _minNormalizedTime;
    private readonly Func<bool> _condition;

    public string FromStateId { get; }
    public string ToStateId { get; }
    public int Priority { get; }

    public ExitTimeTransitionRule(
        string fromStateId,
        string toStateId,
        float minNormalizedTime,
        Func<bool> condition,
        int priority = 0)
    {
        FromStateId = fromStateId;
        ToStateId = toStateId;
        _minNormalizedTime = Mathf.Clamp01(minNormalizedTime);
        _condition = condition;
        Priority = priority;
    }

    public bool Evaluate(in AnimPlaybackState currentState)
    {
        return currentState.NormalizedTime >= _minNormalizedTime && _condition();
    }
}

[RequireComponent(typeof(CustomAnimController))]
public sealed class ComboStateDriver : MonoBehaviour
{
    [SerializeField] private AnimationClip idleClip;
    [SerializeField] private AnimationClip combo1Clip;
    [SerializeField] private AnimationClip combo2Clip;

    private AnimStateMachine _fsm;
    private bool _comboQueued;

    private void Awake()
    {
        var anim = GetComponent<CustomAnimController>();
        _fsm = new AnimStateMachine(anim, layer: 0);

        _fsm.RegisterState("Idle", idleClip, fadeDuration: 0.1f)
            .RegisterState(new AnimStateDefinition
            {
                StateId = "Combo1",
                Clip = combo1Clip,
                FadeDuration = 0.05f,
                Priority = 10,
                AutoReturnStateId = "Idle",
                OnEnter = () => _comboQueued = false
            })
            .RegisterState("Combo2", combo2Clip, fadeDuration: 0.05f, priority: 10, autoReturnStateId: "Idle");

        _fsm.AddTransition(new ExitTimeTransitionRule(
            fromStateId: "Combo1",
            toStateId: "Combo2",
            minNormalizedTime: 0.6f,
            condition: () => _comboQueued,
            priority: 20));

        _fsm.TrySetState("Idle");
    }

    private void Update() => _fsm.Tick();

    public void PressAttack()
    {
        if (_fsm.CurrentStateId == "Combo1")
        {
            _comboQueued = true;
            return;
        }

        _fsm.TrySetState("Combo1");
    }
}
```

### 4. Standalone `PlayableAnimGraph` in manual update mode

For custom update schedulers, timeline previews, or EditMode unit tests, construct `PlayableAnimGraph` directly with `manualUpdateMode: true` and dispose it when finished:

```csharp
using PlayablesAnimatorController.Core;
using UnityEngine;

public sealed class ManualGraphStepper
{
    public static void StepClip(Animator animator, AnimationClip clip, float dt)
    {
        using var graph = new PlayableAnimGraph(animator, "ManualPreview", manualUpdateMode: true);
        graph.Play(AnimPlayRequest.Create(clip, fadeDuration: 0f));
        graph.Tick(dt);
        AnimPlaybackState state = graph.GetLayerState(0);
        Debug.Log($"{state.StateId}: {state.NormalizedTime:P0}");
    }
}
```

## Extension contracts and core API

| Type | Namespace | Purpose | Built-in implementation |
| :--- | :--- | :--- | :--- |
| `IAnimationDriver` | `PlayablesAnimatorController.Core` | Unified playback contract for clip playback, keyed lookups, layer weights, speed, and state queries | `CustomAnimController` (plus mock drivers in tests) |
| `PlayableAnimGraph` | `PlayablesAnimatorController.Core` | Pure C# `IDisposable` wrapper around `PlayableGraph`, `AnimationLayerMixerPlayable`, and per-layer cross-fade mixers | `PlayableAnimGraph` |
| `CustomAnimController` | `PlayablesAnimatorController.Components` | `MonoBehaviour` attaching a `PlayableAnimGraph` to an `Animator` with `AnimSetAsset` support and debugger history | `CustomAnimController` |
| `AnimPlayRequest` | `PlayablesAnimatorController.Core` | Value struct describing clip, layer, fade duration, speed, start time, priority, completion callback, and events | `AnimPlayRequest.Create(...)` |
| `AnimClipEvent` | `PlayablesAnimatorController.Core` | Readonly struct pairing a `[0, 1]` normalized marker with a callback | `AnimClipEvent` |
| `AnimPlaybackState` | `PlayablesAnimatorController.Core` | Observable snapshot of a layer's active/previous clips, timing, weights, loop count, completion, and priority | Returned by `GetLayerState` |
| `AnimLayerConfig` | `PlayablesAnimatorController.Core` | Serialized layer weight, `AvatarMask`, and additive toggle | Used by `CustomAnimController` and `PlayableAnimGraph` |
| `AnimSetAsset` | `PlayablesAnimatorController.Core` | `ScriptableObject` mapping case-sensitive keys to `AnimClipEntry` records | `AnimSetAsset` |
| `AnimStateMachine` | `PlayablesAnimatorController.States` | Pure C# state machine driving one `IAnimationDriver` layer with prioritized rules and auto-return | `AnimStateMachine` |
| `IAnimTransitionRule` | `PlayablesAnimatorController.States` | Pluggable transition predicate evaluated against `AnimPlaybackState` (`"*"` for any-state rules) | `FuncAnimTransitionRule` |

## Included samples

Import **2D & 3D Quick Start** (`Samples~/QuickStart`) from the Package Manager **Samples** tab:

- `Sample2DCodeAnimator`: 2D sprite animation controller using `AnimStateMachine` on layer `0` with `fadeDuration: 0f` for instant switches across `Idle`, `Run`, `Jump` (priority `5`), and `Attack` (priority `10`, `autoReturnStateId: "Idle"`).
- `Sample3DLayeredAnimator`: 3D layered controller demonstrating `0.15f` locomotion cross-fades on layer `0`, a priority-`10` upper-body slash on layer `1` with an `AnimClipEvent` at `0.45` normalized time, and `StopLayer(1, fadeOutDuration: 0.12f)` on completion.

## Testing

The package includes EditMode NUnit tests under `Tests/Editor/` runnable from **Window > General > Test Runner** (EditMode tab):

- `Tests/Editor/PlayableAnimGraphTests.cs`: verifies `PlayableAnimGraph` in `manualUpdateMode: true` across clip playback, cross-fade blending, priority blocking while a higher-priority clip is active, normalized-time `AnimClipEvent` invocation, non-looping `onComplete` execution, and priority lock release after completion.
- `Tests/Editor/AnimStateMachineTests.cs`: verifies `AnimStateMachine` against a fake `IAnimationDriver` for state registration, predicate transitions, priority lockout during high-priority states, and `AutoReturnStateId` transition when a clip finishes.

## Troubleshooting and limitations

- **Leave the `Animator` Controller slot empty:** `CustomAnimController` binds its own `PlayableGraph` directly to the `Animator`. Assigning a separate `RuntimeAnimatorController` on the same `Animator` is unnecessary and can produce unexpected evaluation behavior.
- **Foot IK and Playable IK are disabled on created playables:** `PlayableAnimGraph.Play` calls `SetApplyFootIK(false)` and `SetApplyPlayableIK(false)` on each `AnimationClipPlayable`.
- **Non-looping completion requires `AnimationClip.isLooping == false`:** `OnComplete`, `OnClipCompleted`, `HasCompleted`, `AutoReturnStateId`, and automatic priority release only trigger when `clip.isLooping` is `false` and `clip.length > 1e-5f`. Looping clips wrap `NormalizedTime` and increment `LoopCount` instead.
- **Negative playback speed does not trigger events or completion:** `FireClipEventsInRange` tests `marker >= prevNorm && marker <= currNorm`, and completion checks `time >= length`. Reversed clips (`Speed < 0`) will not fire `AnimClipEvent` markers or complete automatically.
- **Playing on a muted layer restores layer weight to `1`:** When `LayerWeight <= 0.001f` (for example after `StopLayer` or when initialized with `defaultWeight = 0`), calling `Play` on that layer cancels any active layer fade-out and sets its weight to `1f`.
- **Dynamically expanded layers have no `AvatarMask` or additive setting:** Calling `Play` with a `Layer` index beyond the configured `layers` list expands the `AnimationLayerMixerPlayable` automatically, but dynamically added layers use full-body override blending. Configure masked or additive layers up front in `CustomAnimController` or via `layerConfigs` in `PlayableAnimGraph`.
- **Stopping layer `0` vs. overlay layers:** Immediate `StopLayer(layer, 0f)` clears playables on any layer, but only resets `LayerWeight` to `0` when `layer > 0`. Passing a positive `fadeOutDuration` fades `LayerWeight` to `0` before clearing playables on any layer, including layer `0`.
- **`AnimSetAsset` lookup keys are case-sensitive and cached by count:** `AnimSetAsset.TryGetEntry` uses `StringComparer.Ordinal` and keeps the first valid entry if duplicate keys exist. In Play Mode, mutating existing keys in `entries` without changing `entries.Count` does not invalidate the cached dictionary.

## License

[MIT](LICENSE.md)
