# API Boundaries

Vellum keeps three public API areas. New public types should fit one of these areas deliberately.

## User API

The user API is the surface application code uses to build interfaces:

- `Ui`
- `UiId`
- `UiWidgetKind`
- `Response`
- `WindowState`
- `UiCanvas`
- `Theme`
- `ThemePresets`
- `EdgeInsets`
- `TextWrapMode`
- `TextOverflowMode`
- `UiAlign`
- `UiInputState`
- `UiKey`
- `UiMouseButton`
- `IUiPlatform`
- `UiCursor`
- `NullUiPlatform`

These types are the normal application-facing surface. They should stay small, stable, and documented from an app author's point of view.

## Backend API

The backend API is the surface needed to implement a renderer:

- `Vellum.Rendering.IRenderer`
- `Vellum.Rendering.RenderList`
- `Vellum.Rendering.DrawCommand`
- `Vellum.Rendering.DrawVertex`
- `Vellum.Rendering.ClipRect`
- `Vellum.Rendering.Color`
- `Vellum.Rendering.RenderFrameInfo`
- `Vellum.Rendering.RenderTextureIds`

This is public because `IRenderer.Render` receives a `RenderList`. Backend types are advanced API, but they are intentional. See the [Backend Guide](backends.md).

## Advanced Font API

The font API is public for applications that want to load a custom TrueType font or inspect/rasterize glyphs directly:

- `TrueTypeFont`
- `UiFont`
- `UiFontSource`
- `UiFonts`
- `MaterialSymbols`
- `GlyphMetrics`
- `ScaledGlyphMetrics`
- `FontVMetrics`

Most applications only need `Ui.Font = TrueTypeFont.FromFile(...)`, `UiFonts.DefaultSans`, or `Ui.FontStack = UiFont.Merge(...)` for fallback/icon fonts. Direct glyph metrics and rasterization are advanced use cases.

## Internal Implementation

The following are intentionally internal:

- font table parsing and outline loading;
- glyph atlas construction;
- rasterization internals;
- shape tessellation and painting internals;
- layout and text layout scratch data;
- widget state caches.

`Painter`, `GlyphAtlas`, and `GlyphInfo` are examples of implementation details that should not be app or backend API.

## Change Rule

Adding a new public type is an API decision. Add it to the correct section above and update the public API snapshot test in `Vellum.Tests`.
