using System.Numerics;
using Vellum.Rendering;
using Xunit;

namespace Vellum.Tests;

public sealed class PublicValueTests
{
    [Fact]
    public void Color_Factories_Equality_And_WithAlpha_Work()
    {
        Color rgb = Color.FromRgb(10, 20, 30);
        Color rgba = Color.FromRgba(10, 20, 30, 40);
        Color withAlpha = rgb.WithAlpha(40);

        Assert.Equal(new Color(10, 20, 30), rgb);
        Assert.Equal(rgba, withAlpha);
        Assert.Equal(rgba.GetHashCode(), withAlpha.GetHashCode());
        Assert.NotEqual(Color.White, rgb);
    }

    [Fact]
    public void EdgeInsets_Constructors_Compute_Horizontal_And_Vertical_Totals()
    {
        EdgeInsets all = new(5f);
        EdgeInsets symmetric = new(3f, 7f);
        EdgeInsets explicitInsets = new(1f, 2f, 3f, 4f);

        Assert.Equal(10f, all.Horizontal);
        Assert.Equal(10f, all.Vertical);
        Assert.Equal(14f, symmetric.Horizontal);
        Assert.Equal(6f, symmetric.Vertical);
        Assert.Equal(4f, explicitInsets.Vertical);
        Assert.Equal(6f, explicitInsets.Horizontal);
        Assert.Equal(0f, EdgeInsets.Zero.Horizontal);
        Assert.Equal(0f, EdgeInsets.Zero.Vertical);
    }

    [Fact]
    public void RenderFrameInfo_Derives_Scale_And_MaxScale()
    {
        RenderFrameInfo simple = new(320, 180);
        RenderFrameInfo scaled = new(320, 180, 640, 540);

        Assert.Equal(1f, simple.ScaleX);
        Assert.Equal(1f, simple.ScaleY);
        Assert.Equal(2f, scaled.ScaleX);
        Assert.Equal(3f, scaled.ScaleY);
        Assert.Equal(3f, scaled.MaxScale);
    }

    [Fact]
    public void RenderFrameInfo_Normalized_Clamps_Invalid_Sizes()
    {
        RenderFrameInfo raw = new(320, 180, -5, 0);
        RenderFrameInfo normalized = raw.Normalized();

        Assert.Equal(320, normalized.LogicalWidth);
        Assert.Equal(180, normalized.LogicalHeight);
        Assert.Equal(320, normalized.FramebufferWidth);
        Assert.Equal(180, normalized.FramebufferHeight);
        Assert.Equal(1f, normalized.ScaleX);
        Assert.Equal(1f, normalized.ScaleY);
    }

    [Fact]
    public void ThemePresets_Return_Independent_Instances()
    {
        Theme darkA = ThemePresets.Dark();
        Theme darkB = ThemePresets.Dark();
        Theme light = ThemePresets.Light();

        darkA.BorderWidth = 7f;
        darkA.Accent = new Color(1, 2, 3);

        Assert.Equal(1f, darkB.BorderWidth);
        Assert.NotEqual(darkA.Accent, darkB.Accent);
        Assert.NotEqual(light.SurfaceBg, darkB.SurfaceBg);
    }

    [Fact]
    public void UiInputState_Exposes_Modifiers_And_Button_State()
    {
        UiInputState input = new(
            textInput: "x",
            pressedKeys: new HashSet<UiKey> { UiKey.Tab, UiKey.Enter },
            wheelDelta: new Vector2(1, -2),
            shift: true,
            ctrl: true,
            downMouseButtons: new HashSet<UiMouseButton> { UiMouseButton.Right });

        Assert.Equal("x", input.TextInput);
        Assert.True(input.Shift);
        Assert.True(input.Ctrl);
        Assert.True(input.PrimaryModifier);
        Assert.Equal(new Vector2(1, -2), input.WheelDelta);
        Assert.True(input.IsPressed(UiKey.Tab));
        Assert.False(input.IsPressed(UiKey.Space));
        Assert.True(input.IsMouseDown(UiMouseButton.Right));
        Assert.False(input.IsMouseDown(UiMouseButton.Left));
    }

    [Fact]
    public void NullUiPlatform_Is_A_NoOp()
    {
        IUiPlatform platform = NullUiPlatform.Instance;

        Assert.Equal(string.Empty, platform.GetClipboardText());
        platform.SetClipboardText("ignored");
        platform.SetCursor(UiCursor.PointingHand);
        Assert.Equal(string.Empty, platform.GetClipboardText());
    }
}
