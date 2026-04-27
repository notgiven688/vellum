using System.Runtime.InteropServices;
using Vellum.Rendering;
using OpenTK.Graphics.OpenGL;

namespace Vellum.Demo;

internal sealed class OpenTkRenderer : IRenderer, IDisposable
{
    private const string VertexShaderSource = """
        #version 330 core

        layout (location = 0) in vec2 aPosition;
        layout (location = 1) in vec2 aTexCoord;
        layout (location = 2) in vec4 aColor;

        uniform vec2 uViewportSize;

        out vec2 vTexCoord;
        out vec4 vColor;

        void main()
        {
            vec2 clip = vec2(
                (aPosition.x / uViewportSize.x) * 2.0 - 1.0,
                1.0 - (aPosition.y / uViewportSize.y) * 2.0);
            gl_Position = vec4(clip, 0.0, 1.0);
            vTexCoord = aTexCoord;
            vColor = aColor;
        }
        """;

    private const string FragmentShaderSource = """
        #version 330 core

        in vec2 vTexCoord;
        in vec4 vColor;

        uniform sampler2D uTexture;
        uniform int uLcdMaskPass;

        out vec4 fragColor;

        void main()
        {
            vec4 color = uLcdMaskPass != 0 ? vec4(vColor.a) : vColor;
            fragColor = texture(uTexture, vTexCoord) * color;
        }
        """;

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct GpuVertex
    {
        public readonly float X;
        public readonly float Y;
        public readonly float U;
        public readonly float V;
        public readonly byte R;
        public readonly byte G;
        public readonly byte B;
        public readonly byte A;

        public GpuVertex(DrawVertex vertex, Color? overrideColor = null)
        {
            Color color = overrideColor ?? vertex.Color;
            X = vertex.Pos.X;
            Y = vertex.Pos.Y;
            U = vertex.Uv.X;
            V = vertex.Uv.Y;
            R = color.R;
            G = color.G;
            B = color.B;
            A = color.A;
        }
    }

    private readonly Dictionary<int, int> _textures = new();
    private readonly int _shader;
    private readonly int _viewportSizeLocation;
    private readonly int _textureLocation;
    private readonly int _lcdMaskPassLocation;
    private readonly int _vao;
    private readonly int _vbo;
    private readonly int _ebo;
    private readonly int _solidTexture;
    private int _nextTextureId = 1;
    private RenderFrameInfo _frame = new(1, 1);
    private GpuVertex[] _vertexUpload = [];
    private uint[] _indexUpload = [];
    private bool _disposed;

    public OpenTkRenderer()
    {
        _shader = CreateProgram(VertexShaderSource, FragmentShaderSource);
        _viewportSizeLocation = GL.GetUniformLocation(_shader, "uViewportSize");
        _textureLocation = GL.GetUniformLocation(_shader, "uTexture");
        _lcdMaskPassLocation = GL.GetUniformLocation(_shader, "uLcdMaskPass");

        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);

        int stride = Marshal.SizeOf<GpuVertex>();
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, stride, 8);
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(2, 4, VertexAttribPointerType.UnsignedByte, true, stride, 16);

        GL.BindVertexArray(0);

        _solidTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _solidTexture);
        byte[] white = [255, 255, 255, 255];
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, 1, 1, 0, PixelFormat.Rgba, PixelType.UnsignedByte, white);
        SetTextureParameters();
        GL.BindTexture(TextureTarget.Texture2d, 0);
    }

    public void BeginFrame(RenderFrameInfo frame)
    {
        _frame = frame.Normalized();

        GL.Viewport(0, 0, Math.Max(1, _frame.FramebufferWidth), Math.Max(1, _frame.FramebufferHeight));
        GL.Disable(EnableCap.DepthTest);
        GL.Disable(EnableCap.CullFace);
        GL.Enable(EnableCap.Multisample);
        GL.Enable(EnableCap.Blend);
        GL.Disable(EnableCap.ScissorTest);
        SetAlphaBlend();

        GL.ClearColor(0.08f, 0.085f, 0.095f, 1f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
    }

    public void Render(RenderList renderList)
    {
        if (renderList.Commands.Count == 0 || renderList.Vertices.Count == 0 || renderList.Indices.Count == 0)
            return;

        EnsureUploadCapacity(renderList.Vertices.Count, renderList.Indices.Count);
        for (int i = 0; i < renderList.Vertices.Count; i++)
            _vertexUpload[i] = new GpuVertex(renderList.Vertices[i]);
        renderList.Indices.CopyTo(_indexUpload);

        GL.UseProgram(_shader);
        GL.Uniform2f(_viewportSizeLocation, (float)Math.Max(1, _frame.LogicalWidth), (float)Math.Max(1, _frame.LogicalHeight));
        GL.Uniform1i(_textureLocation, 0);
        GL.Uniform1i(_lcdMaskPassLocation, 0);
        GL.ActiveTexture(TextureUnit.Texture0);

        UploadGeometry(renderList.Vertices.Count, renderList.Indices.Count);

        GL.BindVertexArray(_vao);
        foreach (DrawCommand command in renderList.Commands)
        {
            ApplyClip(command);

            if (command.TextureId == RenderTextureIds.Solid)
            {
                SetAlphaBlend();
                DrawCommand(command, _solidTexture);
            }
            else if (_textures.TryGetValue(command.TextureId, out int texture))
            {
                if (command.Lcd)
                    DrawLcdCommand(command, texture);
                else
                {
                    SetAlphaBlend();
                    DrawCommand(command, texture);
                }
            }
        }

        GL.BindVertexArray(0);
        GL.Disable(EnableCap.ScissorTest);
        GL.BindTexture(TextureTarget.Texture2d, 0);
        GL.UseProgram(0);
    }

    public void EndFrame()
    {
    }

    public int CreateTexture(byte[] rgba, int width, int height)
    {
        if (rgba.Length == 0 || width <= 0 || height <= 0)
            return -1;

        int texture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, texture);
        GL.PixelStorei(PixelStoreParameter.UnpackAlignment, 1);
        GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, width, height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, rgba);
        SetTextureParameters();
        GL.BindTexture(TextureTarget.Texture2d, 0);

        int id = _nextTextureId++;
        _textures[id] = texture;
        return id;
    }

    public void DestroyTexture(int textureId)
    {
        if (_textures.Remove(textureId, out int texture))
            GL.DeleteTexture(texture);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        foreach (int texture in _textures.Values)
            GL.DeleteTexture(texture);
        _textures.Clear();

        GL.DeleteTexture(_solidTexture);
        GL.DeleteBuffer(_ebo);
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        GL.DeleteProgram(_shader);
        _disposed = true;
    }

    private static void SetTextureParameters()
    {
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
    }

    private void UploadGeometry(int vertexCount, int indexCount)
    {
        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, vertexCount * Marshal.SizeOf<GpuVertex>(), _vertexUpload, BufferUsage.StreamDraw);
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, indexCount * sizeof(uint), _indexUpload, BufferUsage.StreamDraw);
    }

    private void EnsureUploadCapacity(int vertexCount, int indexCount)
    {
        if (_vertexUpload.Length < vertexCount)
            _vertexUpload = new GpuVertex[GrowCapacity(_vertexUpload.Length, vertexCount)];
        if (_indexUpload.Length < indexCount)
            _indexUpload = new uint[GrowCapacity(_indexUpload.Length, indexCount)];
    }

    private static int GrowCapacity(int current, int required)
    {
        int next = Math.Max(256, current);
        while (next < required)
            next *= 2;
        return next;
    }

    private void DrawLcdCommand(DrawCommand command, int texture)
    {
        GL.Uniform1i(_lcdMaskPassLocation, 1);
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.Zero, BlendingFactor.OneMinusSrcColor);
        DrawCommand(command, texture);

        GL.Uniform1i(_lcdMaskPassLocation, 0);
        GL.BlendFunc(BlendingFactor.One, BlendingFactor.One);
        DrawCommand(command, texture);
        SetAlphaBlend();
    }

    private static void SetAlphaBlend()
    {
        GL.BlendEquation(BlendEquationMode.FuncAdd);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
    }

    private void ApplyClip(DrawCommand command)
    {
        if (!command.HasClip)
        {
            GL.Disable(EnableCap.ScissorTest);
            return;
        }

        int x = (int)MathF.Floor(command.ClipRect.X * _frame.ScaleX);
        int y = _frame.FramebufferHeight - (int)MathF.Ceiling((command.ClipRect.Y + command.ClipRect.Height) * _frame.ScaleY);
        int width = (int)MathF.Ceiling(command.ClipRect.Width * _frame.ScaleX);
        int height = (int)MathF.Ceiling(command.ClipRect.Height * _frame.ScaleY);

        GL.Enable(EnableCap.ScissorTest);
        GL.Scissor(x, y, Math.Max(0, width), Math.Max(0, height));
    }

    private static void DrawCommand(DrawCommand command, int texture)
    {
        GL.BindTexture(TextureTarget.Texture2d, texture);
        GL.DrawElements(
            PrimitiveType.Triangles,
            command.IndexCount,
            DrawElementsType.UnsignedInt,
            (IntPtr)(command.IndexOffset * sizeof(uint)));
    }

    private static int CreateProgram(string vertexSource, string fragmentSource)
    {
        int vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
        int fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);
        int program = GL.CreateProgram();
        GL.AttachShader(program, vertexShader);
        GL.AttachShader(program, fragmentShader);
        GL.LinkProgram(program);
        int linked = GL.GetProgrami(program, ProgramProperty.LinkStatus);
        string log = GetProgramInfoLog(program);
        GL.DetachShader(program, vertexShader);
        GL.DetachShader(program, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        if (linked == 0)
            throw new InvalidOperationException($"Failed to link OpenTK GUI shader: {log}");

        return program;
    }

    private static int CompileShader(ShaderType type, string source)
    {
        int shader = GL.CreateShader(type);
        GL.ShaderSource(shader, source);
        GL.CompileShader(shader);
        int compiled = GL.GetShaderi(shader, ShaderParameterName.CompileStatus);
        if (compiled != 0)
            return shader;

        string log = GetShaderInfoLog(shader);
        GL.DeleteShader(shader);
        throw new InvalidOperationException($"Failed to compile {type} shader: {log}");
    }

    private static string GetProgramInfoLog(int program)
    {
        string log = string.Empty;
        GL.GetProgramInfoLog(program, out log);
        return log;
    }

    private static string GetShaderInfoLog(int shader)
    {
        string log = string.Empty;
        GL.GetShaderInfoLog(shader, out log);
        return log;
    }
}
