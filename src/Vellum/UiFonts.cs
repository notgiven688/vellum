using System.Reflection;

namespace Vellum;

/// <summary>
/// Bundled fonts shipped with Vellum.
/// </summary>
public static class UiFonts
{
    private const string DefaultSansResourceName = "Vellum.Assets.Fonts.JetBrainsMono-Regular.ttf";
    private const string MaterialSymbolsResourceName = "Vellum.Assets.Fonts.MaterialSymbolsOutlined.ttf";
    private static readonly Lazy<TrueTypeFont> s_defaultSans = new(LoadDefaultSans);
    private static readonly Lazy<TrueTypeFont> s_materialSymbols = new(() => LoadFont(MaterialSymbolsResourceName));

    /// <summary>
    /// Embedded fallback UI font used when <see cref="Ui.Font"/> is not set.
    /// </summary>
    public static TrueTypeFont DefaultSans => s_defaultSans.Value;

    /// <summary>
    /// Embedded Google Material Symbols Outlined font for monochrome icon glyphs.
    /// Use Material Symbols codepoints, typically through <see cref="UiFont.Merge(UiFontSource[])"/>; ligature names require OpenType shaping support.
    /// </summary>
    public static TrueTypeFont MaterialSymbols => s_materialSymbols.Value;

    private static TrueTypeFont LoadDefaultSans()
        => LoadFont(DefaultSansResourceName);

    private static TrueTypeFont LoadFont(string resourceName)
    {
        Assembly assembly = typeof(UiFonts).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded font resource '{resourceName}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new TrueTypeFont(memory.ToArray());
    }
}
