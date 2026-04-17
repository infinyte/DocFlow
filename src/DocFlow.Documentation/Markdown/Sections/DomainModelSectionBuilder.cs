using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Markdown.Sections;

/// <summary>
/// Emits <c>domain-model.md</c>: a Mermaid class diagram followed by an entity summary table.
/// </summary>
internal static class DomainModelSectionBuilder
{
    public static async Task<GeneratedFile> BuildAsync(
        SemanticModel model,
        DocumentationOptions options,
        MermaidClassDiagramGenerator classDiagramGenerator,
        MermaidErDiagramGenerator erDiagramGenerator,
        CancellationToken cancellationToken)
    {
        var writer = new MarkdownWriter();
        writer.Heading(1, "Domain Model");
        writer.Line();

        if (model.Entities.Count == 0)
        {
            writer.Line("_No entities discovered._");
            writer.Line();
            return new GeneratedFile("domain-model.md", writer.ToString(), "text/markdown");
        }

        var deterministicModel = SortEntitiesForDeterminism(model);

        if ((options.Diagrams & DiagramKinds.Class) != 0)
        {
            var diagram = await classDiagramGenerator.GenerateAsync(deterministicModel, options: null, cancellationToken);

            writer.Line("```mermaid");
            writer.Raw(diagram.Content ?? string.Empty);
            writer.Line("```");
            writer.Line();
        }

        if ((options.Diagrams & DiagramKinds.Er) != 0)
        {
            var er = erDiagramGenerator.Generate(deterministicModel);
            writer.Line("```mermaid");
            writer.Raw(er);
            writer.Line("```");
            writer.Line();
        }

        writer.Heading(2, "Entities");
        writer.Line();
        writer.Line("| Entity | Stereotype | Properties |");
        writer.Line("| --- | --- | --- |");
        foreach (var entity in model.Entities.Values.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            var stereotype = entity.Classification == EntityClassification.Unknown
                ? ""
                : entity.Classification.ToString();
            // Anchor is inlined inside the cell so cross-links from endpoint pages resolve to
            // this row without breaking table rendering.
            writer.Line($"| <a id=\"entity-{Slug.Kebab(entity.Name)}\"></a>`{entity.Name}` | {stereotype} | {entity.Properties.Count} |");
        }
        writer.Line();

        return new GeneratedFile("domain-model.md", writer.ToString(), "text/markdown");
    }

    /// <summary>
    /// The upstream class-diagram generator iterates <see cref="SemanticModel.Entities"/> in
    /// dictionary order. Reinsert entities sorted alphabetically so the rendered diagram is
    /// stable across runs regardless of upstream insertion order.
    /// </summary>
    private static SemanticModel SortEntitiesForDeterminism(SemanticModel source)
    {
        var clone = new SemanticModel
        {
            Id = source.Id,
            Name = source.Name,
            Description = source.Description,
            Version = source.Version,
            Provenance = source.Provenance,
            Api = source.Api
        };

        foreach (var entity in source.Entities.Values.OrderBy(e => e.Name, StringComparer.Ordinal))
        {
            clone.Entities[entity.Id] = entity;
        }

        clone.Relationships.AddRange(source.Relationships
            .OrderBy(r => r.SourceEntityId, StringComparer.Ordinal)
            .ThenBy(r => r.TargetEntityId, StringComparer.Ordinal)
            .ThenBy(r => r.Type));

        return clone;
    }
}
