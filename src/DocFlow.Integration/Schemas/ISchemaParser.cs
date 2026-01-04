using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Integration.Schemas;

/// <summary>
/// Parses external API schema formats into the semantic model.
/// </summary>
public interface ISchemaParser : IModelParser
{
    /// <summary>
    /// Supported schema formats (e.g., "OpenAPI3", "Swagger2", "JsonSchema", "GraphQL")
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }
    
    /// <summary>
    /// Parse schema and extract API endpoints
    /// </summary>
    Task<SchemaParseResult> ParseSchemaAsync(
        ParserInput input, 
        SchemaParserOptions? options = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Extended parse result that includes API endpoint information
/// </summary>
public sealed class SchemaParseResult
{
    /// <summary>
    /// The parsed semantic model
    /// </summary>
    public required SemanticModel Model { get; init; }

    /// <summary>
    /// Whether parsing succeeded
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Errors encountered during parsing
    /// </summary>
    public IReadOnlyList<ParseError> Errors { get; init; } = [];

    /// <summary>
    /// Warnings encountered during parsing
    /// </summary>
    public IReadOnlyList<ParseWarning> Warnings { get; init; } = [];

    /// <summary>
    /// API endpoints extracted from the schema
    /// </summary>
    public List<Models.ApiEndpoint> Endpoints { get; init; } = [];

    /// <summary>
    /// External system info extracted from schema metadata
    /// </summary>
    public Models.ExternalSystemInfo? ExternalSystem { get; init; }

    /// <summary>
    /// Authentication requirements
    /// </summary>
    public Models.AuthConfig? Authentication { get; init; }

    /// <summary>
    /// Rate limiting info if specified
    /// </summary>
    public Models.RateLimitInfo? RateLimit { get; init; }

    /// <summary>
    /// Statistics about the parsing operation
    /// </summary>
    public ParseStatistics? Statistics { get; init; }

    /// <summary>
    /// Create a failed result
    /// </summary>
    public static SchemaParseResult Failed(ParseError error) => new()
    {
        Model = new SemanticModel { Name = "Error" },
        Success = false,
        Errors = [error]
    };
}

/// <summary>
/// Options for schema parsing
/// </summary>
public sealed class SchemaParserOptions
{
    /// <summary>
    /// Include source information in the model
    /// </summary>
    public bool IncludeSourceInfo { get; init; } = true;

    /// <summary>
    /// Include only endpoints matching these paths (glob patterns)
    /// </summary>
    public List<string>? EndpointFilter { get; init; }

    /// <summary>
    /// Include request/response examples in the model
    /// </summary>
    public bool IncludeExamples { get; init; } = true;

    /// <summary>
    /// Flatten nested objects into separate entities
    /// </summary>
    public bool FlattenNestedObjects { get; init; } = true;

    /// <summary>
    /// Generate entity names from schema $ref names
    /// </summary>
    public bool UseRefNames { get; init; } = true;
}
