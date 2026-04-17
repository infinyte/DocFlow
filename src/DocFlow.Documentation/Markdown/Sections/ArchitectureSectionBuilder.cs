using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown.Sections;

/// <summary>
/// Emits <c>architecture.md</c> (overview sentence + context diagram + server table) and a
/// standalone <c>diagrams/context.mmd</c> file.
/// </summary>
internal static class ArchitectureSectionBuilder
{
    public static IEnumerable<GeneratedFile> Build(
        SemanticModel model,
        DocumentationOptions options,
        MermaidC4ContextGenerator contextGenerator)
    {
        if ((options.Diagrams & DiagramKinds.Context) == 0)
        {
            return [];
        }

        var api = model.Api;
        var contextDiagram = contextGenerator.Generate(api);

        var writer = new MarkdownWriter();
        writer.Heading(1, "Architecture");
        writer.Line();

        writer.Heading(2, "System Context");
        writer.Line();
        writer.Line("```mermaid");
        writer.Raw(contextDiagram);
        writer.Line("```");
        writer.Line();

        writer.Heading(2, "Deployments");
        writer.Line();

        if (api is null || api.Servers.Count == 0)
        {
            writer.Line("_No servers declared._");
            writer.Line();
        }
        else
        {
            writer.Line("| URL | Description |");
            writer.Line("| --- | --- |");
            foreach (var server in api.Servers.OrderBy(s => s.Url, StringComparer.Ordinal))
            {
                writer.Line($"| `{server.Url}` | {server.Description ?? ""} |");
            }
            writer.Line();
        }

        return
        [
            new GeneratedFile("architecture.md", writer.ToString(), "text/markdown"),
            new GeneratedFile("diagrams/context.mmd", contextDiagram, "text/vnd.mermaid")
        ];
    }
}
