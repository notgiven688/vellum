# Vellum ID API Plan

## Direction

Use label-derived IDs by default, scoped IDs for repeated data, and explicit widget IDs only as a named escape hatch.

The goal is to keep simple UI concise while still giving dynamic UI a robust identity model.

## Core Model

Vellum should derive widget identity from:

- the current ID scope stack;
- the widget kind;
- the visible label or caption by default;
- an explicit local widget ID when one is supplied.

This keeps the common case simple:

```csharp
ui.Button("Save");
ui.Checkbox("Enabled", ref enabled);
ui.TextField("Name", ref name);
```

Labels are still part of identity in this default mode, similar to Dear ImGui, but Vellum should avoid ImGui's `##` and `###` string syntax.

## ID Scopes

Use ID scopes for repeated, dynamic, sorted, filtered, or data-bound UI:

```csharp
foreach (var item in items)
{
    ui.Id(item.Id, row =>
    {
        row.TextField("Name", ref item.Name);
        row.Button("Delete");
    });
}
```

Inside an ID scope, labels can stay local and simple. The effective identity becomes hierarchical:

```text
root / item.Id / TextField / "Name"
root / item.Id / Button / "Delete"
```

This avoids formatted string IDs like:

```csharp
ui.TextField($"item-{item.Id}-name", ref item.Name);
```

## Explicit Widget IDs

Some widgets still need a way to separate visible text from identity:

- two widgets with the same label in the same scope;
- dynamic or translated labels whose state should persist;
- labels that include changing values;
- cases where user code wants identity to survive label edits.

Use a named `id` parameter rather than ambiguous positional overloads:

```csharp
ui.Button("Save", id: "save-primary");
ui.Button("Save", id: "save-secondary");

ui.TextField("Display name", ref name, id: "profile-name");
ui.Button("Delete", id: item.Id);   // id: accepts UiId — implicitly converts from int/long/Guid/string
```

Avoid APIs like this as the primary shape:

```csharp
ui.Button("save-primary", "Save");
```

The positional two-string form is easy to misread because both arguments look like labels.

## Naming

Use `Id`, not `Key`, in the public API:

```csharp
ui.Id(item.Id, itemUi =>
{
    itemUi.Button("Delete");
});
```

Internally Vellum may still distinguish between an ID fragment and a resolved ID, for example:

```csharp
public readonly record struct UiIdPart;
internal readonly record struct UiId;
```

But application authors should only need to learn one word: ID.

## Typed ID Inputs

Do not force IDs through strings. Provide typed ID scope overloads:

```csharp
ui.Id("toolbar", ui => { });
ui.Id(item.Id, ui => { });
ui.Id(item.Guid, ui => { });
```

Recommended public inputs:

- `ReadOnlySpan<char>` or `string` for textual IDs;
- `int`;
- `long`;
- `ulong`;
- `Guid`.

Per-widget `id:` parameters accept the same set through the public `UiId` value type, which has implicit conversions from `string`, `int`, `long`, `ulong`, and `Guid`. So `Button("Save", id: 42)` and `Button("Save", id: item.Guid)` work without ceremony.

Avoid a generic `object id` overload because it can box value types, allocate through `ToString()`, and hide unstable identity choices.

## Scope Safety

Use callback scopes as the public ID API:

```csharp
ui.Id(item.Id, itemUi =>
{
    itemUi.Button("Delete");
});
```

All scope implementations must restore internal state with `try/finally`.

Manual push/pop should not be public API.

## Duplicate Detection

In debug builds, Vellum should track widget IDs seen during a frame and report duplicate IDs in the same relevant scope.

Diagnostics should explain the likely fixes:

- wrap repeated data in `ui.Id(item.Id, ...)`;
- provide a named `id:` when two widgets have the same visible label;
- avoid using dynamic labels as the only identity source when state must persist.

Internally, ID tracking is gated by a single counter (`_idTrackingDisabledDepth`). Any code path that needs to run widgets without registering them — measurement passes, the window content double-pass — increments the counter for the scope of that pass and decrements on exit. There is exactly one carve-out in `RegisterWidgetId`.

## Focus

Focus is keyed off the same widget IDs as duplicate detection — the kinded combination of widget kind, scope, and label or `id:`. There is no parallel un-kinded ID universe.

Public focus APIs:

- `Response.Focused` — true when the widget had keyboard focus this frame.
- `ui.RequestFocus(UiWidgetKind kind, UiId id)` — focus the next widget of `kind` whose resolved id matches.
- `ui.ClearFocus()` — clear focus.

`RequestFocus` requires a `UiWidgetKind` because identity is kinded; passing only a label would alias same-label widgets across kinds (a `Button("Save")` and a `Menu("Save")` in the same scope are distinct).

## Practical Rule

- Inert rendering/layout calls do not need IDs: `Label`, `Spacing`, `Separator`.
- Normal interactive widgets may derive IDs from labels.
- Repeated data gets an ID scope.
- Same-label or dynamic-label widgets get an explicit named `id:`.
