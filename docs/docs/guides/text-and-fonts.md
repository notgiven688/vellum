# Text and Fonts

Vellum includes a small built-in text stack so the core library can render labels, text fields, and simple custom text without depending on platform text APIs.

This text stack is intentionally limited. It is good enough for immediate-mode UI labels and editable text in many developer tools, demos, and internal applications. It is not a full shaping engine.

## Supported

Vellum currently supports:

- loading TrueType fonts from bytes or files with `TrueTypeFont`;
- an embedded default sans font via `UiFonts.DefaultSans`;
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
- glyph fallback across multiple fonts;
- emoji color fonts;
- vertical text;
- platform IME composition;
- locale-specific line breaking;
- full Unicode grapheme behavior for every script.

For scripts that require shaping or bidi handling, text may display as individual codepoint glyphs rather than the typographically correct visual form.

## Font API Boundary

Most applications should only set a font on `Ui`:

```csharp
ui.Font = TrueTypeFont.FromFile("Inter-Regular.ttf");
```

`TrueTypeFont`, `GlyphMetrics`, `ScaledGlyphMetrics`, and `FontVMetrics` are public as advanced font API. They are useful when an application wants to inspect metrics, rasterize glyphs directly, or build custom diagnostics.

Font table parsing, outline loading, rasterization internals, and glyph atlas construction are internal implementation details.

## Backend Expectations

Backends do not shape text. By the time a backend receives a `RenderList`, text is already represented as textured quads.

Backends only need to:

- upload RGBA8 glyph atlas textures from `IRenderer.CreateTexture`;
- render normal text with source-over alpha blending;
- render LCD text with the documented LCD blend path, or disable LCD text.

See the [Backend Implementation](backends.md) guide for the render-side contract.