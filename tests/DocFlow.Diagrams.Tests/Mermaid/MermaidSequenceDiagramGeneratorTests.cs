using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using Xunit;

namespace DocFlow.Diagrams.Tests.Mermaid;

public class MermaidSequenceDiagramGeneratorTests
{
    private readonly MermaidSequenceDiagramGenerator _generator = new();

    [Fact]
    public void Sequence_GetOperation_ProducesClientApiMessages()
    {
        var operation = new ApiOperation
        {
            OperationId = "listPets",
            Method = ApiHttpMethod.Get,
            Path = "/pets",
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

        Assert.StartsWith("sequenceDiagram", output);
        Assert.Contains("participant Client", output);
        Assert.Contains("participant API", output);
        Assert.Contains("Client->>API: GET /pets", output);
        Assert.Contains("API-->>Client: 200 Pet", output);
    }

    [Fact]
    public void Sequence_SecuredOperation_IncludesAuthActor()
    {
        var operation = new ApiOperation
        {
            OperationId = "getSecret",
            Method = ApiHttpMethod.Get,
            Path = "/secret",
            SecurityRequirements =
            [
                new ApiSecurityRequirement
                {
                    Schemes = new Dictionary<string, IReadOnlyList<string>> { ["oauth2"] = ["read"] }
                }
            ],
            Responses = new Dictionary<string, ApiResponse>
            {
                ["200"] = new() { Description = "ok" }
            }
        };

        var output = _generator.Generate(operation);

        Assert.Contains("participant Auth", output);
        Assert.Contains("Client->>Auth: authenticate", output);
        Assert.Contains("Auth-->>Client: token", output);
    }

    [Fact]
    public void Sequence_UnsecuredOperation_OmitsAuthActor()
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

        Assert.DoesNotContain("participant Auth", output);
        Assert.DoesNotContain("authenticate", output);
    }

    [Fact]
    public void Sequence_OperationWithRequestBody_IncludesPayloadType()
    {
        var operation = new ApiOperation
        {
            OperationId = "createPet",
            Method = ApiHttpMethod.Post,
            Path = "/pets",
            RequestBody = new ApiRequestBody
            {
                Required = true,
                Content = new Dictionary<string, ApiMediaType>
                {
                    ["application/json"] = new() { EntityName = "Pet" }
                }
            },
            Responses = new Dictionary<string, ApiResponse>
            {
                ["201"] = new()
                {
                    Description = "created",
                    Content = new Dictionary<string, ApiMediaType>
                    {
                        ["application/json"] = new() { EntityName = "Pet" }
                    }
                }
            }
        };

        var output = _generator.Generate(operation);

        Assert.Contains("Client->>API: POST /pets (Pet)", output);
        Assert.Contains("API-->>Client: 201 Pet", output);
    }

    [Fact]
    public void Sequence_PrefersSuccessfulResponseOverOtherStatuses()
    {
        var operation = new ApiOperation
        {
            OperationId = "getPetById",
            Method = ApiHttpMethod.Get,
            Path = "/pets/{id}",
            Responses = new Dictionary<string, ApiResponse>
            {
                ["404"] = new() { Description = "not found" },
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

        Assert.Contains("API-->>Client: 200 Pet", output);
        Assert.DoesNotContain("API-->>Client: 404", output);
    }
}
