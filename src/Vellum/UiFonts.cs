using System.Reflection;

namespace Vellum;

/// <summary>
/// Bundled fonts shipped with Vellum.
/// </summary>
public static class UiFonts
{
    private const string DefaultSansResourceName = "Vellum.Assets.Fonts.Roboto-Regular.ttf";
    private static readonly Lazy<TrueTypeFont> s_defaultSans = new(LoadDefaultSans);

    /// <summary>
    /// Embedded fallback UI font used when <see cref="Ui.Font"/> is not set.
    /// </summary>
    public static TrueTypeFont DefaultSans => s_defaultSans.Value;

    private static TrueTypeFont LoadDefaultSans()
    {
        Assembly assembly = typeof(UiFonts).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(DefaultSansResourceName)
            ?? throw new InvalidOperationException($"Embedded font resource '{DefaultSansResourceName}' was not found.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return new TrueTypeFont(memory.ToArray());
    }
}
