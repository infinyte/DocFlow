using System.Text;
using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown.Sections;

/// <summary>
/// Emits <c>security.md</c> — a scheme table, a Mermaid sequence diagram per OAuth2 flow, and a
/// cross-reference linking each operation to the schemes it requires.
/// Produces an empty list when the model declares no security schemes and no operation declares
/// security requirements.
/// </summary>
internal static class SecuritySectionBuilder
{
    public static IEnumerable<GeneratedFile> Build(SemanticModel model, DocumentationOptions options)
    {
        var api = model.Api;
        if (api is null) return [];

        if (api.SecuritySchemes.Count == 0 && api.Operations.All(o => o.SecurityRequirements.Count == 0))
        {
            return [];
        }

        var writer = new MarkdownWriter();
        writer.Heading(1, "Security");
        writer.Line();

        WriteSchemeTable(writer, api);
        WriteOAuthFlowDiagrams(writer, api);
        WritePerOperationTable(writer, api);

        return [new GeneratedFile("security.md", writer.ToString(), "text/markdown")];
    }

    private static void WriteSchemeTable(MarkdownWriter writer, ApiSurface api)
    {
        writer.Heading(2, "Schemes");
        writer.Line();

        if (api.SecuritySchemes.Count == 0)
        {
            writer.Line("_No security schemes declared, but some operations reference unknown schemes._");
            writer.Line();
            return;
        }

        writer.Line("| Scheme | Type | Details |");
        writer.Line("| --- | --- | --- |");
        foreach (var kvp in api.SecuritySchemes.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            writer.Line($"| `{kvp.Key}` | {kvp.Value.Type} | {DescribeScheme(kvp.Value)} |");
        }
        writer.Line();
    }

    private static void WriteOAuthFlowDiagrams(MarkdownWriter writer, ApiSurface api)
    {
        var oauth = api.SecuritySchemes
            .Where(kvp => kvp.Value.Type == ApiSecuritySchemeType.OAuth2 && kvp.Value.Flows.Count > 0)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        if (oauth.Count == 0) return;

        writer.Heading(2, "OAuth2 Flows");
        writer.Line();

        foreach (var (name, scheme) in oauth)
        {
            foreach (var (flowName, flow) in scheme.Flows.OrderBy(f => f.Key, StringComparer.Ordinal))
            {
                writer.Heading(3, $"`{name}` — {flowName}");
                writer.Line();
                writer.Line("```mermaid");
                writer.Raw(RenderFlowSequence(flowName, flow));
                writer.Line("```");
                writer.Line();

                if (flow.Scopes.Count > 0)
                {
                    writer.Line("**Scopes:**");
                    writer.Line();
                    foreach (var scope in flow.Scopes.OrderBy(s => s.Key, StringComparer.Ordinal))
                    {
                        writer.Line($"- `{scope.Key}` — {scope.Value}");
                    }
                    writer.Line();
                }
            }
        }
    }

    private static void WritePerOperationTable(MarkdownWriter writer, ApiSurface api)
    {
        var secured = api.Operations
            .Where(o => o.SecurityRequirements.Count > 0)
            .OrderBy(o => o.OperationId, StringComparer.Ordinal)
            .ToList();

        if (secured.Count == 0) return;

        writer.Heading(2, "Per-operation requirements");
        writer.Line();
        writer.Line("| Operation | Method | Path | Schemes (scopes) |");
        writer.Line("| --- | --- | --- | --- |");

        foreach (var op in secured)
        {
            var schemes = string.Join("; ", op.SecurityRequirements.Select(req =>
                string.Join(" + ", req.Schemes
                    .OrderBy(k => k.Key, StringComparer.Ordinal)
                    .Select(k => k.Value.Count == 0 ? k.Key : $"{k.Key} ({string.Join(", ", k.Value)})"))));

            writer.Line($"| `{op.OperationId}` | {op.Method.ToString().ToUpperInvariant()} | `{op.Path}` | {schemes} |");
        }
        writer.Line();
    }

    private static string DescribeScheme(ApiSecurityScheme scheme) => scheme.Type switch
    {
        ApiSecuritySchemeType.ApiKey => $"in {scheme.In?.ToString().ToLowerInvariant() ?? "?"} as `{scheme.ParameterName ?? "?"}`",
        ApiSecuritySchemeType.Http => $"scheme `{scheme.Scheme ?? "?"}`" + (string.IsNullOrEmpty(scheme.BearerFormat) ? "" : $", format `{scheme.BearerFormat}`"),
        ApiSecuritySchemeType.OAuth2 => $"{scheme.Flows.Count} flow(s): {string.Join(", ", scheme.Flows.Keys.OrderBy(k => k, StringComparer.Ordinal))}",
        ApiSecuritySchemeType.OpenIdConnect => $"discovery: {scheme.OpenIdConnectUrl ?? "?"}",
        _ => scheme.Description ?? ""
    };

    private static string RenderFlowSequence(string flowName, ApiSecurityFlow flow)
    {
        var sb = new StringBuilder();
        sb.Append("sequenceDiagram\n");
        sb.Append("    participant Client\n");
        sb.Append("    participant Auth\n");
        sb.Append("    participant API\n");

        switch (flowName)
        {
            case "authorizationCode":
                sb.Append("    Client->>Auth: GET authorize\n");
                sb.Append("    Auth-->>Client: authorization code\n");
                sb.Append("    Client->>Auth: POST token (code)\n");
                sb.Append("    Auth-->>Client: access token\n");
                sb.Append("    Client->>API: request (bearer)\n");
                sb.Append("    API-->>Client: response\n");
                break;
            case "implicit":
                sb.Append("    Client->>Auth: GET authorize\n");
                sb.Append("    Auth-->>Client: access token (fragment)\n");
                sb.Append("    Client->>API: request (bearer)\n");
                sb.Append("    API-->>Client: response\n");
                break;
            case "clientCredentials":
                sb.Append("    Client->>Auth: POST token (client_credentials)\n");
                sb.Append("    Auth-->>Client: access token\n");
                sb.Append("    Client->>API: request (bearer)\n");
                sb.Append("    API-->>Client: response\n");
                break;
            case "password":
                sb.Append("    Client->>Auth: POST token (password grant)\n");
                sb.Append("    Auth-->>Client: access token\n");
                sb.Append("    Client->>API: request (bearer)\n");
                sb.Append("    API-->>Client: response\n");
                break;
            default:
                sb.Append("    Client->>Auth: acquire token\n");
                sb.Append("    Auth-->>Client: access token\n");
                sb.Append("    Client->>API: request (bearer)\n");
                sb.Append("    API-->>Client: response\n");
                break;
        }

        _ = flow; // flow-specific URLs are shown in the scheme table, not the diagram.
        return sb.ToString();
    }
}
