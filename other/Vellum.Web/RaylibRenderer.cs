using Vellum.Rendering;
using Raylib_cs;

namespace Vellum.Web;

/// <summary>
/// IRenderer backend. A full render list is submitted each frame and emitted
/// via raylib-cs rlgl bindings.
/// </summary>
internal sealed unsafe class RaylibRenderer : IRenderer
{
    private const int RL_TRIANGLES = 4;

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private readonly record struct TexEntry(Texture2D Tex);
    private readonly Dictionary<int, TexEntry> _textures = new();
    private int _nextTexId = 1;
    private RenderFrameInfo _frame = new(1, 1);

    // -------------------------------------------------------------------------
    // Init
    // -------------------------------------------------------------------------

    public RaylibRenderer()
    {
    }

    public void Shutdown()
    {
        foreach (var e in _textures.Values) Raylib.UnloadTexture(e.Tex);
    }

    // -------------------------------------------------------------------------
    // IRenderer: frame
    // -------------------------------------------------------------------------

    public void BeginFrame(RenderFrameInfo frame)
    {
        _frame = frame.Normalized();
        Raylib.BeginDrawing();
        Rlgl.Viewport(
            0,
            0,
            Math.Max(1, _frame.FramebufferWidth),
            Math.Max(1, _frame.FramebufferHeight));
        Rlgl.MatrixMode(MatrixMode.Projection);
        Rlgl.LoadIdentity();
        Rlgl.MultMatrixf(Raymath.MatrixOrtho(
            0,
            Math.Max(1, _frame.LogicalWidth),
            Math.Max(1, _frame.LogicalHeight),
            0,
            0,
            1));
        Rlgl.MatrixMode(MatrixMode.ModelView);
        Rlgl.LoadIdentity();
        Raylib.ClearBackground(new Raylib_cs.Color(30, 30, 30, 255));
    }

    public void EndFrame() => Raylib.EndDrawing();

    public void Render(RenderList renderList)
    {
        DrawRenderList(renderList);
    }

    // -------------------------------------------------------------------------
    // IRenderer: textures
    // -------------------------------------------------------------------------

    public int CreateTexture(byte[] rgba, int width, int height)
    {
        if (rgba.Length == 0 || width == 0 || height == 0) return -1;
        Texture2D tex;
        fixed (byte* ptr = rgba)
        {
            var img = new Image
            {
                Data    = ptr,
                Width   = width,
                Height  = height,
                Mipmaps = 1,
                Format  = PixelFormat.UncompressedR8G8B8A8
            };
            tex = Raylib.LoadTextureFromImage(img);
        }
        // Font atlases have AA baked in — bilinear would double-blur, use point sampling
        Raylib.SetTextureFilter(tex, TextureFilter.Point);
        int id = _nextTexId++;
        _textures[id] = new TexEntry(tex);
        return id;
    }

    public void DestroyTexture(int textureId)
    {
        if (_textures.Remove(textureId, out var e))
            Raylib.UnloadTexture(e.Tex);
    }

    // GL constants used by the LCD/subpixel text compositor.
    private const int GL_ZERO                  = 0;
    private const int GL_ONE                   = 1;
    private const int GL_ONE_MINUS_SRC_COLOR   = 0x0301;
    private const int GL_FUNC_ADD              = 0x8006;

    private void DrawRenderList(RenderList renderList)
    {
        if (renderList.Commands.Count == 0) return;

        bool clipActive = false;
        Vellum.Rendering.ClipRect activeClip = default;

        Rlgl.DisableBackfaceCulling();

        foreach (var cmd in renderList.Commands)
        {
            if (cmd.HasClip != clipActive || (cmd.HasClip && !cmd.ClipRect.Equals(activeClip)))
            {
                if (clipActive)
                    EndClip();
                if (cmd.HasClip)
                {
                    BeginClip(cmd.ClipRect);
                    activeClip = cmd.ClipRect;
                }

                clipActive = cmd.HasClip;
            }

            if (cmd.TextureId == RenderTextureIds.Solid)
                DrawSolidTriangles(renderList, cmd);
            else
                DrawTexturedQuads(renderList, cmd);
        }

        if (clipActive)
            EndClip();
        Rlgl.EnableBackfaceCulling();
    }

    private void DrawSolidTriangles(RenderList renderList, DrawCommand cmd)
    {
        Rlgl.SetTexture((uint)Rlgl.GetTextureIdDefault());
        Rlgl.Begin(RL_TRIANGLES);

        int indexEnd = cmd.IndexOffset + cmd.IndexCount;
        for (int i = cmd.IndexOffset; i < indexEnd; i += 3)
        {
            Rlgl.Normal3f(0, 0, 1);
            EmitVertex(renderList.Vertices[(int)renderList.Indices[i]]);
            EmitVertex(renderList.Vertices[(int)renderList.Indices[i + 1]]);
            EmitVertex(renderList.Vertices[(int)renderList.Indices[i + 2]]);
        }

        Rlgl.End();
        Rlgl.DrawRenderBatchActive();
        Rlgl.SetTexture(0);
    }

    private void DrawTexturedQuads(RenderList renderList, DrawCommand cmd)
    {
        if (!_textures.TryGetValue(cmd.TextureId, out var entry))
            return;

        if (cmd.Lcd)
        {
            SetCustomBlend(GL_ZERO, GL_ONE_MINUS_SRC_COLOR);
            DrawTexturedQuadPass(renderList, cmd, entry.Tex, lcdMaskPass: true);

            SetCustomBlend(GL_ONE, GL_ONE);
            DrawTexturedQuadPass(renderList, cmd, entry.Tex, lcdMaskPass: false);

            SetAlphaBlend();
            return;
        }

        SetAlphaBlend();
        DrawTexturedQuadPass(renderList, cmd, entry.Tex, lcdMaskPass: false);
    }

    private static void DrawTexturedQuadPass(RenderList renderList, DrawCommand cmd, Texture2D texture, bool lcdMaskPass)
    {
        int indexEnd = cmd.IndexOffset + cmd.IndexCount;
        Span<DrawVertex> quad = stackalloc DrawVertex[4];
        for (int i = cmd.IndexOffset; i + 5 < indexEnd; i += 6)
        {
            if (!TryCollectQuad(renderList, i, quad))
                continue;

            var tint = lcdMaskPass ? GetLcdMaskTint(quad[0].Color) : quad[0].Color;
            DrawTexturedQuad(texture, quad, tint);
        }
    }

    private static bool TryCollectQuad(RenderList renderList, int indexOffset, Span<DrawVertex> quad)
    {
        int uniqueCount = 0;

        for (int j = 0; j < 6 && uniqueCount < 4; j++)
        {
            var vertex = renderList.Vertices[(int)renderList.Indices[indexOffset + j]];
            bool seen = false;
            for (int k = 0; k < uniqueCount; k++)
            {
                if (quad[k].Pos == vertex.Pos && quad[k].Uv == vertex.Uv && quad[k].Color.Equals(vertex.Color))
                {
                    seen = true;
                    break;
                }
            }

            if (!seen)
                quad[uniqueCount++] = vertex;
        }

        return uniqueCount == 4;
    }

    private static void DrawTexturedQuad(Texture2D texture, Span<DrawVertex> quad, Vellum.Rendering.Color tint)
    {
        float minX = quad[0].Pos.X;
        float minY = quad[0].Pos.Y;
        float maxX = quad[0].Pos.X;
        float maxY = quad[0].Pos.Y;
        float minU = quad[0].Uv.X;
        float minV = quad[0].Uv.Y;
        float maxU = quad[0].Uv.X;
        float maxV = quad[0].Uv.Y;

        for (int i = 1; i < quad.Length; i++)
        {
            minX = MathF.Min(minX, quad[i].Pos.X);
            minY = MathF.Min(minY, quad[i].Pos.Y);
            maxX = MathF.Max(maxX, quad[i].Pos.X);
            maxY = MathF.Max(maxY, quad[i].Pos.Y);
            minU = MathF.Min(minU, quad[i].Uv.X);
            minV = MathF.Min(minV, quad[i].Uv.Y);
            maxU = MathF.Max(maxU, quad[i].Uv.X);
            maxV = MathF.Max(maxV, quad[i].Uv.Y);
        }

        var source = new Rectangle(
            minU * texture.Width,
            minV * texture.Height,
            (maxU - minU) * texture.Width,
            (maxV - minV) * texture.Height);

        var dest = new Rectangle(minX, minY, maxX - minX, maxY - minY);
        Raylib.DrawTexturePro(texture, source, dest, System.Numerics.Vector2.Zero, 0, ToRaylibColor(tint));
    }

    private static void SetCustomBlend(int srcFactor, int dstFactor)
    {
        Rlgl.SetBlendFactors(srcFactor, dstFactor, GL_FUNC_ADD);
        Rlgl.SetBlendMode(BlendMode.Custom);
    }

    private static void SetAlphaBlend()
        => Rlgl.SetBlendMode(BlendMode.Alpha);

    private static Vellum.Rendering.Color GetLcdMaskTint(Vellum.Rendering.Color tint)
        => new(tint.A, tint.A, tint.A, tint.A);

    private void BeginClip(Vellum.Rendering.ClipRect clip)
    {
        int x1 = (int)MathF.Floor(clip.X * _frame.ScaleX);
        int y1 = _frame.FramebufferHeight - (int)MathF.Ceiling((clip.Y + clip.Height) * _frame.ScaleY);
        int x2 = (int)MathF.Ceiling((clip.X + clip.Width) * _frame.ScaleX);
        int y2 = _frame.FramebufferHeight - (int)MathF.Floor(clip.Y * _frame.ScaleY);

        x1 = Math.Clamp(x1, 0, _frame.FramebufferWidth);
        y1 = Math.Clamp(y1, 0, _frame.FramebufferHeight);
        x2 = Math.Clamp(x2, 0, _frame.FramebufferWidth);
        y2 = Math.Clamp(y2, 0, _frame.FramebufferHeight);

        Rlgl.DrawRenderBatchActive();
        Rlgl.EnableScissorTest();
        Rlgl.Scissor(x1, y1, Math.Max(0, x2 - x1), Math.Max(0, y2 - y1));
    }

    private static void EndClip()
    {
        Rlgl.DrawRenderBatchActive();
        Rlgl.DisableScissorTest();
    }

    private static void EmitVertex(DrawVertex vertex)
    {
        Rlgl.Color4ub(vertex.Color.R, vertex.Color.G, vertex.Color.B, vertex.Color.A);
        Rlgl.TexCoord2f(vertex.Uv.X, vertex.Uv.Y);
        Rlgl.Vertex2f(vertex.Pos.X, vertex.Pos.Y);
    }

    private static Raylib_cs.Color ToRaylibColor(Vellum.Rendering.Color color)
        => new(color.R, color.G, color.B, color.A);
}
