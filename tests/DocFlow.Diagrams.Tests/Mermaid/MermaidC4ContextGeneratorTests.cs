using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using Xunit;

namespace DocFlow.Diagrams.Tests.Mermaid;

public class MermaidC4ContextGeneratorTests
{
    private readonly MermaidC4ContextGenerator _generator = new();

    [Fact]
    public void Context_NoServers_ProducesSingleContainer()
    {
        var api = new ApiSurface { Title = "Minimal", Version = "1.0" };

        var output = _generator.Generate(api);

        Assert.StartsWith("flowchart LR", output);
        Assert.Contains("Client", output);
        Assert.Contains("API[[\"Minimal\"]]", output);
        Assert.Contains("Client --> API", output);
        // No server nodes emitted.
        Assert.DoesNotContain("Server1", output);
    }

    [Fact]
    public void Context_WithOAuth_AddsIdpActor()
    {
        var api = new ApiSurface
        {
            Title = "Secured",
            Version = "1.0",
            SecuritySchemes = new Dictionary<string, ApiSecurityScheme>
            {
                ["oauth2"] = new()
                {
                    Name = "oauth2",
                    Type = ApiSecuritySchemeType.OAuth2,
                    Flows = new Dictionary<string, ApiSecurityFlow>
                    {
                        ["authorizationCode"] = new()
                        {
                            AuthorizationUrl = "https://auth.example/auth",
                            TokenUrl = "https://auth.example/token"
                        }
                    }
                }
            }
        };

        var output = _generator.Generate(api);

        Assert.Contains("Idp1", output);
        Assert.Contains("IdP: oauth2", output);
        Assert.Contains("https://auth.example/auth", output);
        Assert.Contains("Client --> Idp1", output);
    }

    [Fact]
    public void Context_Deterministic_OrderingByName()
    {
        ApiSurface Build(params (string url, string desc)[] servers) => new()
        {
            Title = "Det",
            Version = "1.0",
            Servers = servers.Select(s => new ApiServer { Url = s.url, Description = s.desc }).ToList()
        };

        var a = Build(
            ("https://api-z.example", "Z"),
            ("https://api-a.example", "A"),
            ("https://api-m.example", "M"));

        var b = Build(
            ("https://api-m.example", "M"),
            ("https://api-a.example", "A"),
            ("https://api-z.example", "Z"));

        Assert.Equal(_generator.Generate(a), _generator.Generate(b));

        var output = _generator.Generate(a);
        var aIdx = output.IndexOf("api-a", StringComparison.Ordinal);
        var mIdx = output.IndexOf("api-m", StringComparison.Ordinal);
        var zIdx = output.IndexOf("api-z", StringComparison.Ordinal);
        Assert.True(aIdx < mIdx && mIdx < zIdx, "Servers should appear in alphabetical URL order.");
    }

    [Fact]
    public void Context_NullApi_ProducesMinimalDiagram()
    {
        var output = _generator.Generate(null);

        Assert.StartsWith("flowchart LR", output);
        Assert.Contains("Client", output);
        Assert.Contains("API[[\"API\"]]", output);
    }
}
