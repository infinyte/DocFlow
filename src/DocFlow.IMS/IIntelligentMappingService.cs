using DocFlow.Core.CanonicalModel;

namespace DocFlow.IMS;

/// <summary>
/// The Intelligent Mapping Service - learns transformation patterns and applies them to new inputs.
/// 
/// Key capabilities:
/// - Learns from example transformations (observe)
/// - Generalizes patterns across examples (learn)
/// - Suggests mappings for new inputs with confidence scores (suggest)
/// - Improves from user feedback (refine)
/// 
/// All transformations are BIDIRECTIONAL - if we can map A→B, we can map B→A.
/// </summary>
public interface IIntelligentMappingService
{
    /// <summary>
    /// Observe a transformation and learn patterns from it
    /// </summary>
    Task<LearnResult> LearnFromExampleAsync(
        TransformationExample example,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Suggest mappings for transforming from source to target format
    /// </summary>
    Task<MappingSuggestions> SuggestMappingsAsync(
        SemanticModel sourceModel,
        string targetFormat,
        MappingContext? context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Apply a user's feedback to improve future suggestions
    /// </summary>
    Task ApplyFeedbackAsync(
        MappingFeedback feedback,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get patterns learned for a specific transformation type
    /// </summary>
    Task<IReadOnlyList<LearnedPattern>> GetPatternsAsync(
        string sourceFormat,
        string targetFormat,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Export learned patterns (for backup/sharing)
    /// </summary>
    Task<PatternExport> ExportPatternsAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Import patterns (from backup/shared source)
    /// </summary>
    Task ImportPatternsAsync(
        PatternExport patterns,
        ImportMode mode = ImportMode.Merge,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// An example transformation for the IMS to learn from
/// </summary>
public sealed class TransformationExample
{
    /// <summary>
    /// Source format (e.g., "CSharp", "Mermaid")
    /// </summary>
    public required string SourceFormat { get; init; }
    
    /// <summary>
    /// Target format
    /// </summary>
    public required string TargetFormat { get; init; }
    
    /// <summary>
    /// The source content/model
    /// </summary>
    public required SemanticModel SourceModel { get; init; }
    
    /// <summary>
    /// The target content/model (what the source was transformed to)
    /// </summary>
    public required SemanticModel TargetModel { get; init; }
    
    /// <summary>
    /// Explicit mappings if known (entity ID → entity ID)
    /// </summary>
    public Dictionary<string, string>? ExplicitEntityMappings { get; init; }
    
    /// <summary>
    /// Additional context about this transformation
    /// </summary>
    public string? Context { get; init; }
    
    /// <summary>
    /// Is this a high-quality example (manually verified)?
    /// </summary>
    public bool IsVerified { get; init; }
}

/// <summary>
/// Result of learning from an example
/// </summary>
public sealed class LearnResult
{
    public bool Success { get; init; }
    public int NewPatternsLearned { get; init; }
    public int ExistingPatternsReinforced { get; init; }
    public List<LearnedPattern> Patterns { get; init; } = [];
    public List<string> Errors { get; init; } = [];
}

/// <summary>
/// Context for mapping suggestions
/// </summary>
public sealed class MappingContext
{
    /// <summary>
    /// Domain/project context (e.g., "e-commerce", "healthcare")
    /// </summary>
    public string? Domain { get; init; }
    
    /// <summary>
    /// Coding conventions to follow
    /// </summary>
    public CodingConventions? Conventions { get; init; }
    
    /// <summary>
    /// Previously applied mappings in this session (for consistency)
    /// </summary>
    public List<AppliedMapping>? PreviousMappings { get; init; }
    
    /// <summary>
    /// User preferences
    /// </summary>
    public Dictionary<string, object>? Preferences { get; init; }
}

public sealed class CodingConventions
{
    public NamingConvention PropertyNaming { get; init; } = NamingConvention.PascalCase;
    public NamingConvention MethodNaming { get; init; } = NamingConvention.PascalCase;
    public NamingConvention FieldNaming { get; init; } = NamingConvention.CamelCaseWithUnderscore;
    public bool UseRecordsForValueObjects { get; init; } = true;
    public bool GenerateNullableAnnotations { get; init; } = true;
}

public enum NamingConvention
{
    PascalCase,
    CamelCase,
    SnakeCase,
    CamelCaseWithUnderscore,
    KebabCase
}

public sealed class AppliedMapping
{
    public required string SourceElementId { get; init; }
    public required string TargetElementId { get; init; }
    public required string MappingType { get; init; }
}

/// <summary>
/// Mapping suggestions from the IMS
/// </summary>
public sealed class MappingSuggestions
{
    /// <summary>
    /// High confidence mappings (>90%) - can be auto-applied
    /// </summary>
    public List<SuggestedMapping> HighConfidence { get; init; } = [];
    
    /// <summary>
    /// Medium confidence mappings (70-90%) - recommend review
    /// </summary>
    public List<SuggestedMapping> MediumConfidence { get; init; } = [];
    
    /// <summary>
    /// Low confidence mappings (less than 70%) - need user input
    /// </summary>
    public List<SuggestedMapping> LowConfidence { get; init; } = [];
    
    /// <summary>
    /// Elements that couldn't be mapped - need manual handling
    /// </summary>
    public List<UnmappedElement> Unmapped { get; init; } = [];
    
    /// <summary>
    /// Overall statistics
    /// </summary>
    public MappingStatistics Statistics { get; init; } = new();
}

/// <summary>
/// A suggested mapping from the IMS
/// </summary>
public sealed class SuggestedMapping
{
    public required string Id { get; init; }
    
    /// <summary>
    /// Source element identifier
    /// </summary>
    public required string SourceElementId { get; init; }
    
    /// <summary>
    /// What type of mapping this is
    /// </summary>
    public required MappingType Type { get; init; }
    
    /// <summary>
    /// The suggested transformation
    /// </summary>
    public required TransformationSuggestion Transformation { get; init; }
    
    /// <summary>
    /// Confidence score (0-1)
    /// </summary>
    public double Confidence { get; init; }
    
    /// <summary>
    /// Why this mapping was suggested
    /// </summary>
    public required string Reasoning { get; init; }
    
    /// <summary>
    /// Which learned pattern(s) this is based on
    /// </summary>
    public List<string> BasedOnPatterns { get; init; } = [];
    
    /// <summary>
    /// Alternative suggestions if this one is rejected
    /// </summary>
    public List<TransformationSuggestion> Alternatives { get; init; } = [];
}

public enum MappingType
{
    /// <summary>Entity to entity mapping</summary>
    EntityMapping,
    
    /// <summary>Property to property mapping</summary>
    PropertyMapping,
    
    /// <summary>Relationship type transformation</summary>
    RelationshipMapping,
    
    /// <summary>Classification inference</summary>
    ClassificationMapping,
    
    /// <summary>Name/identifier transformation</summary>
    NameTransformation,
    
    /// <summary>Type transformation (e.g., Java Optional to C# nullable)</summary>
    TypeTransformation,
    
    /// <summary>Structural transformation (e.g., flatten/nest)</summary>
    StructuralTransformation
}

/// <summary>
/// A specific transformation suggestion
/// </summary>
public sealed class TransformationSuggestion
{
    /// <summary>
    /// Description of the transformation
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// The transformation function/rule to apply
    /// </summary>
    public required ITransformationFunction Function { get; init; }
    
    /// <summary>
    /// Preview of the output if this transformation is applied
    /// </summary>
    public string? OutputPreview { get; init; }
}

/// <summary>
/// A bidirectional transformation function - the core of the IMS.
/// Inspired by Terraform's state model: if we can create, we can destroy.
/// If we can map forward, we can map backward.
/// </summary>
public interface ITransformationFunction
{
    /// <summary>
    /// Unique identifier for this transformation
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Human-readable name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Apply the forward transformation
    /// </summary>
    object Transform(object input);
    
    /// <summary>
    /// Apply the reverse transformation (if possible)
    /// </summary>
    object? ReverseTransform(object output);
    
    /// <summary>
    /// Is this transformation reversible?
    /// </summary>
    bool IsReversible { get; }
}

/// <summary>
/// Element that couldn't be mapped
/// </summary>
public sealed class UnmappedElement
{
    public required string ElementId { get; init; }
    public required string ElementType { get; init; }
    public required string Reason { get; init; }
    public List<string> SuggestedActions { get; init; } = [];
}

public sealed class MappingStatistics
{
    public int TotalElements { get; init; }
    public int HighConfidenceMappings { get; init; }
    public int MediumConfidenceMappings { get; init; }
    public int LowConfidenceMappings { get; init; }
    public int UnmappedElements { get; init; }
    public double AverageConfidence { get; init; }
    public int PatternsApplied { get; init; }
}

/// <summary>
/// Feedback on a mapping suggestion (for learning)
/// </summary>
public sealed class MappingFeedback
{
    /// <summary>
    /// The suggestion this feedback is for
    /// </summary>
    public required string SuggestionId { get; init; }
    
    /// <summary>
    /// Was the suggestion accepted?
    /// </summary>
    public required FeedbackType Type { get; init; }
    
    /// <summary>
    /// If corrected, what was the correct mapping?
    /// </summary>
    public TransformationSuggestion? Correction { get; init; }
    
    /// <summary>
    /// Additional context about why this feedback was given
    /// </summary>
    public string? Reason { get; init; }
}

public enum FeedbackType
{
    /// <summary>Suggestion was correct and accepted</summary>
    Accepted,
    
    /// <summary>Suggestion was rejected as incorrect</summary>
    Rejected,
    
    /// <summary>Suggestion was modified/corrected</summary>
    Corrected,
    
    /// <summary>Suggestion was partially correct</summary>
    PartiallyCorrect
}

/// <summary>
/// A pattern learned by the IMS
/// </summary>
public sealed class LearnedPattern
{
    public required string Id { get; init; }
    
    /// <summary>
    /// Source format this pattern applies to
    /// </summary>
    public required string SourceFormat { get; init; }
    
    /// <summary>
    /// Target format this pattern produces
    /// </summary>
    public required string TargetFormat { get; init; }
    
    /// <summary>
    /// Human-readable description of the pattern
    /// </summary>
    public required string Description { get; init; }
    
    /// <summary>
    /// The pattern rule/condition
    /// </summary>
    public required PatternRule Rule { get; init; }
    
    /// <summary>
    /// The transformation to apply when pattern matches
    /// </summary>
    public required ITransformationFunction Transformation { get; init; }
    
    /// <summary>
    /// How many times this pattern has been observed
    /// </summary>
    public int ObservationCount { get; init; }
    
    /// <summary>
    /// How many times this pattern was confirmed correct
    /// </summary>
    public int ConfirmationCount { get; init; }
    
    /// <summary>
    /// How many times this pattern was rejected/corrected
    /// </summary>
    public int RejectionCount { get; init; }
    
    /// <summary>
    /// Current confidence score (Bayesian-style update from observations)
    /// </summary>
    public double Confidence => ObservationCount > 0 
        ? (double)(ConfirmationCount + 1) / (ObservationCount + 2) // Laplace smoothing
        : 0.5;
    
    /// <summary>
    /// When this pattern was first learned
    /// </summary>
    public DateTime CreatedAt { get; init; }
    
    /// <summary>
    /// When this pattern was last applied/updated
    /// </summary>
    public DateTime LastUsedAt { get; set; }
    
    /// <summary>
    /// Example instances where this pattern was observed
    /// </summary>
    public List<PatternExample> Examples { get; init; } = [];
}

/// <summary>
/// A rule that defines when a pattern matches
/// </summary>
public sealed class PatternRule
{
    /// <summary>
    /// Type of rule
    /// </summary>
    public required PatternRuleType Type { get; init; }
    
    /// <summary>
    /// The condition expression
    /// </summary>
    public required string Condition { get; init; }
    
    /// <summary>
    /// Additional parameters for the rule
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = [];
}

public enum PatternRuleType
{
    /// <summary>Match by element name pattern (regex)</summary>
    NamePattern,
    
    /// <summary>Match by element type</summary>
    TypeMatch,
    
    /// <summary>Match by attribute/annotation presence</summary>
    AttributePresence,
    
    /// <summary>Match by relationship pattern</summary>
    RelationshipPattern,
    
    /// <summary>Match by structural pattern (e.g., has children of type X)</summary>
    StructuralPattern,
    
    /// <summary>Composite rule (AND/OR of other rules)</summary>
    Composite,
    
    /// <summary>ML-based pattern matching using embeddings</summary>
    MachineLearned
}

/// <summary>
/// An example instance of a pattern
/// </summary>
public sealed class PatternExample
{
    public required string SourceElement { get; init; }
    public required string TargetElement { get; init; }
    public DateTime ObservedAt { get; init; }
    public bool WasConfirmed { get; init; }
}

/// <summary>
/// Export of learned patterns (for backup, sharing, or team sync)
/// </summary>
public sealed class PatternExport
{
    public string Version { get; init; } = "1.0";
    public DateTime ExportedAt { get; init; } = DateTime.UtcNow;
    public string? ExportedBy { get; init; }
    public List<LearnedPattern> Patterns { get; init; } = [];
    public Dictionary<string, object> Metadata { get; init; } = [];
}

public enum ImportMode
{
    /// <summary>Merge with existing patterns, keeping higher confidence versions</summary>
    Merge,
    
    /// <summary>Replace all existing patterns</summary>
    Replace,
    
    /// <summary>Only add new patterns, don't update existing</summary>
    AddOnly
}
