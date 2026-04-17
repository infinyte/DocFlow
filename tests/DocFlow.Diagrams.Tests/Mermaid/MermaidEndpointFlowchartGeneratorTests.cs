using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using Xunit;

namespace DocFlow.Diagrams.Tests.Mermaid;

public class MermaidEndpointFlowchartGeneratorTests
{
    private readonly MermaidEndpointFlowchartGenerator _generator = new();

    [Fact]
    public void EndpointFlow_WithMultipleResponses_BranchesForEachStatus()
    {
        var operation = new ApiOperation
        {
            OperationId = "getPetById",
            Method = ApiHttpMethod.Get,
            Path = "/pets/{id}",
            Responses = new Dictionary<string, ApiResponse>
            {
                ["200"] = new()
                {
                    Description = "ok",
                    Content = new Dictionary<string, ApiMediaType>
                    {
                        ["application/json"] = new() { EntityName = "Pet" }
                    }
                },
                ["404"] = new() { Description = "not found" },
                ["500"] = new() { Description = "server error" }
            }
        };

        var output = _generator.Generate(operation);

        Assert.StartsWith("flowchart LR", output);
        Assert.Contains("Response200", output);
        Assert.Contains("Response404", output);
        Assert.Contains("Response500", output);
        // Success path is solid, non-2xx are dashed.
        Assert.Contains("Handler --> Response200", output);
        Assert.Contains("Handler -.-> Response404", output);
        Assert.Contains("Handler -.-> Response500", output);
    }

    [Fact]
    public void EndpointFlow_WithoutSecurity_OmitsAuthorizeNode()
    {
        var operation = new ApiOperation
        {
            OperationId = "listPets",
            Method = ApiHttpMethod.Get,
            Path = "/pets",
            Responses = new Dictionary<string, ApiResponse>
            {
                ["200"] = new() { Description = "ok" }
            }
        };

        var output = _generator.Generate(operation);

        Assert.DoesNotContain("Authorize", output);
        // Request flows directly: Request -> Validate -> Handler.
        Assert.Contains("Request --> Validate", output);
        Assert.Contains("Validate --> Handler", output);
    }

    [Fact]
    public void EndpointFlow_WithSecurity_IncludesAuthorizeNode()
    {
        var operation = new ApiOperation
        {
            OperationId = "deletePet",
            Method = ApiHttpMethod.Delete,
            Path = "/pets/{id}",
            SecurityRequirements =
            [
                new ApiSecurityRequirement
                {
                    Schemes = new Dictionary<string, IReadOnlyList<string>> { ["oauth2"] = ["write"] }
                }
            ],
            Responses = new Dictionary<string, ApiResponse>
            {
                ["204"] = new() { Description = "deleted" }
            }
        };

        var output = _generator.Generate(operation);

        Assert.Contains("Authorize[\"Authorize\"]", output);
        Assert.Contains("Validate --> Authorize", output);
        Assert.Contains("Authorize --> Handler", output);
    }

    [Fact]
    public void EndpointFlow_IncludesResponseBodySummary()
    {
        var operation = new ApiOperation
        {
            OperationId = "getPetById",
            Method = ApiHttpMethod.Get,
            Path = "/pets/{id}",
            Responses = new Dictionary<string, ApiResponse>
            {
                ["200"] = new()
                {
                    Description = "ok",
                    Content = new Dictionary<string, ApiMediaType>
                    {
                        ["application/json"] = new() { EntityName = "Pet" }
                    }
                }
            }
        };

        var output = _generator.Generate(operation);

        Assert.Contains("200: Pet", output);
    }
}
