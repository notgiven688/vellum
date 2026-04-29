using System.Numerics;
using Xunit;

namespace Vellum.Tests;

public sealed class UiMenuTests
{
    [Fact]
    public void MenuBar_Switches_Open_Top_Level_Menus_On_Hover()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);

        Response menuBar = default;
        Response fileMenu = default;
        Response editMenu = default;
        Response fileItem = default;
        Response editItem = default;

        void Frame(Vector2 mouse, UiInputState input = default)
        {
            menuBar = default;
            fileMenu = default;
            editMenu = default;
            fileItem = default;
            editItem = default;

            ui.Frame(320, 180, mouse, input, frame =>
            {
                menuBar = frame.MenuBar(220, bar =>
                {
                    fileMenu = bar.Menu("File", popup =>
                    {
                        fileItem = popup.MenuItem("New");
                    });
                    editMenu = bar.Menu("Edit", popup =>
                    {
                        editItem = popup.MenuItem("Undo");
                    });
                });
            });
        }

        Frame(new Vector2(300, 140));
        Assert.InRange(menuBar.W, 219.9f, 220.1f);
        Assert.True(menuBar.H > 0);

        Vector2 filePoint = UiTestSupport.Inside(fileMenu);
        Vector2 editPoint = UiTestSupport.Inside(editMenu);

        Frame(filePoint, UiTestSupport.Input(mouseButtons: [UiMouseButton.Left]));
        Frame(filePoint);
        Assert.True(ui.IsChildPopupOpen("File", "menu"));
        Assert.True(fileMenu.Opened);
        Assert.True(fileItem.W > 0);

        Frame(editPoint);
        Assert.True(ui.IsChildPopupOpen("Edit", "menu"));
        Assert.False(ui.IsChildPopupOpen("File", "menu"));
        Assert.True(editMenu.Opened);
        Assert.True(editItem.W > 0);
        Assert.Equal(0f, fileItem.W);
    }

    [Fact]
    public void ContextMenu_Opens_On_Right_Click_And_CloseAllPopups_Clears_State()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);

        Response target = default;
        Response popupItem = default;

        void Frame(Vector2 mouse, UiInputState input = default)
        {
            target = default;
            popupItem = default;

            ui.Frame(320, 180, mouse, input, frame =>
            {
                target = frame.Button("Target");
                frame.ContextMenu("target-menu", target, popup =>
                {
                    popupItem = popup.MenuItem("Inspect");
                });
            });
        }

        Frame(new Vector2(240, 120));
        Vector2 clickPoint = UiTestSupport.Inside(target);

        Frame(clickPoint, UiTestSupport.Input(mouseButtons: [UiMouseButton.Right]));
        Assert.True(ui.IsPopupOpen("target-menu"));
        Assert.True(popupItem.W > 0);
        Assert.True(ui.TryGetPopupBounds("target-menu", out float popupX, out float popupY, out _, out _));
        Assert.InRange(popupX, clickPoint.X - 0.1f, clickPoint.X + 0.1f);
        Assert.InRange(popupY, clickPoint.Y - 0.1f, clickPoint.Y + 0.1f);

        ui.CloseAllPopups();
        Frame(new Vector2(240, 120));
        Assert.False(ui.IsPopupOpen("target-menu"));
        Assert.Equal(0f, popupItem.W);
    }

    [Fact]
    public void SideOpening_Menu_Anchors_To_Explicit_Row_Width_With_Long_Label()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);
        const string label = "Demo 17 - Voxel World (Current island tessellation stress scene)";

        Response menu = default;
        Response item = default;

        ui.Frame(640, 240, new Vector2(20, 20), false, frame =>
        {
            menu = frame.Menu(label, popup =>
            {
                item = popup.MenuItem("Demo 00 - Convex Decomposition");
            }, width: 180f, openOnHover: true, openToSide: true);
        });

        Assert.InRange(menu.W, 179.9f, 180.1f);
        Assert.True(item.W > 0);
        Assert.True(ui.TryGetChildPopupBounds(label, "menu", out float popupX, out _, out _, out _));
        Assert.InRange(popupX, menu.X + menu.W + 3.9f, menu.X + menu.W + 4.1f);
        float expectedItemX = menu.X + menu.W + 4f + MathF.Max(0, ui.Theme.BorderWidth) + ui.Theme.PopupPadding.Left;
        Assert.InRange(item.X, expectedItemX - 0.6f, expectedItemX + 0.6f);
    }

    [Fact]
    public void ModalPopup_Blocks_Underlying_Controls_And_Can_Close_Itself()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);

        Response outside = default;
        Response inside = default;
        bool closeNow = false;

        void Frame(Vector2 mouse, UiInputState input = default)
        {
            outside = default;
            inside = default;

            ui.Frame(400, 300, mouse, input, frame =>
            {
                outside = frame.Button("Outside");
                frame.ModalPopup("modal", 160, 120, popup =>
                {
                    inside = popup.Button("Inside");
                    if (closeNow)
                        popup.CloseCurrentPopup();
                });
            });
        }

        ui.OpenPopup("modal");
        Frame(Vector2.Zero);
        Assert.True(ui.IsPopupOpen("modal"));

        Vector2 outsidePoint = UiTestSupport.Inside(outside);

        Frame(outsidePoint, UiTestSupport.Input(mouseButtons: [UiMouseButton.Left]));
        Frame(outsidePoint);
        Assert.True(ui.IsPopupOpen("modal"));
        Assert.True(inside.W > 0);
        Assert.False(outside.Hovered);
        Assert.False(outside.Pressed);
        Assert.False(outside.Clicked);
        Assert.True(UiTestSupport.HasVertexColor(renderer.LastRenderList, ui.Theme.ModalBackdrop));

        Assert.True(ui.TryGetPopupBounds("modal", out float popupX, out float popupY, out float popupW, out float popupH));
        Assert.InRange(popupX, ((400f - popupW) * 0.5f) - 0.6f, ((400f - popupW) * 0.5f) + 0.6f);
        Assert.InRange(popupY, ((300f - popupH) * 0.5f) - 0.6f, ((300f - popupH) * 0.5f) + 0.6f);

        closeNow = true;
        Frame(Vector2.Zero);
        Assert.False(ui.IsPopupOpen("modal"));
    }
}
