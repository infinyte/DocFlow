using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Models;
using DocFlow.Documentation.Options;

namespace DocFlow.Documentation.Abstractions;

/// <summary>
/// Produces a documentation bundle (a set of <see cref="GeneratedFile"/>s) from a
/// <see cref="SemanticModel"/>. Implementations are pure: they return files in memory and
/// do not write to disk — callers (the CLI) own persistence.
/// </summary>
public interface IDocumentationGenerator
{
    /// <summary>
    /// Generate the documentation bundle.
    /// </summary>
    /// <param name="model">The canonical model to document.</param>
    /// <param name="options">Generation options (format, diagram kinds, grouping, etc.).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An ordered, deterministic list of files to emit.</returns>
    Task<IReadOnlyList<GeneratedFile>> GenerateAsync(
        SemanticModel model,
        DocumentationOptions options,
        CancellationToken cancellationToken = default);
}
