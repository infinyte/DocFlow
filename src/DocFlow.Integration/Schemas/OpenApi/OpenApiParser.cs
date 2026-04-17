using System.Text.RegularExpressions;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Integration.Models;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;

namespace DocFlow.Integration.Schemas.OpenApi;

/// <summary>
/// Parses OpenAPI 3.x specifications into the semantic model.
/// </summary>
public sealed class OpenApiParser : ISchemaParser, IApiSpecParser
{
    // IApiSpecParser members (Phase 5: pluggable spec parsing).

    string IApiSpecParser.Name => "OpenAPI";

    bool IApiSpecParser.CanParse(string? path, string? content)
    {
        if (!string.IsNullOrEmpty(path))
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".json" or ".yaml" or ".yml") return true;
        }

        if (!string.IsNullOrEmpty(content))
        {
            return content.Contains("openapi", StringComparison.Ordinal)
                   || content.Contains("swagger", StringComparison.Ordinal);
        }

        return false;
    }

    async Task<SemanticModel> IApiSpecParser.ParseAsync(Stream input, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(input, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);

        var result = await ParseSchemaAsync(
            ParserInput.FromContent(content),
            options: null,
            cancellationToken);

        if (!result.Success)
        {
            var errors = string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Message}"));
            throw new FormatException($"Failed to parse OpenAPI spec. {errors}");
        }

        return result.Model;
    }

    private readonly ILogger<OpenApiParser>? _logger;
    
    public OpenApiParser(ILogger<OpenApiParser>? logger = null)
    {
        _logger = logger;
    }
    
    public string SourceFormat => "OpenAPI";
    public IReadOnlyList<string> SupportedExtensions => [".json", ".yaml", ".yml"];
    public IReadOnlyList<string> SupportedFormats => ["OpenAPI3", "OpenAPI3.0", "OpenAPI3.1"];
    
    public bool CanParse(ParserInput input)
    {
        if (input.FilePath is not null)
        {
            var ext = Path.GetExtension(input.FilePath).ToLowerInvariant();
            return SupportedExtensions.Contains(ext);
        }
        
        // Try to detect OpenAPI content
        if (input.Content is not null)
        {
            return input.Content.Contains("openapi") || input.Content.Contains("swagger");
        }
        
        return false;
    }
    
    public async Task<ParseResult> ParseAsync(
        ParserInput input,
        ParserOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        // Convert base ParseResult to SchemaParseResult
        var schemaResult = await ParseSchemaAsync(input, new SchemaParserOptions(), cancellationToken);
        return new ParseResult
        {
            Model = schemaResult.Model,
            Success = schemaResult.Success,
            Errors = schemaResult.Errors.ToList(),
            Warnings = schemaResult.Warnings.ToList(),
            Statistics = schemaResult.Statistics
        };
    }
    
    public async Task<SchemaParseResult> ParseSchemaAsync(
        ParserInput input, 
        SchemaParserOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new SchemaParserOptions();
        
        var model = new SemanticModel
        {
            Name = "OpenAPI Schema",
            Provenance = new ModelProvenance
            {
                SourceFormat = SourceFormat,
                CreatedAt = DateTime.UtcNow
            }
        };
        
        var endpoints = new List<ApiEndpoint>();
        var errors = new List<ParseError>();
        var warnings = new List<ParseWarning>();
        
        try
        {
            // Read the OpenAPI document
            OpenApiDocument document;
            
            if (input.FilePath is not null)
            {
                using var stream = File.OpenRead(input.FilePath);
                var readResult = await new OpenApiStreamReader().ReadAsync(stream, cancellationToken);
                document = readResult.OpenApiDocument;
                
                foreach (var diagnostic in readResult.OpenApiDiagnostic.Errors)
                {
                    errors.Add(new ParseError
                    {
                        Code = "OPENAPI_ERROR",
                        Message = diagnostic.Message
                    });
                }
            }
            else if (input.Content is not null)
            {
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(input.Content));
                var readResult = await new OpenApiStreamReader().ReadAsync(stream, cancellationToken);
                document = readResult.OpenApiDocument;
            }
            else
            {
                return new SchemaParseResult
                {
                    Model = model,
                    Success = false,
                    Errors = [new ParseError { Code = "NO_INPUT", Message = "No input provided" }]
                };
            }
            
            // Extract external system info
            var externalSystem = ExtractExternalSystemInfo(document);
            model.Name = externalSystem.Name;
            
            // Extract schemas as entities
            if (document.Components?.Schemas != null)
            {
                foreach (var (name, schema) in document.Components.Schemas)
                {
                    var entity = ParseSchemaToEntity(name, schema, options);
                    model.AddEntity(entity);
                }
            }
            
            // Extract endpoints
            foreach (var (path, pathItem) in document.Paths)
            {
                foreach (var (method, operation) in pathItem.Operations)
                {
                    if (ShouldIncludeEndpoint(path, options.EndpointFilter))
                    {
                        var endpoint = ParseEndpoint(path, method, operation, model);
                        endpoints.Add(endpoint);
                    }
                }
            }
            
            // Extract authentication
            var authConfig = ExtractAuthConfig(document);

            // Populate the canonical ApiSurface (additive — legacy ApiEndpoint list remains available).
            model.Api = BuildApiSurface(document, model);

            _logger?.LogInformation(
                "Parsed OpenAPI spec: {EntityCount} entities, {EndpointCount} endpoints",
                model.Entities.Count, endpoints.Count);
            
            return new SchemaParseResult
            {
                Model = model,
                Success = errors.Count == 0,
                Endpoints = endpoints,
                ExternalSystem = externalSystem,
                Authentication = authConfig,
                Errors = errors,
                Warnings = warnings,
                Statistics = new ParseStatistics
                {
                    EntitiesParsed = model.Entities.Count,
                    RelationshipsParsed = model.Relationships.Count
                }
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to parse OpenAPI spec");
            
            return new SchemaParseResult
            {
                Model = model,
                Success = false,
                Errors = [new ParseError { Code = "PARSE_EXCEPTION", Message = ex.Message }]
            };
        }
    }
    
    private static ExternalSystemInfo ExtractExternalSystemInfo(OpenApiDocument document)
    {
        var info = document.Info;
        var servers = document.Servers;
        
        return new ExternalSystemInfo
        {
            Name = info?.Title ?? "Unknown API",
            BaseUrl = servers?.FirstOrDefault()?.Url ?? "https://api.example.com",
            Version = info?.Version,
            DocumentationUrl = info?.Contact?.Url?.ToString(),
            Contact = info?.Contact?.Email
        };
    }
    
    private SemanticEntity ParseSchemaToEntity(
        string name, 
        OpenApiSchema schema,
        SchemaParserOptions options)
    {
        var entity = new SemanticEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = schema.Description,
            Classification = InferClassification(name, schema)
        };
        
        // Parse properties
        foreach (var (propName, propSchema) in schema.Properties)
        {
            var property = new SemanticProperty
            {
                Name = ToPascalCase(propName),
                Type = MapSchemaType(propSchema),
                Description = propSchema.Description,
                IsRequired = schema.Required?.Contains(propName) ?? false,
                Visibility = Visibility.Public
            };
            
            // Store original API name in metadata for mapping
            property.Attributes.Add(new SemanticAttribute
            {
                Name = "JsonPropertyName",
                Arguments = new Dictionary<string, object> { ["name"] = propName }
            });
            
            entity.Properties.Add(property);
        }
        
        return entity;
    }
    
    private static EntityClassification InferClassification(string name, OpenApiSchema schema)
    {
        // Simple heuristics - IMS will improve these over time
        var nameLower = name.ToLowerInvariant();
        
        if (nameLower.EndsWith("request")) return EntityClassification.Command;
        if (nameLower.EndsWith("response")) return EntityClassification.DataTransferObject;
        if (nameLower.EndsWith("event")) return EntityClassification.DomainEvent;
        if (nameLower.EndsWith("dto")) return EntityClassification.DataTransferObject;
        
        // Default to DTO for API schemas
        return EntityClassification.DataTransferObject;
    }
    
    private static SemanticType MapSchemaType(OpenApiSchema schema)
    {
        return schema.Type switch
        {
            "string" when schema.Format == "date-time" => SemanticType.DateTime,
            "string" when schema.Format == "uuid" => SemanticType.Guid,
            "string" => SemanticType.String,
            "integer" when schema.Format == "int64" => SemanticType.Long,
            "integer" => SemanticType.Int,
            "number" => SemanticType.Decimal,
            "boolean" => SemanticType.Bool,
            "array" => SemanticType.CollectionOf(
                schema.Items != null ? MapSchemaType(schema.Items) : SemanticType.String),
            _ when schema.Reference != null => SemanticType.EntityReference(
                schema.Reference.Id, schema.Reference.Id),
            _ => SemanticType.String
        };
    }
    
    private ApiEndpoint ParseEndpoint(
        string path,
        OperationType method,
        OpenApiOperation operation,
        SemanticModel model)
    {
        var endpoint = new ApiEndpoint
        {
            Id = Guid.NewGuid().ToString(),
            Path = path,
            Method = MapHttpMethod(method),
            Summary = operation.Summary,
            Description = operation.Description
        };
        
        // Parse path parameters
        foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Path))
        {
            endpoint.PathParameters.Add(new Models.ApiParameter
            {
                Name = param.Name,
                Type = param.Schema != null ? MapSchemaType(param.Schema) : SemanticType.String,
                IsRequired = param.Required,
                Description = param.Description
            });
        }

        // Parse query parameters
        foreach (var param in operation.Parameters.Where(p => p.In == ParameterLocation.Query))
        {
            endpoint.QueryParameters.Add(new Models.ApiParameter
            {
                Name = param.Name,
                Type = param.Schema != null ? MapSchemaType(param.Schema) : SemanticType.String,
                IsRequired = param.Required,
                Description = param.Description
            });
        }
        
        // Parse request body
        if (operation.RequestBody?.Content != null)
        {
            var jsonContent = operation.RequestBody.Content
                .FirstOrDefault(c => c.Key.Contains("json")).Value;
                
            if (jsonContent?.Schema?.Reference != null)
            {
                endpoint.RequestEntityId = jsonContent.Schema.Reference.Id;
            }
        }
        
        // Parse response
        var successResponse = operation.Responses
            .FirstOrDefault(r => r.Key.StartsWith("2")).Value;
            
        if (successResponse?.Content != null)
        {
            var jsonContent = successResponse.Content
                .FirstOrDefault(c => c.Key.Contains("json")).Value;
                
            if (jsonContent?.Schema?.Reference != null)
            {
                endpoint.ResponseEntityId = jsonContent.Schema.Reference.Id;
            }
        }
        
        // Parse response codes
        foreach (var (code, response) in operation.Responses)
        {
            if (int.TryParse(code, out var statusCode))
            {
                endpoint.ResponseCodes[statusCode] = response.Description ?? "";
            }
        }
        
        return endpoint;
    }
    
    private static Models.HttpMethod MapHttpMethod(OperationType method) => method switch
    {
        OperationType.Get => Models.HttpMethod.Get,
        OperationType.Post => Models.HttpMethod.Post,
        OperationType.Put => Models.HttpMethod.Put,
        OperationType.Patch => Models.HttpMethod.Patch,
        OperationType.Delete => Models.HttpMethod.Delete,
        OperationType.Head => Models.HttpMethod.Head,
        OperationType.Options => Models.HttpMethod.Options,
        _ => Models.HttpMethod.Get
    };
    
    private static AuthConfig? ExtractAuthConfig(OpenApiDocument document)
    {
        if (document.Components?.SecuritySchemes == null)
            return null;
            
        var scheme = document.Components.SecuritySchemes.FirstOrDefault();
        if (scheme.Value == null)
            return null;
            
        return scheme.Value.Type switch
        {
            SecuritySchemeType.ApiKey => new AuthConfig
            {
                Type = AuthType.ApiKey,
                Parameters = new Dictionary<string, string>
                {
                    ["name"] = scheme.Value.Name ?? "api_key",
                    ["in"] = scheme.Value.In.ToString().ToLowerInvariant()
                }
            },
            SecuritySchemeType.Http when scheme.Value.Scheme == "bearer" => new AuthConfig
            {
                Type = AuthType.Bearer
            },
            SecuritySchemeType.Http when scheme.Value.Scheme == "basic" => new AuthConfig
            {
                Type = AuthType.Basic
            },
            SecuritySchemeType.OAuth2 => new AuthConfig
            {
                Type = AuthType.OAuth2,
                Parameters = new Dictionary<string, string>
                {
                    ["flow"] = scheme.Value.Flows?.AuthorizationCode != null ? "authorization_code" : "client_credentials"
                }
            },
            _ => null
        };
    }
    
    private static bool ShouldIncludeEndpoint(string path, List<string>? filters)
    {
        if (filters == null || filters.Count == 0)
            return true;
            
        return filters.Any(filter => 
            Regex.IsMatch(path, "^" + Regex.Escape(filter).Replace("\\*", ".*") + "$"));
    }
    
    private static string ToPascalCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        // Handle snake_case and kebab-case
        var parts = name.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        return string.Concat(parts.Select(p =>
            char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }

    // ---------------------------------------------------------------------
    // ApiSurface population. Additive layer over the existing parse.
    // ---------------------------------------------------------------------

    private static Core.CanonicalModel.ApiSurface BuildApiSurface(OpenApiDocument document, SemanticModel model)
    {
        var knownEntityNames = model.Entities.Values.Select(e => e.Name).ToHashSet(StringComparer.Ordinal);

        var operations = new List<Core.CanonicalModel.ApiOperation>();
        foreach (var (path, pathItem) in document.Paths)
        {
            foreach (var (method, operation) in pathItem.Operations)
            {
                operations.Add(BuildOperation(path, method, operation, knownEntityNames));
            }
        }

        var servers = document.Servers?
            .Select(s => new Core.CanonicalModel.ApiServer { Url = s.Url ?? string.Empty, Description = s.Description })
            .ToList() ?? [];

        var tags = document.Tags?
            .Select(t => new Core.CanonicalModel.ApiTag { Name = t.Name, Description = t.Description })
            .ToList() ?? [];

        var securitySchemes = new Dictionary<string, Core.CanonicalModel.ApiSecurityScheme>(StringComparer.Ordinal);
        if (document.Components?.SecuritySchemes is not null)
        {
            foreach (var (name, scheme) in document.Components.SecuritySchemes)
            {
                securitySchemes[name] = BuildSecurityScheme(name, scheme);
            }
        }

        var defaultRequirements = BuildSecurityRequirements(document.SecurityRequirements);

        return new Core.CanonicalModel.ApiSurface
        {
            Title = document.Info?.Title ?? "API",
            Version = document.Info?.Version ?? "0.0.0",
            Description = document.Info?.Description,
            Servers = servers,
            Tags = tags,
            Operations = operations,
            SecuritySchemes = securitySchemes,
            SecurityRequirements = defaultRequirements
        };
    }

    private static Core.CanonicalModel.ApiOperation BuildOperation(
        string path,
        OperationType method,
        OpenApiOperation operation,
        HashSet<string> knownEntityNames)
    {
        var canonicalMethod = MapOperationType(method);
        var operationId = !string.IsNullOrWhiteSpace(operation.OperationId)
            ? operation.OperationId
            : GenerateOperationId(canonicalMethod, path);

        var parameters = operation.Parameters?
            .Select(p => BuildParameter(p, knownEntityNames))
            .ToList() ?? [];

        Core.CanonicalModel.ApiRequestBody? requestBody = null;
        if (operation.RequestBody is not null)
        {
            requestBody = new Core.CanonicalModel.ApiRequestBody
            {
                Description = operation.RequestBody.Description,
                Required = operation.RequestBody.Required,
                Content = BuildContentMap(operation.RequestBody.Content, knownEntityNames)
            };
        }

        var responses = new Dictionary<string, Core.CanonicalModel.ApiResponse>(StringComparer.Ordinal);
        if (operation.Responses is not null)
        {
            foreach (var (statusCode, response) in operation.Responses)
            {
                responses[statusCode] = new Core.CanonicalModel.ApiResponse
                {
                    Description = response.Description ?? string.Empty,
                    Content = BuildContentMap(response.Content, knownEntityNames)
                };
            }
        }

        return new Core.CanonicalModel.ApiOperation
        {
            OperationId = operationId,
            Method = canonicalMethod,
            Path = path,
            Summary = operation.Summary,
            Description = operation.Description,
            Tags = operation.Tags?.Select(t => t.Name).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? [],
            Parameters = parameters,
            RequestBody = requestBody,
            Responses = responses,
            SecurityRequirements = BuildSecurityRequirements(operation.Security),
            Deprecated = operation.Deprecated
        };
    }

    private static Core.CanonicalModel.ApiParameter BuildParameter(OpenApiParameter param, HashSet<string> knownEntityNames)
    {
        return new Core.CanonicalModel.ApiParameter
        {
            Name = param.Name,
            Location = MapParameterLocation(param.In),
            Description = param.Description,
            Required = param.Required,
            Deprecated = param.Deprecated,
            Schema = param.Schema is null ? null : BuildMediaType(param.Schema, knownEntityNames)
        };
    }

    private static IReadOnlyDictionary<string, Core.CanonicalModel.ApiMediaType> BuildContentMap(
        IDictionary<string, OpenApiMediaType>? content,
        HashSet<string> knownEntityNames)
    {
        if (content is null || content.Count == 0)
        {
            return new Dictionary<string, Core.CanonicalModel.ApiMediaType>();
        }

        var result = new Dictionary<string, Core.CanonicalModel.ApiMediaType>(StringComparer.Ordinal);
        foreach (var (mediaType, mediaValue) in content)
        {
            if (mediaValue?.Schema is null) continue;
            result[mediaType] = BuildMediaType(mediaValue.Schema, knownEntityNames, mediaValue);
        }
        return result;
    }

    private static Core.CanonicalModel.ApiMediaType BuildMediaType(
        OpenApiSchema schema,
        HashSet<string> knownEntityNames,
        OpenApiMediaType? mediaTypeSource = null)
    {
        var example = SerializeExample(mediaTypeSource);

        // Direct named reference → link to the SemanticEntity by name.
        if (schema.Reference is { Id: { Length: > 0 } refId } && knownEntityNames.Contains(refId))
        {
            return new Core.CanonicalModel.ApiMediaType { EntityName = refId, Example = example };
        }

        // Array of named references → still an inline array schema, but Items carries the link.
        return new Core.CanonicalModel.ApiMediaType
        {
            Schema = BuildSchema(schema, knownEntityNames),
            Example = example
        };
    }

    private static string? SerializeExample(OpenApiMediaType? mediaType)
    {
        var any = mediaType?.Example
            ?? mediaType?.Examples?
                .OrderBy(k => k.Key, StringComparer.Ordinal)
                .Select(k => k.Value?.Value)
                .FirstOrDefault(v => v is not null);
        if (any is null) return null;

        using var sw = new StringWriter();
        var writer = new Microsoft.OpenApi.Writers.OpenApiJsonWriter(sw);
        any.Write(writer, Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0);
        return sw.ToString();
    }

    private static Core.CanonicalModel.ApiSchema BuildSchema(OpenApiSchema schema, HashSet<string> knownEntityNames)
    {
        var type = !string.IsNullOrEmpty(schema.Type)
            ? schema.Type
            : (schema.Reference is not null ? "object" : "string");

        var enumValues = schema.Enum?
            .Select(e => e is Microsoft.OpenApi.Any.OpenApiString s ? s.Value : e?.ToString() ?? string.Empty)
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList() ?? [];

        Core.CanonicalModel.ApiSchema? items = null;
        if (schema.Items is not null)
        {
            items = BuildSchema(schema.Items, knownEntityNames);
        }

        string? entityName = null;
        if (schema.Reference is { Id: { Length: > 0 } refId } && knownEntityNames.Contains(refId))
        {
            entityName = refId;
        }

        return new Core.CanonicalModel.ApiSchema
        {
            Type = type,
            Format = schema.Format,
            Items = items,
            Enum = enumValues,
            Nullable = schema.Nullable,
            EntityName = entityName
        };
    }

    private static Core.CanonicalModel.ApiSecurityScheme BuildSecurityScheme(string name, OpenApiSecurityScheme scheme)
    {
        var flows = new Dictionary<string, Core.CanonicalModel.ApiSecurityFlow>(StringComparer.Ordinal);
        if (scheme.Type == SecuritySchemeType.OAuth2 && scheme.Flows is not null)
        {
            if (scheme.Flows.AuthorizationCode is not null)
                flows["authorizationCode"] = MapFlow(scheme.Flows.AuthorizationCode);
            if (scheme.Flows.ClientCredentials is not null)
                flows["clientCredentials"] = MapFlow(scheme.Flows.ClientCredentials);
            if (scheme.Flows.Implicit is not null)
                flows["implicit"] = MapFlow(scheme.Flows.Implicit);
            if (scheme.Flows.Password is not null)
                flows["password"] = MapFlow(scheme.Flows.Password);
        }

        return new Core.CanonicalModel.ApiSecurityScheme
        {
            Name = name,
            Type = MapSecuritySchemeType(scheme.Type),
            Description = scheme.Description,
            In = scheme.Type == SecuritySchemeType.ApiKey ? MapParameterLocation(scheme.In) : null,
            ParameterName = scheme.Type == SecuritySchemeType.ApiKey ? scheme.Name : null,
            Scheme = scheme.Type == SecuritySchemeType.Http ? scheme.Scheme : null,
            BearerFormat = scheme.Type == SecuritySchemeType.Http ? scheme.BearerFormat : null,
            OpenIdConnectUrl = scheme.OpenIdConnectUrl?.ToString(),
            Flows = flows
        };
    }

    private static Core.CanonicalModel.ApiSecurityFlow MapFlow(OpenApiOAuthFlow flow)
    {
        var scopes = flow.Scopes?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        return new Core.CanonicalModel.ApiSecurityFlow
        {
            AuthorizationUrl = flow.AuthorizationUrl?.ToString(),
            TokenUrl = flow.TokenUrl?.ToString(),
            RefreshUrl = flow.RefreshUrl?.ToString(),
            Scopes = scopes
        };
    }

    private static IReadOnlyList<Core.CanonicalModel.ApiSecurityRequirement> BuildSecurityRequirements(
        IList<OpenApiSecurityRequirement>? requirements)
    {
        if (requirements is null || requirements.Count == 0) return [];

        var result = new List<Core.CanonicalModel.ApiSecurityRequirement>();
        foreach (var requirement in requirements)
        {
            var schemes = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            foreach (var (schemeRef, scopes) in requirement)
            {
                var schemeName = schemeRef?.Reference?.Id;
                if (string.IsNullOrEmpty(schemeName)) continue;
                schemes[schemeName] = (scopes ?? (IList<string>)Array.Empty<string>()).ToList();
            }
            if (schemes.Count > 0)
            {
                result.Add(new Core.CanonicalModel.ApiSecurityRequirement { Schemes = schemes });
            }
        }
        return result;
    }

    private static Core.CanonicalModel.ApiHttpMethod MapOperationType(OperationType method) => method switch
    {
        OperationType.Get => Core.CanonicalModel.ApiHttpMethod.Get,
        OperationType.Put => Core.CanonicalModel.ApiHttpMethod.Put,
        OperationType.Post => Core.CanonicalModel.ApiHttpMethod.Post,
        OperationType.Delete => Core.CanonicalModel.ApiHttpMethod.Delete,
        OperationType.Options => Core.CanonicalModel.ApiHttpMethod.Options,
        OperationType.Head => Core.CanonicalModel.ApiHttpMethod.Head,
        OperationType.Patch => Core.CanonicalModel.ApiHttpMethod.Patch,
        OperationType.Trace => Core.CanonicalModel.ApiHttpMethod.Trace,
        _ => Core.CanonicalModel.ApiHttpMethod.Get
    };

    private static Core.CanonicalModel.ApiParameterLocation MapParameterLocation(ParameterLocation? location) => location switch
    {
        ParameterLocation.Query => Core.CanonicalModel.ApiParameterLocation.Query,
        ParameterLocation.Header => Core.CanonicalModel.ApiParameterLocation.Header,
        ParameterLocation.Path => Core.CanonicalModel.ApiParameterLocation.Path,
        ParameterLocation.Cookie => Core.CanonicalModel.ApiParameterLocation.Cookie,
        _ => Core.CanonicalModel.ApiParameterLocation.Query
    };

    private static Core.CanonicalModel.ApiSecuritySchemeType MapSecuritySchemeType(SecuritySchemeType type) => type switch
    {
        SecuritySchemeType.ApiKey => Core.CanonicalModel.ApiSecuritySchemeType.ApiKey,
        SecuritySchemeType.Http => Core.CanonicalModel.ApiSecuritySchemeType.Http,
        SecuritySchemeType.OAuth2 => Core.CanonicalModel.ApiSecuritySchemeType.OAuth2,
        SecuritySchemeType.OpenIdConnect => Core.CanonicalModel.ApiSecuritySchemeType.OpenIdConnect,
        _ => Core.CanonicalModel.ApiSecuritySchemeType.ApiKey
    };

    /// <summary>
    /// Produce a deterministic operationId for operations that omit one.
    /// Format: <c>{method}_{path}</c> with the path lowercased, non-alphanumerics replaced by <c>_</c>,
    /// and runs of underscores collapsed.
    /// </summary>
    internal static string GenerateOperationId(Core.CanonicalModel.ApiHttpMethod method, string path)
    {
        var methodToken = method.ToString().ToLowerInvariant();
        var pathBuilder = new System.Text.StringBuilder(path.Length);
        var previousWasUnderscore = false;
        foreach (var ch in path)
        {
            if (char.IsLetterOrDigit(ch))
            {
                pathBuilder.Append(char.ToLowerInvariant(ch));
                previousWasUnderscore = false;
            }
            else if (!previousWasUnderscore)
            {
                pathBuilder.Append('_');
                previousWasUnderscore = true;
            }
        }
        var pathToken = pathBuilder.ToString().Trim('_');
        return string.IsNullOrEmpty(pathToken) ? methodToken : $"{methodToken}_{pathToken}";
    }
}
