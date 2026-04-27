# Backend Guide

Vellum is backend-neutral. The core library builds a `RenderList`; a backend implements `IRenderer` and turns that list into draw calls for OpenGL, Direct3D, Vulkan, Metal, Raylib, a browser canvas, or another host.

The reference backend is the OpenTK demo. It is intentionally the cleanest implementation to study because it shows the whole contract directly: shader setup, vertex/index uploads, texture handles, scissor clipping, alpha blending, and LCD text blending.

The Raylib demo is kept for browser-oriented demo work. It is useful as a compact backend, but OpenTK should be treated as the reference backend.

## Renderer Contract

Implement `Vellum.Rendering.IRenderer`:

```csharp
public interface IRenderer
{
    void BeginFrame(RenderFrameInfo frame);
    void Render(RenderList renderList);
    void EndFrame();

    int CreateTexture(byte[] rgba, int width, int height);
    void DestroyTexture(int textureId);
}
```

`BeginFrame` should prepare the target for a Vellum frame. A typical backend sets the physical framebuffer viewport, disables depth/culling, enables blending, clears or prepares the target, and resets clipping state.

`RenderFrameInfo` carries both coordinate spaces:

- `LogicalWidth` and `LogicalHeight`: the Vellum layout, input, vertex, and clip coordinate space.
- `FramebufferWidth` and `FramebufferHeight`: the physical render target size.
- `ScaleX` and `ScaleY`: physical pixels per logical pixel.

For non-HiDPI rendering these values are usually identical and the scale is `1`. On HiDPI targets, pass the window/client size as logical size and the actual framebuffer size as framebuffer size.

`Render` receives all geometry for the frame. Upload the render list vertices and indices, then draw each `DrawCommand` in order.

`EndFrame` is the place for backend-specific presentation cleanup. It can be empty if the windowing layer owns buffer swapping.

`CreateTexture` receives RGBA8 pixels and returns an opaque integer handle. Vellum stores only this integer. `DestroyTexture` must release the backend texture for that handle.

## Render List Data

`RenderList.Vertices` contains `DrawVertex` values:

- `Pos`: top-left coordinates in Vellum logical pixels.
- `Uv`: normalized texture coordinates.
- `Color`: straight RGBA tint.

`RenderList.Indices` contains triangle indices into `Vertices`.

`RenderList.Commands` contains draw ranges:

- `TextureId`: backend texture handle, or `RenderTextureIds.Solid`.
- `IndexOffset`: first index in `RenderList.Indices`.
- `IndexCount`: number of indices to draw.
- `HasClip` and `ClipRect`: scissor rectangle in Vellum's top-left logical coordinate system.
- `Lcd`: true for LCD text atlas draws.

Draw commands are already batched by compatible texture, clip, and LCD state. The backend should preserve command order.

## Solid Geometry

`RenderTextureIds.Solid` is `0`. This is reserved for untextured geometry such as panels, borders, strokes, and custom canvas fills.

Backends usually handle this by creating or binding a private 1x1 white texture and using the same textured shader path for everything. Vertex color then supplies the final solid color.

Do not return `0` from `CreateTexture`; reserve it for solid geometry.

## Clipping

Vellum clip rectangles are top-left based and expressed in logical pixels:

```text
x = clip.X
y = clip.Y
width = clip.Width
height = clip.Height
```

APIs with bottom-left scissor coordinates, such as OpenGL, must flip Y:

```csharp
int x = (int)MathF.Floor(clip.X * frame.ScaleX);
int y = frame.FramebufferHeight - (int)MathF.Ceiling((clip.Y + clip.Height) * frame.ScaleY);
int width = (int)MathF.Ceiling(clip.Width * frame.ScaleX);
int height = (int)MathF.Ceiling(clip.Height * frame.ScaleY);
```

Disable scissor when `HasClip` is false.

## DPI And Text Scale

`Ui.Frame(width, height, ...)` is a convenience path for scale-1 rendering. Hosts with separate logical and framebuffer sizes should use `RenderFrameInfo`:

```csharp
var frame = new RenderFrameInfo(
    logicalWidth,
    logicalHeight,
    framebufferWidth,
    framebufferHeight);

ui.Frame(frame, mousePositionInLogicalPixels, input, root => DrawUi(root));
```

By default `Ui.AutoTextRasterScale` updates `Ui.TextRasterScale` from the largest frame scale before layout. This makes glyph atlases rasterize at the framebuffer density while layout remains in logical pixels. Set `AutoTextRasterScale = false` if the host needs to manage text raster scale manually.

## Blending

Normal commands use standard source-over alpha blending:

```text
src = SrcAlpha
dst = OneMinusSrcAlpha
```

LCD text commands need a two-pass blend if the backend wants subpixel text:

1. Mask pass: use the glyph texture as a per-channel mask against the destination.
2. Tint pass: add the tinted glyph color.

The OpenTK backend's `DrawLcdCommand` is the reference implementation. A simpler backend can set `Ui.Lcd = false` or `Theme.UseLcdText = false` and render grayscale text with the normal alpha path.

## Texture Lifetime

Glyph atlases are created lazily and may be rebuilt as new glyphs are requested. When an atlas is replaced or a `Ui` is disposed, Vellum calls `DestroyTexture`.

A backend should:

- return stable non-zero handles from `CreateTexture`;
- keep a map from Vellum texture id to backend texture object;
- tolerate `DestroyTexture` for unknown or already-destroyed ids as a no-op;
- set unpack alignment to 1 for tightly packed RGBA8 pixels when the graphics API requires it.

## Performance Notes

Avoid per-frame allocations in the renderer. The OpenTK backend keeps reusable vertex and index upload arrays that grow only when needed.

The render list lists are owned by Vellum. Treat them as read-only during `Render`.

Upload and draw only the active counts from the current render list. Cached upload buffers can be larger than the current frame.

## Minimal Backend Checklist

- Create a shader that transforms Vellum logical pixel coordinates to clip space.
- Use `RenderFrameInfo.LogicalWidth/Height` for vertex projection.
- Use `RenderFrameInfo.FramebufferWidth/Height` for the physical viewport.
- Use vertex attributes for position, UV, and normalized RGBA color.
- Create a private white texture for `RenderTextureIds.Solid`.
- Implement RGBA8 texture creation and destruction.
- Upload `RenderList.Vertices` and `RenderList.Indices`.
- Draw every `DrawCommand` in order.
- Apply scissor rectangles for clipped commands, converting logical clip rectangles to framebuffer pixels.
- Use source-over alpha blending for normal commands.
- Either implement LCD text blending or disable LCD text.
