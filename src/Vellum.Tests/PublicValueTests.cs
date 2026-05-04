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
    public void UiId_ExplicitIdContainers_Accept_Typed_Ids()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);

        bool popupRendered = false;
        ui.OpenPopup(17);

        ui.Frame(320, 240, Vector2.Zero, false, frame =>
        {
            frame.ScrollArea(1, 80, 42, area => area.Label("Vertical"));
            frame.ScrollAreaBoth(2L, 80, 42, area => area.Label("Both"));
            frame.TabBar(3UL, tabs => tabs.Tab("One", tab => tab.Label("Selected")));
            frame.ContextMenu(4, default, menu => menu.Label("Context"));
            popupRendered = frame.Popup(17, 10, 10, 120, 80, popup => popup.Label("Popup"));
        });

        Assert.True(popupRendered);
        Assert.True(ui.IsPopupOpen(17));
        Assert.True(ui.TryGetPopupBounds(17, out _, out _, out _, out _));

        ui.ClosePopup(17);

        Assert.False(ui.IsPopupOpen(17));
        Assert.Throws<ArgumentException>(() => ui.OpenPopup(default(UiId)));
    }

    [Fact]
    public void Ui_StateOverloads_Accept_Static_Content_For_Delayed_Containers()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);
        int[] modalCount = [0];
        int[] contextCount = [0];
        int[] menuCount = [0];
        int[] tabCount = [0];

        ui.OpenPopup(10);
        ui.Frame(320, 240, Vector2.Zero, false, frame =>
        {
            frame.ModalPopup(10, 120, 80, modalCount, static (popup, count) =>
            {
                count[0]++;
                popup.Label("Modal");
            });
        });

        ui.OpenPopup(20);
        ui.Frame(320, 240, Vector2.Zero, false, frame =>
        {
            frame.ContextMenu(20, default, contextCount, static (popup, count) =>
            {
                count[0]++;
                popup.Label("Context");
            });
        });

        ui.Frame(320, 240, Vector2.Zero, false, frame =>
        {
            frame.MenuBar(menuCount, static (bar, count) =>
            {
                bar.Menu("File", count, static (popup, count) =>
                {
                    count[0]++;
                    popup.Label("Open");
                });
            });

            frame.TabBar(30, tabCount, static (tabs, count) =>
            {
                tabs.Tab("One", count, static (tab, count) =>
                {
                    count[0]++;
                    tab.Label("Tab");
                });
            });
        });

        Assert.Equal(1, modalCount[0]);
        Assert.True(contextCount[0] >= 1);
        Assert.Equal(0, menuCount[0]);
        Assert.Equal(1, tabCount[0]);
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
