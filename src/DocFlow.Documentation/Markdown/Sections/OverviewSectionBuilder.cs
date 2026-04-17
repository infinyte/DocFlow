using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown.Sections;

/// <summary>
/// Emits <c>overview.md</c>: API title, version, description, servers table, and auth summary.
/// </summary>
internal static class OverviewSectionBuilder
{
    public static GeneratedFile Build(SemanticModel model, DocumentationOptions options)
    {
        var api = model.Api;
        var writer = new MarkdownWriter();

        var title = options.Title ?? api?.Title ?? model.Name ?? "API";
        writer.Heading(1, $"{title} — Overview");
        writer.Line();

        if (!string.IsNullOrWhiteSpace(api?.Version))
        {
            writer.Line($"**Version:** {api.Version}");
            writer.Line();
        }

        if (!string.IsNullOrWhiteSpace(api?.Description))
        {
            writer.Line(api.Description.Trim());
            writer.Line();
        }

        WriteServers(writer, api);
        WriteAuthentication(writer, api);

        return new GeneratedFile("overview.md", writer.ToString(), "text/markdown");
    }

    private static void WriteServers(MarkdownWriter writer, ApiSurface? api)
    {
        writer.Heading(2, "Servers");
        writer.Line();

        if (api is null || api.Servers.Count == 0)
        {
            writer.Line("_No servers declared._");
            writer.Line();
            return;
        }

        writer.Line("| URL | Description |");
        writer.Line("| --- | --- |");
        foreach (var server in api.Servers.OrderBy(s => s.Url, StringComparer.Ordinal))
        {
            writer.Line($"| `{server.Url}` | {server.Description ?? ""} |");
        }
        writer.Line();
    }

    private static void WriteAuthentication(MarkdownWriter writer, ApiSurface? api)
    {
        writer.Heading(2, "Authentication");
        writer.Line();

        if (api is null || api.SecuritySchemes.Count == 0)
        {
            writer.Line("_No authentication configured._");
            writer.Line();
            return;
        }

        writer.Line("| Scheme | Type |");
        writer.Line("| --- | --- |");
        foreach (var kvp in api.SecuritySchemes.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            writer.Line($"| `{kvp.Key}` | {kvp.Value.Type} |");
        }
        writer.Line();
    }
}
