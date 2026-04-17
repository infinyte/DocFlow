using System.Text;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Diagrams.Mermaid;

/// <summary>
/// Produces an architecture "context" diagram for an <see cref="ApiSurface"/> using a
/// <c>flowchart LR</c> base (the Mermaid <c>C4Context</c> primitive is still experimental at
/// the time of writing, so we fall back to flowchart-based C4-style grouping).
///
/// Nodes:
/// <list type="bullet">
/// <item><description>a single <c>Client</c> actor on the left</description></item>
/// <item><description>the API as one container (labeled with <see cref="ApiSurface.Title"/>)</description></item>
/// <item><description>each <see cref="ApiSurface.Servers"/> entry as a deployment node</description></item>
/// <item><description>each OAuth2 security scheme as an external identity provider node</description></item>
/// </list>
/// Output is deterministic: every collection is ordered alphabetically before rendering.
/// </summary>
public sealed class MermaidC4ContextGenerator
{
    public string Generate(ApiSurface? api)
    {
        var sb = new StringBuilder();
        sb.Append("flowchart LR\n");

        var apiLabel = string.IsNullOrWhiteSpace(api?.Title) ? "API" : api.Title;
        sb.Append("    Client([\"Client\"])\n");
        sb.Append($"    API[[\"{EscapeLabel(apiLabel)}\"]]\n");
        sb.Append("    Client --> API\n");

        if (api is null)
        {
            return sb.ToString();
        }

        var servers = api.Servers
            .OrderBy(s => s.Url, StringComparer.Ordinal)
            .ToList();
        for (var i = 0; i < servers.Count; i++)
        {
            var nodeId = $"Server{i + 1}";
            var label = string.IsNullOrWhiteSpace(servers[i].Description)
                ? servers[i].Url
                : $"{servers[i].Description}: {servers[i].Url}";
            sb.Append($"    {nodeId}[(\"{EscapeLabel(label)}\")]\n");
            sb.Append($"    API --> {nodeId}\n");
        }

        var oauthSchemes = api.SecuritySchemes
            .Where(kvp => kvp.Value.Type == ApiSecuritySchemeType.OAuth2
                          || kvp.Value.Type == ApiSecuritySchemeType.OpenIdConnect)
            .OrderBy(kvp => kvp.Key, StringComparer.Ordinal)
            .ToList();

        for (var i = 0; i < oauthSchemes.Count; i++)
        {
            var (name, scheme) = (oauthSchemes[i].Key, oauthSchemes[i].Value);
            var nodeId = $"Idp{i + 1}";
            var url = scheme.Flows
                .Select(f => f.Value.AuthorizationUrl ?? f.Value.TokenUrl)
                .FirstOrDefault(u => !string.IsNullOrEmpty(u))
                ?? scheme.OpenIdConnectUrl
                ?? string.Empty;
            var label = string.IsNullOrEmpty(url) ? $"IdP: {name}" : $"IdP: {name} ({url})";

            sb.Append($"    {nodeId}{{{{\"{EscapeLabel(label)}\"}}}}\n");
            sb.Append($"    Client --> {nodeId}\n");
            sb.Append($"    API --> {nodeId}\n");
        }

        return sb.ToString();
    }

    private static string EscapeLabel(string value) =>
        value.Replace('"', '\'').Replace('\n', ' ').Replace('\r', ' ').Trim();
}
