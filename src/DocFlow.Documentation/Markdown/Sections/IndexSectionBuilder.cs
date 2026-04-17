using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown.Sections;

/// <summary>
/// Emits <c>index.md</c>: a TOC linking every other Markdown file in the bundle.
/// </summary>
internal static class IndexSectionBuilder
{
    public static GeneratedFile Build(SemanticModel model, DocumentationOptions options, IReadOnlyList<GeneratedFile> siblings)
    {
        var writer = new MarkdownWriter();
        var title = options.Title ?? model.Api?.Title ?? model.Name ?? "API";

        writer.Heading(1, $"{title} — Documentation");
        writer.Line();

        var markdownFiles = siblings
            .Where(f => f.RelativePath.EndsWith(".md", StringComparison.Ordinal)
                        && !f.RelativePath.Equals("index.md", StringComparison.Ordinal))
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();

        var topLevel = markdownFiles.Where(f => !f.RelativePath.Contains('/')).ToList();
        var endpointPages = markdownFiles
            .Where(f => f.RelativePath.StartsWith("endpoints/", StringComparison.Ordinal))
            .ToList();

        foreach (var file in topLevel)
        {
            writer.Line($"- [{LinkLabel(file.RelativePath)}](./{file.RelativePath})");
        }

        if (endpointPages.Count > 0)
        {
            writer.Line("- Endpoints");
            foreach (var file in endpointPages)
            {
                writer.Line($"  - [{LinkLabel(file.RelativePath)}](./{file.RelativePath})");
            }
        }

        writer.Line();
        return new GeneratedFile("index.md", writer.ToString(), "text/markdown");
    }

    private static string LinkLabel(string relativePath)
    {
        var fileName = relativePath.Split('/').Last();
        var stem = fileName.EndsWith(".md", StringComparison.Ordinal) ? fileName[..^3] : fileName;
        // Turn "domain-model" → "Domain Model", "pet" → "Pet"
        return string.Join(' ', stem.Split('-').Select(Capitalize));
    }

    private static string Capitalize(string token) =>
        string.IsNullOrEmpty(token) ? token : char.ToUpperInvariant(token[0]) + token[1..];
}
