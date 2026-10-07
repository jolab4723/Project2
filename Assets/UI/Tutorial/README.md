# KY Tutorial Overlay

`KY_TutorialOverlay.prefab` is a reusable full-screen tutorial Canvas. It does not start automatically.
It is created at runtime by one persistent `KY_TutorialBootstrap`, rather than being placed
in every battle scene.

## Setup

1. Place `KY_TutorialBootstrap` once in the persistent startup manager and assign this prefab.
2. Configure `KY_TutorialOverlay.steps` on the `TutorialOverlay` child.
3. Use `runtimeHighlightTarget` for a scene HUD target instead of serializing a scene reference:
   `BottomHud`, `TopLeftHud`, or `TopRightHud`.
4. Keep movement and attack steps as illustrations without a highlight target.
5. Bootstrap waits for `KY_HUDManager.Ready`, creates the prefab, then calls `Open()`.
6. It handles `Completed` and `Skipped`, records completion, and removes the instance.

## Step modes

- Default: pauses the game and exposes the Next button.
- `allowGameplayInput`: keeps time and input running while the prompt is visible.
- `advanceAutomatically`: hides the Next button. The gameplay controller advances the UI by calling `ShowNextStep()` after it detects the required action.

The overlay owns presentation only: dimming, spotlight, text, next, and skip. `tutorialDisplay`
controls whether to show (`0`: always, `1`: once, `2`: never), while `tutorialCompleted`
stores whether the one-time tutorial has already been seen.
