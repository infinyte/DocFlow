using System.Text;
using DocFlow.Core.CanonicalModel;
using DocFlow.Integration.Mapping;
using DocFlow.Integration.Models;
using DocFlow.Integration.Schemas;

namespace DocFlow.Integration.CodeGen;

/// <summary>
/// Generates integration code from OpenAPI specs and CDM mappings.
/// </summary>
public sealed class IntegrationCodeGenerator
{
    /// <summary>
    /// Options for code generation
    /// </summary>
    public sealed class GeneratorOptions
    {
        public string Namespace { get; init; } = "Integration.Generated";
        public bool GenerateDtos { get; init; } = true;
        public bool GenerateMappers { get; init; } = true;
        public bool GenerateClient { get; init; } = true;
        public bool GenerateValidators { get; init; } = true;
    }

    /// <summary>
    /// Result of code generation
    /// </summary>
    public sealed class GeneratorResult
    {
        public List<GeneratedFile> Files { get; init; } = [];
        public int TotalLines { get; init; }
        public int MappingsNeedingReview { get; init; }
        public bool Success { get; init; }
        public List<string> Errors { get; init; } = [];
    }

    /// <summary>
    /// A generated file
    /// </summary>
    public sealed class GeneratedFile
    {
        public required string RelativePath { get; init; }
        public required string Content { get; init; }
        public int LineCount => Content.Split('\n').Length;
        public bool IsNew { get; init; } = true;
    }

    private readonly GeneratorOptions _options;

    public IntegrationCodeGenerator(GeneratorOptions? options = null)
    {
        _options = options ?? new GeneratorOptions();
    }

    /// <summary>
    /// Generate integration code from parsed schema and mappings
    /// </summary>
    public GeneratorResult Generate(
        SchemaParseResult schemaResult,
        SemanticModel cdmModel,
        MappingResult mappingResult)
    {
        var files = new List<GeneratedFile>();
        var errors = new List<string>();
        var mappingsNeedingReview = 0;

        try
        {
            var systemName = SanitizeName(schemaResult.ExternalSystem?.Name ?? "External");

            // Generate DTOs
            if (_options.GenerateDtos)
            {
                foreach (var entity in schemaResult.Model.Entities.Values)
                {
                    var dtoFile = GenerateDto(entity, schemaResult.ExternalSystem);
                    files.Add(dtoFile);
                }
            }

            // Generate mapping profile
            if (_options.GenerateMappers)
            {
                var (mapperFile, reviewCount) = GenerateMappingProfile(
                    systemName, schemaResult, cdmModel, mappingResult);
                files.Add(mapperFile);
                mappingsNeedingReview = reviewCount;
            }

            // Generate client interface
            if (_options.GenerateClient && schemaResult.Endpoints.Count > 0)
            {
                var clientFile = GenerateClientInterface(systemName, schemaResult);
                files.Add(clientFile);
            }

            // Generate validators
            if (_options.GenerateValidators)
            {
                foreach (var entity in schemaResult.Model.Entities.Values)
                {
                    var validatorFile = GenerateValidator(entity);
                    if (validatorFile != null)
                    {
                        files.Add(validatorFile);
                    }
                }
            }

            return new GeneratorResult
            {
                Files = files,
                TotalLines = files.Sum(f => f.LineCount),
                MappingsNeedingReview = mappingsNeedingReview,
                Success = true
            };
        }
        catch (Exception ex)
        {
            errors.Add(ex.Message);
            return new GeneratorResult
            {
                Files = files,
                TotalLines = files.Sum(f => f.LineCount),
                Success = false,
                Errors = errors
            };
        }
    }

    private GeneratedFile GenerateDto(SemanticEntity entity, ExternalSystemInfo? systemInfo)
    {
        var sb = new StringBuilder();
        var dtoName = $"{entity.Name}Dto";

        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"// Generated from: {systemInfo?.Name ?? "API"} v{systemInfo?.Version ?? "1.0"}");
        sb.AppendLine($"// Entity: {entity.Name}");
        sb.AppendLine();
        sb.AppendLine($"namespace {_options.Namespace}.External;");
        sb.AppendLine();

        if (!string.IsNullOrEmpty(entity.Description))
        {
            sb.AppendLine("/// <summary>");
            sb.AppendLine($"/// {entity.Description}");
            sb.AppendLine("/// </summary>");
        }

        sb.AppendLine($"public class {dtoName}");
        sb.AppendLine("{");

        foreach (var prop in entity.Properties)
        {
            // Get JSON property name from attribute if available
            var jsonName = prop.Attributes
                .FirstOrDefault(a => a.Name == "JsonPropertyName")
                ?.Arguments.GetValueOrDefault("name")?.ToString()
                ?? ToCamelCase(prop.Name);

            if (!string.IsNullOrEmpty(prop.Description))
            {
                sb.AppendLine($"    /// <summary>{prop.Description}</summary>");
            }

            sb.AppendLine($"    [JsonPropertyName(\"{jsonName}\")]");
            sb.AppendLine($"    public {MapToCSharpType(prop.Type)} {prop.Name} {{ get; set; }}{GetDefaultValue(prop.Type)}");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return new GeneratedFile
        {
            RelativePath = $"External/{dtoName}.cs",
            Content = sb.ToString()
        };
    }

    private (GeneratedFile file, int reviewCount) GenerateMappingProfile(
        string systemName,
        SchemaParseResult schemaResult,
        SemanticModel cdmModel,
        MappingResult mappingResult)
    {
        var sb = new StringBuilder();
        var reviewCount = 0;
        var profileName = $"{systemName}MappingProfile";

        sb.AppendLine("using AutoMapper;");
        sb.AppendLine($"using {_options.Namespace}.External;");
        sb.AppendLine();
        sb.AppendLine("// Generated mapping profile");
        sb.AppendLine($"// Overall confidence: {(int)(mappingResult.ConfidenceReport.OverallConfidence * 100)}%");
        sb.AppendLine();
        sb.AppendLine($"namespace {_options.Namespace}.Mapping;");
        sb.AppendLine();
        sb.AppendLine($"public class {profileName} : Profile");
        sb.AppendLine("{");
        sb.AppendLine($"    public {profileName}()");
        sb.AppendLine("    {");

        foreach (var mapping in mappingResult.EntityMappings)
        {
            var dtoName = $"{mapping.ExternalEntityName}Dto";
            var cdmEntity = cdmModel.Entities.Values
                .FirstOrDefault(e => e.Name == mapping.CdmEntityName);

            var confidence = (int)(mapping.Confidence * 100);
            sb.AppendLine();
            sb.AppendLine($"        // {mapping.ExternalEntityName} -> {mapping.CdmEntityName} ({confidence}% confidence)");
            sb.AppendLine($"        CreateMap<{dtoName}, {mapping.CdmEntityName}>()");

            var fieldMappings = mapping.FieldMappings
                .Where(f => f.TargetField != "???")
                .ToList();

            var unmappedFields = mapping.FieldMappings
                .Where(f => f.TargetField == "???")
                .ToList();

            // Track mapped target fields to prevent duplicates
            var mappedTargets = new HashSet<string>();

            // Generate field mappings
            for (int i = 0; i < fieldMappings.Count; i++)
            {
                var field = fieldMappings[i];

                // Skip if this target field was already mapped (prevent duplicates)
                if (!mappedTargets.Add(field.TargetField))
                {
                    continue;
                }

                var fieldConfidence = (int)(field.Confidence * 100);
                var isLast = i == fieldMappings.Count - 1 && unmappedFields.Count == 0;

                // Check if it needs special mapping (enum, type conversion, etc.)
                var needsConversion = NeedsTypeConversion(field, cdmEntity, out var enumTypeName);

                if (needsConversion && !string.IsNullOrEmpty(enumTypeName))
                {
                    sb.AppendLine($"            .ForMember(d => d.{field.TargetField}, opt => opt.MapFrom(s => Map{enumTypeName}(s.{field.SourceField})))  // {fieldConfidence}% - {field.Reasoning}");
                }
                else
                {
                    sb.AppendLine($"            .ForMember(d => d.{field.TargetField}, opt => opt.MapFrom(s => s.{field.SourceField}))  // {fieldConfidence}% - {field.Reasoning}");
                }
            }

            // Add TODO comments for unmapped fields
            foreach (var field in unmappedFields)
            {
                sb.AppendLine($"            // TODO: Manual mapping required for {field.SourceField} (no target field)");
                reviewCount++;
            }

            // Check for CDM fields with no source
            if (cdmEntity != null)
            {
                // Reuse mappedTargets which already contains all mapped target fields
                var unmappedCdmFields = cdmEntity.Properties
                    .Where(p => !mappedTargets.Contains(p.Name))
                    .ToList();

                foreach (var cdmField in unmappedCdmFields)
                {
                    sb.AppendLine($"            // TODO: Manual mapping required for {cdmField.Name} (no source field)");
                    sb.AppendLine($"            .ForMember(d => d.{cdmField.Name}, opt => opt.Ignore())");
                    reviewCount++;
                }
            }

            sb.AppendLine("            ;");
        }

        sb.AppendLine("    }");

        // Generate helper methods for enum/type conversions
        var enumMappings = GenerateEnumMappingMethods(mappingResult, schemaResult.Model, cdmModel);
        if (!string.IsNullOrEmpty(enumMappings))
        {
            sb.AppendLine();
            sb.Append(enumMappings);
        }

        sb.AppendLine("}");

        return (new GeneratedFile
        {
            RelativePath = $"Mapping/{profileName}.cs",
            Content = sb.ToString()
        }, reviewCount);
    }

    private GeneratedFile GenerateClientInterface(string systemName, SchemaParseResult schemaResult)
    {
        var sb = new StringBuilder();
        var interfaceName = $"I{systemName}Client";

        sb.AppendLine($"using {_options.Namespace}.External;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_options.Namespace}.Client;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// HTTP client interface for {schemaResult.ExternalSystem?.Name ?? systemName}");
        sb.AppendLine("/// </summary>");
        sb.AppendLine($"public interface {interfaceName}");
        sb.AppendLine("{");

        foreach (var endpoint in schemaResult.Endpoints)
        {
            var methodName = GenerateMethodName(endpoint);
            var returnType = GenerateReturnType(endpoint);
            var parameters = GenerateParameters(endpoint);

            if (!string.IsNullOrEmpty(endpoint.Summary))
            {
                sb.AppendLine($"    /// <summary>{endpoint.Summary}</summary>");
            }
            if (!string.IsNullOrEmpty(endpoint.Description))
            {
                sb.AppendLine($"    /// <remarks>{endpoint.Description}</remarks>");
            }

            sb.AppendLine($"    Task<{returnType}> {methodName}Async({parameters}CancellationToken ct = default);");
            sb.AppendLine();
        }

        sb.AppendLine("}");

        return new GeneratedFile
        {
            RelativePath = $"Client/{interfaceName}.cs",
            Content = sb.ToString()
        };
    }

    private GeneratedFile? GenerateValidator(SemanticEntity entity)
    {
        // Only generate if there are validation rules
        var requiredProps = entity.Properties.Where(p => p.IsRequired).ToList();
        var stringProps = entity.Properties.Where(p => p.Type.Name == "string").ToList();

        if (requiredProps.Count == 0 && stringProps.Count == 0)
        {
            return null;
        }

        var sb = new StringBuilder();
        var dtoName = $"{entity.Name}Dto";
        var validatorName = $"{dtoName}Validator";

        sb.AppendLine("using FluentValidation;");
        sb.AppendLine($"using {_options.Namespace}.External;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_options.Namespace}.Validation;");
        sb.AppendLine();
        sb.AppendLine($"public class {validatorName} : AbstractValidator<{dtoName}>");
        sb.AppendLine("{");
        sb.AppendLine($"    public {validatorName}()");
        sb.AppendLine("    {");

        foreach (var prop in entity.Properties)
        {
            var rules = new List<string>();

            if (prop.IsRequired)
            {
                if (prop.Type.Name == "string")
                {
                    rules.Add("NotEmpty()");
                }
                else
                {
                    rules.Add("NotNull()");
                }
            }

            // Add MaximumLength for strings (default 500)
            if (prop.Type.Name == "string" && !prop.Type.IsCollection)
            {
                rules.Add("MaximumLength(500)");
            }

            if (rules.Count > 0)
            {
                sb.AppendLine($"        RuleFor(x => x.{prop.Name}).{string.Join(".", rules)};");
            }
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return new GeneratedFile
        {
            RelativePath = $"Validation/{validatorName}.cs",
            Content = sb.ToString()
        };
    }

    private string GenerateEnumMappingMethods(
        MappingResult mappingResult,
        SemanticModel externalModel,
        SemanticModel cdmModel)
    {
        var sb = new StringBuilder();

        // Track which enum types we've already generated methods for
        var generatedEnumMethods = new HashSet<string>();

        // Find enum mappings by looking at CDM entities with enum properties
        foreach (var mapping in mappingResult.EntityMappings)
        {
            var cdmEntity = cdmModel.Entities.Values
                .FirstOrDefault(e => e.Name == mapping.CdmEntityName);

            if (cdmEntity == null) continue;

            foreach (var field in mapping.FieldMappings.Where(f => f.TargetField != "???"))
            {
                var cdmProp = cdmEntity.Properties.FirstOrDefault(p => p.Name == field.TargetField);
                if (cdmProp == null) continue;

                // Check if CDM property is an enum type
                var cdmEnumEntity = cdmModel.Entities.Values
                    .FirstOrDefault(e => e.Name == cdmProp.Type.Name &&
                        e.Classification == EntityClassification.Enum);

                if (cdmEnumEntity != null)
                {
                    // Skip if we've already generated a method for this enum type
                    if (!generatedEnumMethods.Add(cdmProp.Type.Name))
                    {
                        continue;
                    }

                    // Generate enum mapping method using enum type name (not field name)
                    sb.AppendLine($"    private static {cdmProp.Type.Name} Map{cdmProp.Type.Name}(string value) => value?.ToLowerInvariant() switch");
                    sb.AppendLine("    {");

                    foreach (var enumValue in cdmEnumEntity.Properties)
                    {
                        var apiValue = ToCamelCase(enumValue.Name);
                        sb.AppendLine($"        \"{apiValue}\" => {cdmProp.Type.Name}.{enumValue.Name},");
                    }

                    var defaultValue = cdmEnumEntity.Properties.FirstOrDefault()?.Name ?? "Unknown";
                    sb.AppendLine($"        _ => {cdmProp.Type.Name}.{defaultValue}");
                    sb.AppendLine("    };");
                    sb.AppendLine();
                }
            }
        }

        return sb.ToString();
    }

    private static bool NeedsTypeConversion(FieldMapping field, SemanticEntity? cdmEntity, out string? enumTypeName)
    {
        enumTypeName = null;
        if (cdmEntity == null) return false;

        var cdmProp = cdmEntity.Properties.FirstOrDefault(p => p.Name == field.TargetField);
        if (cdmProp == null) return false;

        // Check if it's likely an enum (non-primitive, not a collection)
        var needsConversion = !cdmProp.Type.IsPrimitive &&
                              !cdmProp.Type.IsCollection &&
                              cdmProp.Type.Name != "string";

        if (needsConversion)
        {
            enumTypeName = cdmProp.Type.Name;
        }

        return needsConversion;
    }

    private static string GenerateMethodName(ApiEndpoint endpoint)
    {
        // Generate method name from path and method
        var path = endpoint.Path.Trim('/');
        var parts = path.Split('/');

        var nameParts = new List<string>();

        // Add HTTP method prefix
        nameParts.Add(endpoint.Method.ToString());

        foreach (var part in parts)
        {
            if (part.StartsWith("{") && part.EndsWith("}"))
            {
                // Path parameter - skip or use "ById"
                nameParts.Add("ById");
            }
            else if (!string.IsNullOrEmpty(part))
            {
                nameParts.Add(ToPascalCase(part));
            }
        }

        return string.Concat(nameParts);
    }

    private static string GenerateReturnType(ApiEndpoint endpoint)
    {
        if (!string.IsNullOrEmpty(endpoint.ResponseEntityId))
        {
            return $"{endpoint.ResponseEntityId}Dto?";
        }

        return endpoint.Method switch
        {
            Models.HttpMethod.Delete => "bool",
            Models.HttpMethod.Post => "bool",
            Models.HttpMethod.Put => "bool",
            _ => "object?"
        };
    }

    private static string GenerateParameters(ApiEndpoint endpoint)
    {
        var parameters = new List<string>();

        // Add path parameters
        foreach (var param in endpoint.PathParameters)
        {
            parameters.Add($"{MapToCSharpType(param.Type)} {ToCamelCase(param.Name)}");
        }

        // Add request body if present
        if (!string.IsNullOrEmpty(endpoint.RequestEntityId))
        {
            parameters.Add($"{endpoint.RequestEntityId}Dto {ToCamelCase(endpoint.RequestEntityId)}");
        }

        // Add query parameters (optional)
        foreach (var param in endpoint.QueryParameters.Where(p => p.IsRequired))
        {
            parameters.Add($"{MapToCSharpType(param.Type)} {ToCamelCase(param.Name)}");
        }

        if (parameters.Count > 0)
        {
            return string.Join(", ", parameters) + ", ";
        }

        return "";
    }

    private static string MapToCSharpType(SemanticType type)
    {
        if (type.IsCollection)
        {
            var elementType = type.GenericArguments.FirstOrDefault();
            var elementCsType = elementType != null ? MapToCSharpType(elementType) : "object";
            return $"List<{elementCsType}>";
        }

        if (!string.IsNullOrEmpty(type.ReferencedEntityId))
        {
            return $"{type.ReferencedEntityId}Dto{(type.IsNullable ? "?" : "")}";
        }

        var csType = type.Name switch
        {
            "string" => "string",
            "int" => "int",
            "long" => "long",
            "decimal" => "decimal",
            "bool" => "bool",
            "DateTime" => "DateTime",
            "Guid" => "Guid",
            _ => type.Name
        };

        if (type.IsNullable && csType != "string")
        {
            csType += "?";
        }

        return csType;
    }

    private static string GetDefaultValue(SemanticType type)
    {
        if (type.IsCollection) return " = [];";
        if (type.Name == "string") return " = \"\";";
        if (!type.IsPrimitive && !type.IsNullable) return " = null!;";
        return "";
    }

    private static string SanitizeName(string name)
    {
        // Remove spaces, special characters
        return string.Concat(name.Split(' ', '-', '_', '.')
            .Select(ToPascalCase));
    }

    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToUpperInvariant(name[0]) + name[1..];
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}
