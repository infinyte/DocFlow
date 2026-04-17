using DocFlow.Documentation.Abstractions;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;
using Xunit;

namespace DocFlow.Documentation.Tests;

public class ScaffoldingTests
{
    [Fact]
    public void Scaffolding_CompilesAndReferencesCore()
    {
        Assert.True(typeof(IDocumentationGenerator).IsInterface);

        // Types from Core and Diagrams are reachable via the transitive references.
        Assert.NotNull(typeof(IDocumentationGenerator).Assembly.GetName().Name);
    }

    [Fact]
    public void DiagramKinds_AllIsUnionOfIndividualFlags()
    {
        Assert.Equal(
            DiagramKinds.Class | DiagramKinds.Er | DiagramKinds.Sequence | DiagramKinds.Context | DiagramKinds.Flow,
            DiagramKinds.All);
    }

    [Fact]
    public void DocumentationOptions_Defaults_AreSensible()
    {
        var options = new DocumentationOptions();

        Assert.Equal(DocumentationFormat.Markdown, options.Format);
        Assert.Equal(DiagramKinds.Class, options.Diagrams);
        Assert.False(options.WithExamples);
        Assert.Equal(GroupBy.Tag, options.GroupBy);
        Assert.Null(options.Title);
    }

    [Fact]
    public void GeneratedFile_IsValueEqual()
    {
        var a = new GeneratedFile("index.md", "# Hi", "text/markdown");
        var b = new GeneratedFile("index.md", "# Hi", "text/markdown");
        Assert.Equal(a, b);
    }
}
