using System.Text;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Diagrams.Mermaid;

/// <summary>
/// Produces a per-operation request-lifecycle flowchart:
/// <c>Request → Validate Params → [Authorize] → Handler → Response (2xx)</c> with branches
/// from Handler to each non-2xx response declared on the operation.
/// The Authorize node is omitted when the operation has no security requirements.
/// </summary>
public sealed class MermaidEndpointFlowchartGenerator
{
    public string Generate(ApiOperation operation)
    {
        var sb = new StringBuilder();
        sb.Append("flowchart LR\n");

        sb.Append("    Request[\"Request\"]\n");
        sb.Append("    Validate[\"Validate Params\"]\n");
        sb.Append("    Handler[\"Handler\"]\n");

        var requiresAuth = operation.SecurityRequirements.Count > 0;
        if (requiresAuth)
        {
            sb.Append("    Authorize[\"Authorize\"]\n");
        }

        // Partition responses into success (2xx) and others.
        var orderedResponses = operation.Responses
            .OrderBy(r => r.Key, StringComparer.Ordinal)
            .ToList();

        var successResponses = orderedResponses
            .Where(r => r.Key.StartsWith("2", StringComparison.Ordinal))
            .ToList();
        var otherResponses = orderedResponses
            .Where(r => !r.Key.StartsWith("2", StringComparison.Ordinal))
            .ToList();

        // Emit terminal response nodes.
        foreach (var (status, response) in orderedResponses)
        {
            var label = $"{status}: {Summarize(response)}";
            sb.Append($"    Response{status}[\"{EscapeLabel(label)}\"]\n");
        }

        // Wire up the happy path.
        sb.Append("    Request --> Validate\n");
        var handlerPredecessor = requiresAuth ? "Authorize" : "Validate";
        if (requiresAuth)
        {
            sb.Append("    Validate --> Authorize\n");
        }
        sb.Append($"    {handlerPredecessor} --> Handler\n");

        foreach (var (status, _) in successResponses)
        {
            sb.Append($"    Handler --> Response{status}\n");
        }

        // Branches to non-2xx responses (dashed to distinguish from the happy path).
        foreach (var (status, _) in otherResponses)
        {
            sb.Append($"    Handler -.-> Response{status}\n");
        }

        return sb.ToString();
    }

    private static string Summarize(ApiResponse response)
    {
        if (response.Content.Count == 0)
        {
            return string.IsNullOrWhiteSpace(response.Description) ? "no body" : response.Description;
        }

        var first = response.Content
            .OrderBy(c => c.Key, StringComparer.Ordinal)
            .First()
            .Value;

        if (!string.IsNullOrEmpty(first.EntityName)) return first.EntityName;

        var schema = first.Schema;
        if (schema is null) return response.Description ?? string.Empty;

        if (schema.Type == "array" && schema.Items is not null)
        {
            var inner = !string.IsNullOrEmpty(schema.Items.EntityName)
                ? schema.Items.EntityName
                : schema.Items.Type;
            return $"array<{inner}>";
        }

        return !string.IsNullOrEmpty(schema.EntityName) ? schema.EntityName : schema.Type;
    }

    private static string EscapeLabel(string value) =>
        value.Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' ').Trim();
}
