# Animation

UI animation system for Unity, designed for practical production use, Inspector-driven setup, and list-based reuse.

## Purpose

This system was created to animate UI elements in a generic way, without relying on custom scripts for each screen, group, or widget.

`AnimationProperty` is the core configuration unit, but the main intended usage of the system is through **lists of `AnimationProperty`**, so the same flow can be reused across different UI contexts.

## Main Usage Rule

This system is expected to follow these rules:

- the primary usage is through `List<AnimationProperty>`;
- execution should happen through `AnimationPropertyController`;
- `Show` and `Hide` should operate on the same list;
- `ConfigureStartPosition()` should be executed only once per list.

Conceptually, a list represents a bidirectional animated group. The initial state prepared for that list is the hidden state, and that same group is reused both for reveal and hide.

## Recommended Flow

Expected usage flow:

1. create a `List<AnimationProperty>`;
2. configure its entries in the Inspector;
3. execute `ConfigureStartPosition()` once;
4. if needed, force the initial state with `ForceStartPosition()`;
5. use `RevealThis()` to show;
6. use `HideThis()` to hide.

Example:

```csharp
animations.ConfigureStartPosition();
animations.ForceStartPosition();

animations.RevealThis();
animations.HideThis();
```

## `AnimationProperty`

Each `AnimationProperty` describes a bidirectional animation for a UI target.

Expected contract:

- `ConfigureStartPosition()` prepares the initial state;
- `ForceStart()` immediately applies the initial state;
- `ForceEnd()` immediately applies the final state;
- `Show()` animates from the initial state to the visible state;
- `Hide()` returns from the visible state to the initial state.

Important: the system is designed to reuse **the same configuration** for both reveal and hide. There is no need to keep one list for reveal and another for hide.

## `AnimationPropertyController`

`AnimationPropertyController` is the recommended layer for operating animation lists.

It exposes the expected controls for using the system:

- `ConfigureStartPosition(this List<AnimationProperty>)`
- `ForceStartPosition(this List<AnimationProperty>)`
- `ForceEndPosition(this List<AnimationProperty>)`
- `RevealThis(this List<AnimationProperty>, ...)`
- `HideThis(this List<AnimationProperty>, ...)`
- `IsRevealed(this List<AnimationProperty>)`
- `IsAnimating(this List<AnimationProperty>)`

If another agent or system needs to integrate with this package, these extensions should be preferred over manually triggering each `AnimationProperty`.

## Initialization Convention

`ConfigureStartPosition()` should not be called on every transition.

It should be treated as a list preparation step, usually during an initial phase such as `Awake`, `Start`, or an explicit bootstrap stage for the group.

Intent summary:

- `ConfigureStartPosition()` prepares;
- `ForceStartPosition()` places the list in the hidden state;
- `RevealThis()` shows;
- `HideThis()` hides.

## Inspector Configuration

Each `AnimationProperty` allows configuring:

- target object;
- duration;
- show delay;
- hide delay;
- ease in;
- ease out;
- animation type;
- idle synchronization;
- manual start/end range, when supported.

The custom `PropertyDrawer` exists to improve Inspector usability and reduce operational mistakes. It also provides preview support and global default application.

## Supported Types

The current core supports:

- `Scale`
- `Fade`
- `OutsideScreen_Left`
- `OutsideScreen_Right`
- `OutsideScreen_Up`
- `OutsideScreen_Down`
- `Text_Char`
- `RectTween`

Manual start/end configuration is mainly intended for:

- `Scale`
- `Fade`
- `OutsideScreen_*`

## Idle Integration

The system integrates with components based on `UI_IdleBase`.

During primary animations, `AnimationProperty` can temporarily pause idles acting on the same channels:

- position;
- scale;
- rotation;
- alpha.

This coordination exists to avoid conflicts between idle animation and the main tween.

## Scope

This system was built for UI animation.

It was not designed to replace:

- a general gameplay animation system;
- timeline;
- a full visual state machine.

Its focus is:

- Inspector-configurable UI;
- low coupling;
- list-based reuse;
- simple usage in screens, groups, and UI components.

## Recommended Reading Order

If another agent needs to understand or evolve this package, the recommended reading order is:

1. `AnimationProperty`
2. `AnimationPropertyController`
3. system consumers, such as screen controllers or testers
4. `UI_IdleBase`, if idle interaction is relevant
5. editor scripts, if the task involves Inspector UX or tooling

## Evolution Guideline

When expanding this system:

- preserve list-based usage as the main path;
- preserve `Show` and `Hide` on the same list;
- do not turn `ConfigureStartPosition()` into a recurring transition step;
- prefer extending the controller and Inspector before creating more specific parallel flows.

## Summary

The expected architectural contract of this package is:

- UI animation system;
- primary usage through lists;
- execution via `AnimationPropertyController`;
- `Show` and `Hide` sharing the same list;
- `ConfigureStartPosition()` executed only once per list.

If a future change breaks any of these premises, it should be treated as an architectural change to the system, not as a small implementation tweak.
