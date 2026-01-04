using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocFlow.AI.Providers;

/// <summary>
/// Claude AI provider implementation for DocFlow.
/// Uses the Anthropic Messages API for vision and text analysis.
/// </summary>
public sealed class ClaudeProvider : IAiProvider, IDisposable
{
    private const string ApiBaseUrl = "https://api.anthropic.com/v1/messages";
    private const string ApiVersion = "2023-06-01";
    private const int DefaultMaxTokens = 4096;

    private const string UserConfigFileName = "config.json";
    private const string ProjectConfigFileName = "docflow.json";
    private const string DocFlowConfigDir = ".docflow";

    private readonly HttpClient _httpClient;
    private readonly ILogger<ClaudeProvider>? _logger;
    private readonly AiProviderOptions _options;
    private readonly bool _ownsHttpClient;
    private readonly string? _resolvedApiKey;

    public string ProviderId => "Claude";
    public bool SupportsVision => true;

    /// <summary>
    /// Create a ClaudeProvider with default options.
    /// API key is resolved from environment, user config, or project config.
    /// </summary>
    public ClaudeProvider(ILogger<ClaudeProvider>? logger = null)
        : this(new AiProviderOptions(), null, logger)
    {
    }

    /// <summary>
    /// Create a ClaudeProvider with explicit options.
    /// </summary>
    public ClaudeProvider(
        AiProviderOptions options,
        HttpClient? httpClient = null,
        ILogger<ClaudeProvider>? logger = null)
    {
        _options = options;
        _logger = logger;

        // Resolve API key from multiple sources
        _resolvedApiKey = ResolveApiKey(options, logger);

        if (httpClient != null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            _httpClient = new HttpClient
            {
                Timeout = options.Timeout
            };
            _ownsHttpClient = true;
        }

        // Set up headers
        if (!string.IsNullOrEmpty(_resolvedApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _resolvedApiKey);
        }
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        _httpClient.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// Create a ClaudeProvider for dependency injection.
    /// </summary>
    public ClaudeProvider(
        IOptions<AiProviderOptions> options,
        HttpClient httpClient,
        ILogger<ClaudeProvider> logger)
        : this(options.Value, httpClient, logger)
    {
    }

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_resolvedApiKey))
        {
            _logger?.LogWarning("Claude API key not configured");
            return Task.FromResult(false);
        }

        // Could do a test request here, but for now just check the key exists
        return Task.FromResult(true);
    }

    /// <summary>
    /// Resolves the API key from multiple sources in priority order:
    /// 1. Options (explicitly provided)
    /// 2. Environment variable: ANTHROPIC_API_KEY
    /// 3. User config: ~/.docflow/config.json
    /// 4. Project config: ./docflow.json
    /// </summary>
    /// <returns>The API key if found, null otherwise</returns>
    public static string? ResolveApiKey(AiProviderOptions? options = null, ILogger? logger = null)
    {
        // 1. Check options (explicitly provided)
        if (!string.IsNullOrEmpty(options?.ClaudeApiKey))
        {
            logger?.LogDebug("Using API key from options");
            return options.ClaudeApiKey;
        }

        // 2. Check environment variable
        var envKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
        if (!string.IsNullOrEmpty(envKey))
        {
            logger?.LogDebug("Using API key from ANTHROPIC_API_KEY environment variable");
            return envKey;
        }

        // 3. Check user config file (~/.docflow/config.json)
        var userConfigPath = GetUserConfigPath();
        var userKey = LoadApiKeyFromConfig(userConfigPath, logger);
        if (!string.IsNullOrEmpty(userKey))
        {
            logger?.LogDebug("Using API key from user config: {Path}", userConfigPath);
            return userKey;
        }

        // 4. Check project config file (./docflow.json)
        var projectConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ProjectConfigFileName);
        var projectKey = LoadApiKeyFromConfig(projectConfigPath, logger);
        if (!string.IsNullOrEmpty(projectKey))
        {
            logger?.LogDebug("Using API key from project config: {Path}", projectConfigPath);
            return projectKey;
        }

        return null;
    }

    /// <summary>
    /// Gets the API key, throwing an exception with helpful instructions if not found.
    /// </summary>
    public static string GetApiKeyOrThrow(AiProviderOptions? options = null)
    {
        var apiKey = ResolveApiKey(options);

        if (string.IsNullOrEmpty(apiKey))
        {
            var userConfigPath = GetUserConfigPath();
            var projectConfigPath = Path.Combine(Directory.GetCurrentDirectory(), ProjectConfigFileName);

            throw new ApiKeyNotFoundException(
                $$"""
                Claude API key not configured.

                Please configure your API key using one of these methods (in priority order):

                1. Environment variable:
                   Linux/macOS: export ANTHROPIC_API_KEY='sk-ant-...'
                   Windows CMD: set ANTHROPIC_API_KEY=sk-ant-...
                   PowerShell:  $env:ANTHROPIC_API_KEY='sk-ant-...'

                2. User config file: {{userConfigPath}}
                   {
                     "anthropicApiKey": "sk-ant-..."
                   }

                3. Project config file: {{projectConfigPath}}
                   {
                     "anthropicApiKey": "sk-ant-..."
                   }

                Get your API key at: https://console.anthropic.com/
                """);
        }

        return apiKey;
    }

    /// <summary>
    /// Gets the path to the user config directory (~/.docflow)
    /// </summary>
    public static string GetUserConfigDirectory()
    {
        // Cross-platform: Use HOME on Unix, USERPROFILE on Windows
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, DocFlowConfigDir);
    }

    /// <summary>
    /// Gets the path to the user config file (~/.docflow/config.json)
    /// </summary>
    public static string GetUserConfigPath()
    {
        return Path.Combine(GetUserConfigDirectory(), UserConfigFileName);
    }

    private static string? LoadApiKeyFromConfig(string configPath, ILogger? logger)
    {
        try
        {
            if (!File.Exists(configPath))
            {
                return null;
            }

            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<DocFlowConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return config?.AnthropicApiKey;
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to parse config file: {Path}", configPath);
            return null;
        }
        catch (IOException ex)
        {
            logger?.LogWarning(ex, "Failed to read config file: {Path}", configPath);
            return null;
        }
    }

    public async Task<ImageAnalysisResult> AnalyzeImageAsync(
        ImageAnalysisRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Build the message content with image
            var imageBase64 = Convert.ToBase64String(request.ImageData);
            var mediaType = request.MimeType switch
            {
                "image/png" => "image/png",
                "image/gif" => "image/gif",
                "image/webp" => "image/webp",
                _ => "image/jpeg"
            };

            var userContent = new List<object>
            {
                new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = mediaType,
                        data = imageBase64
                    }
                },
                new
                {
                    type = "text",
                    text = request.Prompt
                }
            };

            var requestBody = new ClaudeRequest
            {
                Model = _options.ClaudeModel,
                MaxTokens = request.MaxTokens ?? DefaultMaxTokens,
                System = request.SystemPrompt,
                Messages =
                [
                    new ClaudeMessage
                    {
                        Role = "user",
                        Content = userContent
                    }
                ]
            };

            if (request.Temperature.HasValue)
            {
                requestBody.Temperature = request.Temperature.Value;
            }

            var response = await SendRequestAsync(requestBody, cancellationToken);
            stopwatch.Stop();

            if (response.Error != null)
            {
                return new ImageAnalysisResult
                {
                    Success = false,
                    ErrorMessage = $"{response.Error.Type}: {response.Error.Message}",
                    Duration = stopwatch.Elapsed
                };
            }

            var content = ExtractTextContent(response);

            return new ImageAnalysisResult
            {
                Success = true,
                Content = content,
                Usage = response.Usage != null ? new TokenUsage
                {
                    PromptTokens = response.Usage.InputTokens,
                    CompletionTokens = response.Usage.OutputTokens
                } : null,
                Duration = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error calling Claude API");
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = $"HTTP error: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger?.LogError(ex, "Claude API request timed out");
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = "Request timed out",
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling Claude API");
            return new ImageAnalysisResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<TextCompletionResult> CompleteTextAsync(
        TextCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var messages = new List<ClaudeMessage>();

            // Add conversation history if present
            if (request.ConversationHistory != null)
            {
                foreach (var msg in request.ConversationHistory)
                {
                    messages.Add(new ClaudeMessage
                    {
                        Role = msg.Role == MessageRole.User ? "user" : "assistant",
                        Content = msg.Content
                    });
                }
            }

            // Add the current prompt
            messages.Add(new ClaudeMessage
            {
                Role = "user",
                Content = request.Prompt
            });

            var requestBody = new ClaudeRequest
            {
                Model = _options.ClaudeModel,
                MaxTokens = request.MaxTokens ?? DefaultMaxTokens,
                System = request.SystemPrompt,
                Messages = messages
            };

            if (request.Temperature.HasValue)
            {
                requestBody.Temperature = request.Temperature.Value;
            }

            var response = await SendRequestAsync(requestBody, cancellationToken);
            stopwatch.Stop();

            if (response.Error != null)
            {
                return new TextCompletionResult
                {
                    Success = false,
                    ErrorMessage = $"{response.Error.Type}: {response.Error.Message}",
                    Duration = stopwatch.Elapsed
                };
            }

            var content = ExtractTextContent(response);

            return new TextCompletionResult
            {
                Success = true,
                Content = content,
                Usage = response.Usage != null ? new TokenUsage
                {
                    PromptTokens = response.Usage.InputTokens,
                    CompletionTokens = response.Usage.OutputTokens
                } : null,
                Duration = stopwatch.Elapsed
            };
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error calling Claude API");
            return new TextCompletionResult
            {
                Success = false,
                ErrorMessage = $"HTTP error: {ex.Message}",
                Duration = stopwatch.Elapsed
            };
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error calling Claude API");
            return new TextCompletionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    public async Task<StructuredOutputResult<T>> GenerateStructuredAsync<T>(
        StructuredOutputRequest request,
        CancellationToken cancellationToken = default) where T : class
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            var userContent = new List<object>();

            // Add image if present
            if (request.ImageData != null)
            {
                var imageBase64 = Convert.ToBase64String(request.ImageData);
                var mediaType = request.ImageMimeType ?? "image/jpeg";

                userContent.Add(new
                {
                    type = "image",
                    source = new
                    {
                        type = "base64",
                        media_type = mediaType,
                        data = imageBase64
                    }
                });
            }

            // Add text prompt
            userContent.Add(new
            {
                type = "text",
                text = request.Prompt
            });

            var requestBody = new ClaudeRequest
            {
                Model = _options.ClaudeModel,
                MaxTokens = request.MaxTokens ?? DefaultMaxTokens,
                System = request.SystemPrompt,
                Messages =
                [
                    new ClaudeMessage
                    {
                        Role = "user",
                        Content = userContent.Count == 1 ? request.Prompt : userContent
                    }
                ]
            };

            if (request.Temperature.HasValue)
            {
                requestBody.Temperature = request.Temperature.Value;
            }

            var response = await SendRequestAsync(requestBody, cancellationToken);
            stopwatch.Stop();

            if (response.Error != null)
            {
                return new StructuredOutputResult<T>
                {
                    Success = false,
                    ErrorMessage = $"{response.Error.Type}: {response.Error.Message}",
                    Duration = stopwatch.Elapsed
                };
            }

            var content = ExtractTextContent(response);

            // Try to parse as JSON
            T? data = null;
            string? parseError = null;

            if (!string.IsNullOrEmpty(content))
            {
                try
                {
                    // Try to extract JSON from the response (it might be wrapped in markdown)
                    var jsonContent = ExtractJson(content);
                    data = JsonSerializer.Deserialize<T>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                }
                catch (JsonException ex)
                {
                    parseError = $"Failed to parse JSON: {ex.Message}";
                    _logger?.LogWarning(ex, "Failed to parse Claude response as JSON");
                }
            }

            return new StructuredOutputResult<T>
            {
                Success = data != null,
                Data = data,
                RawResponse = content,
                ErrorMessage = parseError,
                Usage = response.Usage != null ? new TokenUsage
                {
                    PromptTokens = response.Usage.InputTokens,
                    CompletionTokens = response.Usage.OutputTokens
                } : null,
                Duration = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error in GenerateStructuredAsync");
            return new StructuredOutputResult<T>
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    private async Task<ClaudeResponse> SendRequestAsync(
        ClaudeRequest request,
        CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(request, jsonOptions);
        _logger?.LogDebug("Sending request to Claude API: {RequestLength} chars", json.Length);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(ApiBaseUrl, content, cancellationToken);

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        _logger?.LogDebug("Received response from Claude API: {StatusCode}", response.StatusCode);

        var result = JsonSerializer.Deserialize<ClaudeResponse>(responseJson, jsonOptions);

        if (result == null)
        {
            return new ClaudeResponse
            {
                Error = new ClaudeError
                {
                    Type = "parse_error",
                    Message = "Failed to parse API response"
                }
            };
        }

        return result;
    }

    private static string ExtractTextContent(ClaudeResponse response)
    {
        if (response.Content == null || response.Content.Count == 0)
            return string.Empty;

        var textParts = new List<string>();

        foreach (var block in response.Content)
        {
            if (block.Type == "text" && !string.IsNullOrEmpty(block.Text))
            {
                textParts.Add(block.Text);
            }
        }

        return string.Join("\n", textParts);
    }

    private static string ExtractJson(string content)
    {
        // Try to extract JSON from markdown code blocks
        var jsonStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (jsonStart >= 0)
        {
            jsonStart = content.IndexOf('\n', jsonStart) + 1;
            var jsonEnd = content.IndexOf("```", jsonStart, StringComparison.Ordinal);
            if (jsonEnd > jsonStart)
            {
                return content[jsonStart..jsonEnd].Trim();
            }
        }

        // Try to find JSON object or array
        var firstBrace = content.IndexOf('{');
        var firstBracket = content.IndexOf('[');

        if (firstBrace >= 0 || firstBracket >= 0)
        {
            var start = (firstBrace >= 0 && firstBracket >= 0)
                ? Math.Min(firstBrace, firstBracket)
                : Math.Max(firstBrace, firstBracket);

            var isArray = content[start] == '[';
            var endChar = isArray ? ']' : '}';

            var depth = 0;
            for (var i = start; i < content.Length; i++)
            {
                if (content[i] == (isArray ? '[' : '{')) depth++;
                else if (content[i] == endChar) depth--;

                if (depth == 0)
                {
                    return content[start..(i + 1)];
                }
            }
        }

        return content;
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}

#region Claude API Models

internal sealed class ClaudeRequest
{
    public required string Model { get; init; }

    [JsonPropertyName("max_tokens")]
    public int MaxTokens { get; init; } = 4096;

    public string? System { get; init; }

    public double? Temperature { get; set; }

    public required List<ClaudeMessage> Messages { get; init; }
}

internal sealed class ClaudeMessage
{
    public required string Role { get; init; }

    // Can be string or array of content blocks
    public required object Content { get; init; }
}

internal sealed class ClaudeResponse
{
    public string? Id { get; init; }
    public string? Type { get; init; }
    public string? Role { get; init; }
    public List<ContentBlock>? Content { get; init; }
    public string? Model { get; init; }

    [JsonPropertyName("stop_reason")]
    public string? StopReason { get; init; }

    public ClaudeUsage? Usage { get; init; }
    public ClaudeError? Error { get; init; }
}

internal sealed class ContentBlock
{
    public string? Type { get; init; }
    public string? Text { get; init; }
}

internal sealed class ClaudeUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}

internal sealed class ClaudeError
{
    public string? Type { get; init; }
    public string? Message { get; init; }
}

#endregion

#region Config Models

/// <summary>
/// DocFlow configuration file format for config.json and docflow.json
/// </summary>
public sealed class DocFlowConfig
{
    /// <summary>
    /// Anthropic/Claude API key
    /// </summary>
    [JsonPropertyName("anthropicApiKey")]
    public string? AnthropicApiKey { get; init; }

    /// <summary>
    /// OpenAI API key (for future use)
    /// </summary>
    [JsonPropertyName("openaiApiKey")]
    public string? OpenAiApiKey { get; init; }

    /// <summary>
    /// Preferred AI provider
    /// </summary>
    [JsonPropertyName("preferredProvider")]
    public string? PreferredProvider { get; init; }
}

/// <summary>
/// Exception thrown when the API key is not found in any configured location.
/// </summary>
public sealed class ApiKeyNotFoundException : Exception
{
    public ApiKeyNotFoundException(string message) : base(message)
    {
    }

    public ApiKeyNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

#endregion
