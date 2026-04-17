using DocFlow.Core.CanonicalModel;

namespace DocFlow.Core.Abstractions;

/// <summary>
/// Strategy for turning an API specification (OpenAPI, AsyncAPI, GraphQL SDL, Postman, …)
/// into a canonical <see cref="SemanticModel"/>. Implementations are stateless and may
/// inspect both the path and the content to decide whether they apply.
/// </summary>
public interface IApiSpecParser
{
    /// <summary>A short, stable identifier (e.g. <c>"OpenAPI"</c>) used in error messages.</summary>
    string Name { get; }

    /// <summary>
    /// Returns <c>true</c> when this parser believes it can parse the given input.
    /// <paramref name="path"/> may be the original filename (the extension is a strong signal);
    /// <paramref name="content"/> is an optional first look at the file contents.
    /// Either argument may be <c>null</c> / empty.
    /// </summary>
    bool CanParse(string? path, string? content);

    /// <summary>
    /// Parse the stream into a <see cref="SemanticModel"/>. The stream is read to completion.
    /// Throws <see cref="FormatException"/> when the input cannot be parsed.
    /// </summary>
    Task<SemanticModel> ParseAsync(Stream input, CancellationToken cancellationToken = default);
}
