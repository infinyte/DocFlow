namespace DocFlow.Documentation.Diff;

/// <summary>Which facet of the spec a change affects.</summary>
public enum ChangeCategory
{
    Operation,
    Parameter,
    RequestBody,
    Response,
    Schema,
    Security
}

/// <summary>How disruptive a change is to consumers.</summary>
public enum ChangeSeverity
{
    Breaking,
    NonBreaking
}

/// <summary>A single spec-to-spec difference.</summary>
public sealed record SpecChange
{
    public required ChangeCategory Category { get; init; }
    public required ChangeSeverity Severity { get; init; }
    public required string Description { get; init; }
    public string? Path { get; init; }
}

/// <summary>The full set of differences plus convenience counters.</summary>
public sealed record SpecDiff
{
    public IReadOnlyList<SpecChange> Changes { get; init; } = [];

    public bool HasChanges => Changes.Count > 0;
    public int BreakingCount => Changes.Count(c => c.Severity == ChangeSeverity.Breaking);
    public int NonBreakingCount => Changes.Count(c => c.Severity == ChangeSeverity.NonBreaking);
}
