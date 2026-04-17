using DocFlow.Documentation.Models;

namespace DocFlow.Documentation.Options;

/// <summary>
/// Options controlling how a documentation bundle is generated.
/// </summary>
public sealed record DocumentationOptions
{
    /// <summary>Output format. Defaults to <see cref="DocumentationFormat.Markdown"/>.</summary>
    public DocumentationFormat Format { get; init; } = DocumentationFormat.Markdown;

    /// <summary>Diagram kinds to emit. Phase 1 default is <see cref="DiagramKinds.Class"/>.</summary>
    public DiagramKinds Diagrams { get; init; } = DiagramKinds.Class;

    /// <summary>When true, emit Example Request/Response blocks on endpoint pages (Phase 3).</summary>
    public bool WithExamples { get; init; }

    /// <summary>How to group endpoint pages. Defaults to <see cref="GroupBy.Tag"/>.</summary>
    public GroupBy GroupBy { get; init; } = GroupBy.Tag;

    /// <summary>Override the API title rendered in the bundle. Null means "use the spec title".</summary>
    public string? Title { get; init; }

    /// <summary>
    /// Optional raw source spec to include in the bundle under <c>assets/</c>.
    /// Callers (the CLI) supply the original bytes so the spec is preserved verbatim.
    /// The generator emits this file as-is without parsing or re-encoding.
    /// </summary>
    public GeneratedFile? SourceSpec { get; init; }
}
