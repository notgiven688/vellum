# API Style

Vellum is immediate-mode: application state stays in your code, and each frame describes the UI from that state. The public API is shaped around three related choices: widget identity, scoped layout, and optional state-passing callbacks.

## Widget Identity

Visible widgets derive their identity from the visible label and the current ID scope:

```csharp
ui.Button("Save");
ui.TextField("Name", ref item.Name);
```

That keeps normal UI code terse. When repeated rows use the same labels, add a parent ID scope for each data object:

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

When two widgets in the same scope need the same visible label, give the widget an explicit `id:`:

```csharp
ui.Button("Save", id: "save-primary");
ui.Button("Save", id: "save-secondary");
```

The `id:` parameter accepts `UiId`, which converts implicitly from `string`, `int`, `long`, `ulong`, and `Guid`.

## Explicit-ID Containers

Some containers do not have a visible label. Those take an explicit `UiId` as the first argument:

```csharp
ui.ScrollArea(item.Id, 320f, 180f, area =>
{
    area.Label(item.Name);
});

ui.TabBar("workspace-tabs", tabs =>
{
    tabs.Tab("Overview", tab => tab.Label("Summary"));
});
```

Use stable data identity here. If a container ID is based on editable text, changing the text creates a new identity and loses that container's state.

## Scope Forms

Layout-like scopes expose `using` handles:

```csharp
using (ui.Row())
{
    ui.Button("OK");
    ui.Button("Cancel");
}
```

Handles are the best choice inside hot loops or when the body needs normal C# control flow, `ref` values, or `Span<T>` locals.

Container scopes also expose callback forms:

```csharp
ui.Panel(320f, panel =>
{
    panel.Label("Settings");
});
```

Use the `TState` overload with a `static` lambda when the body would otherwise capture application state:

```csharp
ui.Window("Inspector", inspectorWindow, 320f, selected, static (window, selected) =>
{
    window.Label(selected.Name);
});
```

For multiple values, pass a tuple:

```csharp
ui.ScrollArea(
    "items",
    320f,
    240f,
    (Items: items, SelectedId: selectedId),
    static (area, state) =>
    {
        foreach (var item in state.Items)
            area.Selectable(item.Name, item.Id == state.SelectedId);
    });
```

The `TState` shape avoids hidden closure allocations when you use a `static` lambda. Delayed scopes such as windows, popups, menus, and selected tab content store the delegate and state separately before rendering. Value-type state may box in those delayed paths; application state objects and other reference types do not.

