using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Markdown;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;
using DocFlow.Integration.Schemas.OpenApi;
using VerifyXunit;
using Xunit;

namespace DocFlow.Documentation.Tests.Markdown;

public class MarkdownDocumentationGeneratorTests
{
    private const string PetstorePath = "Fixtures/petstore.json";

    private static async Task<SemanticModel> ParsePetstoreAsync()
    {
        var parser = new OpenApiParser();
        var result = await parser.ParseAsync(ParserInput.FromFile(PetstorePath));
        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        return result.Model;
    }

    [Fact]
    public async Task Generate_Petstore_ProducesExpectedFileSet()
    {
        var model = await ParsePetstoreAsync();
        var generator = new MarkdownDocumentationGenerator();

        var files = await generator.GenerateAsync(model, new DocumentationOptions());

        var paths = files.Select(f => f.RelativePath).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "index.md",
                "overview.md",
                "domain-model.md",
                "endpoints/pet.md",
                "endpoints/store.md",
                "security.md"  // Petstore declares an `api_key` security scheme.
            },
            paths);
    }

    [Fact]
    public async Task Generate_DomainModel_EmbedsMermaidClassDiagram()
    {
        var model = await ParsePetstoreAsync();
        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());

        var domainModel = files.Single(f => f.RelativePath == "domain-model.md");

        Assert.Contains("```mermaid\nclassDiagram", domainModel.Content);
        Assert.Contains("```", domainModel.Content);
    }

    [Fact]
    public async Task Generate_EndpointPage_ContainsAllOperationsForTag()
    {
        var model = await ParsePetstoreAsync();
        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());

        var petPage = files.Single(f => f.RelativePath == "endpoints/pet.md");

        // Every operation tagged "pet" in the sample appears on the pet endpoint page.
        var petOperations = model.Api!.Operations
            .Where(op => op.Tags.Contains("pet"))
            .Select(op => op.OperationId)
            .ToList();

        Assert.NotEmpty(petOperations);
        foreach (var opId in petOperations)
        {
            Assert.Contains(opId, petPage.Content);
        }

        // And the "store" operation does not leak into the pet page.
        Assert.DoesNotContain("placeOrder", petPage.Content);
    }

    [Fact]
    public async Task Generate_IsDeterministic()
    {
        var model = await ParsePetstoreAsync();
        var generator = new MarkdownDocumentationGenerator();

        var first = await generator.GenerateAsync(model, new DocumentationOptions());
        var second = await generator.GenerateAsync(model, new DocumentationOptions());

        Assert.Equal(first.Count, second.Count);
        for (var i = 0; i < first.Count; i++)
        {
            Assert.Equal(first[i].RelativePath, second[i].RelativePath);
            Assert.Equal(first[i].Content, second[i].Content);
        }

        // Enforce LF line endings for cross-platform reproducibility.
        foreach (var file in first)
        {
            Assert.DoesNotContain("\r", file.Content);
        }
    }

    [Fact]
    public async Task Generate_EmptyModel_ProducesIndexAndOverviewOnly()
    {
        var model = new SemanticModel { Name = "Empty" };
        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());

        var paths = files.Select(f => f.RelativePath).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("index.md", paths);
        Assert.Contains("overview.md", paths);
        Assert.Contains("domain-model.md", paths);
        Assert.DoesNotContain(paths, p => p.StartsWith("endpoints/"));
    }

    [Fact]
    public Task Generate_TitleOverride_AppearsInOverviewAndIndex()
    {
        return Verify_TitleOverride();
    }

    private async Task Verify_TitleOverride()
    {
        var model = await ParsePetstoreAsync();
        var files = await new MarkdownDocumentationGenerator().GenerateAsync(
            model,
            new DocumentationOptions { Title = "My Custom Title" });

        var overview = files.Single(f => f.RelativePath == "overview.md").Content;
        var index = files.Single(f => f.RelativePath == "index.md").Content;

        Assert.Contains("My Custom Title", overview);
        Assert.Contains("My Custom Title", index);
    }

    [Fact]
    public async Task Documentation_ProducesArchitectureMd()
    {
        var model = await ParsePetstoreAsync();
        var options = new DocumentationOptions { Diagrams = DiagramKinds.Class | DiagramKinds.Context };

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, options);
        var byPath = files.ToDictionary(f => f.RelativePath, f => f.Content, StringComparer.Ordinal);

        Assert.True(byPath.ContainsKey("architecture.md"));
        Assert.Contains("flowchart LR", byPath["architecture.md"]);
        Assert.Contains("Client", byPath["architecture.md"]);
        // Standalone .mmd asset.
        Assert.True(byPath.ContainsKey("diagrams/context.mmd"));
        Assert.Contains("flowchart LR", byPath["diagrams/context.mmd"]);
    }

    [Fact]
    public async Task Documentation_WithFlow_EmbedsFlowchartInEndpointPage()
    {
        var model = await ParsePetstoreAsync();
        var options = new DocumentationOptions { Diagrams = DiagramKinds.Class | DiagramKinds.Flow };

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, options);
        var petPage = files.Single(f => f.RelativePath == "endpoints/pet.md").Content;

        Assert.Contains("flowchart LR", petPage);
        Assert.Contains("Request", petPage);
        Assert.Contains("Handler", petPage);
    }

    [Fact]
    public async Task Documentation_WithErAndSequence_EmbedsBothInMarkdown()
    {
        var model = await ParsePetstoreAsync();
        var options = new DocumentationOptions
        {
            Diagrams = DiagramKinds.Class | DiagramKinds.Er | DiagramKinds.Sequence
        };

        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, options);
        var byPath = files.ToDictionary(f => f.RelativePath, f => f.Content, StringComparer.Ordinal);

        // Domain model contains both class and ER fences.
        var domainModel = byPath["domain-model.md"];
        Assert.Contains("```mermaid\nclassDiagram", domainModel);
        Assert.Contains("```mermaid\nerDiagram", domainModel);

        // endpoints/pet.md contains a sequence fence for createPet.
        var petPage = byPath["endpoints/pet.md"];
        Assert.Contains("sequenceDiagram", petPage);
        Assert.Contains("Client->>API: POST /pets", petPage);

        // A standalone sequence page exists per operation.
        Assert.True(byPath.ContainsKey("sequences/createPet.md"));
        Assert.True(byPath.ContainsKey("sequences/listPets.md"));
        Assert.True(byPath.ContainsKey("sequences/getPetById.md"));
        Assert.True(byPath.ContainsKey("sequences/placeOrder.md"));
    }

    [Fact]
    public async Task Snapshot_PetEndpointPage()
    {
        var model = await ParsePetstoreAsync();
        var files = await new MarkdownDocumentationGenerator().GenerateAsync(model, new DocumentationOptions());

        var petPage = files.Single(f => f.RelativePath == "endpoints/pet.md");

        await Verifier.Verify(petPage.Content, extension: "md");
    }
}
