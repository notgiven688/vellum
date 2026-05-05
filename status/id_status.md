# Vellum ID API

Label-derived IDs by default, scoped IDs for repeated data, explicit widget IDs only as a named escape hatch. Simple UI stays concise; dynamic UI still has a robust identity model.

## Core Model

Widget identity is derived from:

- the current ID scope stack;
- the widget kind;
- the visible label or caption by default;
- an explicit local widget ID when one is supplied.

The common case is plain:

```csharp
ui.Button("Save");
ui.Checkbox("Enabled", ref enabled);
ui.TextField("Name", ref name);
```

Labels are part of identity in this default mode, similar to Dear ImGui, but Vellum does not use ImGui's `##` and `###` string syntax.

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

Inside an ID scope, labels stay local. Effective identity is hierarchical:

```text
root / item.Id / TextField / "Name"
root / item.Id / Button / "Delete"
```

This avoids formatted string IDs like `ui.TextField($"item-{item.Id}-name", ref item.Name)`.

## Explicit Widget IDs

Some widgets need to separate visible text from identity:

- two widgets with the same label in the same scope;
- dynamic or translated labels whose state should persist;
- labels that include changing values;
- identity that should survive label edits.

The shape is a named `id:` parameter, not a positional overload:

```csharp
ui.Button("Save", id: "save-primary");
ui.Button("Save", id: "save-secondary");

ui.TextField("Display name", ref name, id: "profile-name");
ui.Button("Delete", id: item.Id);   // id: accepts UiId — implicitly converts from int/long/Guid/string
```

A positional two-string form (`ui.Button("save-primary", "Save")`) is rejected because both arguments look like labels.

## Naming

The public API uses `Id`, not `Key`:

```csharp
ui.Id(item.Id, itemUi =>
{
    itemUi.Button("Delete");
});
```

Application authors only need to learn one word: ID.

## Typed ID Inputs

IDs are not forced through strings. ID scopes accept typed overloads:

```csharp
ui.Id("toolbar", ui => { });
ui.Id(item.Id, ui => { });
ui.Id(item.Guid, ui => { });
```

Public inputs:

- `ReadOnlySpan<char>` or `string` for textual IDs;
- `int`;
- `long`;
- `ulong`;
- `Guid`.

Per-widget `id:` parameters accept the same set through the public `UiId` value type, which has implicit conversions from `string`, `int`, `long`, `ulong`, and `Guid`. `Button("Save", id: 42)` and `Button("Save", id: item.Guid)` work without ceremony.

There is no generic `object id` overload: it would box value types, allocate through `ToString()`, and hide unstable identity choices.

## Scope Safety

Callback scopes are the public ID API:

```csharp
ui.Id(item.Id, itemUi =>
{
    itemUi.Button("Delete");
});
```

All scope implementations restore internal state with `try/finally`. Manual push/pop is not public.

## Duplicate Detection

In debug builds, Vellum tracks widget IDs seen during a frame and throws on duplicates in the same scope. The diagnostic names the likely fixes:

- wrap repeated data in `ui.Id(item.Id, ...)`;
- provide a named `id:` when two widgets share a visible label;
- avoid using dynamic labels as the only identity source when state must persist.

Internally, ID tracking is gated by a single counter (`_idTrackingDisabledDepth`). Code paths that need to run widgets without registering them — measurement passes, the window content double-pass — increment the counter for the scope of that pass and decrement on exit. There is exactly one carve-out in `RegisterWidgetId`.

## Focus

Focus is keyed off the same widget IDs as duplicate detection — the kinded combination of widget kind, scope, and label or `id:`. There is no parallel un-kinded ID universe.

Public focus APIs:

- `Response.Focused` — true when the widget had keyboard focus this frame.
- `ui.RequestFocus(UiWidgetKind kind, UiId id)` — focus the next widget of `kind` whose resolved id matches.
- `ui.ClearFocus()` — clear focus.

`RequestFocus` requires a `UiWidgetKind` because identity is kinded; passing only a label would alias same-label widgets across kinds (a `Button("Save")` and a `Menu("Save")` in the same scope are distinct).

## Practical Rule

- Inert rendering/layout calls do not need IDs: `Label`, `Spacing`, `Separator`.
- Normal interactive widgets derive IDs from labels.
- Repeated data gets an ID scope.
- Same-label or dynamic-label widgets get an explicit named `id:`.
