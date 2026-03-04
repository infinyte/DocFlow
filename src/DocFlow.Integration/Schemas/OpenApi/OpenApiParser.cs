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
public sealed class OpenApiParser : ISchemaParser
{
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
            endpoint.PathParameters.Add(new ApiParameter
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
            endpoint.QueryParameters.Add(new ApiParameter
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
}
