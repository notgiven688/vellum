# Text and Fonts

Vellum includes a small built-in text stack so the core library can render labels, text fields, and simple custom text without depending on platform text APIs.

This text stack is intentionally limited. It is good enough for immediate-mode UI labels and editable text in many developer tools, demos, and internal applications. It is not a full shaping engine.

## Supported

Vellum currently supports:

- loading TrueType fonts from bytes or files with `TrueTypeFont`;
- an embedded default sans font via `UiFonts.DefaultSans`;
- an embedded Google Material Symbols Outlined font via `UiFonts.MaterialSymbols` and `MaterialSymbols.Font`;
- generated Material Symbols glyph string constants such as `MaterialSymbols.Home`;
- merged font stacks via `Ui.FontStack`, `UiFont.Merge(...)`, and `UiFont.Source(...)`;
- first-match codepoint fallback across merged font sources;
- Unicode text enumeration by scalar value;
- glyph lookup through TrueType `cmap` format 4 and 12 tables;
- simple and composite glyph outlines;
- horizontal metrics and basic kerning;
- grayscale glyph rasterization;
- optional LCD/subpixel glyph rasterization;
- glyph atlas generation and reuse inside `Ui`;
- single-line labels;
- word-wrapped labels;
- clipped and ellipsized labels;
- text fields;
- multiline text areas;
- caret movement, selection, clipboard operations, and common editing keys.

## Deliberately Not Supported

Vellum does not currently implement:

- bidirectional text layout;
- OpenType shaping;
- ligatures;
- contextual substitutions;
- OpenType icon ligature name resolution;
- automatic platform font fallback;
- cross-font kerning in merged font stacks;
- emoji color fonts;
- vertical text;
- platform IME composition;
- locale-specific line breaking;
- full Unicode grapheme behavior for every script.

For scripts that require shaping or bidi handling, text may display as individual codepoint glyphs rather than the typographically correct visual form.

## Font API Boundary

Most applications should set either a single font or a merged font stack on `Ui`:

```csharp
ui.Font = TrueTypeFont.FromFile("Inter-Regular.ttf");
```

```csharp
ui.FontStack = UiFont.Merge(
    UiFont.Source(UiFonts.DefaultSans),
    UiFont.Source(MaterialSymbols.Font, offsetY: 4f));
```

`Ui.FontStack` takes precedence over `Ui.Font` when both are set. Use the generated Material Symbols constants when drawing icon glyphs:

```csharp
root.Button($"{MaterialSymbols.Home} Home");
```

`TrueTypeFont`, `UiFont`, `UiFontSource`, `UiFonts`, `MaterialSymbols`, `GlyphMetrics`, `ScaledGlyphMetrics`, and `FontVMetrics` are public as advanced font API. They are useful when an application wants to inspect metrics, rasterize glyphs directly, tune fallback font placement, or build custom diagnostics.

Font table parsing, outline loading, rasterization internals, and glyph atlas construction are internal implementation details.

## Backend Expectations

Backends do not shape text. By the time a backend receives a `RenderList`, text is already represented as textured quads.

Backends only need to:

- upload RGBA8 glyph atlas textures from `IRenderer.CreateTexture`;
- render normal text with source-over alpha blending;
- render LCD text with the documented LCD blend path, or disable LCD text.

See the [Backend Implementation](backends.md) guide for the render-side contract.
