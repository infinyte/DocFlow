namespace DocFlow.Core.CanonicalModel;

/// <summary>
/// Represents a semantic entity in the canonical model.
/// This is the core representation that all formats (diagrams, code, docs) map to/from.
/// </summary>
public sealed class SemanticEntity
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// The semantic classification of this entity (e.g., AggregateRoot, Entity, ValueObject, Service)
    /// </summary>
    public EntityClassification Classification { get; set; } = EntityClassification.Unknown;
    
    /// <summary>
    /// Properties/attributes of this entity
    /// </summary>
    public List<SemanticProperty> Properties { get; init; } = [];
    
    /// <summary>
    /// Methods/operations on this entity
    /// </summary>
    public List<SemanticOperation> Operations { get; init; } = [];
    
    /// <summary>
    /// Stereotypes applied to this entity (e.g., <<interface>>, <<abstract>>)
    /// </summary>
    public HashSet<string> Stereotypes { get; init; } = [];
    
    /// <summary>
    /// Metadata and annotations from source formats
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
    
    /// <summary>
    /// Confidence score (0-1) if this entity was inferred/detected
    /// </summary>
    public double? Confidence { get; set; }
    
    /// <summary>
    /// Source information - where this entity was parsed from
    /// </summary>
    public SourceInfo? Source { get; set; }
}

/// <summary>
/// Classification of semantic entities based on DDD tactical patterns and common modeling concepts
/// </summary>
public enum EntityClassification
{
    Unknown,
    
    // DDD Tactical Patterns
    AggregateRoot,
    Entity,
    ValueObject,
    DomainService,
    DomainEvent,
    Repository,
    Factory,
    Specification,
    
    // Common Code Constructs
    Class,
    Interface,
    AbstractClass,
    Record,
    Struct,
    Enum,
    
    // Architectural Components
    Service,
    Controller,
    Handler,
    Validator,
    Mapper,
    
    // Data Patterns
    DataTransferObject,
    ViewModel,
    Command,
    Query,
    
    // External
    ExternalService,
    ExternalEntity
}

/// <summary>
/// Represents a property/attribute of a semantic entity
/// </summary>
public sealed class SemanticProperty
{
    public required string Name { get; set; }
    public required SemanticType Type { get; set; }
    public string? Description { get; set; }
    
    /// <summary>
    /// Semantic role of this property
    /// </summary>
    public PropertySemantics Semantics { get; set; } = PropertySemantics.State;
    
    /// <summary>
    /// Visibility/access level
    /// </summary>
    public Visibility Visibility { get; set; } = Visibility.Public;
    
    /// <summary>
    /// Is this property required/non-nullable?
    /// </summary>
    public bool IsRequired { get; set; }
    
    /// <summary>
    /// Is this property read-only/immutable?
    /// </summary>
    public bool IsReadOnly { get; set; }
    
    /// <summary>
    /// Default value if any
    /// </summary>
    public string? DefaultValue { get; set; }
    
    /// <summary>
    /// Attributes/annotations on this property
    /// </summary>
    public List<SemanticAttribute> Attributes { get; init; } = [];
    
    /// <summary>
    /// Confidence score (0-1) if this property was inferred
    /// </summary>
    public double? Confidence { get; set; }
}

/// <summary>
/// Semantic role of a property within its entity
/// </summary>
public enum PropertySemantics
{
    /// <summary>Regular state property</summary>
    State,
    
    /// <summary>Identity property (e.g., Id, Key)</summary>
    Identity,
    
    /// <summary>Derived/computed property</summary>
    Derived,
    
    /// <summary>Navigation/reference to another entity</summary>
    Navigation,
    
    /// <summary>Collection of related entities</summary>
    Collection,
    
    /// <summary>Timestamp or audit field</summary>
    Audit,
    
    /// <summary>Version/concurrency token</summary>
    Version
}

/// <summary>
/// Represents a method/operation on a semantic entity
/// </summary>
public sealed class SemanticOperation
{
    public required string Name { get; set; }
    public SemanticType? ReturnType { get; set; }
    public string? Description { get; set; }
    public Visibility Visibility { get; set; } = Visibility.Public;
    public List<SemanticParameter> Parameters { get; init; } = [];
    public List<SemanticAttribute> Attributes { get; init; } = [];
    public bool IsAbstract { get; set; }
    public bool IsStatic { get; set; }
    public bool IsAsync { get; set; }
}

/// <summary>
/// Parameter for an operation
/// </summary>
public sealed class SemanticParameter
{
    public required string Name { get; set; }
    public required SemanticType Type { get; set; }
    public string? DefaultValue { get; set; }
    public bool IsOptional { get; set; }
}

/// <summary>
/// Represents a type reference in the semantic model
/// </summary>
public sealed record SemanticType
{
    public required string Name { get; init; }

    /// <summary>
    /// Is this a primitive/built-in type?
    /// </summary>
    public bool IsPrimitive { get; init; }

    /// <summary>
    /// Is this a collection type?
    /// </summary>
    public bool IsCollection { get; init; }

    /// <summary>
    /// Is this nullable?
    /// </summary>
    public bool IsNullable { get; init; }

    /// <summary>
    /// Generic type arguments if applicable
    /// </summary>
    public List<SemanticType> GenericArguments { get; init; } = [];

    /// <summary>
    /// Reference to the entity definition if this type refers to another entity
    /// </summary>
    public string? ReferencedEntityId { get; init; }
    
    // Common primitive type helpers
    public static SemanticType String => new() { Name = "string", IsPrimitive = true };
    public static SemanticType Int => new() { Name = "int", IsPrimitive = true };
    public static SemanticType Long => new() { Name = "long", IsPrimitive = true };
    public static SemanticType Decimal => new() { Name = "decimal", IsPrimitive = true };
    public static SemanticType Bool => new() { Name = "bool", IsPrimitive = true };
    public static SemanticType DateTime => new() { Name = "DateTime", IsPrimitive = true };
    public static SemanticType Guid => new() { Name = "Guid", IsPrimitive = true };
    
    public static SemanticType CollectionOf(SemanticType elementType) => new()
    {
        Name = "ICollection",
        IsCollection = true,
        GenericArguments = [elementType]
    };
    
    public static SemanticType Nullable(SemanticType innerType) => innerType with { IsNullable = true };
    
    public static SemanticType EntityReference(string entityId, string typeName) => new()
    {
        Name = typeName,
        IsPrimitive = false,
        ReferencedEntityId = entityId
    };
}

/// <summary>
/// Represents an attribute/annotation
/// </summary>
public sealed class SemanticAttribute
{
    public required string Name { get; set; }
    public Dictionary<string, object> Arguments { get; init; } = [];
}

/// <summary>
/// Access visibility levels
/// </summary>
public enum Visibility
{
    Public,
    Private,
    Protected,
    Internal,
    ProtectedInternal,
    PrivateProtected
}

/// <summary>
/// Information about where a semantic element was parsed from
/// </summary>
public sealed class SourceInfo
{
    public required string SourceType { get; init; } // "CSharp", "Mermaid", "Whiteboard", etc.
    public string? FilePath { get; init; }
    public int? LineNumber { get; init; }
    public int? ColumnNumber { get; init; }
    public string? RawContent { get; init; }
}
