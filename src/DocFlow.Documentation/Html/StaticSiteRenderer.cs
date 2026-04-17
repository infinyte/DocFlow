using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using DocFlow.Documentation.Models;
using Markdig;
using MarkdigMarkdown = Markdig.Markdown;

namespace DocFlow.Documentation.Html;

/// <summary>
/// Converts a Markdown documentation bundle into a self-contained static HTML site.
/// The Markdown is rendered with Markdig; Mermaid fences (<c>```mermaid ... ```</c>) are
/// rewritten to <c>&lt;pre class="mermaid"&gt;</c> so Mermaid.js picks them up; intra-bundle
/// <c>.md</c> links are rewritten to <c>.html</c>. A sidebar nav is built from the file tree
/// with the current page highlighted.
/// </summary>
public sealed class StaticSiteRenderer
{
    private const string MermaidCdn = "https://cdn.jsdelivr.net/npm/mermaid@10/dist/mermaid.min.js";

    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .Build();

    private static readonly Regex MdHrefPattern = new(
        @"(href=""[^""]*?)\.md(#[^""]*)?""",
        RegexOptions.Compiled);

    public IReadOnlyList<GeneratedFile> Render(IReadOnlyList<GeneratedFile> markdownBundle)
    {
        var markdownFiles = markdownBundle
            .Where(f => f.RelativePath.EndsWith(".md", StringComparison.Ordinal))
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();

        var navItems = BuildNavItems(markdownFiles);

        var output = new List<GeneratedFile>();

        foreach (var file in markdownBundle)
        {
            if (file.RelativePath.EndsWith(".md", StringComparison.Ordinal))
            {
                output.Add(RenderPage(file, navItems));
            }
            else
            {
                output.Add(file);
            }
        }

        output.Add(new GeneratedFile("assets/theme.css", LoadEmbeddedCss(), "text/css"));

        return output
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();
    }

    private static GeneratedFile RenderPage(GeneratedFile markdown, IReadOnlyList<NavItem> navItems)
    {
        // Markdig's advanced-diagrams extension renders ```mermaid fences as
        // <div class="mermaid">…</div>, which Mermaid.js auto-initialises on load.
        var htmlBody = MarkdigMarkdown.ToHtml(markdown.Content, Pipeline);

        // Rewrite intra-bundle .md links to .html (preserving any fragment).
        htmlBody = MdHrefPattern.Replace(htmlBody, m =>
            $"{m.Groups[1].Value}.html{m.Groups[2].Value}\"");

        var htmlPath = markdown.RelativePath[..^3] + ".html";
        var relativeRoot = RelativeRootFor(htmlPath);
        var title = ExtractTitle(markdown.Content) ?? Path.GetFileNameWithoutExtension(htmlPath);
        var sidebar = RenderSidebar(navItems, htmlPath, relativeRoot);

        var sb = new StringBuilder();
        sb.Append("<!doctype html>\n");
        sb.Append("<html lang=\"en\">\n");
        sb.Append("<head>\n");
        sb.Append("  <meta charset=\"utf-8\">\n");
        sb.Append("  <meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">\n");
        sb.Append($"  <title>{HtmlEncode(title)}</title>\n");
        sb.Append($"  <link rel=\"stylesheet\" href=\"{relativeRoot}assets/theme.css\">\n");
        sb.Append("</head>\n");
        sb.Append("<body>\n");
        sb.Append("<div class=\"layout\">\n");
        sb.Append("<aside class=\"sidebar\">\n");
        sb.Append(sidebar);
        sb.Append("</aside>\n");
        sb.Append("<main class=\"content\">\n");
        sb.Append(htmlBody);
        sb.Append("</main>\n");
        sb.Append("</div>\n");
        sb.Append($"<script src=\"{MermaidCdn}\"></script>\n");
        sb.Append("<script>mermaid.initialize({ startOnLoad: true, theme: window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'default' });</script>\n");
        sb.Append("</body>\n");
        sb.Append("</html>\n");

        return new GeneratedFile(htmlPath, sb.ToString(), "text/html");
    }

    private static string RenderSidebar(IReadOnlyList<NavItem> items, string currentPath, string relativeRoot)
    {
        var sb = new StringBuilder();
        sb.Append("<h2>Documentation</h2>\n");
        sb.Append("<ul>\n");

        foreach (var item in items)
        {
            if (item.Children.Count == 0)
            {
                RenderNavLink(sb, item, currentPath, relativeRoot);
            }
            else
            {
                sb.Append("<li>");
                sb.Append(HtmlEncode(item.Label));
                sb.Append("<ul>\n");
                foreach (var child in item.Children)
                {
                    RenderNavLink(sb, child, currentPath, relativeRoot);
                }
                sb.Append("</ul></li>\n");
            }
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static void RenderNavLink(StringBuilder sb, NavItem item, string currentPath, string relativeRoot)
    {
        var cls = string.Equals(item.Path, currentPath, StringComparison.Ordinal) ? " class=\"active\"" : "";
        sb.Append($"<li><a href=\"{relativeRoot}{item.Path}\"{cls}>{HtmlEncode(item.Label)}</a></li>\n");
    }

    private static IReadOnlyList<NavItem> BuildNavItems(IReadOnlyList<GeneratedFile> markdownFiles)
    {
        var items = new List<NavItem>();

        // Top-level files first (index, overview, domain-model, architecture, security…).
        foreach (var file in markdownFiles.Where(f => !f.RelativePath.Contains('/')))
        {
            var htmlPath = file.RelativePath[..^3] + ".html";
            items.Add(new NavItem(LinkLabelFor(file.RelativePath), htmlPath, []));
        }

        // Group subdirectories (endpoints/, sequences/) under a parent node.
        var grouped = markdownFiles
            .Where(f => f.RelativePath.Contains('/'))
            .GroupBy(f => f.RelativePath.Split('/')[0])
            .OrderBy(g => g.Key, StringComparer.Ordinal);

        foreach (var group in grouped)
        {
            var children = group
                .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
                .Select(f => new NavItem(
                    LinkLabelFor(f.RelativePath),
                    f.RelativePath[..^3] + ".html",
                    []))
                .ToList();
            items.Add(new NavItem(Capitalize(group.Key), string.Empty, children));
        }

        return items;
    }

    private static string LinkLabelFor(string relativePath)
    {
        var stem = Path.GetFileNameWithoutExtension(relativePath);
        return string.Join(' ', stem.Split('-').Select(Capitalize));
    }

    private static string Capitalize(string token) =>
        string.IsNullOrEmpty(token) ? token : char.ToUpperInvariant(token[0]) + token[1..];

    private static string RelativeRootFor(string path)
    {
        var depth = path.Count(c => c == '/');
        return depth == 0 ? "./" : string.Concat(Enumerable.Repeat("../", depth));
    }

    private static string? ExtractTitle(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                return trimmed[2..].Trim();
            }
        }
        return null;
    }

    private static string HtmlEncode(string value) =>
        System.Net.WebUtility.HtmlEncode(value);

    private static string LoadEmbeddedCss()
    {
        var assembly = typeof(StaticSiteRenderer).Assembly;
        var name = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("theme.css", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("theme.css embedded resource not found.");
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Could not load {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record NavItem(string Label, string Path, IReadOnlyList<NavItem> Children);
}
