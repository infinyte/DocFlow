namespace DocFlow.Core.CanonicalModel;

/// <summary>
/// The complete semantic model - a self-contained representation of a domain model
/// that can be transformed to/from any supported format.
/// 
/// This is the "single source of truth" that all parsers write to and all generators read from.
/// </summary>
public sealed class SemanticModel
{
    /// <summary>
    /// Unique identifier for this model
    /// </summary>
    public string Id { get; init; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Human-readable name for this model
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Description of what this model represents
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Version of this model (for tracking changes)
    /// </summary>
    public string? Version { get; set; }
    
    /// <summary>
    /// All entities in this model, keyed by their ID
    /// </summary>
    public Dictionary<string, SemanticEntity> Entities { get; init; } = [];
    
    /// <summary>
    /// All relationships between entities
    /// </summary>
    public List<SemanticRelationship> Relationships { get; init; } = [];
    
    /// <summary>
    /// Namespace/package groupings
    /// </summary>
    public List<SemanticNamespace> Namespaces { get; init; } = [];
    
    /// <summary>
    /// Global metadata for the entire model
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];
    
    /// <summary>
    /// Information about how this model was created
    /// </summary>
    public ModelProvenance? Provenance { get; set; }
    
    /// <summary>
    /// Validation issues found in this model
    /// </summary>
    public List<ValidationIssue> ValidationIssues { get; init; } = [];
    
    // Convenience methods
    
    /// <summary>
    /// Add an entity to the model
    /// </summary>
    public SemanticEntity AddEntity(SemanticEntity entity)
    {
        Entities[entity.Id] = entity;
        return entity;
    }
    
    /// <summary>
    /// Create and add a new entity
    /// </summary>
    public SemanticEntity CreateEntity(string name, EntityClassification classification = EntityClassification.Class)
    {
        var entity = new SemanticEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Classification = classification
        };
        return AddEntity(entity);
    }
    
    /// <summary>
    /// Add a relationship between entities
    /// </summary>
    public SemanticRelationship AddRelationship(
        string sourceEntityId, 
        string targetEntityId, 
        RelationshipType type,
        string? name = null)
    {
        var relationship = new SemanticRelationship
        {
            Id = Guid.NewGuid().ToString(),
            SourceEntityId = sourceEntityId,
            TargetEntityId = targetEntityId,
            Type = type,
            Name = name
        };
        Relationships.Add(relationship);
        return relationship;
    }
    
    /// <summary>
    /// Get an entity by ID
    /// </summary>
    public SemanticEntity? GetEntity(string id) => 
        Entities.TryGetValue(id, out var entity) ? entity : null;
    
    /// <summary>
    /// Find entities by name (case-insensitive)
    /// </summary>
    public IEnumerable<SemanticEntity> FindEntitiesByName(string name) =>
        Entities.Values.Where(e => e.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    
    /// <summary>
    /// Find entities by classification
    /// </summary>
    public IEnumerable<SemanticEntity> FindEntitiesByClassification(EntityClassification classification) =>
        Entities.Values.Where(e => e.Classification == classification);
    
    /// <summary>
    /// Get all relationships where the given entity is the source
    /// </summary>
    public IEnumerable<SemanticRelationship> GetOutgoingRelationships(string entityId) =>
        Relationships.Where(r => r.SourceEntityId == entityId);
    
    /// <summary>
    /// Get all relationships where the given entity is the target
    /// </summary>
    public IEnumerable<SemanticRelationship> GetIncomingRelationships(string entityId) =>
        Relationships.Where(r => r.TargetEntityId == entityId);
    
    /// <summary>
    /// Get all relationships involving the given entity (in either direction)
    /// </summary>
    public IEnumerable<SemanticRelationship> GetAllRelationships(string entityId) =>
        Relationships.Where(r => r.SourceEntityId == entityId || r.TargetEntityId == entityId);
    
    /// <summary>
    /// Find aggregate roots in this model
    /// </summary>
    public IEnumerable<SemanticEntity> GetAggregateRoots() =>
        FindEntitiesByClassification(EntityClassification.AggregateRoot);
    
    /// <summary>
    /// Get entities that belong to an aggregate (composed by the aggregate root)
    /// </summary>
    public IEnumerable<SemanticEntity> GetAggregateMembers(string aggregateRootId)
    {
        var compositionTargets = Relationships
            .Where(r => r.SourceEntityId == aggregateRootId && r.Type == RelationshipType.Composition)
            .Select(r => r.TargetEntityId);
            
        return compositionTargets
            .Select(id => GetEntity(id))
            .Where(e => e != null)!;
    }
    
    /// <summary>
    /// Validate the model and return any issues
    /// </summary>
    public List<ValidationIssue> Validate()
    {
        ValidationIssues.Clear();
        
        // Check for orphaned relationship references
        foreach (var rel in Relationships)
        {
            if (!Entities.ContainsKey(rel.SourceEntityId))
            {
                ValidationIssues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "ORPHAN_SOURCE",
                    Message = $"Relationship '{rel.Id}' references non-existent source entity '{rel.SourceEntityId}'"
                });
            }
            if (!Entities.ContainsKey(rel.TargetEntityId))
            {
                ValidationIssues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Code = "ORPHAN_TARGET",
                    Message = $"Relationship '{rel.Id}' references non-existent target entity '{rel.TargetEntityId}'"
                });
            }
        }
        
        // Check for entities without identity when they should have one
        foreach (var entity in Entities.Values)
        {
            if (entity.Classification is EntityClassification.Entity or EntityClassification.AggregateRoot)
            {
                var hasIdentity = entity.Properties.Any(p => p.Semantics == PropertySemantics.Identity);
                if (!hasIdentity)
                {
                    ValidationIssues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Code = "MISSING_IDENTITY",
                        Message = $"Entity '{entity.Name}' is classified as {entity.Classification} but has no identity property",
                        EntityId = entity.Id
                    });
                }
            }
        }
        
        // Check for value objects with identity (anti-pattern)
        foreach (var entity in Entities.Values.Where(e => e.Classification == EntityClassification.ValueObject))
        {
            var hasIdentity = entity.Properties.Any(p => p.Semantics == PropertySemantics.Identity);
            if (hasIdentity)
            {
                ValidationIssues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Code = "VALUE_OBJECT_WITH_IDENTITY",
                    Message = $"Value Object '{entity.Name}' has an identity property - consider if this should be an Entity",
                    EntityId = entity.Id
                });
            }
        }
        
        return ValidationIssues;
    }
}

/// <summary>
/// Namespace/package grouping for entities
/// </summary>
public sealed class SemanticNamespace
{
    public required string Name { get; init; }
    public string? Description { get; set; }
    public List<string> EntityIds { get; init; } = [];
    public List<SemanticNamespace> ChildNamespaces { get; init; } = [];
}

/// <summary>
/// Information about how a model was created
/// </summary>
public sealed class ModelProvenance
{
    /// <summary>
    /// Primary source format (e.g., "CSharp", "Mermaid", "Whiteboard")
    /// </summary>
    public required string SourceFormat { get; init; }
    
    /// <summary>
    /// When this model was created/parsed
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// Source file paths if applicable
    /// </summary>
    public List<string> SourceFiles { get; init; } = [];
    
    /// <summary>
    /// Tool/parser version used
    /// </summary>
    public string? ToolVersion { get; set; }
    
    /// <summary>
    /// Any notes about the parsing/creation process
    /// </summary>
    public List<string> Notes { get; init; } = [];
}

/// <summary>
/// A validation issue found in the model
/// </summary>
public sealed class ValidationIssue
{
    public required ValidationSeverity Severity { get; init; }
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? EntityId { get; init; }
    public string? RelationshipId { get; init; }
    public string? PropertyName { get; init; }
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
