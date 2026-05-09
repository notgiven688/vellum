using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Global style defaults. Same model as ImGui's ImGuiStyle / egui's Style —
/// no selectors, no cascade, just a struct of defaults.
/// </summary>
public sealed class Theme
{
    // Surfaces
    /// <summary>Default full-viewport background color.</summary>
    public Color SurfaceBg       = new(30, 30, 30);
    /// <summary>Panel fill color.</summary>
    public Color PanelBg         = new(45, 45, 45);
    /// <summary>Panel border color.</summary>
    public Color PanelBorder     = new(82, 82, 82);

    // Text
    /// <summary>Primary text color.</summary>
    public Color TextPrimary     = new(220, 220, 220);
    /// <summary>Secondary text color.</summary>
    public Color TextSecondary   = new(170, 170, 170);
    /// <summary>Muted text color.</summary>
    public Color TextMuted       = new(120, 120, 120);
    /// <summary>Window title text color.</summary>
    public Color WindowTitleText = new(220, 220, 220);
    /// <summary>Window title bar fill color.</summary>
    public Color WindowTitleBg   = Color.Transparent;
    /// <summary>Window title bar fill color while hovered.</summary>
    public Color WindowTitleBgHover = Color.Transparent;
    /// <summary>Whether text may use LCD/subpixel rasterization.</summary>
    public bool UseLcdText       = true;
    /// <summary>Primary accent color.</summary>
    public Color Accent          = new(255, 200, 90);
    /// <summary>Keyboard focus border color.</summary>
    public Color FocusBorder     = new(255, 200, 90);

    // Button
    /// <summary>Button fill color.</summary>
    public Color ButtonBg        = new(70, 70, 70);
    /// <summary>Button fill color while hovered.</summary>
    public Color ButtonBgHover   = new(95, 95, 95);
    /// <summary>Button fill color while pressed.</summary>
    public Color ButtonBgPressed = new(50, 50, 50);
    /// <summary>Button border color.</summary>
    public Color ButtonBorder        = new(108, 108, 108);
    /// <summary>Button border color while hovered.</summary>
    public Color ButtonBorderHover   = new(138, 138, 138);
    /// <summary>Button border color while pressed.</summary>
    public Color ButtonBorderPressed = new(80, 80, 80);

    // Selectable / menu item
    /// <summary>Selectable row fill color.</summary>
    public Color SelectableBg         = new(36, 36, 36);
    /// <summary>Selectable row fill color while hovered.</summary>
    public Color SelectableBgHover    = new(52, 52, 52);
    /// <summary>Selectable row fill color while pressed.</summary>
    public Color SelectableBgPressed  = new(28, 28, 28);
    /// <summary>Selectable row fill color when selected.</summary>
    public Color SelectableBgSelected = new(48, 40, 28);
    /// <summary>Selectable row border color.</summary>
    public Color SelectableBorder     = new(82, 82, 82);
    /// <summary>Selectable row border color while hovered.</summary>
    public Color SelectableBorderHover = new(118, 118, 118);
    /// <summary>Selectable row border color while pressed.</summary>
    public Color SelectableBorderPressed = new(66, 66, 66);
    /// <summary>Selectable row border color when selected.</summary>
    public Color SelectableBorderSelected = new(255, 200, 90);
    /// <summary>Indicator color for selected rows and menu items.</summary>
    public Color SelectableIndicator   = new(255, 200, 90);
    /// <summary>Menu item label text color.</summary>
    public Color MenuItemText          = new(220, 220, 220);
    /// <summary>Menu item shortcut text color.</summary>
    public Color MenuItemShortcutText  = new(170, 170, 170);

    // Text field
    /// <summary>Text field fill color.</summary>
    public Color TextFieldBg            = new(24, 24, 24);
    /// <summary>Text field fill color while hovered.</summary>
    public Color TextFieldBgHover       = new(30, 30, 30);
    /// <summary>Text field fill color while focused.</summary>
    public Color TextFieldBgFocused     = new(20, 20, 20);
    /// <summary>Text field border color.</summary>
    public Color TextFieldBorder        = new(82, 82, 82);
    /// <summary>Text field border color while focused.</summary>
    public Color TextFieldBorderFocused = new(255, 200, 90);
    /// <summary>Text selection highlight color.</summary>
    public Color TextFieldSelectionBg   = new(86, 122, 178, 160);
    /// <summary>Text caret color.</summary>
    public Color TextFieldCaret         = new(255, 240, 210);
    /// <summary>Text field placeholder text color.</summary>
    public Color TextFieldPlaceholder   = new(120, 120, 120);

    // Scroll area
    /// <summary>Scroll area fill color.</summary>
    public Color ScrollAreaBg           = new(24, 24, 24);
    /// <summary>Scroll area border color.</summary>
    public Color ScrollAreaBorder       = new(82, 82, 82);
    /// <summary>Scrollbar track color.</summary>
    public Color ScrollbarTrack         = new(18, 18, 18, 180);
    /// <summary>Scrollbar thumb color.</summary>
    public Color ScrollbarThumb         = new(90, 90, 90, 220);
    /// <summary>Scrollbar thumb color while hovered.</summary>
    public Color ScrollbarThumbHover    = new(120, 120, 120, 235);
    /// <summary>Scrollbar thumb color while dragged.</summary>
    public Color ScrollbarThumbActive   = new(160, 160, 160, 245);

    // Popup
    /// <summary>Popup fill color.</summary>
    public Color PopupBg                = new(34, 34, 34);
    /// <summary>Popup border color.</summary>
    public Color PopupBorder            = new(92, 92, 92);
    /// <summary>Modal popup backdrop color.</summary>
    public Color ModalBackdrop          = new(0, 0, 0, 150);
    /// <summary>Tooltip fill color.</summary>
    public Color TooltipBg              = new(24, 24, 24, 236);
    /// <summary>Tooltip border color.</summary>
    public Color TooltipBorder          = new(104, 104, 104, 220);
    /// <summary>Tooltip text color.</summary>
    public Color TooltipText            = new(220, 220, 220);

    // Toggle controls
    /// <summary>Checkbox, radio, and switch fill color.</summary>
    public Color ToggleBg               = new(24, 24, 24);
    /// <summary>Toggle fill color while hovered.</summary>
    public Color ToggleBgHover          = new(30, 30, 30);
    /// <summary>Toggle fill color while pressed.</summary>
    public Color ToggleBgPressed        = new(18, 18, 18);
    /// <summary>Toggle fill color while active.</summary>
    public Color ToggleBgActive         = new(48, 40, 28);
    /// <summary>Toggle border color.</summary>
    public Color ToggleBorder           = new(82, 82, 82);
    /// <summary>Toggle border color while hovered.</summary>
    public Color ToggleBorderHover      = new(118, 118, 118);
    /// <summary>Toggle border color while pressed.</summary>
    public Color ToggleBorderPressed    = new(66, 66, 66);
    /// <summary>Toggle border color while active.</summary>
    public Color ToggleBorderActive     = new(255, 200, 90);
    /// <summary>Toggle check, radio dot, and active indicator color.</summary>
    public Color ToggleIndicator        = new(255, 200, 90);

    // Progress + separators
    /// <summary>Progress bar fill background color.</summary>
    public Color ProgressBarBg          = new(24, 24, 24);
    /// <summary>Progress bar border color.</summary>
    public Color ProgressBarBorder      = new(82, 82, 82);
    /// <summary>Progress bar filled portion color.</summary>
    public Color ProgressBarFill        = new(255, 200, 90);
    /// <summary>Plot and histogram background color.</summary>
    public Color PlotBg                 = new(24, 24, 24);
    /// <summary>Plot and histogram border color.</summary>
    public Color PlotBorder             = new(82, 82, 82);
    /// <summary>Plot and histogram fill color.</summary>
    public Color PlotFill               = new(255, 200, 90, 220);
    /// <summary>Separator line color.</summary>
    public Color Separator              = new(82, 82, 82, 180);
    /// <summary>Collapsing header fill color.</summary>
    public Color CollapsingHeaderBg     = default;
    /// <summary>Collapsing header fill color while hovered.</summary>
    public Color CollapsingHeaderBgHover = new(45, 45, 45, 180);
    /// <summary>Collapsing header fill color while pressed.</summary>
    public Color CollapsingHeaderBgPressed = new(45, 45, 45, 220);
    /// <summary>Collapsing header fill color while open.</summary>
    public Color CollapsingHeaderBgOpen = new(45, 45, 45, 140);

    // Slider
    /// <summary>Slider background color.</summary>
    public Color SliderBg               = new(24, 24, 24);
    /// <summary>Slider background color while hovered.</summary>
    public Color SliderBgHover          = new(30, 30, 30);
    /// <summary>Slider background color while active.</summary>
    public Color SliderBgActive         = new(18, 18, 18);
    /// <summary>Slider filled block color.</summary>
    public Color SliderFill             = new(255, 200, 90);
    /// <summary>Slider filled block color while active.</summary>
    public Color SliderFillActive       = new(255, 214, 128);
    /// <summary>Text color drawn over the slider filled block.</summary>
    public Color SliderFillText         = new(36, 30, 20);
    /// <summary>Slider border color.</summary>
    public Color SliderBorder           = new(82, 82, 82);

    // Spacing
    /// <summary>Button inner padding.</summary>
    public EdgeInsets ButtonPadding = new(6f, 14f);
    /// <summary>Panel inner padding.</summary>
    public EdgeInsets PanelPadding = new(12f);
    /// <summary>Menu bar outer padding.</summary>
    public EdgeInsets MenuBarPadding = new(2f, 4f);
    /// <summary>Menu bar item padding.</summary>
    public EdgeInsets MenuBarItemPadding = new(3f, 8f);
    /// <summary>Menu item padding.</summary>
    public EdgeInsets MenuItemPadding = new(2f, 6f);
    /// <summary>Combo box padding.</summary>
    public EdgeInsets ComboBoxPadding = new(2f, 6f);
    /// <summary>Text field padding.</summary>
    public EdgeInsets TextFieldPadding = new(6f, 10f);
    /// <summary>Popup content padding.</summary>
    public EdgeInsets PopupPadding = new(8f);
    /// <summary>Tooltip content padding.</summary>
    public EdgeInsets TooltipPadding = new(6f, 8f);
    /// <summary>Collapsing header padding.</summary>
    public EdgeInsets CollapsingHeaderPadding = new(4f, 2f);
    /// <summary>Tree node padding.</summary>
    public EdgeInsets TreeNodePadding = new(2f, 6f);
    /// <summary>Indent width for each tree depth.</summary>
    public float TreeIndent      = 14f;
    /// <summary>Tab label padding.</summary>
    public EdgeInsets TabPadding = new(5f, 12f);
    /// <summary>Thickness of active tab indicators.</summary>
    public float TabIndicatorThickness = 2f;
    /// <summary>Spacing between adjacent tabs.</summary>
    public float TabSpacing = 2f;
    /// <summary>Default spacing between stacked widgets.</summary>
    public float Gap             = 8f;
    /// <summary>Default border width.</summary>
    public float BorderWidth     = 1f;
    /// <summary>Default corner radius.</summary>
    public float BorderRadius    = 6f;
    /// <summary>Scrollbar thickness.</summary>
    public float ScrollbarWidth  = 10f;
    /// <summary>Minimum scrollbar thumb size.</summary>
    public float ScrollbarMinThumbSize = 20f;
    /// <summary>Scroll distance applied for each wheel unit.</summary>
    public float ScrollWheelStep = 36f;
    /// <summary>Slider height.</summary>
    public float SliderHeight      = 22f;
    /// <summary>Fractional slider block width. Values above zero override <see cref="SliderBlockWidth"/>.</summary>
    public float SliderBlockWidthFactor = 0f;
    /// <summary>Fixed slider block width when <see cref="SliderBlockWidthFactor"/> is zero.</summary>
    public float SliderBlockWidth  = 42f;
    /// <summary>Keyboard step for sliders. Zero derives the step from slider range.</summary>
    public float SliderKeyboardStep = 0f;
    /// <summary>Gap between collapsing header arrow and label.</summary>
    public float CollapsingHeaderArrowGap = 4f;
    /// <summary>Horizontal offset from pointer to tooltip.</summary>
    public float TooltipOffsetX = 14f;
    /// <summary>Vertical offset from pointer to tooltip.</summary>
    public float TooltipOffsetY = 18f;
}
