using System.Numerics;
using Vellum.Rendering;
using Xunit;

namespace Vellum.Tests;

public sealed class UiTabTreeTooltipTests
{
    [Fact]
    public void TabBar_Selects_First_Tab_By_Default_And_Switches_On_Click()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);

        Response general = default;
        Response advanced = default;
        Response panel = default;
        string activePanel = string.Empty;

        void Frame(Vector2 mouse, UiInputState input = default)
        {
            general = default;
            advanced = default;
            panel = default;
            activePanel = string.Empty;

            ui.Frame(320, 180, mouse, input, frame =>
            {
                frame.TabBar("settings-tabs", tabs =>
                {
                    general = tabs.Tab("General", content =>
                    {
                        activePanel = "General";
                        panel = content.Label("General panel");
                    });

                    advanced = tabs.Tab("Advanced", content =>
                    {
                        activePanel = "Advanced";
                        panel = content.Label("Advanced panel");
                    });
                });
            });
        }

        Frame(Vector2.Zero);
        Assert.Equal("General", activePanel);
        Assert.True(panel.W > 0);

        Vector2 advancedPoint = UiTestSupport.Inside(advanced);
        Frame(advancedPoint, UiTestSupport.Input(mouseButtons: [UiMouseButton.Left]));
        Frame(advancedPoint);
        Assert.Equal("Advanced", activePanel);
        Assert.True(advanced.Changed);
        Assert.True(panel.W > 0);

        Frame(Vector2.Zero);
        Assert.Equal("Advanced", activePanel);
    }

    [Fact]
    public void TreeNode_DefaultOpen_Renders_Children_And_Click_Toggles_Closed()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);

        Response node = default;
        Response leaf = default;

        void Frame(Vector2 mouse, UiInputState input = default)
        {
            node = default;
            leaf = default;

            ui.Frame(300, 180, mouse, input, frame =>
            {
                node = frame.TreeNode("Assets", content =>
                {
                    leaf = content.TreeLeaf("Mesh");
                }, defaultOpen: true);
            });
        }

        Frame(Vector2.Zero);
        Assert.True(leaf.W > 0);

        Vector2 nodePoint = UiTestSupport.Inside(node);
        Frame(nodePoint, UiTestSupport.Input(mouseButtons: [UiMouseButton.Left]));
        Frame(nodePoint);
        Assert.True(node.Toggled);
        Assert.True(node.Closed);
        Assert.Equal(0f, leaf.W);

        Frame(Vector2.Zero);
        Assert.Equal(0f, leaf.W);
    }

    [Fact]
    public void Tooltip_Response_Overload_Requires_Hover_And_Nonempty_Text()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);
        ui.Theme.TooltipBg = new Color(9, 23, 41, 244);
        ui.Theme.TooltipBorder = new Color(91, 13, 7, 250);
        ui.Theme.TooltipText = new Color(217, 239, 111, 255);

        Response anchor = default;
        bool shown = false;

        void Frame(Vector2 mouse, string text)
        {
            anchor = default;
            shown = false;

            ui.Frame(240, 120, mouse, false, frame =>
            {
                anchor = frame.Button("Hover me");
                shown = frame.Tooltip(anchor, text);
            });
        }

        Frame(new Vector2(220, 100), "Info");
        Assert.False(shown);
        Assert.False(UiTestSupport.HasVertexColor(renderer.LastRenderList, ui.Theme.TooltipBg));

        Vector2 anchorPoint = UiTestSupport.Inside(anchor);
        Frame(anchorPoint, "Info");
        Assert.True(shown);
        Assert.True(UiTestSupport.HasVertexColor(renderer.LastRenderList, ui.Theme.TooltipBg));
        Assert.True(UiTestSupport.HasVertexColor(renderer.LastRenderList, ui.Theme.TooltipBorder));
        Assert.True(UiTestSupport.HasVertexColor(renderer.LastRenderList, ui.Theme.TooltipText));

        Frame(anchorPoint, "   ");
        Assert.False(shown);
        Assert.False(UiTestSupport.HasVertexColor(renderer.LastRenderList, ui.Theme.TooltipBg));
    }

    [Fact]
    public void Tooltip_Position_Overload_Clamps_To_Viewport()
    {
        var renderer = new UiTestRenderer();
        var ui = UiTestSupport.CreateUi(renderer);
        ui.Theme.TooltipBg = new Color(33, 71, 17, 242);

        bool shown = false;

        ui.Frame(120, 80, Vector2.Zero, false, frame =>
        {
            shown = frame.Tooltip(118, 78, "Tooltip text that should clamp", maxWidth: 90f);
        });

        Assert.True(shown);
        DrawVertex[] tooltipVertices = UiTestSupport.VerticesWithColor(renderer.LastRenderList, ui.Theme.TooltipBg);
        Assert.NotEmpty(tooltipVertices);
        Assert.All(tooltipVertices, vertex =>
        {
            Assert.InRange(vertex.Pos.X, -0.1f, 120.1f);
            Assert.InRange(vertex.Pos.Y, -0.1f, 80.1f);
        });
    }
}
