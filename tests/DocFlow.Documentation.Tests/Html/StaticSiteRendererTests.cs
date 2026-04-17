using DocFlow.Core.Abstractions;
using DocFlow.Documentation.Html;
using DocFlow.Documentation.Markdown;
using DocFlow.Documentation.Options;
using DocFlow.Integration.Schemas.OpenApi;
using Xunit;

namespace DocFlow.Documentation.Tests.Html;

public class StaticSiteRendererTests
{
    private static async Task<IReadOnlyList<DocFlow.Documentation.Models.GeneratedFile>> GeneratePetstoreHtmlAsync()
    {
        var parser = new OpenApiParser();
        var result = await parser.ParseAsync(ParserInput.FromFile("Fixtures/petstore.json"));
        Assert.True(result.Success);

        var markdown = await new MarkdownDocumentationGenerator().GenerateAsync(result.Model, new DocumentationOptions());
        return new StaticSiteRenderer().Render(markdown);
    }

    [Fact]
    public async Task Html_Petstore_ProducesParallelHtmlFiles()
    {
        var files = await GeneratePetstoreHtmlAsync();
        var byPath = files.ToDictionary(f => f.RelativePath, f => f, StringComparer.Ordinal);

        // Every Markdown file has a corresponding .html.
        string[] expectedHtml =
        [
            "index.html",
            "overview.html",
            "domain-model.html",
            "endpoints/pet.html",
            "endpoints/store.html",
            "security.html"
        ];

        foreach (var path in expectedHtml)
        {
            Assert.True(byPath.ContainsKey(path), $"Expected {path} in bundle.");
            Assert.Equal("text/html", byPath[path].MediaType);
        }

        // CSS asset is present.
        Assert.True(byPath.ContainsKey("assets/theme.css"));
        Assert.Equal("text/css", byPath["assets/theme.css"].MediaType);
    }

    [Fact]
    public async Task Html_EmbedsMermaidScript()
    {
        var files = await GeneratePetstoreHtmlAsync();

        foreach (var file in files.Where(f => f.RelativePath.EndsWith(".html", StringComparison.Ordinal)))
        {
            Assert.Contains("mermaid", file.Content);
            Assert.Contains("<script src=\"https://cdn.jsdelivr.net/npm/mermaid", file.Content);
        }
    }

    [Fact]
    public async Task Html_ConvertsMdLinksToHtml()
    {
        var files = await GeneratePetstoreHtmlAsync();
        var petPage = files.Single(f => f.RelativePath == "endpoints/pet.html").Content;

        // Original Markdown had href="../domain-model.md#entity-pet".
        Assert.Contains("../domain-model.html#entity-pet", petPage);
        // And no stale .md href should leak through (the anchor preservation test above also
        // exercises the fragment-preserving branch).
        Assert.DoesNotContain("href=\"../domain-model.md", petPage);
    }

    [Fact]
    public async Task Html_Sidebar_ListsAllPages()
    {
        var files = await GeneratePetstoreHtmlAsync();
        var index = files.Single(f => f.RelativePath == "index.html").Content;

        Assert.Contains("<aside class=\"sidebar\">", index);
        Assert.Contains("overview.html", index);
        Assert.Contains("domain-model.html", index);
        Assert.Contains("Endpoints", index);
        Assert.Contains("endpoints/pet.html", index);
        Assert.Contains("endpoints/store.html", index);
        // Current page is highlighted.
        Assert.Contains("class=\"active\"", index);
    }

    [Fact]
    public async Task Html_RendersMermaidFencesForMermaidJs()
    {
        var files = await GeneratePetstoreHtmlAsync();
        var domainModel = files.Single(f => f.RelativePath == "domain-model.html").Content;

        // Markdig's advanced-diagrams extension emits <div class="mermaid">; Mermaid.js
        // auto-initialises elements with that class.
        Assert.Contains("class=\"mermaid\"", domainModel);
        Assert.Contains("classDiagram", domainModel);
        // The raw Markdig code-block wrapper should not leak through.
        Assert.DoesNotContain("<code class=\"language-mermaid\">", domainModel);
    }

    [Fact(Skip = "Tracked: offline Mermaid asset packaging not yet implemented.")]
    public void Html_IsSelfContained_WhenOfflineFlagSet()
    {
        // Reserved for when --offline is implemented and Mermaid ships as a bundled asset.
    }
}
