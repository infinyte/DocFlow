namespace DocFlow.AI.Providers;

/// <summary>
/// Abstraction for AI providers that can analyze images and generate structured output.
/// Implementations include Claude, OpenAI GPT-4V, local models, etc.
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Provider identifier (e.g., "Claude", "OpenAI", "Local")
    /// </summary>
    string ProviderId { get; }
    
    /// <summary>
    /// Does this provider support vision/image analysis?
    /// </summary>
    bool SupportsVision { get; }
    
    /// <summary>
    /// Is this provider available (API key configured, model loaded, etc.)?
    /// </summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Analyze an image and return structured analysis
    /// </summary>
    Task<ImageAnalysisResult> AnalyzeImageAsync(
        ImageAnalysisRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate text completion
    /// </summary>
    Task<TextCompletionResult> CompleteTextAsync(
        TextCompletionRequest request,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generate structured data from a prompt
    /// </summary>
    Task<StructuredOutputResult<T>> GenerateStructuredAsync<T>(
        StructuredOutputRequest request,
        CancellationToken cancellationToken = default) where T : class;
}

/// <summary>
/// Request for image analysis
/// </summary>
public sealed class ImageAnalysisRequest
{
    /// <summary>
    /// The image data
    /// </summary>
    public required byte[] ImageData { get; init; }
    
    /// <summary>
    /// MIME type of the image
    /// </summary>
    public string MimeType { get; init; } = "image/jpeg";
    
    /// <summary>
    /// The analysis prompt/instructions
    /// </summary>
    public required string Prompt { get; init; }
    
    /// <summary>
    /// System prompt/context
    /// </summary>
    public string? SystemPrompt { get; init; }
    
    /// <summary>
    /// Expected output format hint
    /// </summary>
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Text;
    
    /// <summary>
    /// Maximum tokens in response
    /// </summary>
    public int? MaxTokens { get; init; }
    
    /// <summary>
    /// Temperature (0-1) for response randomness
    /// </summary>
    public double? Temperature { get; init; }
}

/// <summary>
/// Result of image analysis
/// </summary>
public sealed class ImageAnalysisResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? ErrorMessage { get; init; }
    public TokenUsage? Usage { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Request for text completion
/// </summary>
public sealed class TextCompletionRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public List<Message>? ConversationHistory { get; init; }
    public OutputFormat OutputFormat { get; init; } = OutputFormat.Text;
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
}

public sealed class Message
{
    public required MessageRole Role { get; init; }
    public required string Content { get; init; }
}

public enum MessageRole
{
    User,
    Assistant,
    System
}

/// <summary>
/// Result of text completion
/// </summary>
public sealed class TextCompletionResult
{
    public bool Success { get; init; }
    public string? Content { get; init; }
    public string? ErrorMessage { get; init; }
    public TokenUsage? Usage { get; init; }
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Request for structured output generation
/// </summary>
public sealed class StructuredOutputRequest
{
    public required string Prompt { get; init; }
    public string? SystemPrompt { get; init; }
    public byte[]? ImageData { get; init; }
    public string? ImageMimeType { get; init; }
    public int? MaxTokens { get; init; }
    public double? Temperature { get; init; }
}

/// <summary>
/// Result of structured output generation
/// </summary>
public sealed class StructuredOutputResult<T> where T : class
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? RawResponse { get; init; }
    public string? ErrorMessage { get; init; }
    public TokenUsage? Usage { get; init; }
    public TimeSpan Duration { get; init; }
}

public enum OutputFormat
{
    Text,
    Json,
    Markdown,
    Mermaid,
    Code
}

public sealed class TokenUsage
{
    public int PromptTokens { get; init; }
    public int CompletionTokens { get; init; }
    public int TotalTokens => PromptTokens + CompletionTokens;
}

/// <summary>
/// Configuration for AI providers
/// </summary>
public sealed class AiProviderOptions
{
    /// <summary>
    /// Claude API key
    /// </summary>
    public string? ClaudeApiKey { get; set; }
    
    /// <summary>
    /// Claude model to use
    /// </summary>
    public string ClaudeModel { get; set; } = "claude-sonnet-4-20250514";
    
    /// <summary>
    /// OpenAI API key
    /// </summary>
    public string? OpenAiApiKey { get; set; }
    
    /// <summary>
    /// OpenAI model to use
    /// </summary>
    public string OpenAiModel { get; set; } = "gpt-4o";
    
    /// <summary>
    /// Preferred provider order (first available will be used)
    /// </summary>
    public List<string> PreferredProviders { get; set; } = ["Claude", "OpenAI", "Local"];
    
    /// <summary>
    /// Timeout for API calls
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(2);
    
    /// <summary>
    /// Enable local model fallback
    /// </summary>
    public bool EnableLocalFallback { get; set; } = true;
    
    /// <summary>
    /// Path to local ONNX models
    /// </summary>
    public string? LocalModelsPath { get; set; }
}
