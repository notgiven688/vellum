using System.Numerics;
using Vellum.Rendering;
using Xunit;

namespace Vellum.Tests;

public sealed class UiDockingTests
{
    [Theory]
    [InlineData(0.75f)]
    [InlineData(1f)]
    [InlineData(1.5f)]
    [InlineData(2f)]
    public void Docked_Tab_Title_Does_Not_Ellipsize_When_Buttons_Fit(float borderWidth)
    {
        var renderer = new UiTestRenderer();
        var docking = new DockingState();
        var ui = UiTestSupport.CreateUi(renderer);
        ui.Docking = docking;
        ui.Theme.BorderWidth = borderWidth;

        var windowState = new WindowState(Vector2.Zero);

        void Frame()
        {
            ui.Frame(360, 180, Vector2.Zero, false, frame =>
            {
                frame.DockSpace("main", 128, 120);
                frame.DockWindow("main", "dock-caption", DockPlacement.Center);
                frame.Window("Theme", windowState, 180, content => { }, id: "dock-caption");
            });
        }

        Frame();
        Frame();

        Assert.NotNull(renderer.LastRenderList);
        DrawCommand[] textCommands = renderer.LastRenderList!.Commands
            .Where(command => command.TextureId != RenderTextureIds.Solid)
            .ToArray();

        Assert.NotEmpty(textCommands);
        Assert.All(textCommands, command => Assert.False(command.HasClip));
    }
}
