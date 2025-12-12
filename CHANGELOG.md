# Changelog

## [1.0.0] - 2025-12-12
### Added
- `PlayableAnimGraph` pure C# wrapper around `PlayableGraph`, `AnimationLayerMixerPlayable`, and per-layer `AnimationMixerPlayable` cross-fade slots.
- `IAnimationDriver`, `AnimPlayRequest`, `AnimPlaybackState`, `AnimClipEvent`, `AnimLayerConfig`, and `AnimSetAsset`.
- Optional code-driven `AnimStateMachine` and `IAnimTransitionRule` with prioritized transitions, any-state rules, and auto-return on completion.
- `CustomAnimController` MonoBehaviour component supporting both 2D and 3D `AnimationClip`s without an `AnimatorController`.
- UI Toolkit `CustomAnimInspector`, `AnimDebuggerWindow`, 2D/3D quick-start samples, and NUnit EditMode tests.
