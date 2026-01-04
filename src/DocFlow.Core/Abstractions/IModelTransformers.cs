using DocFlow.Core.CanonicalModel;

namespace DocFlow.Core.Abstractions;

/// <summary>
/// Parses a source format into the canonical semantic model.
/// All input formats (C#, Mermaid, whiteboard images, etc.) implement this interface.
/// </summary>
public interface IModelParser
{
    /// <summary>
    /// The format this parser handles (e.g., "CSharp", "Mermaid", "PlantUML", "Whiteboard")
    /// </summary>
    string SourceFormat { get; }
    
    /// <summary>
    /// File extensions this parser can handle (e.g., [".cs"], [".mmd", ".mermaid"])
    /// </summary>
    IReadOnlyList<string> SupportedExtensions { get; }
    
    /// <summary>
    /// Can this parser handle the given input?
    /// </summary>
    bool CanParse(ParserInput input);
    
    /// <summary>
    /// Parse the input into a semantic model
    /// </summary>
    Task<ParseResult> ParseAsync(ParserInput input, ParserOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Input for a parser - can be file content, file path, or binary data (for images)
/// </summary>
public sealed class ParserInput
{
    /// <summary>
    /// Text content to parse (for text-based formats)
    /// </summary>
    public string? Content { get; init; }
    
    /// <summary>
    /// File path (if parsing from file)
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// Binary data (for images, etc.)
    /// </summary>
    public byte[]? BinaryData { get; init; }
    
    /// <summary>
    /// MIME type if known
    /// </summary>
    public string? MimeType { get; init; }
    
    /// <summary>
    /// Additional context/hints for the parser
    /// </summary>
    public Dictionary<string, object> Hints { get; init; } = [];
    
    // Factory methods
    public static ParserInput FromContent(string content) => new() { Content = content };
    public static ParserInput FromFile(string filePath) => new() { FilePath = filePath };
    public static ParserInput FromImage(byte[] imageData, string? mimeType = null) => 
        new() { BinaryData = imageData, MimeType = mimeType ?? "image/jpeg" };
}

/// <summary>
/// Options for parsing
/// </summary>
public sealed class ParserOptions
{
    /// <summary>
    /// Include detailed source location info
    /// </summary>
    public bool IncludeSourceInfo { get; init; } = true;
    
    /// <summary>
    /// Try to infer relationship types based on patterns
    /// </summary>
    public bool InferRelationships { get; init; } = true;
    
    /// <summary>
    /// Try to infer entity classifications based on patterns
    /// </summary>
    public bool InferClassifications { get; init; } = true;
    
    /// <summary>
    /// Minimum confidence threshold for including inferred elements
    /// </summary>
    public double MinimumConfidence { get; init; } = 0.5;
    
    /// <summary>
    /// Namespace/package filter - only parse entities in these namespaces
    /// </summary>
    public List<string>? NamespaceFilter { get; init; }
    
    /// <summary>
    /// Additional parser-specific options
    /// </summary>
    public Dictionary<string, object> ExtendedOptions { get; init; } = [];
}

/// <summary>
/// Result of parsing
/// </summary>
public sealed class ParseResult
{
    /// <summary>
    /// The parsed semantic model
    /// </summary>
    public required SemanticModel Model { get; init; }
    
    /// <summary>
    /// Was parsing successful?
    /// </summary>
    public bool Success { get; init; } = true;
    
    /// <summary>
    /// Any errors encountered during parsing
    /// </summary>
    public List<ParseError> Errors { get; init; } = [];
    
    /// <summary>
    /// Warnings (non-fatal issues)
    /// </summary>
    public List<ParseWarning> Warnings { get; init; } = [];
    
    /// <summary>
    /// Elements that couldn't be parsed (for partial success scenarios)
    /// </summary>
    public List<UnparsedElement> UnparsedElements { get; init; } = [];
    
    /// <summary>
    /// Parsing statistics
    /// </summary>
    public ParseStatistics? Statistics { get; init; }
    
    public static ParseResult Failed(params ParseError[] errors) => new()
    {
        Model = new SemanticModel(),
        Success = false,
        Errors = errors.ToList()
    };
}

public sealed class ParseError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public int? Line { get; init; }
    public int? Column { get; init; }
    public string? Context { get; init; }
}

public sealed class ParseWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public int? Line { get; init; }
}

public sealed class UnparsedElement
{
    public required string RawContent { get; init; }
    public string? Reason { get; init; }
    public int? Line { get; init; }
}

public sealed class ParseStatistics
{
    public int EntitiesParsed { get; init; }
    public int RelationshipsParsed { get; init; }
    public int ElementsSkipped { get; init; }
    public TimeSpan ParseDuration { get; init; }
}

/// <summary>
/// Generates output from a semantic model.
/// All output formats (C#, Mermaid, documentation, etc.) implement this interface.
/// </summary>
public interface IModelGenerator
{
    /// <summary>
    /// The format this generator produces (e.g., "CSharp", "Mermaid", "Markdown")
    /// </summary>
    string TargetFormat { get; }
    
    /// <summary>
    /// Default file extension for generated files
    /// </summary>
    string DefaultExtension { get; }
    
    /// <summary>
    /// Generate output from the semantic model
    /// </summary>
    Task<GenerateResult> GenerateAsync(SemanticModel model, GeneratorOptions? options = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for generation
/// </summary>
public sealed class GeneratorOptions
{
    /// <summary>
    /// Output file path (if generating to file)
    /// </summary>
    public string? OutputPath { get; init; }
    
    /// <summary>
    /// Filter to specific entities by ID
    /// </summary>
    public List<string>? EntityFilter { get; init; }
    
    /// <summary>
    /// Include relationships in output
    /// </summary>
    public bool IncludeRelationships { get; init; } = true;
    
    /// <summary>
    /// Add comments/documentation to generated output
    /// </summary>
    public bool IncludeComments { get; init; } = true;
    
    /// <summary>
    /// Generator-specific options
    /// </summary>
    public Dictionary<string, object> ExtendedOptions { get; init; } = [];
}

/// <summary>
/// Result of generation
/// </summary>
public sealed class GenerateResult
{
    /// <summary>
    /// The generated content (for single-file output)
    /// </summary>
    public string? Content { get; init; }
    
    /// <summary>
    /// Generated files (for multi-file output like C# projects)
    /// </summary>
    public List<GeneratedFile> Files { get; init; } = [];
    
    /// <summary>
    /// Binary content (for images, PDFs, etc.)
    /// </summary>
    public byte[]? BinaryContent { get; init; }
    
    /// <summary>
    /// Was generation successful?
    /// </summary>
    public bool Success { get; init; } = true;
    
    /// <summary>
    /// Any errors during generation
    /// </summary>
    public List<GenerateError> Errors { get; init; } = [];
    
    /// <summary>
    /// Warnings
    /// </summary>
    public List<GenerateWarning> Warnings { get; init; } = [];
    
    public static GenerateResult FromContent(string content) => new() { Content = content };
    public static GenerateResult FromFiles(params GeneratedFile[] files) => new() { Files = files.ToList() };
    public static GenerateResult Failed(params GenerateError[] errors) => new() { Success = false, Errors = errors.ToList() };
}

public sealed class GeneratedFile
{
    public required string Path { get; init; }
    public required string Content { get; init; }
    public string? Description { get; init; }
}

public sealed class GenerateError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? EntityId { get; init; }
}

public sealed class GenerateWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}

/// <summary>
/// Bidirectional transformer that can both parse and generate a format.
/// Combines IModelParser and IModelGenerator for formats that support round-tripping.
/// </summary>
public interface IBidirectionalTransformer : IModelParser, IModelGenerator
{
    /// <summary>
    /// Can this transformer perfectly round-trip content?
    /// (Parse → Generate → Parse produces identical model)
    /// </summary>
    bool SupportsLosslessRoundTrip { get; }
}
