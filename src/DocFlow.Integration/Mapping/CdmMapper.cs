using System.Text.RegularExpressions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Integration.Models;
using DocFlow.Integration.Patterns;
using DocFlow.IMS;
using Microsoft.Extensions.Logging;

namespace DocFlow.Integration.Mapping;

/// <summary>
/// Maps external API schemas to your Canonical Data Model using pattern matching and IMS learning.
/// </summary>
public sealed class CdmMapper
{
    private readonly IIntelligentMappingService? _ims;
    private readonly ILogger<CdmMapper>? _logger;
    
    public CdmMapper(
        IIntelligentMappingService? ims = null,
        ILogger<CdmMapper>? logger = null)
    {
        _ims = ims;
        _logger = logger;
    }
    
    /// <summary>
    /// Generate mapping suggestions between external schema and CDM
    /// </summary>
    public async Task<MappingResult> MapToCdmAsync(
        SemanticModel externalSchema,
        SemanticModel cdm,
        MappingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new MappingOptions();
        
        var entityMappings = new List<EntityMapping>();
        var unmappedEntities = new List<string>();
        
        foreach (var externalEntity in externalSchema.Entities.Values)
        {
            var mapping = await MapEntityAsync(
                externalEntity, cdm, options, cancellationToken);
                
            if (mapping != null)
            {
                entityMappings.Add(mapping);
            }
            else
            {
                unmappedEntities.Add(externalEntity.Name);
            }
        }
        
        // Calculate confidence report
        var confidenceReport = CalculateConfidenceReport(entityMappings);
        
        _logger?.LogInformation(
            "Mapped {MappedCount}/{TotalCount} entities. Overall confidence: {Confidence:P1}",
            entityMappings.Count,
            externalSchema.Entities.Count,
            confidenceReport.OverallConfidence);
        
        return new MappingResult
        {
            EntityMappings = entityMappings,
            UnmappedEntities = unmappedEntities,
            ConfidenceReport = confidenceReport
        };
    }
    
    private async Task<EntityMapping?> MapEntityAsync(
        SemanticEntity externalEntity,
        SemanticModel cdm,
        MappingOptions options,
        CancellationToken cancellationToken)
    {
        // Try to find matching CDM entity
        var cdmEntity = FindMatchingCdmEntity(externalEntity, cdm);
        
        if (cdmEntity == null && !options.CreateMappingsForUnmatchedEntities)
        {
            return null;
        }
        
        var fieldMappings = new List<FieldMapping>();
        var totalConfidence = 0.0;
        
        foreach (var externalProperty in externalEntity.Properties)
        {
            var fieldMapping = await MapFieldAsync(
                externalProperty, 
                cdmEntity, 
                options,
                cancellationToken);
                
            if (fieldMapping != null)
            {
                fieldMappings.Add(fieldMapping);
                totalConfidence += fieldMapping.Confidence;
            }
            else
            {
                // Create unmatched field mapping
                fieldMappings.Add(new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = "???",
                    Confidence = 0,
                    IsAutoMapped = false,
                    Reasoning = "No matching CDM field found"
                });
            }
        }
        
        var avgConfidence = fieldMappings.Count > 0 
            ? totalConfidence / fieldMappings.Count 
            : 0;
        
        return new EntityMapping
        {
            ExternalEntityName = externalEntity.Name,
            CdmEntityName = cdmEntity?.Name ?? externalEntity.Name,
            FieldMappings = fieldMappings,
            Confidence = avgConfidence,
            Status = avgConfidence > 0.9 
                ? IntegrationStatus.Verified 
                : avgConfidence > 0.7 
                    ? IntegrationStatus.NeedsReview 
                    : IntegrationStatus.InProgress
        };
    }
    
    private SemanticEntity? FindMatchingCdmEntity(
        SemanticEntity externalEntity,
        SemanticModel cdm)
    {
        var externalName = NormalizeName(externalEntity.Name);

        // Exact match (highest priority)
        var exact = cdm.Entities.Values
            .FirstOrDefault(e => NormalizeName(e.Name) == externalName);
        if (exact != null) return exact;

        // Try removing common suffixes from external name
        var withoutSuffix = RemoveCommonSuffixes(externalName);
        var suffixMatch = cdm.Entities.Values
            .FirstOrDefault(e => NormalizeName(e.Name) == withoutSuffix);
        if (suffixMatch != null) return suffixMatch;

        // Try removing common suffixes from CDM names to match external
        var cdmSuffixMatch = cdm.Entities.Values
            .FirstOrDefault(e => RemoveCommonSuffixes(NormalizeName(e.Name)) == externalName);
        if (cdmSuffixMatch != null) return cdmSuffixMatch;

        // Semantic concept matching for common domain patterns
        var semanticMatch = TrySemanticEntityMatch(externalEntity.Name, cdm);
        if (semanticMatch != null) return semanticMatch;

        // Try fuzzy matching based on properties (but require higher overlap to avoid false positives)
        var bestMatch = cdm.Entities.Values
            .Select(e => new
            {
                Entity = e,
                Score = CalculatePropertyOverlap(externalEntity, e)
            })
            .Where(x => x.Score > 0.6) // Increased threshold to reduce false positives
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        return bestMatch?.Entity;
    }

    private static SemanticEntity? TrySemanticEntityMatch(string externalName, SemanticModel cdm)
    {
        // Common domain concept mappings
        var semanticMappings = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            // E-commerce / Retail
            ["Pet"] = ["Product", "Item", "Goods", "Merchandise"],
            ["Product"] = ["Pet", "Item", "Goods", "Merchandise"],
            ["Item"] = ["Product", "Pet", "Goods"],
            ["Customer"] = ["User", "Client", "Account", "Member"],
            ["User"] = ["Customer", "Client", "Account", "Member"],
            ["Cart"] = ["Basket", "ShoppingCart"],
            ["Basket"] = ["Cart", "ShoppingCart"],

            // General
            ["Category"] = ["ProductCategory", "ItemCategory", "Type", "Classification"],
            ["Tag"] = ["Label", "Keyword"],
            ["Status"] = ["State", "Condition"]
        };

        if (semanticMappings.TryGetValue(externalName, out var possibleMatches))
        {
            foreach (var match in possibleMatches)
            {
                var cdmEntity = cdm.Entities.Values
                    .FirstOrDefault(e => NormalizeName(e.Name).Contains(NormalizeName(match)));
                if (cdmEntity != null) return cdmEntity;
            }
        }

        return null;
    }
    
    private async Task<FieldMapping?> MapFieldAsync(
        SemanticProperty externalProperty,
        SemanticEntity? cdmEntity,
        MappingOptions options,
        CancellationToken cancellationToken)
    {
        // First, try IMS suggestions if available
        if (_ims != null)
        {
            var suggestion = await TryGetImsSuggestionAsync(
                externalProperty, cdmEntity, cancellationToken);
            if (suggestion != null) return suggestion;
        }
        
        // Fall back to pattern matching
        var patternMatch = TryPatternMatch(externalProperty);
        if (patternMatch != null && cdmEntity != null)
        {
            // Verify the pattern match target exists in CDM entity
            var targetProperty = cdmEntity.Properties
                .FirstOrDefault(p => p.Name.Equals(
                    patternMatch.Value.targetField, 
                    StringComparison.OrdinalIgnoreCase));
                    
            if (targetProperty != null)
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = targetProperty.Name,
                    Confidence = 0.85, // High confidence for pattern matches
                    IsAutoMapped = true,
                    Reasoning = $"Pattern match: {patternMatch.Value.pattern}"
                };
            }
        }
        
        // Try name-based matching
        if (cdmEntity != null)
        {
            var nameMatch = TryNameMatch(externalProperty, cdmEntity);
            if (nameMatch != null) return nameMatch;
        }
        
        return null;
    }
    
    private async Task<FieldMapping?> TryGetImsSuggestionAsync(
        SemanticProperty externalProperty,
        SemanticEntity? cdmEntity,
        CancellationToken cancellationToken)
    {
        if (_ims == null || cdmEntity == null) return null;
        
        try
        {
            // Build a mini model for the IMS query
            var sourceModel = new SemanticModel();
            var tempEntity = new SemanticEntity
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Source"
            };
            tempEntity.Properties.Add(externalProperty);
            sourceModel.AddEntity(tempEntity);
            
            var suggestions = await _ims.SuggestMappingsAsync(
                sourceModel, "CDM", cancellationToken: cancellationToken);
                
            var highConfidence = suggestions.HighConfidence.FirstOrDefault();
            if (highConfidence != null)
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = highConfidence.Transformation.Description,
                    Confidence = highConfidence.Confidence,
                    IsAutoMapped = true,
                    Reasoning = highConfidence.Reasoning
                };
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "IMS suggestion failed for {Property}", externalProperty.Name);
        }
        
        return null;
    }
    
    private static (string pattern, string targetField)? TryPatternMatch(SemanticProperty property)
    {
        var propName = property.Name.ToLowerInvariant();
        
        foreach (var pattern in ApiMappingPatterns.GetAllPatterns())
        {
            if (Regex.IsMatch(propName, pattern.Condition, RegexOptions.IgnoreCase))
            {
                if (pattern.Parameters.TryGetValue("targetField", out var target))
                {
                    return (pattern.Condition, target.ToString()!);
                }
            }
        }
        
        return null;
    }
    
    private static FieldMapping? TryNameMatch(
        SemanticProperty externalProperty,
        SemanticEntity cdmEntity)
    {
        var normalizedExternal = NormalizeName(externalProperty.Name);
        var externalLower = externalProperty.Name.ToLowerInvariant();

        // Multi-pass matching: check all properties for each match type before falling back
        // This prevents early matches (like type matching) from blocking better matches

        // Pass 1: Exact match (highest priority)
        foreach (var cdmProperty in cdmEntity.Properties)
        {
            if (normalizedExternal == NormalizeName(cdmProperty.Name))
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = cdmProperty.Name,
                    Confidence = 0.95,
                    IsAutoMapped = true,
                    Reasoning = "Exact name match"
                };
            }
        }

        // Pass 2: ID field matching
        foreach (var cdmProperty in cdmEntity.Properties)
        {
            var cdmLower = cdmProperty.Name.ToLowerInvariant();

            if ((externalLower == "id" && cdmLower.EndsWith("id")) ||
                (cdmLower == "id" && externalLower.EndsWith("id")))
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = cdmProperty.Name,
                    Confidence = 0.85,
                    IsAutoMapped = true,
                    Reasoning = "ID field match"
                };
            }
        }

        // Pass 3: Contains match (partial name)
        foreach (var cdmProperty in cdmEntity.Properties)
        {
            var normalizedCdm = NormalizeName(cdmProperty.Name);

            if (normalizedCdm.Contains(normalizedExternal) ||
                normalizedExternal.Contains(normalizedCdm))
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = cdmProperty.Name,
                    Confidence = 0.75,
                    IsAutoMapped = true,
                    Reasoning = "Partial name match"
                };
            }
        }

        // Pass 4: Foreign key pattern matching
        foreach (var cdmProperty in cdmEntity.Properties)
        {
            var cdmLower = cdmProperty.Name.ToLowerInvariant();

            if (externalLower.EndsWith("id") && cdmLower.EndsWith("id") &&
                externalLower != "id" && cdmLower != "id")
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = cdmProperty.Name,
                    Confidence = 0.70,
                    IsAutoMapped = true,
                    Reasoning = "Foreign key pattern match"
                };
            }
        }

        // Pass 5: Date/time field matching
        foreach (var cdmProperty in cdmEntity.Properties)
        {
            var cdmLower = cdmProperty.Name.ToLowerInvariant();

            if ((externalLower.Contains("date") || externalLower.Contains("time")) &&
                (cdmLower.Contains("date") || cdmLower.Contains("time")))
            {
                return new FieldMapping
                {
                    SourceField = externalProperty.Name,
                    TargetField = cdmProperty.Name,
                    Confidence = 0.70,
                    IsAutoMapped = true,
                    Reasoning = "Date/time field match"
                };
            }
        }

        // Pass 6: Type-based matching (lowest priority - only for complete mismatches)
        // Skip type matching as it causes too many false positives
        // Fields that don't match by name are better left as unmapped

        return null;
    }

    private static bool AreTypesCompatible(SemanticType external, SemanticType cdm)
    {
        // Exact type match
        if (external.Name == cdm.Name) return true;

        // Common type equivalences
        var compatibleTypes = new Dictionary<string, HashSet<string>>
        {
            ["int"] = ["long", "Int32", "Int64"],
            ["long"] = ["int", "Int32", "Int64"],
            ["string"] = ["String"],
            ["bool"] = ["boolean", "Boolean"],
            ["DateTime"] = ["DateTimeOffset", "DateOnly"],
            ["Guid"] = ["string", "String"]
        };

        if (compatibleTypes.TryGetValue(external.Name, out var compatible))
        {
            return compatible.Contains(cdm.Name);
        }

        return false;
    }
    
    private static string NormalizeName(string name)
    {
        // Remove common prefixes/suffixes, convert to lowercase, remove separators
        return Regex.Replace(name, @"[_\-\s]", "").ToLowerInvariant();
    }
    
    private static string RemoveCommonSuffixes(string name)
    {
        var suffixes = new[] { "dto", "model", "entity", "request", "response", "vm", "viewmodel" };
        
        foreach (var suffix in suffixes)
        {
            if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return name[..^suffix.Length];
            }
        }
        
        return name;
    }
    
    private static double CalculatePropertyOverlap(
        SemanticEntity external, 
        SemanticEntity cdm)
    {
        var externalProps = external.Properties
            .Select(p => NormalizeName(p.Name))
            .ToHashSet();
            
        var cdmProps = cdm.Properties
            .Select(p => NormalizeName(p.Name))
            .ToHashSet();
            
        var intersection = externalProps.Intersect(cdmProps).Count();
        var union = externalProps.Union(cdmProps).Count();
        
        return union > 0 ? (double)intersection / union : 0;
    }
    
    private static MappingConfidenceReport CalculateConfidenceReport(
        List<EntityMapping> mappings)
    {
        var allFieldMappings = mappings.SelectMany(m => m.FieldMappings).ToList();
        
        return new MappingConfidenceReport
        {
            TotalMappings = allFieldMappings.Count,
            HighConfidence = allFieldMappings.Count(m => m.Confidence > 0.9),
            MediumConfidence = allFieldMappings.Count(m => m.Confidence > 0.7 && m.Confidence <= 0.9),
            LowConfidence = allFieldMappings.Count(m => m.Confidence > 0 && m.Confidence <= 0.7),
            Unmapped = allFieldMappings.Count(m => m.Confidence == 0),
            OverallConfidence = allFieldMappings.Count > 0 
                ? allFieldMappings.Average(m => m.Confidence) 
                : 0,
            Issues = allFieldMappings
                .Where(m => m.Confidence < 0.7)
                .Select(m => new MappingIssue
                {
                    Field = m.SourceField,
                    Type = m.Confidence == 0 ? MappingIssueType.NoMatch : MappingIssueType.AmbiguousMatch,
                    Description = m.Reasoning ?? "Low confidence mapping"
                })
                .ToList()
        };
    }
}

public sealed class MappingOptions
{
    /// <summary>
    /// Create mappings even for entities with no CDM match
    /// </summary>
    public bool CreateMappingsForUnmatchedEntities { get; init; } = true;
    
    /// <summary>
    /// Minimum confidence threshold for auto-approving mappings
    /// </summary>
    public double AutoApproveThreshold { get; init; } = 0.9;
    
    /// <summary>
    /// Use IMS for pattern suggestions
    /// </summary>
    public bool UseIms { get; init; } = true;
    
    /// <summary>
    /// Domain-specific pattern set to use
    /// </summary>
    public string? DomainPatternSet { get; init; }
}

public sealed class MappingResult
{
    public List<EntityMapping> EntityMappings { get; init; } = [];
    public List<string> UnmappedEntities { get; init; } = [];
    public MappingConfidenceReport ConfidenceReport { get; init; } = new();
}
