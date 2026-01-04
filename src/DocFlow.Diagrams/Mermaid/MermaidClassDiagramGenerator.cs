using System.Text;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Diagrams.Mermaid;

/// <summary>
/// Generates Mermaid class diagram syntax from a SemanticModel.
/// Produces valid Mermaid classDiagram that can be rendered by Mermaid.js.
/// </summary>
public sealed class MermaidClassDiagramGenerator : IModelGenerator
{
    public string TargetFormat => "Mermaid";
    public string DefaultExtension => ".mmd";

    public Task<GenerateResult> GenerateAsync(SemanticModel model, GeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new GeneratorOptions();

        var sb = new StringBuilder();
        sb.AppendLine("classDiagram");

        var warnings = new List<GenerateWarning>();
        var entityIds = options.EntityFilter?.ToHashSet() ?? model.Entities.Keys.ToHashSet();

        // Generate class definitions
        foreach (var entity in model.Entities.Values.Where(e => entityIds.Contains(e.Id)))
        {
            GenerateClassDefinition(sb, entity, options);
        }

        sb.AppendLine();

        // Generate relationships
        if (options.IncludeRelationships)
        {
            foreach (var relationship in model.Relationships)
            {
                if (!entityIds.Contains(relationship.SourceEntityId) ||
                    !entityIds.Contains(relationship.TargetEntityId))
                    continue;

                var sourceEntity = model.GetEntity(relationship.SourceEntityId);
                var targetEntity = model.GetEntity(relationship.TargetEntityId);

                if (sourceEntity == null || targetEntity == null)
                {
                    warnings.Add(new GenerateWarning
                    {
                        Code = "MISSING_ENTITY",
                        Message = $"Relationship references missing entity"
                    });
                    continue;
                }

                GenerateRelationship(sb, relationship, sourceEntity, targetEntity);
            }
        }

        var content = sb.ToString();

        // Write to file if output path specified
        if (options.OutputPath != null)
        {
            File.WriteAllText(options.OutputPath, content);
        }

        return Task.FromResult(new GenerateResult
        {
            Content = content,
            Success = true,
            Warnings = warnings
        });
    }

    private void GenerateClassDefinition(StringBuilder sb, SemanticEntity entity, GeneratorOptions options)
    {
        var safeName = SanitizeName(entity.Name);

        // Add stereotype annotation
        var stereotype = GetStereotype(entity);
        if (stereotype != null)
        {
            sb.AppendLine($"    class {safeName} {{");
            sb.AppendLine($"        <<{stereotype}>>");
        }
        else
        {
            sb.AppendLine($"    class {safeName} {{");
        }

        // Add properties (using Mermaid's colon syntax: +name : type)
        foreach (var property in entity.Properties)
        {
            var visibility = GetVisibilitySymbol(property.Visibility);
            var typeName = FormatTypeName(property.Type);

            sb.AppendLine($"        {visibility}{property.Name} : {typeName}");
        }

        // Add methods (if any and not too many)
        var publicMethods = entity.Operations
            .Where(o => o.Visibility == Visibility.Public)
            .Take(10) // Limit to keep diagram readable
            .ToList();

        foreach (var method in publicMethods)
        {
            var visibility = GetVisibilitySymbol(method.Visibility);
            var returnType = method.ReturnType != null ? FormatTypeName(method.ReturnType) : "void";
            var parameters = string.Join(", ", method.Parameters.Select(p => $"{p.Name}"));
            // Mermaid syntax: modifiers ($/*) go at the very end after return type
            var modifiers = method.IsAbstract ? "*" : (method.IsStatic ? "$" : "");

            sb.AppendLine($"        {visibility}{method.Name}({parameters}) {returnType}{modifiers}");
        }

        sb.AppendLine("    }");
    }

    private void GenerateRelationship(StringBuilder sb, SemanticRelationship relationship, SemanticEntity source, SemanticEntity target)
    {
        var sourceName = SanitizeName(source.Name);
        var targetName = SanitizeName(target.Name);

        var arrow = GetRelationshipArrow(relationship.Type);
        var sourceMultiplicity = FormatMultiplicity(relationship.SourceMultiplicity);
        var targetMultiplicity = FormatMultiplicity(relationship.TargetMultiplicity);

        // Build the relationship line
        var line = new StringBuilder();
        line.Append($"    {sourceName} ");

        // Add source multiplicity if not "1"
        if (sourceMultiplicity != "1")
        {
            line.Append($"\"{sourceMultiplicity}\" ");
        }

        line.Append(arrow);

        // Add target multiplicity if not "1"
        if (targetMultiplicity != "1")
        {
            line.Append($" \"{targetMultiplicity}\"");
        }

        line.Append($" {targetName}");

        // Add label if present
        if (!string.IsNullOrEmpty(relationship.Name) &&
            relationship.Type != RelationshipType.Inheritance &&
            relationship.Type != RelationshipType.Implementation)
        {
            line.Append($" : {relationship.Name}");
        }

        sb.AppendLine(line.ToString());
    }

    private string? GetStereotype(SemanticEntity entity)
    {
        // Prioritize explicit stereotypes
        if (entity.Stereotypes.Contains("interface")) return "interface";
        if (entity.Stereotypes.Contains("abstract")) return "abstract";
        if (entity.Stereotypes.Contains("enumeration")) return "enumeration";

        // Map classifications to stereotypes
        return entity.Classification switch
        {
            EntityClassification.AggregateRoot => "AggregateRoot",
            EntityClassification.Entity => "Entity",
            EntityClassification.ValueObject => "ValueObject",
            EntityClassification.DomainService => "Service",
            EntityClassification.DomainEvent => "Event",
            EntityClassification.Repository => "Repository",
            EntityClassification.Factory => "Factory",
            EntityClassification.Specification => "Specification",
            EntityClassification.Interface => "interface",
            EntityClassification.AbstractClass => "abstract",
            EntityClassification.Enum => "enumeration",
            EntityClassification.DataTransferObject => "DTO",
            EntityClassification.ViewModel => "ViewModel",
            EntityClassification.Command => "Command",
            EntityClassification.Query => "Query",
            EntityClassification.Handler => "Handler",
            _ => null
        };
    }

    private string GetVisibilitySymbol(Visibility visibility)
    {
        return visibility switch
        {
            Visibility.Public => "+",
            Visibility.Private => "-",
            Visibility.Protected => "#",
            Visibility.Internal => "~",
            Visibility.ProtectedInternal => "#",
            Visibility.PrivateProtected => "-",
            _ => "+"
        };
    }

    private string GetRelationshipArrow(RelationshipType type)
    {
        return type switch
        {
            RelationshipType.Inheritance => "--|>",
            RelationshipType.Implementation => "..|>",
            RelationshipType.Composition => "--*",
            RelationshipType.Aggregation => "--o",
            RelationshipType.Association => "-->",
            RelationshipType.Dependency => "..>",
            RelationshipType.Realization => "..|>",
            RelationshipType.ReferenceById => "..>",
            RelationshipType.PublishesEvent => "..>",
            RelationshipType.HandlesEvent => "..>",
            _ => "-->"
        };
    }

    private string FormatMultiplicity(Multiplicity multiplicity)
    {
        if (multiplicity.LowerBound == 1 && multiplicity.UpperBound == 1)
            return "1";
        if (multiplicity.LowerBound == 0 && multiplicity.UpperBound == 1)
            return "0..1";
        if (multiplicity.LowerBound == 0 && multiplicity.UpperBound == null)
            return "*";
        if (multiplicity.LowerBound == 1 && multiplicity.UpperBound == null)
            return "1..*";
        if (multiplicity.LowerBound == multiplicity.UpperBound)
            return multiplicity.LowerBound?.ToString() ?? "*";

        var lower = multiplicity.LowerBound?.ToString() ?? "0";
        var upper = multiplicity.UpperBound?.ToString() ?? "*";
        return $"{lower}..{upper}";
    }

    private string FormatTypeName(SemanticType type)
    {
        if (type.IsCollection && type.GenericArguments.Count > 0)
        {
            var elementType = FormatTypeName(type.GenericArguments[0]);
            return $"{type.Name}~{elementType}~";
        }

        var name = type.Name;
        if (type.IsNullable && !name.EndsWith("?"))
        {
            name += "?";
        }

        return name;
    }

    private string GetPropertyModifiers(SemanticProperty property)
    {
        // Note: Mermaid doesn't have native syntax for required/readonly markers
        // We omit these to ensure valid diagram syntax
        return "";
    }

    private string SanitizeName(string name)
    {
        // Mermaid doesn't like certain characters in class names
        return name
            .Replace("<", "_")
            .Replace(">", "_")
            .Replace(",", "_")
            .Replace(" ", "_")
            .Replace(".", "_");
    }
}
