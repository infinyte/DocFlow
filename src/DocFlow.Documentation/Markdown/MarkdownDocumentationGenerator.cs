using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using DocFlow.Documentation.Abstractions;
using DocFlow.Documentation.Markdown.Sections;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown;

/// <summary>
/// Default <see cref="IDocumentationGenerator"/>: produces a Markdown bundle
/// (overview, domain model with Mermaid class diagram, per-group endpoint pages, index TOC).
/// </summary>
public sealed class MarkdownDocumentationGenerator : IDocumentationGenerator
{
    private readonly MermaidClassDiagramGenerator _classDiagramGenerator;
    private readonly MermaidErDiagramGenerator _erDiagramGenerator;
    private readonly MermaidSequenceDiagramGenerator _sequenceDiagramGenerator;
    private readonly MermaidC4ContextGenerator _contextGenerator;
    private readonly MermaidEndpointFlowchartGenerator _flowchartGenerator;

    public MarkdownDocumentationGenerator()
        : this(new MermaidClassDiagramGenerator(),
               new MermaidErDiagramGenerator(),
               new MermaidSequenceDiagramGenerator(),
               new MermaidC4ContextGenerator(),
               new MermaidEndpointFlowchartGenerator())
    {
    }

    public MarkdownDocumentationGenerator(
        MermaidClassDiagramGenerator classDiagramGenerator,
        MermaidErDiagramGenerator erDiagramGenerator,
        MermaidSequenceDiagramGenerator sequenceDiagramGenerator,
        MermaidC4ContextGenerator contextGenerator,
        MermaidEndpointFlowchartGenerator flowchartGenerator)
    {
        _classDiagramGenerator = classDiagramGenerator;
        _erDiagramGenerator = erDiagramGenerator;
        _sequenceDiagramGenerator = sequenceDiagramGenerator;
        _contextGenerator = contextGenerator;
        _flowchartGenerator = flowchartGenerator;
    }

    public async Task<IReadOnlyList<GeneratedFile>> GenerateAsync(
        SemanticModel model,
        DocumentationOptions options,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var files = new List<GeneratedFile>
        {
            OverviewSectionBuilder.Build(model, options),
            await DomainModelSectionBuilder.BuildAsync(model, options, _classDiagramGenerator, _erDiagramGenerator, cancellationToken)
        };

        files.AddRange(ArchitectureSectionBuilder.Build(model, options, _contextGenerator));
        files.AddRange(SecuritySectionBuilder.Build(model, options));
        files.AddRange(EndpointSectionBuilder.Build(model, options, _sequenceDiagramGenerator, _flowchartGenerator));

        // Index is built last so it can link every sibling file.
        files.Add(IndexSectionBuilder.Build(model, options, files));

        // Preserve the source spec inside the bundle so consumers have the original handy.
        if (options.SourceSpec is not null)
        {
            files.Add(options.SourceSpec);
        }

        return files
            .OrderBy(f => f.RelativePath, StringComparer.Ordinal)
            .ToList();
    }
}
