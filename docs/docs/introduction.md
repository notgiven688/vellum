# Vellum Documentation

Vellum is an immediate-mode GUI library for C#. It is backend-neutral: application code builds UI with `Ui`, and a renderer backend turns Vellum draw data into graphics API calls.

## What Immediate Mode Means

In a retained-mode UI, the toolkit owns a long-lived tree of controls. Application code creates widgets once, mutates them later, and the toolkit remembers which object is which.

In an immediate-mode UI, your application owns the state and describes the interface again every frame:

```csharp
ui.Frame(width, height, mouse, input, root =>
{
    if (root.Button("Save").Clicked)
        Save();

    root.TextField("Name", ref name);
});
```

The button and text field are not persistent control objects. Each call describes what should exist this frame, returns what happened this frame, and emits draw data. Persistent application state stays in your variables (`name`, selected item IDs, open document models). Persistent UI state that belongs to Vellum, such as focus, scroll positions, active text edits, selected tabs, and window positions, is looked up by widget identity.

## Why Identity Matters

Because widgets are recreated by calls every frame, Vellum needs stable identifiers to connect this frame's calls to last frame's internal state. If an identifier changes, Vellum treats it as a different widget.

Vellum solves the common case by deriving IDs from visible labels and the current ID scope:

```csharp
ui.Button("Save");
ui.TextField("Name", ref name);
```

That means normal UI code does not need explicit IDs. When repeated rows use the same labels, wrap each row in a stable `Id(...)` scope:

```csharp
foreach (var item in items)
{
    using (ui.Id(item.Id))
    {
        ui.TextField("Name", ref item.Name);
        ui.Button("Delete");
    }
}
```

When two widgets in the same scope need the same label, pass `id:`:

```csharp
ui.Button("Save", id: "save-primary");
ui.Button("Save", id: "save-secondary");
```

Containers without visible labels, such as scroll areas and tab bars, ask for an explicit `UiId`:

```csharp
ui.ScrollArea(item.Id, 320f, 180f, area => DrawItem(area, item));
ui.TabBar("workspace-tabs", tabs => DrawTabs(tabs));
```

Use data IDs, GUIDs, stable names, or other durable values for identity. Avoid deriving IDs from editable labels unless losing that widget's UI state when the label changes is acceptable.

Start with the [Quickstart](quickstart.md) for small, working examples of the main API style.

Use the other guides when you need a specific topic:

- [API Style](guides/api-style.md) explains widget identity, explicit IDs, `using` scopes, and `static` state callbacks.
- [Text and Fonts](guides/text-and-fonts.md) explains the built-in text renderer, custom fonts, wrapping, clipping, and known text limitations.
- [Backend Implementation](guides/backends.md) explains how to implement `IRenderer`.
- The API section contains generated reference documentation from the public XML comments.
