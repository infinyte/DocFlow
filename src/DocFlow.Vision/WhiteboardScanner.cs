using System.Diagnostics;
using System.Text.RegularExpressions;
using DocFlow.AI.Providers;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using Microsoft.Extensions.Logging;

namespace DocFlow.Vision;

/// <summary>
/// Whiteboard scanner implementation using Claude's vision API.
/// Analyzes whiteboard photos and extracts Mermaid diagrams.
/// </summary>
public sealed partial class WhiteboardScanner : IWhiteboardScanner
{
    private readonly IAiProvider _aiProvider;
    private readonly IModelParser? _mermaidParser;
    private readonly ILogger<WhiteboardScanner>? _logger;

    // Supported image formats
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".webp", ".heic"
    };

    private const string SystemPrompt = """
        You are an expert at analyzing software architecture diagrams and converting them to Mermaid syntax.
        You have deep knowledge of UML, DDD (Domain-Driven Design), and software modeling patterns.
        Your task is to accurately extract diagram elements and relationships from whiteboard photos.
        """;

    private const string DiagramAnalysisPrompt = """
        Analyze this whiteboard/sketch image of a software diagram.

        1. Identify the diagram type (UML class diagram, flowchart, ER diagram, sequence diagram, etc.)
        2. Extract all entities/classes with their properties and methods
        3. Identify relationships between entities (inheritance, composition, association, dependency)
        4. Note any stereotypes or annotations visible (<<interface>>, <<abstract>>, <<aggregate root>>, etc.)

        Return ONLY valid Mermaid classDiagram syntax. No explanations, just the Mermaid code.

        Use proper Mermaid syntax:
        - Start with: classDiagram
        - Classes: class ClassName { }
        - Properties: +propertyName : Type (public), -propertyName : Type (private), #propertyName : Type (protected)
        - Methods: +methodName() ReturnType
        - Stereotypes: class ClassName { <<stereotype>> }
        - Inheritance: Parent <|-- Child
        - Composition: Container *-- Part
        - Aggregation: Container o-- Part
        - Association: ClassA --> ClassB
        - Dependency: ClassA ..> ClassB

        If you can't identify something clearly, make your best guess based on context.
        If there are multiple interpretations, choose the most common software modeling interpretation.

        IMPORTANT: Output ONLY the Mermaid code, no markdown code blocks, no explanations.
        """;

    private const string DiagramTypePrompt = """
        Quickly analyze this image and determine what type of software diagram it shows.

        Respond with ONLY a JSON object in this exact format:
        {"type": "CLASS_DIAGRAM", "confidence": 0.85, "alternativeTypes": [{"type": "ER_DIAGRAM", "confidence": 0.10}]}

        Valid types: CLASS_DIAGRAM, SEQUENCE_DIAGRAM, FLOWCHART, ER_DIAGRAM, STATE_DIAGRAM,
                     ACTIVITY_DIAGRAM, USE_CASE_DIAGRAM, COMPONENT_DIAGRAM, ARCHITECTURE_DIAGRAM,
                     MIND_MAP, DATA_FLOW_DIAGRAM, NETWORK_DIAGRAM, INFORMAL_SKETCH, UNKNOWN

        Output ONLY the JSON, nothing else.
        """;

    /// <summary>
    /// Create a WhiteboardScanner with default Claude provider.
    /// </summary>
    public WhiteboardScanner(ILogger<WhiteboardScanner>? logger = null)
        : this(new ClaudeProvider(), null, logger)
    {
    }

    /// <summary>
    /// Create a WhiteboardScanner with explicit AI provider.
    /// </summary>
    public WhiteboardScanner(
        IAiProvider aiProvider,
        IModelParser? mermaidParser = null,
        ILogger<WhiteboardScanner>? logger = null)
    {
        _aiProvider = aiProvider;
        _mermaidParser = mermaidParser;
        _logger = logger;
    }

    public async Task<WhiteboardScanResult> ScanAsync(
        WhiteboardInput input,
        WhiteboardScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        options ??= new WhiteboardScanOptions();
        var totalStopwatch = Stopwatch.StartNew();
        var errors = new List<ScanError>();
        var warnings = new List<ScanWarning>();

        try
        {
            // Load the image data
            var (imageData, mimeType) = await LoadImageAsync(input, cancellationToken);

            if (imageData == null || imageData.Length == 0)
            {
                return CreateFailedResult("No image data provided", errors);
            }

            _logger?.LogInformation("Scanning whiteboard image ({Size} bytes)", imageData.Length);

            // Detect diagram type first
            var detectionStopwatch = Stopwatch.StartNew();
            var diagramType = await DetectDiagramTypeAsync(imageData, cancellationToken);
            detectionStopwatch.Stop();

            _logger?.LogInformation(
                "Detected diagram type: {Type} (confidence: {Confidence:P0})",
                diagramType.PrimaryType, diagramType.Confidence);

            // Build the prompt with any context hints
            var prompt = DiagramAnalysisPrompt;
            if (!string.IsNullOrEmpty(input.ContextHint))
            {
                prompt = $"Context: {input.ContextHint}\n\n{prompt}";
            }

            // Call the AI provider to analyze the image
            var analysisStopwatch = Stopwatch.StartNew();
            var analysisResult = await _aiProvider.AnalyzeImageAsync(new ImageAnalysisRequest
            {
                ImageData = imageData,
                MimeType = mimeType,
                Prompt = prompt,
                SystemPrompt = SystemPrompt,
                OutputFormat = OutputFormat.Mermaid,
                MaxTokens = 4096,
                Temperature = 0.1 // Low temperature for more deterministic output
            }, cancellationToken);
            analysisStopwatch.Stop();

            if (!analysisResult.Success)
            {
                _logger?.LogError("AI analysis failed: {Error}", analysisResult.ErrorMessage);
                errors.Add(new ScanError
                {
                    Code = "AI_ANALYSIS_FAILED",
                    Message = analysisResult.ErrorMessage ?? "Unknown AI error"
                });
                return CreateFailedResult(analysisResult.ErrorMessage ?? "AI analysis failed", errors);
            }

            var mermaidContent = CleanMermaidOutput(analysisResult.Content ?? string.Empty);
            _logger?.LogDebug("Generated Mermaid:\n{Mermaid}", mermaidContent);

            if (string.IsNullOrWhiteSpace(mermaidContent))
            {
                errors.Add(new ScanError
                {
                    Code = "EMPTY_RESULT",
                    Message = "AI returned empty diagram content"
                });
                return CreateFailedResult("No diagram content extracted", errors);
            }

            // Parse the Mermaid into a SemanticModel
            SemanticModel model;
            if (_mermaidParser != null)
            {
                var parseResult = await _mermaidParser.ParseAsync(
                    ParserInput.FromContent(mermaidContent),
                    cancellationToken: cancellationToken);

                if (!parseResult.Success)
                {
                    // Include warnings but continue if we have partial results
                    foreach (var error in parseResult.Errors)
                    {
                        warnings.Add(new ScanWarning
                        {
                            Code = "PARSE_WARNING",
                            Message = $"Mermaid parse issue: {error.Message}"
                        });
                    }
                }

                model = parseResult.Model;
            }
            else
            {
                // Create a basic model from the Mermaid content
                model = CreateBasicModelFromMermaid(mermaidContent);
            }

            // Update model provenance
            model.Provenance = new ModelProvenance
            {
                SourceFormat = "Whiteboard",
                CreatedAt = DateTime.UtcNow,
                SourceFiles = input.FilePath != null ? [input.FilePath] : [],
                Notes = [$"Detected as {diagramType.PrimaryType} with {diagramType.Confidence:P0} confidence"]
            };

            totalStopwatch.Stop();

            // Create the generate result for Mermaid output
            var generatedOutputs = new Dictionary<string, GenerateResult>
            {
                ["Mermaid"] = new GenerateResult
                {
                    Success = true,
                    Content = mermaidContent
                }
            };

            return new WhiteboardScanResult
            {
                Model = model,
                Success = true,
                DetectedDiagramType = diagramType.PrimaryType,
                OverallConfidence = diagramType.Confidence,
                GeneratedOutputs = generatedOutputs,
                Errors = errors,
                Warnings = warnings,
                Statistics = new ScanStatistics
                {
                    EntitiesCreated = model.Entities.Count,
                    RelationshipsCreated = model.Relationships.Count,
                    DetectionDuration = detectionStopwatch.Elapsed,
                    AnalysisDuration = analysisStopwatch.Elapsed,
                    TotalDuration = totalStopwatch.Elapsed
                }
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error scanning whiteboard");
            errors.Add(new ScanError
            {
                Code = "SCAN_EXCEPTION",
                Message = ex.Message
            });
            return CreateFailedResult(ex.Message, errors);
        }
    }

    public async Task<DiagramTypeDetection> DetectDiagramTypeAsync(
        byte[] imageData,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _aiProvider.AnalyzeImageAsync(new ImageAnalysisRequest
            {
                ImageData = imageData,
                MimeType = "image/jpeg",
                Prompt = DiagramTypePrompt,
                MaxTokens = 256,
                Temperature = 0.1
            }, cancellationToken);

            if (!result.Success || string.IsNullOrEmpty(result.Content))
            {
                return new DiagramTypeDetection
                {
                    PrimaryType = DiagramType.Unknown,
                    Confidence = 0.0
                };
            }

            // Parse the JSON response
            return ParseDiagramTypeResponse(result.Content);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to detect diagram type, defaulting to Unknown");
            return new DiagramTypeDetection
            {
                PrimaryType = DiagramType.Unknown,
                Confidence = 0.0
            };
        }
    }

    private static async Task<(byte[]? Data, string MimeType)> LoadImageAsync(
        WhiteboardInput input,
        CancellationToken cancellationToken)
    {
        if (input.ImageData != null)
        {
            return (input.ImageData, "image/jpeg");
        }

        if (input.ImageStream != null)
        {
            using var ms = new MemoryStream();
            await input.ImageStream.CopyToAsync(ms, cancellationToken);
            return (ms.ToArray(), "image/jpeg");
        }

        if (!string.IsNullOrEmpty(input.FilePath))
        {
            if (!File.Exists(input.FilePath))
            {
                throw new FileNotFoundException($"Image file not found: {input.FilePath}");
            }

            var extension = Path.GetExtension(input.FilePath).ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension))
            {
                throw new NotSupportedException($"Unsupported image format: {extension}");
            }

            var data = await File.ReadAllBytesAsync(input.FilePath, cancellationToken);
            var mimeType = extension switch
            {
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/jpeg"
            };

            return (data, mimeType);
        }

        return (null, "image/jpeg");
    }

    private static string CleanMermaidOutput(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return string.Empty;

        // Remove markdown code blocks if present
        var cleaned = content.Trim();

        // Remove ```mermaid ... ``` wrapper
        if (cleaned.StartsWith("```mermaid", StringComparison.OrdinalIgnoreCase))
        {
            var endIndex = cleaned.IndexOf('\n');
            if (endIndex > 0)
                cleaned = cleaned[(endIndex + 1)..];
        }
        else if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var endIndex = cleaned.IndexOf('\n');
            if (endIndex > 0)
                cleaned = cleaned[(endIndex + 1)..];
        }

        // Remove trailing ``` if present
        if (cleaned.EndsWith("```", StringComparison.Ordinal))
        {
            cleaned = cleaned[..^3];
        }

        // Ensure it starts with classDiagram
        cleaned = cleaned.Trim();
        if (!cleaned.StartsWith("classDiagram", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = "classDiagram\n" + cleaned;
        }

        return cleaned.Trim();
    }

    private DiagramTypeDetection ParseDiagramTypeResponse(string content)
    {
        try
        {
            // Extract JSON from the response
            var jsonMatch = JsonRegex().Match(content);
            if (!jsonMatch.Success)
            {
                return new DiagramTypeDetection
                {
                    PrimaryType = DiagramType.Unknown,
                    Confidence = 0.0
                };
            }

            var json = jsonMatch.Value;

            // Simple JSON parsing for the type field
            var typeMatch = TypeRegex().Match(json);
            var confidenceMatch = ConfidenceRegex().Match(json);

            var typeString = typeMatch.Success ? typeMatch.Groups[1].Value : "UNKNOWN";
            var confidence = confidenceMatch.Success
                ? double.TryParse(confidenceMatch.Groups[1].Value, out var c) ? c : 0.5
                : 0.5;

            var primaryType = MapStringToDiagramType(typeString);

            // Parse alternatives if present
            var alternatives = new List<DiagramTypeCandidate>();
            var alternativesMatch = AlternativesRegex().Match(json);
            if (alternativesMatch.Success)
            {
                var altContent = alternativesMatch.Groups[1].Value;
                var altTypeMatches = TypeRegex().Matches(altContent);
                var altConfMatches = ConfidenceRegex().Matches(altContent);

                for (var i = 0; i < Math.Min(altTypeMatches.Count, altConfMatches.Count); i++)
                {
                    alternatives.Add(new DiagramTypeCandidate
                    {
                        Type = MapStringToDiagramType(altTypeMatches[i].Groups[1].Value),
                        Confidence = double.TryParse(altConfMatches[i].Groups[1].Value, out var altConf) ? altConf : 0.0
                    });
                }
            }

            return new DiagramTypeDetection
            {
                PrimaryType = primaryType,
                Confidence = confidence,
                Alternatives = alternatives
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse diagram type response: {Content}", content);
            return new DiagramTypeDetection
            {
                PrimaryType = DiagramType.Unknown,
                Confidence = 0.0
            };
        }
    }

    private static DiagramType MapStringToDiagramType(string typeString) => typeString.ToUpperInvariant() switch
    {
        "CLASS_DIAGRAM" => DiagramType.ClassDiagram,
        "SEQUENCE_DIAGRAM" => DiagramType.SequenceDiagram,
        "FLOWCHART" => DiagramType.Flowchart,
        "ER_DIAGRAM" or "ENTITY_RELATIONSHIP_DIAGRAM" => DiagramType.EntityRelationshipDiagram,
        "STATE_DIAGRAM" => DiagramType.StateDiagram,
        "ACTIVITY_DIAGRAM" => DiagramType.ActivityDiagram,
        "USE_CASE_DIAGRAM" => DiagramType.UseCaseDiagram,
        "COMPONENT_DIAGRAM" => DiagramType.ComponentDiagram,
        "DEPLOYMENT_DIAGRAM" => DiagramType.DeploymentDiagram,
        "ARCHITECTURE_DIAGRAM" => DiagramType.ArchitectureDiagram,
        "NETWORK_DIAGRAM" => DiagramType.NetworkDiagram,
        "MIND_MAP" => DiagramType.MindMap,
        "DATA_FLOW_DIAGRAM" => DiagramType.DataFlowDiagram,
        "INFORMAL_SKETCH" => DiagramType.InformalSketch,
        _ => DiagramType.Unknown
    };

    private static SemanticModel CreateBasicModelFromMermaid(string mermaidContent)
    {
        // Extract class names from the Mermaid content
        var model = new SemanticModel { Name = "WhiteboardDiagram" };

        var classMatches = ClassNameRegex().Matches(mermaidContent);
        var seenClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in classMatches)
        {
            var className = match.Groups[1].Value;
            if (seenClasses.Add(className))
            {
                model.CreateEntity(className, EntityClassification.Class);
            }
        }

        return model;
    }

    private static WhiteboardScanResult CreateFailedResult(string errorMessage, List<ScanError> errors)
    {
        if (errors.Count == 0)
        {
            errors.Add(new ScanError
            {
                Code = "SCAN_FAILED",
                Message = errorMessage
            });
        }

        return new WhiteboardScanResult
        {
            Model = new SemanticModel { Name = "Error" },
            Success = false,
            DetectedDiagramType = DiagramType.Unknown,
            OverallConfidence = 0.0,
            Errors = errors
        };
    }

    // Compiled regex patterns for performance
    [GeneratedRegex(@"\{[^{}]*\}")]
    private static partial Regex JsonRegex();

    [GeneratedRegex(@"""type""\s*:\s*""([^""]+)""")]
    private static partial Regex TypeRegex();

    [GeneratedRegex(@"""confidence""\s*:\s*([0-9.]+)")]
    private static partial Regex ConfidenceRegex();

    [GeneratedRegex(@"""alternativeTypes""\s*:\s*\[(.*?)\]", RegexOptions.Singleline)]
    private static partial Regex AlternativesRegex();

    [GeneratedRegex(@"class\s+(\w+)")]
    private static partial Regex ClassNameRegex();
}
