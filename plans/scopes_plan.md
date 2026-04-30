# Vellum Scope API Plan

## Direction

Vellum avoids public `Begin* / End*` pairs almost everywhere. A scoped construct in Vellum opens and closes through one of three idioms — a `using`-disposable handle, a single `Action<Ui>` callback, or an `Action<Ui, TState>` callback — and each scoped feature picks the subset of those forms that fits its usage.

The user should never have to remember to call an `End`. The single deliberate exception is `BeginFrame` / `EndFrame`, where the host loop owns timing across the boundary.

## The Three Scope Idioms

### 1. `using` + ref-struct handle

```csharp
using (ui.Row())
{
    ui.Button("OK");
    ui.Button("Cancel");
}

using (ui.Disabled(!canSave))
{
    ui.Button("Save");
}

using (ui.Id(item.Id))
{
    ui.TextField("Name", ref item.Name);
}
```

- Handle is a `ref struct`, so it stays on the stack. It cannot be boxed, captured, stored in a field, returned from `async`, used inside `yield return`, or crossed over `await`.
- Zero allocation, zero indirection.
- The body is plain in-line code — `ref` parameters, control flow (`return`, `break`, `continue`), and `Span<T>` locals all work as they do anywhere else.
- `Dispose` is idempotent; an accidental double-dispose is a no-op rather than a stack corruption.

Used by today: `Id(...)`, `Row()` / `Column()` / `FixedWidth()` / `MaxWidth()` (`LayoutScopeHandle`), `Disabled(...)` (`DisabledScopeHandle`).

### 2. Single-callback lambda — `Action<Ui>`

```csharp
ui.Window("Inspector", state, 320f, w =>
{
    w.Label("Position");
    w.Vec3Field(ref pos);
});

ui.Panel(p =>
{
    p.Button("Reset");
    p.Button("Apply");
});

ui.TreeNode("Hierarchy", t =>
{
    foreach (var child in scene.Children)
        t.Label(child.Name);
});
```

- Reads as a block, makes the scope visually obvious, removes any way to forget closing the scope.
- The compiler caches the delegate when the lambda captures nothing. The moment it captures, every call allocates a closure plus a delegate.
- Best fit for top-level layout, windows, panels, menus, popups — code paths that run once per frame and aren't allocation-sensitive.

Used by today: `Frame(...)`, `Window(...)`, `Panel(...)`, `MenuBar(...)`, `Menu(...)`, `ContextMenu(...)`, `ModalPopup(...)`, `TabBar(...)`, `Tab(...)`, `TreeNode(...)`, `ScrollArea(...)` / `ScrollAreaBoth(...)`, plus `Action<Ui>` overloads of `Id`, `Row` / `Column` / `FixedWidth` / `MaxWidth`, and `Disabled`.

### 3. State-passing lambda — `Action<Ui, TState>`

```csharp
foreach (var item in items)
{
    ui.Id(item.Id, item, static (row, item) =>
    {
        row.TextField("Name", ref item.Name);
        row.Button("Delete");
    });
}

ui.Window("Inspector", state, 320f, selected, static (w, sel) =>
{
    w.Label(sel.Name);
});
```

- `static` on the lambda forbids captures, so the delegate is cached once per call site and never reallocated.
- `TState` flows the per-iteration data in by parameter instead of by capture. Use a tuple when more than one value is needed: `(item, index, isSelected)`.
- Best fit for content inside loops, scrolled lists, virtualised trees, or any other path that runs many times per frame.

Used by today: every scope that has an `Action<Ui>` overload also has a paired `Action<Ui, TState>` overload — `Frame`, `Id`, `Disabled`, `Window`, `Panel`, `MenuBar`, `Menu`, `ContextMenu`, `ModalPopup`, `TabBar`, `Tab`, `TreeNode`, `ScrollArea`, `ScrollAreaBoth`.

## What's Available Where

| Construct                                    | `using` handle | `Action<Ui>` | `Action<Ui, TState>` |
| -------------------------------------------- | :------------: | :----------: | :------------------: |
| `Id`                                         | yes            | yes          | yes                  |
| `Row` / `Column` / `FixedWidth` / `MaxWidth` | yes            | yes          | —                    |
| `Disabled`                                   | yes            | yes          | yes                  |
| `Frame`                                      | —              | yes          | yes                  |
| `Window`                                     | —              | yes          | yes                  |
| `Panel`                                      | —              | yes          | yes                  |
| `MenuBar` / `Menu` / `ContextMenu`           | —              | yes          | yes                  |
| `ModalPopup`                                 | —              | yes          | yes                  |
| `TabBar` / `Tab`                             | —              | yes          | yes                  |
| `TreeNode`                                   | —              | yes          | yes                  |
| `ScrollArea` / `ScrollAreaBoth`              | —              | yes          | yes                  |

Two patterns to notice:

- Layout primitives (`Row`, `Column`, `FixedWidth`, `MaxWidth`) are the only constructs that expose a handle but no `TState` lambda. They run inside hot per-row code, so the handle is the recommended shape; the `Action<Ui>` overload exists for short top-level layouts. A `TState` overload is unnecessary because users either reach for the handle (zero alloc) or write a one-off lambda.
- Container widgets that also need to reason about returned `Response`, animation state, or popup wiring (`Window`, `Panel`, `Popup`, `TabBar`, `TreeNode`, `ScrollArea`) only expose the lambda forms. The lambda boundary is also where Vellum performs measurement passes, scroll virtualisation, and clipping; these would be awkward to bracket with a handle.

## Choosing Between the Forms

| Situation                                              | Use                                |
| ------------------------------------------------------ | ---------------------------------- |
| Inner loop, hot path                                   | `using` handle, or `static` `TState` lambda |
| Top-level layout, per frame, captures nothing          | `Action<Ui>` lambda                 |
| Need to assign to outer locals from inside the scope   | `using` handle (lambdas can't)      |
| Need `ref` / `Span<T>` locals across the scope         | `using` handle                      |
| Need to `return` / `break` / `continue` out            | `using` handle                      |
| Construct only exposes lambda forms                    | `static` `TState` lambda when in a loop, plain lambda otherwise |

The `using` handle should be presented as a first-class option, not as an advanced escape hatch — it is the only form that composes cleanly with `ref`, `Span<T>`, and ordinary control flow.

## Mixing Idioms

The three forms compose freely, and idiomatic Vellum code mixes them by layer:

```csharp
ui.Window("Items", state, 320f, w =>
{
    using (w.Column())
    {
        foreach (var item in items)
        {
            w.Id(item.Id, item, static (row, item) =>
            {
                using (row.Row())
                {
                    row.TextField("Name", ref item.Name);
                    row.Button("Delete");
                }
            });
        }
    }
});
```

Outer container as a lambda, inner layout as a `using` handle, per-row identity as a `static` `TState` lambda, innermost layout again as a `using` handle. Each layer chose the form that minimises noise and allocation for its job.

## Pairing and Exception Safety

- Every public scope is exception-safe regardless of form. Lambda forms wrap the body in `using (Open(...)) content(...)`, and `Frame` uses an explicit `try / finally`.
- `IdScopeHandle.Dispose`, `LayoutScopeHandle.Dispose`, and `DisabledScopeHandle.Dispose` all null their internal `Ui` reference before unwinding state, so double-dispose and dispose-after-finally are safe.
- Manual `Begin* / End*` is intentionally not exposed for any scope except `Frame`. Pairing those by hand makes the immediate-mode loop's exception path a user concern, which is the failure mode the three idioms exist to remove.

## The `Frame` Exception

`Frame(...)` is the one place where Vellum publishes both an explicit `BeginFrame` / `EndFrame` pair *and* the `Action<Ui>` / `Action<Ui, TState>` forms.

```csharp
// Lambda form — preferred when the host can run inside a callback.
ui.Frame(frame, mouse, input, w => DrawScene(w));

// Begin/End form — for hosts whose game loop cannot accept a callback.
ui.BeginFrame(frame, mouse, input);
try { DrawScene(ui); }
finally { ui.EndFrame(); }
```

The split is justified because `BeginFrame` and `EndFrame` straddle host concerns — input snapshotting, render queue submission, swap-chain timing — that some embedders need to interleave between Vellum and their own code. Every other scope is fully owned by Vellum and so does not need the explicit pair.

## Naming

- Scope-opening methods are named after the construct, not the action: `Window`, `Panel`, `TreeNode`, `Row`, `Disabled`, `Id`. Never `BeginWindow` / `EndWindow`.
- `Frame` keeps its `BeginFrame` / `EndFrame` pair for host-loop integration. No other public `Begin*` / `End*` methods should be added.
- Handle types are named `<Scope>ScopeHandle` (`IdScopeHandle`, `LayoutScopeHandle`, `DisabledScopeHandle`) and are always nested `ref struct` types on `Ui`.

## Adding a New Scope

When introducing a new scoped construct, the default checklist is:

1. Pick the verb-free name (`Foo`, not `BeginFoo`).
2. Implement an internal `EnterFoo` / `ExitFoo` pair that mutates `Ui` state.
3. If the scope is layout-like or runs inside hot loops, expose a `FooScopeHandle` (`ref struct`) and a public `FooScopeHandle Foo(...)` method. Otherwise skip the handle.
4. Always expose `void Foo(..., Action<Ui> content)` and `void Foo<TState>(..., TState state, Action<Ui, TState> content)` overloads. The lambda forms route through the same `Enter` / `Exit` pair, wrapped in a `using` (or `try / finally`).
5. Add XML docs that show the `static` shape on the `TState` overload — that is the form users should learn first.
6. Do not add `BeginFoo` / `EndFoo` unless the scope genuinely needs to straddle host code, the way `Frame` does.

`Frame` itself does not follow step 2 — there is no internal `EnterFrame` / `ExitFrame` pair. `BeginFrame` and `EndFrame` are the exposed entry points that host loops use directly, and they do their work inline rather than routing through a private `Enter` / `Exit` pair. Treat that as the deliberate exception, not the model for new scopes.

## Detecting Missed Closures

Vellum cannot use C#'s analyzers to enforce `using` on a ref-struct handle, so it falls back to runtime detection in DEBUG builds. `EndFrame` asserts that the layout stack is at the root, the ID stack is empty, and the disabled-scope counter is zero. A leaked handle throws an `InvalidOperationException` that names the form (`Row` / `Column` / `FixedWidth` / `MaxWidth`, `Id`, or `Disabled`) and tells the user to wrap the call in `using (...)`. Release builds skip the check.
