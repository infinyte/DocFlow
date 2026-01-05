using System.Text;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.CodeGen.CSharp;

/// <summary>
/// Generates C# code from a SemanticModel.
/// Uses modern C# 12 syntax with file-scoped namespaces, records for value objects, etc.
/// </summary>
public sealed class CSharpModelGenerator : IModelGenerator
{
    public string TargetFormat => "CSharp";
    public string DefaultExtension => ".cs";

    public Task<GenerateResult> GenerateAsync(SemanticModel model, GeneratorOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new GeneratorOptions();

        var sb = new StringBuilder();
        var warnings = new List<GenerateWarning>();

        // Determine namespace
        var namespaceName = GetNamespace(model, options);

        // File-scoped namespace
        sb.AppendLine($"namespace {namespaceName};");
        sb.AppendLine();

        // Build relationship lookup for generating navigation properties
        var compositionsBySource = model.Relationships
            .Where(r => r.Type == RelationshipType.Composition)
            .GroupBy(r => r.SourceEntityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var associationsBySource = model.Relationships
            .Where(r => r.Type == RelationshipType.Association)
            .GroupBy(r => r.SourceEntityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var inheritanceBySource = model.Relationships
            .Where(r => r.Type == RelationshipType.Inheritance)
            .ToDictionary(r => r.SourceEntityId, r => r);

        var implementationsBySource = model.Relationships
            .Where(r => r.Type == RelationshipType.Implementation)
            .GroupBy(r => r.SourceEntityId)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Generate each entity
        var entityIds = options.EntityFilter?.ToHashSet() ?? model.Entities.Keys.ToHashSet();
        var entities = model.Entities.Values
            .Where(e => entityIds.Contains(e.Id))
            .OrderBy(e => GetEntityOrder(e.Classification))
            .ThenBy(e => e.Name);

        foreach (var entity in entities)
        {
            GenerateEntity(sb, entity, model, inheritanceBySource, implementationsBySource, options);
            sb.AppendLine();
        }

        var content = sb.ToString().TrimEnd() + Environment.NewLine;

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

    private void GenerateEntity(
        StringBuilder sb,
        SemanticEntity entity,
        SemanticModel model,
        Dictionary<string, SemanticRelationship> inheritanceBySource,
        Dictionary<string, List<SemanticRelationship>> implementationsBySource,
        GeneratorOptions options)
    {
        // XML documentation
        if (options.IncludeComments && !string.IsNullOrEmpty(entity.Description))
        {
            sb.AppendLine("/// <summary>");
            foreach (var line in entity.Description.Split('\n'))
            {
                sb.AppendLine($"/// {line.Trim()}");
            }
            sb.AppendLine("/// </summary>");
        }

        // Determine type keyword and base types
        var typeKeyword = GetTypeKeyword(entity);
        var baseTypes = GetBaseTypes(entity, model, inheritanceBySource, implementationsBySource);

        // Handle enums specially
        if (entity.Classification == EntityClassification.Enum)
        {
            GenerateEnum(sb, entity);
            return;
        }

        // Handle interfaces specially
        if (entity.Classification == EntityClassification.Interface)
        {
            GenerateInterface(sb, entity, baseTypes, options);
            return;
        }

        // Record types (ValueObjects, Events)
        if (typeKeyword == "record")
        {
            GenerateRecord(sb, entity, baseTypes, options);
            return;
        }

        // Regular class
        GenerateClass(sb, entity, baseTypes, options);
    }

    private void GenerateEnum(StringBuilder sb, SemanticEntity entity)
    {
        sb.AppendLine($"public enum {entity.Name}");
        sb.AppendLine("{");

        var members = entity.Properties.Select(p => p.Name).ToList();
        for (var i = 0; i < members.Count; i++)
        {
            var comma = i < members.Count - 1 ? "," : "";
            sb.AppendLine($"    {members[i]}{comma}");
        }

        sb.AppendLine("}");
    }

    private void GenerateInterface(StringBuilder sb, SemanticEntity entity, string baseTypes, GeneratorOptions options)
    {
        var declaration = string.IsNullOrEmpty(baseTypes)
            ? $"public interface {entity.Name}"
            : $"public interface {entity.Name} : {baseTypes}";

        sb.AppendLine(declaration);
        sb.AppendLine("{");

        foreach (var method in entity.Operations)
        {
            GenerateInterfaceMethod(sb, method, options);
        }

        sb.AppendLine("}");
    }

    private void GenerateRecord(StringBuilder sb, SemanticEntity entity, string baseTypes, GeneratorOptions options)
    {
        var properties = entity.Properties.Where(p => p.Semantics != PropertySemantics.Collection).ToList();

        // Build primary constructor parameters
        var parameters = properties
            .Select(p => $"{FormatTypeName(p.Type, p.IsRequired)} {p.Name}")
            .ToList();

        var paramString = string.Join(",\n    ", parameters);
        var declaration = properties.Count > 0
            ? $"public record {entity.Name}(\n    {paramString})"
            : $"public record {entity.Name}";

        if (!string.IsNullOrEmpty(baseTypes))
        {
            declaration += $" : {baseTypes}";
        }

        // Check if we need a body (for static methods, etc.)
        var staticMethods = entity.Operations.Where(o => o.IsStatic).ToList();

        if (staticMethods.Count == 0)
        {
            sb.AppendLine($"{declaration};");
        }
        else
        {
            sb.AppendLine(declaration);
            sb.AppendLine("{");

            foreach (var method in staticMethods)
            {
                GenerateMethod(sb, method, options);
            }

            sb.AppendLine("}");
        }
    }

    private void GenerateClass(StringBuilder sb, SemanticEntity entity, string baseTypes, GeneratorOptions options)
    {
        // Class modifiers
        var modifiers = "public";
        if (entity.Stereotypes.Contains("abstract") || entity.Classification == EntityClassification.AbstractClass)
            modifiers += " abstract";
        if (entity.Stereotypes.Contains("sealed"))
            modifiers += " sealed";

        var declaration = string.IsNullOrEmpty(baseTypes)
            ? $"{modifiers} class {entity.Name}"
            : $"{modifiers} class {entity.Name} : {baseTypes}";

        sb.AppendLine(declaration);
        sb.AppendLine("{");

        // Properties
        foreach (var property in entity.Properties)
        {
            GenerateProperty(sb, property, options);
        }

        // Methods
        if (entity.Operations.Count > 0 && entity.Properties.Count > 0)
        {
            sb.AppendLine();
        }

        foreach (var method in entity.Operations)
        {
            GenerateMethod(sb, method, options);
        }

        sb.AppendLine("}");
    }

    private void GenerateProperty(StringBuilder sb, SemanticProperty property, GeneratorOptions options)
    {
        // XML doc comment
        if (options.IncludeComments && !string.IsNullOrEmpty(property.Description))
        {
            sb.AppendLine($"    /// <summary>{property.Description}</summary>");
        }

        // Attributes
        if (property.IsRequired && !property.Type.IsNullable)
        {
            // Use required keyword instead of attribute for C# 11+
        }

        foreach (var attr in property.Attributes)
        {
            sb.AppendLine($"    [{attr.Name}]");
        }

        var visibility = GetVisibilityKeyword(property.Visibility);
        var typeName = FormatTypeName(property.Type, property.IsRequired);
        var requiredKeyword = property.IsRequired && !property.Type.IsNullable && !property.Type.IsPrimitive ? "required " : "";
        var accessor = GetPropertyAccessor(property);

        sb.AppendLine($"    {visibility} {requiredKeyword}{typeName} {property.Name} {accessor}");
    }

    private void GenerateMethod(StringBuilder sb, SemanticOperation method, GeneratorOptions options)
    {
        if (options.IncludeComments && !string.IsNullOrEmpty(method.Description))
        {
            sb.AppendLine($"    /// <summary>{method.Description}</summary>");
        }

        var visibility = GetVisibilityKeyword(method.Visibility);
        var modifiers = new List<string> { visibility };

        if (method.IsStatic) modifiers.Add("static");
        if (method.IsAbstract) modifiers.Add("abstract");
        if (method.IsAsync) modifiers.Add("async");

        var returnType = method.ReturnType != null ? FormatTypeName(method.ReturnType, false) : "void";
        var parameters = string.Join(", ", method.Parameters.Select(p =>
            $"{FormatTypeName(p.Type, !p.IsOptional)} {p.Name}" + (p.DefaultValue != null ? $" = {p.DefaultValue}" : "")));

        var modifierStr = string.Join(" ", modifiers);
        var signature = $"    {modifierStr} {returnType} {method.Name}({parameters})";

        if (method.IsAbstract)
        {
            sb.AppendLine($"{signature};");
        }
        else
        {
            sb.AppendLine(signature);
            sb.AppendLine("    {");
            if (method.ReturnType != null)
            {
                sb.AppendLine($"        throw new NotImplementedException();");
            }
            sb.AppendLine("    }");
        }
    }

    private void GenerateInterfaceMethod(StringBuilder sb, SemanticOperation method, GeneratorOptions options)
    {
        if (options.IncludeComments && !string.IsNullOrEmpty(method.Description))
        {
            sb.AppendLine($"    /// <summary>{method.Description}</summary>");
        }

        var returnType = method.ReturnType != null ? FormatTypeName(method.ReturnType, false) : "void";
        var parameters = string.Join(", ", method.Parameters.Select(p =>
            $"{FormatTypeName(p.Type, !p.IsOptional)} {p.Name}" + (p.DefaultValue != null ? $" = {p.DefaultValue}" : "")));

        sb.AppendLine($"    {returnType} {method.Name}({parameters});");
    }

    private string GetTypeKeyword(SemanticEntity entity)
    {
        return entity.Classification switch
        {
            EntityClassification.ValueObject => "record",
            EntityClassification.DomainEvent => "record",
            EntityClassification.DataTransferObject => "record",
            EntityClassification.Interface => "interface",
            EntityClassification.Enum => "enum",
            _ when entity.Stereotypes.Contains("record") => "record",
            _ => "class"
        };
    }

    private string GetBaseTypes(
        SemanticEntity entity,
        SemanticModel model,
        Dictionary<string, SemanticRelationship> inheritanceBySource,
        Dictionary<string, List<SemanticRelationship>> implementationsBySource)
    {
        var baseTypes = new List<string>();

        // Inheritance (single base class)
        if (inheritanceBySource.TryGetValue(entity.Id, out var inheritance))
        {
            var baseEntity = model.GetEntity(inheritance.TargetEntityId);
            if (baseEntity != null)
            {
                baseTypes.Add(baseEntity.Name);
            }
        }

        // Implementations (multiple interfaces)
        if (implementationsBySource.TryGetValue(entity.Id, out var implementations))
        {
            foreach (var impl in implementations)
            {
                var interfaceEntity = model.GetEntity(impl.TargetEntityId);
                if (interfaceEntity != null)
                {
                    baseTypes.Add(interfaceEntity.Name);
                }
            }
        }

        return string.Join(", ", baseTypes);
    }

    private string FormatTypeName(SemanticType type, bool isRequired)
    {
        var name = type.Name;

        // Handle generic types
        if (type.IsCollection && type.GenericArguments.Count > 0)
        {
            var elementType = FormatTypeName(type.GenericArguments[0], false);
            name = type.Name switch
            {
                "ICollection" => $"ICollection<{elementType}>",
                "List" => $"List<{elementType}>",
                "IList" => $"IList<{elementType}>",
                "IEnumerable" => $"IEnumerable<{elementType}>",
                "Set" or "HashSet" => $"HashSet<{elementType}>",
                "IReadOnlyList" => $"IReadOnlyList<{elementType}>",
                _ => $"ICollection<{elementType}>"
            };
        }

        // Handle nullable
        if (type.IsNullable && !name.EndsWith("?"))
        {
            name += "?";
        }

        return name;
    }

    private string GetPropertyAccessor(SemanticProperty property)
    {
        if (property.IsReadOnly)
        {
            return property.Semantics == PropertySemantics.Identity
                ? "{ get; init; }"
                : "{ get; }";
        }

        return property.Semantics switch
        {
            PropertySemantics.Identity => "{ get; init; }",
            PropertySemantics.Collection => $"{{ get; init; }} = new List<{GetCollectionElementType(property.Type)}>();",
            PropertySemantics.Audit => "{ get; init; }",
            _ => "{ get; set; }"
        };
    }

    private string GetCollectionElementType(SemanticType type)
    {
        if (type.GenericArguments.Count > 0)
        {
            return FormatTypeName(type.GenericArguments[0], false);
        }
        return "object";
    }

    private static string GetVisibilityKeyword(Visibility visibility)
    {
        return visibility switch
        {
            Visibility.Public => "public",
            Visibility.Private => "private",
            Visibility.Protected => "protected",
            Visibility.Internal => "internal",
            Visibility.ProtectedInternal => "protected internal",
            Visibility.PrivateProtected => "private protected",
            _ => "public"
        };
    }

    private static string GetNamespace(SemanticModel model, GeneratorOptions options)
    {
        if (options.ExtendedOptions.TryGetValue("Namespace", out var ns) && ns is string nsStr)
        {
            return nsStr;
        }

        // Try to infer from model name or use default
        if (!string.IsNullOrEmpty(model.Name))
        {
            return $"{model.Name}.Domain";
        }

        return "Generated.Domain";
    }

    private static int GetEntityOrder(EntityClassification classification)
    {
        // Order: Enums first, then interfaces, then value objects, then entities, then services
        return classification switch
        {
            EntityClassification.Enum => 0,
            EntityClassification.Interface => 1,
            EntityClassification.ValueObject => 2,
            EntityClassification.DomainEvent => 3,
            EntityClassification.Entity => 4,
            EntityClassification.AggregateRoot => 5,
            EntityClassification.Repository => 6,
            EntityClassification.DomainService => 7,
            _ => 10
        };
    }
}
