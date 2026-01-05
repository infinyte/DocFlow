namespace DocFlow.Integration.Models;

/// <summary>
/// A complete integration specification between an external API and your CDM.
/// This is what DocFlow generates and what drives code generation.
/// </summary>
public sealed class IntegrationSpec
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string Version { get; init; }
    
    /// <summary>
    /// The external system being integrated
    /// </summary>
    public required ExternalSystemInfo ExternalSystem { get; init; }
    
    /// <summary>
    /// Reference to your Canonical Data Model
    /// </summary>
    public required CdmReference CanonicalModel { get; init; }
    
    /// <summary>
    /// All endpoints being integrated
    /// </summary>
    public List<EndpointIntegration> Endpoints { get; init; } = [];
    
    /// <summary>
    /// Entity mappings (external DTO → CDM entity)
    /// </summary>
    public List<EntityMapping> EntityMappings { get; init; } = [];
    
    /// <summary>
    /// Global transformation rules
    /// </summary>
    public List<TransformationRule> GlobalTransforms { get; init; } = [];
    
    /// <summary>
    /// SLA requirements
    /// </summary>
    public SlaRequirements? Sla { get; init; }
    
    /// <summary>
    /// Authentication configuration
    /// </summary>
    public AuthConfig? Authentication { get; init; }
    
    /// <summary>
    /// When this spec was generated/last updated
    /// </summary>
    public DateTime GeneratedAt { get; init; }
    
    /// <summary>
    /// IMS confidence scores for auto-generated mappings
    /// </summary>
    public MappingConfidenceReport? ConfidenceReport { get; init; }
}

public sealed class ExternalSystemInfo
{
    /// <summary>External system name (e.g., "FlightBridge", "1200Aero")</summary>
    public required string Name { get; init; }
    
    /// <summary>Base URL for API calls</summary>
    public required string BaseUrl { get; init; }
    
    /// <summary>Link to API documentation</summary>
    public string? DocumentationUrl { get; init; }
    
    /// <summary>API version</summary>
    public string? Version { get; init; }
    
    /// <summary>Contact information for the external team</summary>
    public string? Contact { get; init; }
}

public sealed class CdmReference
{
    /// <summary>CDM name (e.g., "OpenFlight CDM")</summary>
    public required string Name { get; init; }
    
    /// <summary>CDM version</summary>
    public required string Version { get; init; }
    
    /// <summary>Path to CDM C# source files</summary>
    public string? SourcePath { get; init; }
    
    /// <summary>Path to CDM schema definition</summary>
    public string? SchemaPath { get; init; }
}

public sealed class EndpointIntegration
{
    public required ApiEndpoint Endpoint { get; init; }
    
    /// <summary>
    /// What CDM operation does this endpoint support?
    /// </summary>
    public required CdmOperation Operation { get; init; }
    
    /// <summary>
    /// Mappings specific to this endpoint
    /// </summary>
    public List<FieldMapping> FieldMappings { get; init; } = [];
    
    /// <summary>
    /// Is this endpoint integration complete and verified?
    /// </summary>
    public IntegrationStatus Status { get; init; }
}

public enum CdmOperation
{
    /// <summary>GET - retrieve data</summary>
    Query,
    
    /// <summary>POST - create new entity</summary>
    Create,
    
    /// <summary>PUT/PATCH - update entity</summary>
    Update,
    
    /// <summary>DELETE - remove entity</summary>
    Delete,
    
    /// <summary>Webhook/event subscription</summary>
    Subscribe,
    
    /// <summary>Outbound notification</summary>
    Notify
}

public enum IntegrationStatus
{
    NotStarted,
    InProgress,
    
    /// <summary>IMS auto-mapped but needs human verification</summary>
    NeedsReview,
    
    /// <summary>Human verified correct</summary>
    Verified,
    
    /// <summary>Deployed and working in production</summary>
    Production
}

public sealed class EntityMapping
{
    public required string ExternalEntityName { get; init; }
    public required string CdmEntityName { get; init; }
    public List<FieldMapping> FieldMappings { get; init; } = [];
    public double Confidence { get; init; }
    public IntegrationStatus Status { get; init; }
}

public sealed class FieldMapping
{
    /// <summary>Source field name in external API (e.g., "arr_time")</summary>
    public required string SourceField { get; init; }
    
    /// <summary>Target field name in CDM (e.g., "ArrivalDateTime")</summary>
    public required string TargetField { get; init; }
    
    /// <summary>Transformation to apply during mapping</summary>
    public TransformationRule? Transformation { get; init; }
    
    /// <summary>IMS confidence score (0-1)</summary>
    public double Confidence { get; init; }
    
    /// <summary>Why this mapping was suggested (e.g., "IMS: 94% match based on 47 prior examples")</summary>
    public string? Reasoning { get; init; }
    
    /// <summary>Was this mapping auto-generated by IMS?</summary>
    public bool IsAutoMapped { get; init; }
    
    /// <summary>Has a human verified this mapping?</summary>
    public bool IsVerified { get; init; }
}

public sealed class TransformationRule
{
    public required string Id { get; init; }
    public required TransformationType Type { get; init; }
    public required string Expression { get; init; }
    public Dictionary<string, object> Parameters { get; init; } = [];
}

public enum TransformationType
{
    /// <summary>No transformation, direct copy</summary>
    Direct,
    
    /// <summary>Type conversion (string → DateTime, int → decimal)</summary>
    TypeConversion,
    
    /// <summary>Date format conversion, string formatting</summary>
    Format,
    
    /// <summary>Value lookup/translation table</summary>
    Lookup,
    
    /// <summary>Computed value from expression</summary>
    Calculation,
    
    /// <summary>Combine multiple fields</summary>
    Concatenation,
    
    /// <summary>Split single field into multiple</summary>
    Split,
    
    /// <summary>If/then/else logic</summary>
    Conditional,
    
    /// <summary>Custom C# expression</summary>
    Custom
}

public sealed class SlaRequirements
{
    /// <summary>Maximum acceptable response latency</summary>
    public TimeSpan MaxLatency { get; init; }
    
    /// <summary>Maximum acceptable data age (e.g., 30 seconds for real-time feeds)</summary>
    public TimeSpan MaxDataAge { get; init; }
    
    /// <summary>Minimum uptime percentage (e.g., 99.9%)</summary>
    public double MinUptime { get; init; }
    
    /// <summary>Maximum error rate per 1000 requests</summary>
    public int MaxErrorRate { get; init; }
}

public sealed class AuthConfig
{
    public required AuthType Type { get; init; }
    public Dictionary<string, string> Parameters { get; init; } = [];
}

public enum AuthType
{
    None,
    ApiKey,
    Basic,
    Bearer,
    OAuth2,
    Certificate,
    Custom
}

public sealed class MappingConfidenceReport
{
    public int TotalMappings { get; init; }
    
    /// <summary>Mappings with confidence > 90%</summary>
    public int HighConfidence { get; init; }
    
    /// <summary>Mappings with confidence 70-90%</summary>
    public int MediumConfidence { get; init; }
    
    /// <summary>Mappings with confidence below 70%</summary>
    public int LowConfidence { get; init; }
    
    /// <summary>Fields with no mapping found</summary>
    public int Unmapped { get; init; }
    
    public double OverallConfidence { get; init; }
    public List<MappingIssue> Issues { get; init; } = [];
}

public sealed class MappingIssue
{
    public required string Field { get; init; }
    public required MappingIssueType Type { get; init; }
    public required string Description { get; init; }
    public List<string> Suggestions { get; init; } = [];
}

public enum MappingIssueType
{
    NoMatch,
    AmbiguousMatch,
    TypeMismatch,
    NullabilityMismatch,
    ValidationConflict,
    SlaRisk
}
