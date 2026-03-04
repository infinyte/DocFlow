using System.Diagnostics;
using System.Text.RegularExpressions;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Diagrams.Mermaid;

/// <summary>
/// Parses Mermaid classDiagram syntax into a SemanticModel.
/// Supports classes, stereotypes, properties, methods, and relationships.
/// </summary>
public sealed partial class MermaidClassDiagramParser : IModelParser
{
    public string SourceFormat => "Mermaid";
    public IReadOnlyList<string> SupportedExtensions => [".mmd", ".mermaid"];

    public bool CanParse(ParserInput input)
    {
        if (input.Content != null)
            return input.Content.TrimStart().StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase);
        if (input.FilePath != null)
            return SupportedExtensions.Any(ext => input.FilePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
        return false;
    }

    public async Task<ParseResult> ParseAsync(ParserInput input, ParserOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ParserOptions();
        var stopwatch = Stopwatch.StartNew();

        string content;
        if (input.Content != null)
        {
            content = input.Content;
        }
        else if (input.FilePath != null)
        {
            content = await File.ReadAllTextAsync(input.FilePath, cancellationToken);
        }
        else
        {
            return ParseResult.Failed(new ParseError { Code = "NO_INPUT", Message = "No content or file path provided" });
        }

        var model = new SemanticModel
        {
            Name = input.FilePath != null ? Path.GetFileNameWithoutExtension(input.FilePath) : "ParsedDiagram",
            Provenance = new ModelProvenance
            {
                SourceFormat = SourceFormat,
                SourceFiles = input.FilePath != null ? [input.FilePath] : [],
                ToolVersion = "1.0.0"
            }
        };

        var errors = new List<ParseError>();
        var warnings = new List<ParseWarning>();
        var entityByName = new Dictionary<string, SemanticEntity>(StringComparer.OrdinalIgnoreCase);

        var lines = content.Split('\n').Select(l => l.Trim()).ToList();

        // First pass: Parse class definitions
        var currentClass = (SemanticEntity?)null;
        var inClassBody = false;

        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var lineNum = i + 1;

            // Skip empty lines and comments
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("%%"))
                continue;

            // Skip classDiagram declaration
            if (line.Equals("classDiagram", StringComparison.OrdinalIgnoreCase))
                continue;

            // Class definition start: "class ClassName {"
            var classMatch = ClassDefinitionRegex().Match(line);
            if (classMatch.Success)
            {
                var className = classMatch.Groups["name"].Value;
                currentClass = new SemanticEntity
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = className,
                    Classification = EntityClassification.Class,
                    Source = options.IncludeSourceInfo ? new SourceInfo
                    {
                        SourceType = SourceFormat,
                        FilePath = input.FilePath,
                        LineNumber = lineNum
                    } : null
                };
                model.AddEntity(currentClass);
                entityByName[className] = currentClass;
                inClassBody = line.Contains('{');
                continue;
            }

            // End of class body
            if (line == "}" && inClassBody && currentClass != null)
            {
                inClassBody = false;
                currentClass = null;
                continue;
            }

            // Inside class body
            if (inClassBody && currentClass != null)
            {
                // Stereotype: <<SomeStereotype>>
                var stereotypeMatch = StereotypeRegex().Match(line);
                if (stereotypeMatch.Success)
                {
                    var stereotype = stereotypeMatch.Groups["stereotype"].Value;
                    currentClass.Stereotypes.Add(stereotype.ToLowerInvariant());
                    currentClass.Classification = MapStereotypeToClassification(stereotype);
                    continue;
                }

                // Method: +methodName(params) ReturnType or +methodName(params) ReturnType$
                var methodMatch = MethodRegex().Match(line);
                if (methodMatch.Success)
                {
                    var operation = ParseMethod(methodMatch);
                    if (operation != null)
                        currentClass.Operations.Add(operation);
                    continue;
                }

                // Property: +Name : Type
                var propertyMatch = PropertyRegex().Match(line);
                if (propertyMatch.Success)
                {
                    var property = ParseProperty(propertyMatch);
                    if (property != null)
                        currentClass.Properties.Add(property);
                    continue;
                }
            }

            // Relationship: ClassName1 --> ClassName2 : label
            var relationshipMatch = RelationshipRegex().Match(line);
            if (relationshipMatch.Success)
            {
                var relationship = ParseRelationship(relationshipMatch, entityByName, model);
                if (relationship != null)
                {
                    model.Relationships.Add(relationship);
                }
                else
                {
                    warnings.Add(new ParseWarning
                    {
                        Code = "UNRESOLVED_RELATIONSHIP",
                        Message = $"Could not resolve entities in relationship: {line}",
                        Line = lineNum
                    });
                }
            }
        }

        stopwatch.Stop();

        return new ParseResult
        {
            Model = model,
            Success = errors.Count == 0,
            Errors = errors,
            Warnings = warnings,
            Statistics = new ParseStatistics
            {
                EntitiesParsed = model.Entities.Count,
                RelationshipsParsed = model.Relationships.Count,
                ParseDuration = stopwatch.Elapsed
            }
        };
    }

    private SemanticProperty ParseProperty(Match match)
    {
        var visibility = ParseVisibility(match.Groups["visibility"].Value);

        // Support two formats:
        // Format 1: +Type name [annotations] (type1/name1 groups)
        // Format 2: +name : Type (name2/type2 groups)
        string name;
        string typeName;

        if (match.Groups["name1"].Success && !string.IsNullOrWhiteSpace(match.Groups["name1"].Value))
        {
            // Format 1: +Type name
            name = match.Groups["name1"].Value;
            typeName = match.Groups["type1"].Value;
        }
        else
        {
            // Format 2: +name : Type
            name = match.Groups["name2"].Value;
            typeName = match.Groups["type2"].Success && !string.IsNullOrWhiteSpace(match.Groups["type2"].Value)
                ? match.Groups["type2"].Value
                : "object";
        }

        return new SemanticProperty
        {
            Name = name,
            Type = ParseType(typeName),
            Visibility = visibility,
            Semantics = InferPropertySemantics(name, typeName)
        };
    }

    private SemanticOperation ParseMethod(Match match)
    {
        var visibility = ParseVisibility(match.Groups["visibility"].Value);
        var name = match.Groups["name"].Value;
        var paramsStr = match.Groups["params"].Value;
        var returnType = match.Groups["return"].Value;
        var modifiers = match.Groups["modifiers"].Value;

        var operation = new SemanticOperation
        {
            Name = name,
            ReturnType = string.IsNullOrEmpty(returnType) || returnType == "void" ? null : ParseType(returnType.TrimEnd('$', '*')),
            Visibility = visibility,
            IsStatic = modifiers.Contains('$') || returnType.EndsWith('$'),
            IsAbstract = modifiers.Contains('*') || returnType.EndsWith('*')
        };

        // Parse parameters
        if (!string.IsNullOrWhiteSpace(paramsStr))
        {
            var paramParts = paramsStr.Split(',', StringSplitOptions.TrimEntries);
            foreach (var param in paramParts)
            {
                if (string.IsNullOrWhiteSpace(param)) continue;

                // Parameters might be just names or "type name"
                var parts = param.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var paramName = parts.Length > 0 ? parts[^1] : param;
                var paramType = parts.Length > 1 ? parts[0] : "object";

                operation.Parameters.Add(new SemanticParameter
                {
                    Name = paramName,
                    Type = ParseType(paramType)
                });
            }
        }

        return operation;
    }

    private SemanticRelationship? ParseRelationship(Match match, Dictionary<string, SemanticEntity> entityByName, SemanticModel model)
    {
        var leftName = match.Groups["source"].Value;
        var leftMultStr = match.Groups["sourceMult"].Value;
        var arrow = match.Groups["arrow"].Value;
        var rightMultStr = match.Groups["targetMult"].Value;
        var rightName = match.Groups["target"].Value;
        var label = match.Groups["label"].Value;

        if (!entityByName.TryGetValue(leftName, out var leftEntity) ||
            !entityByName.TryGetValue(rightName, out var rightEntity))
        {
            return null;
        }

        var relType = ParseRelationshipType(arrow);
        var leftMult = string.IsNullOrEmpty(leftMultStr) ? Multiplicity.One : Multiplicity.Parse(leftMultStr.Trim('"'));
        var rightMult = string.IsNullOrEmpty(rightMultStr) ? Multiplicity.One : Multiplicity.Parse(rightMultStr.Trim('"'));

        // Determine direction based on arrow type
        // Left-pointing arrows (<|--, <|.., *--, o--, <--, <..) mean: right entity is the source
        // Right-pointing arrows (--|>, ..|>, --*, --o, -->, ..>) mean: left entity is the source
        // For inheritance: source = child (the one extending), target = parent (the one being extended)
        var isLeftPointing = arrow.StartsWith('<') || arrow.StartsWith('*') || arrow.StartsWith('o');

        var sourceEntity = isLeftPointing ? rightEntity : leftEntity;
        var targetEntity = isLeftPointing ? leftEntity : rightEntity;
        var sourceMult = isLeftPointing ? rightMult : leftMult;
        var targetMult = isLeftPointing ? leftMult : rightMult;

        return new SemanticRelationship
        {
            Id = Guid.NewGuid().ToString(),
            SourceEntityId = sourceEntity.Id,
            TargetEntityId = targetEntity.Id,
            Type = relType,
            Name = string.IsNullOrEmpty(label) ? null : label.Trim(),
            SourceMultiplicity = sourceMult,
            TargetMultiplicity = targetMult
        };
    }

    private SemanticType ParseType(string typeName)
    {
        typeName = typeName.Trim();

        // Handle nullable types
        var isNullable = typeName.EndsWith('?');
        if (isNullable)
            typeName = typeName.TrimEnd('?');

        // Handle generic types with Mermaid's tilde notation: ICollection~Item~
        var genericMatch = GenericTypeRegex().Match(typeName);
        if (genericMatch.Success)
        {
            var baseName = genericMatch.Groups["base"].Value;
            var argName = genericMatch.Groups["arg"].Value;
            return new SemanticType
            {
                Name = baseName,
                IsCollection = IsCollectionTypeName(baseName),
                IsNullable = isNullable,
                GenericArguments = [ParseType(argName)]
            };
        }

        return new SemanticType
        {
            Name = typeName,
            IsPrimitive = IsPrimitiveTypeName(typeName),
            IsNullable = isNullable
        };
    }

    private static Visibility ParseVisibility(string symbol)
    {
        return symbol switch
        {
            "+" => Visibility.Public,
            "-" => Visibility.Private,
            "#" => Visibility.Protected,
            "~" => Visibility.Internal,
            _ => Visibility.Public
        };
    }

    private static RelationshipType ParseRelationshipType(string arrow)
    {
        return arrow switch
        {
            "--|>" or "<|--" => RelationshipType.Inheritance,
            "..|>" or "<|.." => RelationshipType.Implementation,
            "--*" or "*--" => RelationshipType.Composition,
            "--o" or "o--" => RelationshipType.Aggregation,
            "-->" or "<--" => RelationshipType.Association,
            "..>" or "<.." => RelationshipType.Dependency,
            _ => RelationshipType.Association
        };
    }

    private static EntityClassification MapStereotypeToClassification(string stereotype)
    {
        // Normalize: remove spaces and convert to lowercase for matching
        var normalized = stereotype.ToLowerInvariant().Replace(" ", "");
        return normalized switch
        {
            "aggregateroot" => EntityClassification.AggregateRoot,
            "entity" => EntityClassification.Entity,
            "valueobject" => EntityClassification.ValueObject,
            "service" => EntityClassification.DomainService,
            "domainservice" => EntityClassification.DomainService,
            "event" => EntityClassification.DomainEvent,
            "domainevent" => EntityClassification.DomainEvent,
            "repository" => EntityClassification.Repository,
            "factory" => EntityClassification.Factory,
            "specification" => EntityClassification.Specification,
            "interface" => EntityClassification.Interface,
            "abstract" => EntityClassification.AbstractClass,
            "enumeration" => EntityClassification.Enum,
            "enum" => EntityClassification.Enum,
            "dto" => EntityClassification.DataTransferObject,
            "viewmodel" => EntityClassification.ViewModel,
            "command" => EntityClassification.Command,
            "query" => EntityClassification.Query,
            "handler" => EntityClassification.Handler,
            _ => EntityClassification.Class
        };
    }

    private static PropertySemantics InferPropertySemantics(string name, string typeName)
    {
        var lowerName = name.ToLowerInvariant();

        if (lowerName == "id" || (lowerName.EndsWith("id") && name.Length <= 5))
            return PropertySemantics.Identity;

        if (lowerName.Contains("createdat") || lowerName.Contains("updatedat"))
            return PropertySemantics.Audit;

        if (IsCollectionTypeName(typeName))
            return PropertySemantics.Collection;

        return PropertySemantics.State;
    }

    private static bool IsCollectionTypeName(string typeName)
    {
        var collections = new[] { "ICollection", "IList", "List", "IEnumerable", "IReadOnlyList", "HashSet", "ISet", "Array" };
        return collections.Any(c => typeName.Contains(c, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPrimitiveTypeName(string typeName)
    {
        var primitives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string", "int", "long", "short", "byte", "float", "double", "decimal",
            "bool", "boolean", "char", "DateTime", "DateTimeOffset", "TimeSpan", "Guid"
        };
        return primitives.Contains(typeName);
    }

    // Regex patterns using source generators for performance
    [GeneratedRegex(@"^\s*class\s+(?<name>\w+)\s*\{?\s*$")]
    private static partial Regex ClassDefinitionRegex();

    // Stereotype regex: handles both single words and multi-word (e.g., "aggregate root")
    [GeneratedRegex(@"^\s*<<(?<stereotype>[\w\s]+)>>\s*$")]
    private static partial Regex StereotypeRegex();

    // Property regex: supports two formats:
    // Format 1: +name : Type (standard Mermaid) - Examples: -ID, +name : String
    // Format 2: +Type name [annotations] (alternative) - Examples: +String BaseStayKey PK,FK
    [GeneratedRegex(@"^\s*(?<visibility>[+\-#~])(?:(?<type1>\w+)\s+(?<name1>\w+)(?:\s+\S+)*|(?<name2>\w+)(?:\s*:\s*(?<type2>.+?))?)\s*$")]
    private static partial Regex PropertyRegex();

    [GeneratedRegex(@"^\s*(?<visibility>[+\-#~])(?<name>\w+)\s*\((?<params>[^)]*)\)\s*(?<return>\S+)?(?<modifiers>[$*])?.*$")]
    private static partial Regex MethodRegex();

    [GeneratedRegex(@"^\s*(?<source>\w+)\s*(?<sourceMult>""[^""]*"")?\s*(?<arrow><?\|?[-\.]+[\*o]?(?:\|?>?|>))\s*(?<targetMult>""[^""]*"")?\s*(?<target>\w+)\s*(?::\s*(?<label>.+))?\s*$")]
    private static partial Regex RelationshipRegex();

    [GeneratedRegex(@"^(?<base>\w+)~(?<arg>[^~]+)~$")]
    private static partial Regex GenericTypeRegex();
}
