using System.Text;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Diagrams.Mermaid;

/// <summary>
/// Produces a Mermaid <c>sequenceDiagram</c> for a single <see cref="ApiOperation"/>.
///
/// Participants: <c>Client</c>, <c>API</c>, and — when the operation declares security
/// requirements — <c>Auth</c>. Messages include the HTTP method and path, the first request
/// body media type (when applicable), and the first successful (2xx) response.
/// </summary>
public sealed class MermaidSequenceDiagramGenerator
{
    public string Generate(ApiOperation operation)
    {
        var sb = new StringBuilder();
        sb.Append("sequenceDiagram\n");
        sb.Append("    participant Client\n");
        sb.Append("    participant API\n");

        var requiresAuth = operation.SecurityRequirements.Count > 0;
        if (requiresAuth)
        {
            sb.Append("    participant Auth\n");
            sb.Append("    Client->>Auth: authenticate\n");
            sb.Append("    Auth-->>Client: token\n");
        }

        var requestMessage = BuildRequestMessage(operation);
        sb.Append($"    Client->>API: {requestMessage}\n");

        var responseMessage = BuildResponseMessage(operation);
        sb.Append($"    API-->>Client: {responseMessage}\n");

        return sb.ToString();
    }

    private static string BuildRequestMessage(ApiOperation operation)
    {
        var method = operation.Method.ToString().ToUpperInvariant();
        var line = $"{method} {operation.Path}";

        var payloadType = DescribePayload(operation.RequestBody?.Content);
        if (payloadType is not null)
        {
            line += $" ({payloadType})";
        }

        return Sanitize(line);
    }

    private static string BuildResponseMessage(ApiOperation operation)
    {
        // Prefer the first successful (2xx) response, falling back to the first listed.
        var (status, response) = operation.Responses
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .FirstOrDefault(r => r.Key.StartsWith("2", StringComparison.Ordinal));

        if (response is null && operation.Responses.Count > 0)
        {
            var first = operation.Responses
                .OrderBy(r => r.Key, StringComparer.Ordinal)
                .First();
            status = first.Key;
            response = first.Value;
        }

        if (response is null)
        {
            return "response";
        }

        var schema = DescribePayload(response.Content);
        return schema is null
            ? Sanitize(status)
            : Sanitize($"{status} {schema}");
    }

    private static string? DescribePayload(IReadOnlyDictionary<string, ApiMediaType>? content)
    {
        if (content is null || content.Count == 0) return null;

        var first = content
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .First()
            .Value;

        if (!string.IsNullOrEmpty(first.EntityName)) return first.EntityName;

        var schema = first.Schema;
        if (schema is null) return null;

        if (schema.Type == "array" && schema.Items is not null)
        {
            var itemName = !string.IsNullOrEmpty(schema.Items.EntityName)
                ? schema.Items.EntityName
                : schema.Items.Type;
            return $"array<{itemName}>";
        }

        return !string.IsNullOrEmpty(schema.EntityName) ? schema.EntityName : schema.Type;
    }

    private static string Sanitize(string value) =>
        value.Replace('\n', ' ').Replace('\r', ' ').Trim();
}
