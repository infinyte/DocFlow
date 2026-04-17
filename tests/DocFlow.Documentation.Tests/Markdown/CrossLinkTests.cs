using DocFlow.Core.Abstractions;
using DocFlow.Documentation.Markdown;
using DocFlow.Documentation.Options;
using DocFlow.Integration.Schemas.OpenApi;
using Xunit;

namespace DocFlow.Documentation.Tests.Markdown;

public class CrossLinkTests
{
    [Fact]
    public async Task CrossLinks_EntityReferencesResolve()
    {
        var parser = new OpenApiParser();
        var result = await parser.ParseAsync(ParserInput.FromFile("Fixtures/petstore.json"));
        Assert.True(result.Success);

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(result.Model, new DocumentationOptions());
        var byPath = files.ToDictionary(f => f.RelativePath, f => f.Content, StringComparer.Ordinal);

        // Pet entity anchor appears on domain-model.md.
        Assert.Contains("id=\"entity-pet\"", byPath["domain-model.md"]);

        // endpoints/pet.md references Pet via a relative link into domain-model.md.
        var petPage = byPath["endpoints/pet.md"];
        Assert.Contains("[`Pet`](../domain-model.md#entity-pet)", petPage);

        // The plain backticked form should NOT appear anywhere that a link was expected
        // (excluding the page heading, which uses the HTTP method — not the entity name).
        var linkIndex = petPage.IndexOf("[`Pet`]", StringComparison.Ordinal);
        Assert.True(linkIndex > 0, "Pet reference should be emitted as a Markdown link, not a bare code span.");
    }
}
