namespace DocFlow.Core.Abstractions;

/// <summary>
/// Selects the first registered <see cref="IApiSpecParser"/> that reports it can parse the
/// given input. Registration order matters: callers should register more specific parsers
/// ahead of generic ones.
/// </summary>
public sealed class SpecParserRegistry
{
    private readonly IReadOnlyList<IApiSpecParser> _parsers;

    public SpecParserRegistry(IEnumerable<IApiSpecParser> parsers)
    {
        _parsers = parsers?.ToList() ?? throw new ArgumentNullException(nameof(parsers));
    }

    /// <summary>Parsers in their registration order.</summary>
    public IReadOnlyList<IApiSpecParser> Parsers => _parsers;

    /// <summary>
    /// Finds the first parser that reports <see cref="IApiSpecParser.CanParse"/> for the input.
    /// Throws <see cref="InvalidOperationException"/> when no parser matches — the message
    /// includes the names of the registered parsers so callers can diagnose missing support.
    /// </summary>
    public IApiSpecParser Select(string? path, string? content)
    {
        foreach (var parser in _parsers)
        {
            if (parser.CanParse(path, content)) return parser;
        }

        var names = _parsers.Count == 0 ? "(none registered)" : string.Join(", ", _parsers.Select(p => p.Name));
        throw new InvalidOperationException(
            $"No registered API spec parser can parse '{path ?? "<inline>"}'. Registered parsers: {names}.");
    }
}
