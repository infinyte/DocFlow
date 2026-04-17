using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using DocFlow.Documentation.Examples;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown.Sections;

/// <summary>
/// Emits <c>endpoints/&lt;group&gt;.md</c>: one page per tag (or per first path segment).
/// When <see cref="DiagramKinds.Sequence"/> is enabled, each operation's sequence diagram is
/// also embedded on the endpoint page and emitted as <c>sequences/&lt;operationId&gt;.md</c>.
/// </summary>
internal static class EndpointSectionBuilder
{
    private const string UntaggedBucket = "Untagged";

    public static IEnumerable<GeneratedFile> Build(
        SemanticModel model,
        DocumentationOptions options,
        MermaidSequenceDiagramGenerator sequenceDiagramGenerator,
        MermaidEndpointFlowchartGenerator flowchartGenerator)
    {
        var api = model.Api;
        if (api is null || api.Operations.Count == 0)
        {
            return [];
        }

        var sequenceEnabled = (options.Diagrams & DiagramKinds.Sequence) != 0;
        var flowEnabled = (options.Diagrams & DiagramKinds.Flow) != 0;
        var entityNames = model.Entities.Values
            .Select(e => e.Name)
            .ToHashSet(StringComparer.Ordinal);
        var synthesizer = options.WithExamples ? new ExampleSynthesizer(model) : null;

        var groups = api.Operations
            .GroupBy(op => ChooseGroup(op, options.GroupBy))
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .ToList();

        var files = new List<GeneratedFile>();

        foreach (var group in groups)
        {
            files.Add(BuildPage(
                group.Key,
                group.ToList(),
                sequenceEnabled,
                flowEnabled,
                sequenceDiagramGenerator,
                flowchartGenerator,
                entityNames,
                synthesizer));
        }

        if (sequenceEnabled)
        {
            foreach (var operation in api.Operations.OrderBy(o => o.OperationId, StringComparer.Ordinal))
            {
                files.Add(BuildSequencePage(operation, sequenceDiagramGenerator));
            }
        }

        return files;
    }

    private static string ChooseGroup(ApiOperation op, GroupBy groupBy)
    {
        if (groupBy == GroupBy.Tag)
        {
            var firstTag = op.Tags.FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            return firstTag ?? UntaggedBucket;
        }

        // GroupBy.Path
        var path = op.Path.Trim('/');
        if (string.IsNullOrEmpty(path)) return "root";
        var slash = path.IndexOf('/');
        return slash < 0 ? path : path[..slash];
    }

    private static GeneratedFile BuildPage(
        string groupName,
        List<ApiOperation> operations,
        bool sequenceEnabled,
        bool flowEnabled,
        MermaidSequenceDiagramGenerator sequenceDiagramGenerator,
        MermaidEndpointFlowchartGenerator flowchartGenerator,
        HashSet<string> entityNames,
        ExampleSynthesizer? synthesizer)
    {
        var writer = new MarkdownWriter();
        writer.Heading(1, groupName);
        writer.Line();

        foreach (var op in operations.OrderBy(o => o.OperationId, StringComparer.Ordinal))
        {
            WriteOperation(
                writer,
                op,
                sequenceEnabled,
                flowEnabled,
                sequenceDiagramGenerator,
                flowchartGenerator,
                entityNames,
                synthesizer);
        }

        var slug = Slug.Kebab(groupName);
        if (string.IsNullOrEmpty(slug)) slug = "untagged";
        return new GeneratedFile($"endpoints/{slug}.md", writer.ToString(), "text/markdown");
    }

    private static GeneratedFile BuildSequencePage(ApiOperation op, MermaidSequenceDiagramGenerator generator)
    {
        var writer = new MarkdownWriter();
        writer.Heading(1, $"`{op.Method.ToString().ToUpperInvariant()} {op.Path}`");
        writer.Line();
        writer.Line($"**Operation ID:** `{op.OperationId}`");
        writer.Line();
        writer.Line("```mermaid");
        writer.Raw(generator.Generate(op));
        writer.Line("```");
        writer.Line();

        return new GeneratedFile($"sequences/{op.OperationId}.md", writer.ToString(), "text/markdown");
    }

    private static void WriteOperation(
        MarkdownWriter writer,
        ApiOperation op,
        bool sequenceEnabled,
        bool flowEnabled,
        MermaidSequenceDiagramGenerator sequenceDiagramGenerator,
        MermaidEndpointFlowchartGenerator flowchartGenerator,
        HashSet<string> entityNames,
        ExampleSynthesizer? synthesizer)
    {
        writer.Heading(2, $"`{op.Method.ToString().ToUpperInvariant()} {op.Path}`");
        writer.Line();
        writer.Line($"**Operation ID:** `{op.OperationId}`");
        writer.Line();

        if (!string.IsNullOrWhiteSpace(op.Summary))
        {
            writer.Line(op.Summary.Trim());
            writer.Line();
        }

        if (!string.IsNullOrWhiteSpace(op.Description))
        {
            writer.Line(op.Description.Trim());
            writer.Line();
        }

        if (op.Deprecated)
        {
            writer.Line("> **Deprecated.**");
            writer.Line();
        }

        WriteParameters(writer, op, entityNames);
        WriteRequestBody(writer, op, entityNames);
        WriteResponses(writer, op, entityNames);

        if (synthesizer is not null)
        {
            WriteExamples(writer, op, synthesizer);
        }

        if (sequenceEnabled)
        {
            writer.Line("```mermaid");
            writer.Raw(sequenceDiagramGenerator.Generate(op));
            writer.Line("```");
            writer.Line();
        }

        if (flowEnabled)
        {
            writer.Line("```mermaid");
            writer.Raw(flowchartGenerator.Generate(op));
            writer.Line("```");
            writer.Line();
        }
    }

    private static void WriteParameters(MarkdownWriter writer, ApiOperation op, HashSet<string> entityNames)
    {
        if (op.Parameters.Count == 0) return;

        writer.Heading(3, "Parameters");
        writer.Line();
        writer.Line("| Name | In | Type | Required | Description |");
        writer.Line("| --- | --- | --- | --- | --- |");

        foreach (var param in op.Parameters
                     .OrderBy(p => p.Location)
                     .ThenBy(p => p.Name, StringComparer.Ordinal))
        {
            var type = DescribeSchema(param.Schema, entityNames);
            var required = param.Required ? "yes" : "no";
            var description = (param.Description ?? "").Replace('|', '／').Replace('\n', ' ').Trim();
            writer.Line($"| `{param.Name}` | {param.Location.ToString().ToLowerInvariant()} | {type} | {required} | {description} |");
        }
        writer.Line();
    }

    private static void WriteRequestBody(MarkdownWriter writer, ApiOperation op, HashSet<string> entityNames)
    {
        if (op.RequestBody is null || op.RequestBody.Content.Count == 0) return;

        writer.Heading(3, "Request Body");
        writer.Line();

        if (op.RequestBody.Required)
        {
            writer.Line("_Required._");
            writer.Line();
        }

        foreach (var kvp in op.RequestBody.Content.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            writer.Line($"- `{kvp.Key}` → {DescribeMedia(kvp.Value, entityNames)}");
        }
        writer.Line();
    }

    private static void WriteResponses(MarkdownWriter writer, ApiOperation op, HashSet<string> entityNames)
    {
        if (op.Responses.Count == 0) return;

        writer.Heading(3, "Responses");
        writer.Line();
        writer.Line("| Status | Content-Type | Schema | Description |");
        writer.Line("| --- | --- | --- | --- |");

        foreach (var kvp in op.Responses.OrderBy(r => r.Key, StringComparer.Ordinal))
        {
            var response = kvp.Value;
            var description = (response.Description ?? "").Replace('|', '／').Replace('\n', ' ').Trim();

            if (response.Content.Count == 0)
            {
                writer.Line($"| `{kvp.Key}` | _none_ | _none_ | {description} |");
                continue;
            }

            foreach (var content in response.Content.OrderBy(c => c.Key, StringComparer.Ordinal))
            {
                writer.Line($"| `{kvp.Key}` | `{content.Key}` | {DescribeMedia(content.Value, entityNames)} | {description} |");
            }
        }
        writer.Line();
    }

    private static void WriteExamples(MarkdownWriter writer, ApiOperation op, ExampleSynthesizer synthesizer)
    {
        var requestBodyExample = op.RequestBody?.Content
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .Select(c => synthesizer.Synthesize(c.Value))
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));

        var responseExample = op.Responses
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .Where(r => r.Key.StartsWith("2", StringComparison.Ordinal))
            .SelectMany(r => r.Value.Content.OrderBy(c => c.Key, StringComparer.Ordinal))
            .Select(c => synthesizer.Synthesize(c.Value))
            .FirstOrDefault(e => !string.IsNullOrWhiteSpace(e));

        if (requestBodyExample is null && responseExample is null) return;

        writer.Heading(3, "Example Request/Response");
        writer.Line();

        if (requestBodyExample is not null)
        {
            writer.Line("**Request:**");
            writer.Line();
            writer.Line("```json");
            writer.Raw(requestBodyExample);
            writer.Line("```");
            writer.Line();
        }

        if (responseExample is not null)
        {
            writer.Line("**Response:**");
            writer.Line();
            writer.Line("```json");
            writer.Raw(responseExample);
            writer.Line("```");
            writer.Line();
        }
    }

    private static string DescribeMedia(ApiMediaType media, HashSet<string> entityNames)
    {
        if (!string.IsNullOrEmpty(media.EntityName))
        {
            return EntityLink(media.EntityName, entityNames);
        }

        return DescribeSchema(media, entityNames);
    }

    private static string DescribeSchema(ApiMediaType? media, HashSet<string> entityNames)
    {
        if (media is null) return "`unknown`";

        if (!string.IsNullOrEmpty(media.EntityName))
        {
            return EntityLink(media.EntityName, entityNames);
        }

        var schema = media.Schema;
        if (schema is null) return "`unknown`";

        return FormatSchema(schema, entityNames);
    }

    private static string FormatSchema(ApiSchema schema, HashSet<string> entityNames)
    {
        if (schema.Type == "array" && schema.Items is not null)
        {
            return $"array&lt;{FormatSchema(schema.Items, entityNames)}&gt;";
        }
        if (!string.IsNullOrEmpty(schema.EntityName))
        {
            return EntityLink(schema.EntityName, entityNames);
        }
        return string.IsNullOrEmpty(schema.Format) ? $"`{schema.Type}`" : $"`{schema.Type}({schema.Format})`";
    }

    /// <summary>
    /// Renders a reference to a named entity. When the name matches a known
    /// <see cref="SemanticEntity"/>, emit a Markdown link to its anchor inside
    /// <c>domain-model.md</c>; otherwise fall back to an inline code span.
    /// </summary>
    private static string EntityLink(string name, HashSet<string> entityNames)
    {
        if (entityNames.Contains(name))
        {
            return $"[`{name}`](../domain-model.md#entity-{Slug.Kebab(name)})";
        }
        return $"`{name}`";
    }
}
