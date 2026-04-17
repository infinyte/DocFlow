using System.Text;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Diagrams.Mermaid;

/// <summary>
/// Produces a Mermaid <c>erDiagram</c> from a <see cref="SemanticModel"/>.
///
/// Cardinality mapping (per <see cref="RelationshipType"/>):
/// <list type="bullet">
/// <item><description><see cref="RelationshipType.Composition"/> → <c>||--o{</c></description></item>
/// <item><description><see cref="RelationshipType.Aggregation"/> → <c>}o--o{</c></description></item>
/// <item><description><see cref="RelationshipType.Association"/> → <c>}o--||</c></description></item>
/// </list>
/// Other relationship kinds (inheritance, dependency, etc.) are not rendered in an ER diagram.
/// Output is deterministic: entities and relationships are ordered alphabetically.
/// </summary>
public sealed class MermaidErDiagramGenerator
{
    public string Generate(SemanticModel model)
    {
        var sb = new StringBuilder();
        sb.Append("erDiagram\n");

        var entities = model.Entities.Values
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToList();

        if (entities.Count == 0)
        {
            return sb.ToString();
        }

        var entitiesById = entities.ToDictionary(e => e.Id, e => e);
        var relationships = model.Relationships
            .Where(r => entitiesById.ContainsKey(r.SourceEntityId)
                        && entitiesById.ContainsKey(r.TargetEntityId)
                        && MapCardinality(r.Type) is not null)
            .OrderBy(r => entitiesById[r.SourceEntityId].Name, StringComparer.Ordinal)
            .ThenBy(r => entitiesById[r.TargetEntityId].Name, StringComparer.Ordinal)
            .ThenBy(r => r.Type)
            .ToList();

        var touchedEntityIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rel in relationships)
        {
            var source = entitiesById[rel.SourceEntityId];
            var target = entitiesById[rel.TargetEntityId];
            var cardinality = MapCardinality(rel.Type)!;
            var label = string.IsNullOrWhiteSpace(rel.Name)
                ? rel.Type.ToString().ToLowerInvariant()
                : rel.Name.Trim();

            sb.Append($"    {SanitizeName(source.Name)} {cardinality} {SanitizeName(target.Name)} : {SanitizeLabel(label)}\n");
            touchedEntityIds.Add(source.Id);
            touchedEntityIds.Add(target.Id);
        }

        // Entities not covered by any rendered relationship are emitted as standalone blocks so
        // a model with no relationships still produces a valid erDiagram with one entity.
        foreach (var entity in entities.Where(e => !touchedEntityIds.Contains(e.Id)))
        {
            sb.Append($"    {SanitizeName(entity.Name)} {{\n");
            foreach (var property in entity.Properties
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                var type = FormatType(property.Type);
                sb.Append($"        {type} {SanitizeFieldName(property.Name)}\n");
            }
            sb.Append("    }\n");
        }

        return sb.ToString();
    }

    private static string? MapCardinality(RelationshipType type) => type switch
    {
        RelationshipType.Composition => "||--o{",
        RelationshipType.Aggregation => "}o--o{",
        RelationshipType.Association => "}o--||",
        _ => null
    };

    private static string FormatType(SemanticType type)
    {
        var name = type.IsCollection && type.GenericArguments.Count > 0
            ? type.GenericArguments[0].Name
            : type.Name;
        return SanitizeFieldName(name);
    }

    private static string SanitizeName(string name) =>
        name.Replace(' ', '_').Replace('<', '_').Replace('>', '_').Replace(',', '_').Replace('.', '_');

    private static string SanitizeFieldName(string name) =>
        name.Replace(' ', '_').Replace('<', '_').Replace('>', '_').Replace(',', '_');

    private static string SanitizeLabel(string label) =>
        label.Replace('"', '\'').Replace('\n', ' ').Trim();
}
