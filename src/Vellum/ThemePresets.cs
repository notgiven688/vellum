using Vellum.Rendering;

namespace Vellum;

/// <summary>
/// Built-in theme presets shipped with Vellum.
/// </summary>
public static class ThemePresets
{
    /// <summary>Creates the default dark theme.</summary>
    public static Theme Dark() => new();

    /// <summary>Creates the built-in light theme.</summary>
    public static Theme Light()
    {
        return new Theme
        {
            SurfaceBg = new Color(242, 244, 247),
            PanelBg = new Color(255, 255, 255),
            PanelBorder = new Color(198, 205, 214),

            TextPrimary = new Color(28, 32, 37),
            TextSecondary = new Color(78, 88, 98),
            WindowTitleText = new Color(28, 32, 37),
            TextMuted = new Color(126, 134, 144),
            Accent = new Color(210, 138, 36),
            FocusBorder = new Color(210, 138, 36),

            ButtonBg = new Color(236, 239, 243),
            ButtonBgHover = new Color(226, 231, 237),
            ButtonBgPressed = new Color(214, 220, 227),
            ButtonBorder = new Color(187, 195, 204),
            ButtonBorderHover = new Color(162, 171, 181),
            ButtonBorderPressed = new Color(145, 154, 164),

            SelectableBg = new Color(244, 246, 249),
            SelectableBgHover = new Color(233, 237, 242),
            SelectableBgPressed = new Color(223, 228, 235),
            SelectableBgSelected = new Color(246, 236, 212),
            SelectableBorder = new Color(198, 205, 214),
            SelectableBorderHover = new Color(168, 177, 187),
            SelectableBorderPressed = new Color(150, 159, 169),
            SelectableBorderSelected = new Color(210, 138, 36),
            SelectableIndicator = new Color(210, 138, 36),

            TextFieldBg = new Color(255, 255, 255),
            TextFieldBgHover = new Color(250, 251, 253),
            TextFieldBgFocused = new Color(255, 255, 255),
            TextFieldBorder = new Color(187, 195, 204),
            TextFieldBorderFocused = new Color(210, 138, 36),
            TextFieldSelectionBg = new Color(112, 153, 214, 120),
            TextFieldCaret = new Color(36, 40, 46),
            TextFieldPlaceholder = new Color(147, 154, 162),

            ScrollAreaBg = new Color(248, 249, 251),
            ScrollAreaBorder = new Color(198, 205, 214),
            ScrollbarTrack = new Color(211, 217, 224, 170),
            ScrollbarThumb = new Color(144, 152, 161, 215),
            ScrollbarThumbHover = new Color(122, 131, 140, 230),
            ScrollbarThumbActive = new Color(95, 105, 115, 240),

            PopupBg = new Color(255, 255, 255),
            PopupBorder = new Color(189, 197, 206),
            ModalBackdrop = new Color(30, 34, 40, 96),
            TooltipBg = new Color(249, 250, 252, 242),
            TooltipBorder = new Color(180, 188, 197, 220),
            TooltipText = new Color(28, 32, 37),

            ToggleBg = new Color(244, 246, 249),
            ToggleBgHover = new Color(235, 238, 242),
            ToggleBgPressed = new Color(224, 228, 234),
            ToggleBgActive = new Color(246, 236, 212),
            ToggleBorder = new Color(198, 205, 214),
            ToggleBorderHover = new Color(168, 177, 187),
            ToggleBorderPressed = new Color(150, 159, 169),
            ToggleBorderActive = new Color(210, 138, 36),
            ToggleIndicator = new Color(210, 138, 36),

            ProgressBarBg = new Color(236, 239, 243),
            ProgressBarBorder = new Color(198, 205, 214),
            ProgressBarFill = new Color(210, 138, 36),
            PlotBg = new Color(236, 239, 243),
            PlotBorder = new Color(198, 205, 214),
            PlotFill = new Color(210, 138, 36, 220),
            Separator = new Color(190, 197, 206, 170),
            CollapsingHeaderBg = Color.Transparent,
            CollapsingHeaderBgHover = new Color(233, 237, 242),
            CollapsingHeaderBgPressed = new Color(223, 228, 235),
            CollapsingHeaderBgOpen = new Color(246, 236, 212),

            SliderBg = new Color(236, 239, 243),
            SliderBgHover = new Color(228, 233, 239),
            SliderBgActive = new Color(220, 226, 233),
            SliderFill = new Color(210, 138, 36),
            SliderFillActive = new Color(223, 154, 60),
            SliderFillText = new Color(36, 30, 20),
            SliderBorder = new Color(190, 197, 206)
        };
    }
}
