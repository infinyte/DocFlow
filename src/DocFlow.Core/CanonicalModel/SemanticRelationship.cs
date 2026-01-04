namespace DocFlow.Core.CanonicalModel;

/// <summary>
/// Represents a semantic relationship between entities in the canonical model.
/// This captures not just that two entities are related, but HOW they are related.
/// </summary>
public sealed class SemanticRelationship
{
    public required string Id { get; init; }
    
    /// <summary>
    /// The source entity of this relationship
    /// </summary>
    public required string SourceEntityId { get; init; }
    
    /// <summary>
    /// The target entity of this relationship
    /// </summary>
    public required string TargetEntityId { get; init; }
    
    /// <summary>
    /// The semantic type of this relationship
    /// </summary>
    public RelationshipType Type { get; set; } = RelationshipType.Association;
    
    /// <summary>
    /// Human-readable name/label for this relationship
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Description of what this relationship represents
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Multiplicity at the source end
    /// </summary>
    public Multiplicity SourceMultiplicity { get; set; } = Multiplicity.One;
    
    /// <summary>
    /// Multiplicity at the target end
    /// </summary>
    public Multiplicity TargetMultiplicity { get; set; } = Multiplicity.One;
    
    /// <summary>
    /// Role name at the source end (e.g., "owner", "parent")
    /// </summary>
    public string? SourceRole { get; set; }
    
    /// <summary>
    /// Role name at the target end (e.g., "items", "children")
    /// </summary>
    public string? TargetRole { get; set; }
    
    /// <summary>
    /// Is this relationship bidirectional (navigable from both ends)?
    /// </summary>
    public bool IsBidirectional { get; set; }
    
    /// <summary>
    /// Additional semantic qualifiers for this relationship
    /// </summary>
    public HashSet<RelationshipQualifier> Qualifiers { get; init; } = [];
    
    /// <summary>
    /// Metadata and annotations
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
    
    /// <summary>
    /// Confidence score (0-1) if this relationship was inferred
    /// </summary>
    public double? Confidence { get; set; }
    
    /// <summary>
    /// Source information
    /// </summary>
    public SourceInfo? Source { get; set; }
}

/// <summary>
/// Semantic types of relationships between entities.
/// These follow UML/DDD conventions but with semantic meaning attached.
/// </summary>
public enum RelationshipType
{
    /// <summary>
    /// Unknown or unclassified relationship
    /// </summary>
    Unknown,
    
    // Structural Relationships
    
    /// <summary>
    /// Simple association - entities are related but independent
    /// Example: Customer places Order (Customer can exist without Order)
    /// </summary>
    Association,
    
    /// <summary>
    /// Aggregation - "has-a" relationship where parts can exist independently
    /// Example: Department has Employees (Employees can exist without Department)
    /// </summary>
    Aggregation,
    
    /// <summary>
    /// Composition - strong "owns" relationship, parts don't exist independently
    /// Example: Order contains LineItems (LineItems don't exist without Order)
    /// </summary>
    Composition,
    
    /// <summary>
    /// Inheritance - "is-a" relationship
    /// Example: PremiumCustomer is-a Customer
    /// </summary>
    Inheritance,
    
    /// <summary>
    /// Interface implementation
    /// Example: OrderService implements IOrderService
    /// </summary>
    Implementation,
    
    // Dependency Relationships
    
    /// <summary>
    /// Dependency - one entity uses another
    /// Example: OrderService depends on IPaymentGateway
    /// </summary>
    Dependency,
    
    /// <summary>
    /// Realization - abstraction to implementation
    /// </summary>
    Realization,
    
    // DDD-Specific Relationships
    
    /// <summary>
    /// Aggregate membership - entity belongs to an aggregate
    /// </summary>
    AggregateMembership,
    
    /// <summary>
    /// Reference by ID - cross-aggregate reference using ID only
    /// Example: Order references CustomerId (not full Customer object)
    /// </summary>
    ReferenceById,
    
    /// <summary>
    /// Domain event published by entity
    /// Example: Order publishes OrderPlacedEvent
    /// </summary>
    PublishesEvent,
    
    /// <summary>
    /// Domain event consumed/handled
    /// Example: InventoryService handles OrderPlacedEvent
    /// </summary>
    HandlesEvent,
    
    // Data Flow Relationships
    
    /// <summary>
    /// Data flows from source to target
    /// </summary>
    DataFlow,
    
    /// <summary>
    /// Async/eventual communication
    /// </summary>
    AsyncCommunication,
    
    /// <summary>
    /// Synchronous call/request
    /// </summary>
    SyncCall
}

/// <summary>
/// Multiplicity of a relationship endpoint
/// </summary>
public sealed class Multiplicity
{
    public int? LowerBound { get; init; }
    public int? UpperBound { get; init; } // null = unbounded (*)
    
    // Common multiplicities
    public static Multiplicity Zero => new() { LowerBound = 0, UpperBound = 0 };
    public static Multiplicity One => new() { LowerBound = 1, UpperBound = 1 };
    public static Multiplicity ZeroOrOne => new() { LowerBound = 0, UpperBound = 1 };
    public static Multiplicity ZeroOrMore => new() { LowerBound = 0, UpperBound = null };
    public static Multiplicity OneOrMore => new() { LowerBound = 1, UpperBound = null };
    public static Multiplicity Many => ZeroOrMore;
    
    public static Multiplicity Exactly(int n) => new() { LowerBound = n, UpperBound = n };
    public static Multiplicity Range(int lower, int upper) => new() { LowerBound = lower, UpperBound = upper };
    
    public override string ToString()
    {
        if (LowerBound == UpperBound)
            return LowerBound?.ToString() ?? "*";
        
        var lower = LowerBound?.ToString() ?? "0";
        var upper = UpperBound?.ToString() ?? "*";
        return $"{lower}..{upper}";
    }
    
    /// <summary>
    /// Parse multiplicity from string (e.g., "1", "*", "0..1", "1..*")
    /// </summary>
    public static Multiplicity Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return One;
            
        value = value.Trim();
        
        return value switch
        {
            "1" => One,
            "*" or "n" or "N" => ZeroOrMore,
            "0..1" or "?" => ZeroOrOne,
            "1..*" or "+" => OneOrMore,
            "0..*" => ZeroOrMore,
            _ when value.Contains("..") => ParseRange(value),
            _ when int.TryParse(value, out var n) => Exactly(n),
            _ => One // Default
        };
    }
    
    private static Multiplicity ParseRange(string value)
    {
        var parts = value.Split("..", StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) return One;
        
        var lower = parts[0] == "*" ? null : int.TryParse(parts[0], out var l) ? l : (int?)0;
        var upper = parts[1] == "*" || parts[1] == "n" ? null : int.TryParse(parts[1], out var u) ? u : (int?)null;
        
        return new Multiplicity { LowerBound = lower ?? 0, UpperBound = upper };
    }
}

/// <summary>
/// Additional semantic qualifiers for relationships
/// </summary>
public enum RelationshipQualifier
{
    /// <summary>Relationship is required/mandatory</summary>
    Required,
    
    /// <summary>Relationship is optional</summary>
    Optional,
    
    /// <summary>Cascade delete - deleting source deletes target</summary>
    CascadeDelete,
    
    /// <summary>Cascade update</summary>
    CascadeUpdate,
    
    /// <summary>Relationship is lazy-loaded</summary>
    LazyLoad,
    
    /// <summary>Relationship is eagerly loaded</summary>
    EagerLoad,
    
    /// <summary>Relationship is read-only from this direction</summary>
    ReadOnly,
    
    /// <summary>Relationship represents ownership</summary>
    Owns,
    
    /// <summary>Relationship is ordered (e.g., ordered collection)</summary>
    Ordered,
    
    /// <summary>Relationship is unique (no duplicates)</summary>
    Unique,
    
    /// <summary>Weak reference (doesn't prevent garbage collection)</summary>
    Weak,
    
    /// <summary>Temporal/historical relationship</summary>
    Temporal
}
