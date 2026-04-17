namespace DocFlow.Documentation.Models;

/// <summary>
/// A single file in a generated documentation bundle.
/// </summary>
/// <param name="RelativePath">Forward-slash path relative to the output root (e.g. <c>endpoints/pet.md</c>).</param>
/// <param name="Content">The file's text or binary content.</param>
/// <param name="MediaType">MIME type (e.g. <c>text/markdown</c>, <c>application/json</c>, <c>text/vnd.mermaid</c>).</param>
public sealed record GeneratedFile(
    string RelativePath,
    string Content,
    string MediaType);
