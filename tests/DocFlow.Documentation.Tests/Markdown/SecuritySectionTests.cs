using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Markdown;
using DocFlow.Documentation.Options;
using Xunit;

namespace DocFlow.Documentation.Tests.Markdown;

public class SecuritySectionTests
{
    [Fact]
    public async Task Security_OAuth2AuthorizationCode_ProducesSequenceDiagram()
    {
        var model = new SemanticModel
        {
            Api = new ApiSurface
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
                                TokenUrl = "https://auth.example/token",
                                Scopes = new Dictionary<string, string>
                                {
                                    ["read"] = "Read access",
                                    ["write"] = "Write access"
                                }
                            }
                        }
                    }
                }
            }
        };

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());
        var security = files.Single(f => f.RelativePath == "security.md").Content;

        Assert.Contains("sequenceDiagram", security);
        Assert.Contains("authorize", security); // authorization_code flow step
        Assert.Contains("authorization code", security);
        Assert.Contains("access token", security);
        // Scopes enumerated.
        Assert.Contains("`read`", security);
        Assert.Contains("`write`", security);
    }

    [Fact]
    public async Task Security_NoSchemes_ProducesMinimalPage()
    {
        var model = new SemanticModel
        {
            Api = new ApiSurface { Title = "Open", Version = "1.0" }
        };

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());

        // When no schemes are declared and no operation requires security, security.md is skipped.
        Assert.DoesNotContain(files, f => f.RelativePath == "security.md");
    }

    [Fact]
    public async Task Security_ApiKeyScheme_ProducesSecurityPageWithoutFlowDiagrams()
    {
        var model = new SemanticModel
        {
            Api = new ApiSurface
            {
                Title = "Keyed",
                Version = "1.0",
                SecuritySchemes = new Dictionary<string, ApiSecurityScheme>
                {
                    ["api_key"] = new()
                    {
                        Name = "api_key",
                        Type = ApiSecuritySchemeType.ApiKey,
                        In = ApiParameterLocation.Header,
                        ParameterName = "X-API-Key"
                    }
                }
            }
        };

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());
        var security = files.Single(f => f.RelativePath == "security.md").Content;

        Assert.Contains("api_key", security);
        Assert.Contains("X-API-Key", security);
        // ApiKey schemes don't get a flow diagram.
        Assert.DoesNotContain("OAuth2 Flows", security);
    }
}
