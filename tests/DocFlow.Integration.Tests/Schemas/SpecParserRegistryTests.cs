using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Integration.Schemas.OpenApi;
using Xunit;

namespace DocFlow.Integration.Tests.Schemas;

public class SpecParserRegistryTests
{
    [Fact]
    public void Registry_SelectsOpenApiParser_ForJsonSpec()
    {
        var registry = new SpecParserRegistry([new OpenApiParser()]);

        var parser = registry.Select("petstore.json", content: null);

        Assert.Equal("OpenAPI", parser.Name);
    }

    [Fact]
    public void Registry_SelectsOpenApiParser_ForYamlSpec()
    {
        var registry = new SpecParserRegistry([new OpenApiParser()]);

        var a = registry.Select("petstore.yaml", content: null);
        var b = registry.Select("petstore.yml", content: null);

        Assert.Equal("OpenAPI", a.Name);
        Assert.Equal("OpenAPI", b.Name);
    }

    [Fact]
    public void Registry_SelectsOpenApiParser_ByContentSniff()
    {
        var registry = new SpecParserRegistry([new OpenApiParser()]);

        // No path, but content sniff detects the openapi marker.
        var parser = registry.Select(path: null, content: "{\"openapi\": \"3.0.3\", ...}");

        Assert.Equal("OpenAPI", parser.Name);
    }

    [Fact]
    public void Registry_NoMatchingParser_ThrowsWithHelpfulMessage()
    {
        var registry = new SpecParserRegistry([new OpenApiParser()]);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            registry.Select("schema.graphql", content: "type Query { hello: String }"));

        Assert.Contains("schema.graphql", ex.Message);
        Assert.Contains("OpenAPI", ex.Message);
    }

    [Fact]
    public void Registry_PicksStubParser_WhenItMatches()
    {
        // Adding a new parser is demonstrably a one-file change:
        // register a stub that claims `.graphql` files and verify the registry picks it up.
        var registry = new SpecParserRegistry(new IApiSpecParser[]
        {
            new OpenApiParser(),
            new StubGraphQlParser()
        });

        var parser = registry.Select("schema.graphql", content: null);
        Assert.Equal("GraphQL", parser.Name);
    }

    [Fact]
    public async Task StubParser_ParseAsync_ThrowsNotImplemented()
    {
        // The stub is not yet implemented — confirms NotImplementedException is surfaced by
        // the parser's own ParseAsync (not swallowed by registry selection).
        IApiSpecParser parser = new StubGraphQlParser();
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<NotImplementedException>(() => parser.ParseAsync(stream));
    }

    private sealed class StubGraphQlParser : IApiSpecParser
    {
        public string Name => "GraphQL";

        public bool CanParse(string? path, string? content) =>
            !string.IsNullOrEmpty(path) && path.EndsWith(".graphql", StringComparison.OrdinalIgnoreCase);

        public Task<SemanticModel> ParseAsync(Stream input, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException("GraphQL parser not yet implemented.");
    }
}
