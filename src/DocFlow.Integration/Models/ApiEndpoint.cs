using DocFlow.Core.CanonicalModel;

namespace DocFlow.Integration.Models;

/// <summary>
/// Represents an API endpoint in the semantic model
/// </summary>
public sealed class ApiEndpoint
{
    public required string Id { get; init; }
    public required string Path { get; init; }
    public required HttpMethod Method { get; init; }
    public string? Summary { get; init; }
    public string? Description { get; init; }
    
    /// <summary>
    /// Request body entity (if any)
    /// </summary>
    public string? RequestEntityId { get; set; }

    /// <summary>
    /// Response body entity
    /// </summary>
    public string? ResponseEntityId { get; set; }
    
    /// <summary>
    /// Path parameters (e.g., /reservations/{id})
    /// </summary>
    public List<ApiParameter> PathParameters { get; init; } = [];
    
    /// <summary>
    /// Query parameters
    /// </summary>
    public List<ApiParameter> QueryParameters { get; init; } = [];
    
    /// <summary>
    /// Required headers
    /// </summary>
    public List<ApiParameter> Headers { get; init; } = [];
    
    /// <summary>
    /// Expected response codes and their meanings
    /// </summary>
    public Dictionary<int, string> ResponseCodes { get; init; } = [];
    
    /// <summary>
    /// Rate limiting info if known
    /// </summary>
    public RateLimitInfo? RateLimit { get; init; }
}

public sealed class ApiParameter
{
    public required string Name { get; init; }
    public required SemanticType Type { get; init; }
    public bool IsRequired { get; init; }
    public string? Description { get; init; }
    public string? DefaultValue { get; init; }
    public string? Example { get; init; }
}

public enum HttpMethod
{
    Get,
    Post,
    Put,
    Patch,
    Delete,
    Head,
    Options
}

public sealed class RateLimitInfo
{
    public int? RequestsPerMinute { get; init; }
    public int? RequestsPerHour { get; init; }
    public int? RequestsPerDay { get; init; }
}

/// <summary>
/// API-specific metadata that extends SemanticEntity for integration scenarios.
/// Store this in SemanticEntity.Metadata["api"] for entities parsed from API schemas.
/// </summary>
public sealed class ApiEntityMetadata
{
    /// <summary>
    /// The external system this entity comes from (e.g., "PetStore", "ExternalApi")
    /// </summary>
    public required string SourceSystem { get; init; }
    
    /// <summary>
    /// Original field/property name in external system
    /// </summary>
    public string? ExternalName { get; init; }
    
    /// <summary>
    /// JSON path in API response (e.g., "$.data.reservation.id")
    /// </summary>
    public string? JsonPath { get; init; }
    
    /// <summary>
    /// Is this field required by the external API?
    /// </summary>
    public bool IsExternallyRequired { get; init; }
    
    /// <summary>
    /// External validation rules (regex, min/max, etc.)
    /// </summary>
    public List<ExternalValidation> Validations { get; init; } = [];
    
    /// <summary>
    /// Sample values seen from this field (useful for type inference)
    /// </summary>
    public List<string> SampleValues { get; init; } = [];
    
    /// <summary>
    /// Data freshness requirements (SLA)
    /// </summary>
    public TimeSpan? MaxDataAge { get; init; }
}

public sealed class ExternalValidation
{
    public required ValidationType Type { get; init; }
    public required string Rule { get; init; }
    public string? ErrorMessage { get; init; }
}

public enum ValidationType
{
    Regex,
    MinLength,
    MaxLength,
    MinValue,
    MaxValue,
    Enum,
    Custom
}
