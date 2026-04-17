namespace DocFlow.Documentation.Options;

/// <summary>Output format for the documentation bundle.</summary>
public enum DocumentationFormat
{
    /// <summary>Plain Markdown files with embedded Mermaid fences.</summary>
    Markdown,

    /// <summary>Static HTML site rendered from the Markdown bundle (Phase 4).</summary>
    Html
}
