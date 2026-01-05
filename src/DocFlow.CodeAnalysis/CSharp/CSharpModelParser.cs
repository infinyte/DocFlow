using System.Diagnostics;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RoslynSemanticModel = Microsoft.CodeAnalysis.SemanticModel;
using CanonicalSemanticModel = DocFlow.Core.CanonicalModel.SemanticModel;

namespace DocFlow.CodeAnalysis.CSharp;

/// <summary>
/// Parses C# source code into a SemanticModel using Roslyn.
/// Extracts classes, records, interfaces, enums, properties, methods, and relationships.
/// </summary>
public sealed class CSharpModelParser : IModelParser
{
    public string SourceFormat => "CSharp";
    public IReadOnlyList<string> SupportedExtensions => [".cs"];

    public bool CanParse(ParserInput input)
    {
        if (input.Content != null) return true;
        if (input.FilePath != null) return input.FilePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase);
        return false;
    }

    public async Task<ParseResult> ParseAsync(ParserInput input, ParserOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ParserOptions();
        var stopwatch = Stopwatch.StartNew();

        string sourceCode;
        if (input.Content != null)
        {
            sourceCode = input.Content;
        }
        else if (input.FilePath != null)
        {
            sourceCode = await File.ReadAllTextAsync(input.FilePath, cancellationToken);
        }
        else
        {
            return ParseResult.Failed(new ParseError { Code = "NO_INPUT", Message = "No content or file path provided" });
        }

        var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
        var root = await syntaxTree.GetRootAsync(cancellationToken);

        var compilation = CSharpCompilation.Create("TempAssembly")
            .AddReferences(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddSyntaxTrees(syntaxTree);

        var roslynModel = compilation.GetSemanticModel(syntaxTree);

        var model = new CanonicalSemanticModel
        {
            Name = input.FilePath != null ? Path.GetFileNameWithoutExtension(input.FilePath) : "ParsedModel",
            Provenance = new ModelProvenance
            {
                SourceFormat = SourceFormat,
                SourceFiles = input.FilePath != null ? [input.FilePath] : [],
                ToolVersion = "1.0.0"
            }
        };

        var errors = new List<ParseError>();
        var warnings = new List<ParseWarning>();
        var entityMap = new Dictionary<string, SemanticEntity>();

        // First pass: collect all type declarations
        var typeDeclarations = root.DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .ToList();

        foreach (var typeDecl in typeDeclarations)
        {
            var entity = ParseTypeDeclaration(typeDecl, roslynModel, options, input.FilePath);
            if (entity != null)
            {
                model.AddEntity(entity);
                var fullName = GetFullTypeName(typeDecl, roslynModel);
                entityMap[fullName] = entity;
            }
        }

        // Parse enums
        var enumDeclarations = root.DescendantNodes().OfType<EnumDeclarationSyntax>();
        foreach (var enumDecl in enumDeclarations)
        {
            var entity = ParseEnumDeclaration(enumDecl, input.FilePath);
            if (entity != null)
            {
                model.AddEntity(entity);
                var symbol = roslynModel.GetDeclaredSymbol(enumDecl);
                if (symbol != null)
                {
                    entityMap[symbol.ToDisplayString()] = entity;
                }
            }
        }

        // Second pass: establish relationships
        foreach (var typeDecl in typeDeclarations)
        {
            var fullName = GetFullTypeName(typeDecl, roslynModel);
            if (!entityMap.TryGetValue(fullName, out var sourceEntity)) continue;

            // Inheritance relationships
            if (typeDecl.BaseList != null)
            {
                foreach (var baseType in typeDecl.BaseList.Types)
                {
                    var typeInfo = roslynModel.GetTypeInfo(baseType.Type);
                    var baseTypeName = typeInfo.Type?.ToDisplayString() ?? baseType.Type.ToString();

                    if (entityMap.TryGetValue(baseTypeName, out var targetEntity))
                    {
                        var isInterface = typeInfo.Type?.TypeKind == TypeKind.Interface;
                        model.AddRelationship(
                            sourceEntity.Id,
                            targetEntity.Id,
                            isInterface ? RelationshipType.Implementation : RelationshipType.Inheritance,
                            isInterface ? "implements" : "extends"
                        );
                    }
                }
            }

            // Property-based relationships
            foreach (var property in sourceEntity.Properties)
            {
                var relationship = InferRelationshipFromProperty(property, sourceEntity, entityMap, model);
                if (relationship != null)
                {
                    model.Relationships.Add(relationship);
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

    private SemanticEntity? ParseTypeDeclaration(TypeDeclarationSyntax typeDecl, RoslynSemanticModel roslynModel, ParserOptions options, string? filePath)
    {
        var symbol = roslynModel.GetDeclaredSymbol(typeDecl);
        if (symbol == null) return null;

        var entity = new SemanticEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = typeDecl.Identifier.Text,
            Description = GetXmlDocSummary(typeDecl),
            Classification = InferClassification(typeDecl, symbol),
            Source = options.IncludeSourceInfo ? new SourceInfo
            {
                SourceType = SourceFormat,
                FilePath = filePath,
                LineNumber = typeDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            } : null
        };

        // Add stereotypes
        if (typeDecl is InterfaceDeclarationSyntax)
            entity.Stereotypes.Add("interface");
        if (typeDecl is RecordDeclarationSyntax)
            entity.Stereotypes.Add("record");
        if (typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword))
            entity.Stereotypes.Add("abstract");
        if (typeDecl.Modifiers.Any(SyntaxKind.SealedKeyword))
            entity.Stereotypes.Add("sealed");

        // Parse properties
        var properties = typeDecl.Members.OfType<PropertyDeclarationSyntax>();
        foreach (var prop in properties)
        {
            var semanticProp = ParseProperty(prop, roslynModel);
            if (semanticProp != null)
            {
                entity.Properties.Add(semanticProp);
            }
        }

        // Parse record primary constructor parameters as properties
        if (typeDecl is RecordDeclarationSyntax recordDecl && recordDecl.ParameterList != null)
        {
            foreach (var param in recordDecl.ParameterList.Parameters)
            {
                var semanticProp = ParseRecordParameter(param, roslynModel);
                if (semanticProp != null)
                {
                    entity.Properties.Add(semanticProp);
                }
            }
        }

        // Parse methods
        var methods = typeDecl.Members.OfType<MethodDeclarationSyntax>();
        foreach (var method in methods)
        {
            var semanticOp = ParseMethod(method, roslynModel);
            if (semanticOp != null)
            {
                entity.Operations.Add(semanticOp);
            }
        }

        return entity;
    }

    private SemanticEntity? ParseEnumDeclaration(EnumDeclarationSyntax enumDecl, string? filePath)
    {
        var entity = new SemanticEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = enumDecl.Identifier.Text,
            Description = GetXmlDocSummary(enumDecl),
            Classification = EntityClassification.Enum,
            Source = new SourceInfo
            {
                SourceType = SourceFormat,
                FilePath = filePath,
                LineNumber = enumDecl.GetLocation().GetLineSpan().StartLinePosition.Line + 1
            }
        };

        entity.Stereotypes.Add("enumeration");

        // Add enum members as properties (convention for representing enum values)
        foreach (var member in enumDecl.Members)
        {
            entity.Properties.Add(new SemanticProperty
            {
                Name = member.Identifier.Text,
                Type = new SemanticType { Name = enumDecl.Identifier.Text },
                Visibility = Visibility.Public,
                Semantics = PropertySemantics.State
            });
        }

        return entity;
    }

    private SemanticProperty? ParseProperty(PropertyDeclarationSyntax prop, RoslynSemanticModel roslynModel)
    {
        var typeInfo = roslynModel.GetTypeInfo(prop.Type);
        var typeName = typeInfo.Type?.ToDisplayString() ?? prop.Type.ToString();

        var semanticType = ParseType(typeName, typeInfo.Type);
        var visibility = GetVisibility(prop.Modifiers);

        var semanticProp = new SemanticProperty
        {
            Name = prop.Identifier.Text,
            Type = semanticType,
            Description = GetXmlDocSummary(prop),
            Visibility = visibility,
            IsRequired = HasRequiredAttribute(prop) || prop.Modifiers.Any(SyntaxKind.RequiredKeyword),
            IsReadOnly = IsReadOnlyProperty(prop),
            Semantics = InferPropertySemantics(prop.Identifier.Text, semanticType)
        };

        // Parse attributes
        foreach (var attrList in prop.AttributeLists)
        {
            foreach (var attr in attrList.Attributes)
            {
                semanticProp.Attributes.Add(new SemanticAttribute
                {
                    Name = attr.Name.ToString()
                });
            }
        }

        return semanticProp;
    }

    private SemanticProperty? ParseRecordParameter(ParameterSyntax param, RoslynSemanticModel roslynModel)
    {
        if (param.Type == null) return null;

        var typeInfo = roslynModel.GetTypeInfo(param.Type);
        var typeName = typeInfo.Type?.ToDisplayString() ?? param.Type.ToString();
        var semanticType = ParseType(typeName, typeInfo.Type);

        return new SemanticProperty
        {
            Name = param.Identifier.Text,
            Type = semanticType,
            Visibility = Visibility.Public,
            IsRequired = true, // Record parameters are typically required
            IsReadOnly = true, // Record properties are init-only
            Semantics = InferPropertySemantics(param.Identifier.Text, semanticType)
        };
    }

    private SemanticOperation? ParseMethod(MethodDeclarationSyntax method, RoslynSemanticModel roslynModel)
    {
        // Skip property accessors and compiler-generated methods
        if (method.Modifiers.Any(SyntaxKind.PrivateKeyword) &&
            (method.Identifier.Text.StartsWith("get_") || method.Identifier.Text.StartsWith("set_")))
            return null;

        var returnTypeInfo = roslynModel.GetTypeInfo(method.ReturnType);
        var returnTypeName = returnTypeInfo.Type?.ToDisplayString() ?? method.ReturnType.ToString();

        var operation = new SemanticOperation
        {
            Name = method.Identifier.Text,
            ReturnType = returnTypeName == "void" ? null : ParseType(returnTypeName, returnTypeInfo.Type),
            Description = GetXmlDocSummary(method),
            Visibility = GetVisibility(method.Modifiers),
            IsAbstract = method.Modifiers.Any(SyntaxKind.AbstractKeyword),
            IsStatic = method.Modifiers.Any(SyntaxKind.StaticKeyword),
            IsAsync = method.Modifiers.Any(SyntaxKind.AsyncKeyword)
        };

        foreach (var param in method.ParameterList.Parameters)
        {
            if (param.Type == null) continue;
            var paramTypeInfo = roslynModel.GetTypeInfo(param.Type);
            var paramTypeName = paramTypeInfo.Type?.ToDisplayString() ?? param.Type.ToString();

            operation.Parameters.Add(new SemanticParameter
            {
                Name = param.Identifier.Text,
                Type = ParseType(paramTypeName, paramTypeInfo.Type),
                IsOptional = param.Default != null,
                DefaultValue = param.Default?.Value.ToString()
            });
        }

        return operation;
    }

    private SemanticType ParseType(string typeName, ITypeSymbol? typeSymbol)
    {
        var isNullable = typeName.EndsWith("?") ||
                         (typeSymbol?.NullableAnnotation == NullableAnnotation.Annotated);
        var baseName = typeName.TrimEnd('?');

        // Check for collection types
        if (IsCollectionType(baseName, typeSymbol))
        {
            var elementType = GetCollectionElementType(baseName, typeSymbol);
            return new SemanticType
            {
                Name = GetCollectionTypeName(baseName),
                IsCollection = true,
                IsNullable = isNullable,
                GenericArguments = elementType != null ? [elementType] : []
            };
        }

        // Check for primitive types
        var isPrimitive = IsPrimitiveType(baseName);

        return new SemanticType
        {
            Name = SimplifyTypeName(baseName),
            IsPrimitive = isPrimitive,
            IsNullable = isNullable
        };
    }

    private bool IsCollectionType(string typeName, ITypeSymbol? typeSymbol)
    {
        var collectionPatterns = new[] { "ICollection", "IList", "List", "IEnumerable", "IReadOnlyList", "IReadOnlyCollection", "HashSet", "ISet" };
        return collectionPatterns.Any(p => typeName.Contains(p + "<")) ||
               (typeSymbol is IArrayTypeSymbol);
    }

    private string GetCollectionTypeName(string typeName)
    {
        if (typeName.Contains("ICollection")) return "ICollection";
        if (typeName.Contains("IList") || typeName.Contains("List<")) return "List";
        if (typeName.Contains("IEnumerable")) return "IEnumerable";
        if (typeName.Contains("HashSet") || typeName.Contains("ISet")) return "Set";
        if (typeName.EndsWith("[]")) return "Array";
        return "Collection";
    }

    private SemanticType? GetCollectionElementType(string typeName, ITypeSymbol? typeSymbol)
    {
        // Handle arrays
        if (typeSymbol is IArrayTypeSymbol arrayType)
        {
            return ParseType(arrayType.ElementType.ToDisplayString(), arrayType.ElementType);
        }

        // Handle generic collections
        if (typeSymbol is INamedTypeSymbol namedType && namedType.TypeArguments.Length > 0)
        {
            var elementType = namedType.TypeArguments[0];
            return ParseType(elementType.ToDisplayString(), elementType);
        }

        // Try to parse from string
        var genericStart = typeName.IndexOf('<');
        var genericEnd = typeName.LastIndexOf('>');
        if (genericStart > 0 && genericEnd > genericStart)
        {
            var elementTypeName = typeName.Substring(genericStart + 1, genericEnd - genericStart - 1);
            return ParseType(elementTypeName, null);
        }

        return null;
    }

    private bool IsPrimitiveType(string typeName)
    {
        var primitives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "string", "int", "long", "short", "byte", "float", "double", "decimal",
            "bool", "boolean", "char", "DateTime", "DateTimeOffset", "TimeSpan",
            "Guid", "int32", "int64", "int16", "uint", "uint32", "uint64",
            "System.String", "System.Int32", "System.Int64", "System.Boolean",
            "System.Decimal", "System.Double", "System.Single", "System.Guid",
            "System.DateTime", "System.DateTimeOffset", "System.TimeSpan"
        };
        return primitives.Contains(typeName.Split('.').Last());
    }

    private string SimplifyTypeName(string typeName)
    {
        // Remove namespace prefixes for common types
        var simplified = typeName
            .Replace("System.Collections.Generic.", "")
            .Replace("System.", "");

        // Get just the type name for simple types
        if (!simplified.Contains('<') && simplified.Contains('.'))
        {
            simplified = simplified.Split('.').Last();
        }

        return simplified;
    }

    private EntityClassification InferClassification(TypeDeclarationSyntax typeDecl, INamedTypeSymbol symbol)
    {
        var name = typeDecl.Identifier.Text;

        // Interface
        if (typeDecl is InterfaceDeclarationSyntax)
            return EntityClassification.Interface;

        // Record (likely ValueObject)
        if (typeDecl is RecordDeclarationSyntax)
        {
            // Records with Id might be entities
            var hasId = typeDecl.Members.OfType<PropertyDeclarationSyntax>()
                .Any(p => p.Identifier.Text.Equals("Id", StringComparison.OrdinalIgnoreCase));
            if (hasId) return EntityClassification.Entity;
            return EntityClassification.ValueObject;
        }

        // Check for DDD pattern names
        if (name.EndsWith("Repository")) return EntityClassification.Repository;
        if (name.EndsWith("Service")) return EntityClassification.DomainService;
        if (name.EndsWith("Factory")) return EntityClassification.Factory;
        if (name.EndsWith("Event") || name.EndsWith("Occurred") || name.EndsWith("Created") || name.EndsWith("Updated"))
            return EntityClassification.DomainEvent;
        if (name.EndsWith("Command")) return EntityClassification.Command;
        if (name.EndsWith("Query")) return EntityClassification.Query;
        if (name.EndsWith("Handler")) return EntityClassification.Handler;
        if (name.EndsWith("Dto") || name.EndsWith("DTO")) return EntityClassification.DataTransferObject;
        if (name.EndsWith("ViewModel") || name.EndsWith("VM")) return EntityClassification.ViewModel;
        if (name.EndsWith("Validator")) return EntityClassification.Validator;
        if (name.EndsWith("Mapper")) return EntityClassification.Mapper;
        if (name.EndsWith("Controller")) return EntityClassification.Controller;
        if (name.EndsWith("Specification") || name.EndsWith("Spec")) return EntityClassification.Specification;

        // Check for Id property (Entity or AggregateRoot)
        var properties = typeDecl.Members.OfType<PropertyDeclarationSyntax>().ToList();
        var hasIdProperty = properties.Any(p =>
            p.Identifier.Text.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
            p.Identifier.Text.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && p.Identifier.Text.Length <= 5);

        if (hasIdProperty)
        {
            // Check if it has collection properties of other entities (likely AggregateRoot)
            var hasCollectionOfEntities = properties.Any(p =>
            {
                var typeStr = p.Type.ToString();
                return (typeStr.Contains("ICollection<") || typeStr.Contains("List<") || typeStr.Contains("IList<")) &&
                       !IsPrimitiveType(typeStr);
            });

            return hasCollectionOfEntities ? EntityClassification.AggregateRoot : EntityClassification.Entity;
        }

        // Abstract class
        if (typeDecl.Modifiers.Any(SyntaxKind.AbstractKeyword))
            return EntityClassification.AbstractClass;

        return EntityClassification.Class;
    }

    private PropertySemantics InferPropertySemantics(string propertyName, SemanticType type)
    {
        var lowerName = propertyName.ToLowerInvariant();

        if (lowerName == "id" || lowerName.EndsWith("id") && propertyName.Length <= 5)
            return PropertySemantics.Identity;

        if (lowerName.Contains("createdat") || lowerName.Contains("updatedat") ||
            lowerName.Contains("created") || lowerName.Contains("modified") ||
            lowerName.Contains("timestamp"))
            return PropertySemantics.Audit;

        if (lowerName == "version" || lowerName == "rowversion" || lowerName == "concurrencytoken")
            return PropertySemantics.Version;

        if (type.IsCollection)
            return PropertySemantics.Collection;

        if (!type.IsPrimitive && !type.IsNullable)
            return PropertySemantics.Navigation;

        return PropertySemantics.State;
    }

    private SemanticRelationship? InferRelationshipFromProperty(SemanticProperty property, SemanticEntity sourceEntity, Dictionary<string, SemanticEntity> entityMap, CanonicalSemanticModel model)
    {
        // Skip primitive types and identity properties
        if (property.Type.IsPrimitive || property.Semantics == PropertySemantics.Identity)
            return null;

        SemanticEntity? targetEntity = null;
        var targetTypeName = property.Type.IsCollection && property.Type.GenericArguments.Count > 0
            ? property.Type.GenericArguments[0].Name
            : property.Type.Name;

        // Find target entity
        foreach (var (name, entity) in entityMap)
        {
            if (name.EndsWith("." + targetTypeName) || name == targetTypeName || entity.Name == targetTypeName)
            {
                targetEntity = entity;
                break;
            }
        }

        if (targetEntity == null) return null;

        var relationType = property.Type.IsCollection
            ? RelationshipType.Composition  // Collections typically indicate composition (owned)
            : RelationshipType.Association; // Single references are associations

        return new SemanticRelationship
        {
            Id = Guid.NewGuid().ToString(),
            SourceEntityId = sourceEntity.Id,
            TargetEntityId = targetEntity.Id,
            Type = relationType,
            Name = property.Name,
            TargetRole = property.Name,
            SourceMultiplicity = Multiplicity.One,
            TargetMultiplicity = property.Type.IsCollection ? Multiplicity.ZeroOrMore : (property.Type.IsNullable ? Multiplicity.ZeroOrOne : Multiplicity.One),
            IsBidirectional = false
        };
    }

    private Visibility GetVisibility(SyntaxTokenList modifiers)
    {
        if (modifiers.Any(SyntaxKind.PublicKeyword)) return Visibility.Public;
        if (modifiers.Any(SyntaxKind.PrivateKeyword) && modifiers.Any(SyntaxKind.ProtectedKeyword)) return Visibility.PrivateProtected;
        if (modifiers.Any(SyntaxKind.ProtectedKeyword) && modifiers.Any(SyntaxKind.InternalKeyword)) return Visibility.ProtectedInternal;
        if (modifiers.Any(SyntaxKind.ProtectedKeyword)) return Visibility.Protected;
        if (modifiers.Any(SyntaxKind.InternalKeyword)) return Visibility.Internal;
        if (modifiers.Any(SyntaxKind.PrivateKeyword)) return Visibility.Private;
        return Visibility.Private; // Default in C#
    }

    private bool HasRequiredAttribute(PropertyDeclarationSyntax prop)
    {
        return prop.AttributeLists
            .SelectMany(al => al.Attributes)
            .Any(a => a.Name.ToString().Contains("Required"));
    }

    private bool IsReadOnlyProperty(PropertyDeclarationSyntax prop)
    {
        if (prop.AccessorList == null) return true; // Expression-bodied property

        var hasSetter = prop.AccessorList.Accessors
            .Any(a => a.Kind() == SyntaxKind.SetAccessorDeclaration);
        var hasInitOnly = prop.AccessorList.Accessors
            .Any(a => a.Kind() == SyntaxKind.InitAccessorDeclaration);

        return !hasSetter || hasInitOnly;
    }

    private string? GetXmlDocSummary(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia()
            .Select(t => t.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .FirstOrDefault();

        if (trivia == null) return null;

        var summaryElement = trivia.ChildNodes()
            .OfType<XmlElementSyntax>()
            .FirstOrDefault(e => e.StartTag.Name.ToString() == "summary");

        return summaryElement?.Content.ToString().Trim();
    }

    private string GetFullTypeName(TypeDeclarationSyntax typeDecl, RoslynSemanticModel roslynModel)
    {
        var symbol = roslynModel.GetDeclaredSymbol(typeDecl);
        return symbol?.ToDisplayString() ?? typeDecl.Identifier.Text;
    }
}
