using System.Globalization;
using System.Text;
using Vellum.Rendering;
using Vellum.SoftwareRendering;

namespace Vellum.WidgetGallery;

internal static class Program
{
    private static readonly string[] s_categoryOrder =
    [
        "Text",
        "Controls",
        "Status",
        "Input",
        "Layout",
        "Media",
        "Navigation",
        "Menus",
        "Windows"
    ];

    private static readonly GalleryTheme[] s_themes =
    [
        new("dark", "Dark", ThemePresets.Dark()),
        new("light", "Light", ThemePresets.Light())
    ];

    public static int Main(string[] args)
    {
        string root = args.Length > 0 ? Path.GetFullPath(args[0]) : FindRepoRoot();
        string docsDir = Path.Combine(root, "docs", "docs");
        string imageDir = Path.Combine(docsDir, "images", "widgets");
        Directory.CreateDirectory(imageDir);

        foreach (WidgetExample example in WidgetExamples.All)
        {
            foreach (GalleryTheme theme in s_themes)
            {
                string imagePath = Path.Combine(imageDir, $"{example.Id}-{theme.Id}.png");
                RenderExample(example, theme, imagePath);
            }
        }

        WriteMarkdown(Path.Combine(docsDir, "widget-gallery.md"));
        Console.WriteLine($"Generated {WidgetExamples.All.Count * s_themes.Length} widget screenshots.");
        return 0;
    }

    private static void RenderExample(WidgetExample example, GalleryTheme galleryTheme, string imagePath)
    {
        using var renderer = new SoftwareRenderer(example.Width, example.Height, galleryTheme.Theme.SurfaceBg);
        var context = new WidgetExampleContext(renderer);
        var ui = new Ui(renderer)
        {
            Theme = galleryTheme.Theme,
            Font = UiFonts.DefaultSans,
            DefaultFontSize = 18f,
            Lcd = false,
            RootPadding = 14f
        };

        ui.Frame(example.Width, example.Height, example.Mouse, example.Input, root =>
        {
            root.FillViewport(root.Theme.SurfaceBg);
            example.Draw(root, context);
        });

        renderer.SavePng(imagePath);
    }

    private static void WriteMarkdown(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Widget Gallery");
        sb.AppendLine();
        sb.AppendLine("This page is generated from curated widget examples in `other/Vellum.WidgetGallery`. Each screenshot is rendered headlessly through `Vellum.SoftwareRendering`.");
        sb.AppendLine();

        foreach (string category in s_categoryOrder)
        {
            var examples = WidgetExamples.All
                .Where(example => example.Category == category)
                .OrderBy(example => example.Title, StringComparer.Ordinal)
                .ToArray();
            if (examples.Length == 0) continue;

            sb.AppendLine($"## {category}");
            sb.AppendLine();

            foreach (WidgetExample example in examples)
            {
                sb.AppendLine($"### {example.Title}");
                sb.AppendLine();
                sb.AppendLine("| Dark | Light |");
                sb.AppendLine("| --- | --- |");
                sb.Append("| ");
                for (int i = 0; i < s_themes.Length; i++)
                {
                    GalleryTheme theme = s_themes[i];
                    sb.Append($"![{EscapeAltText(example.Title)} - {theme.Name}](images/widgets/{example.Id}-{theme.Id}.png)");
                    sb.Append(i == s_themes.Length - 1 ? " |" : " | ");
                }
                sb.AppendLine();
                sb.AppendLine();
            }
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static string EscapeAltText(string text)
        => text.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(Environment.CurrentDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "docfx.json")) &&
                Directory.Exists(Path.Combine(dir.FullName, "src", "Vellum")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"Could not locate repository root from '{Environment.CurrentDirectory}'."));
    }

    private sealed record GalleryTheme(string Id, string Name, Theme Theme);
}
