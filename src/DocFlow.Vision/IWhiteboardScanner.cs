using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Vision;

/// <summary>
/// The flagship feature: scan whiteboard photos and convert them to structured diagrams.
/// 
/// This orchestrates the full pipeline:
/// 1. Image preprocessing (rotation, contrast, noise reduction)
/// 2. Shape and line detection
/// 3. Text extraction (OCR)
/// 4. Semantic analysis (understanding what the diagram means)
/// 5. Output generation (Mermaid, C#, etc.)
/// </summary>
public interface IWhiteboardScanner
{
    /// <summary>
    /// Scan a whiteboard image and convert it to a semantic model
    /// </summary>
    Task<WhiteboardScanResult> ScanAsync(
        WhiteboardInput input, 
        WhiteboardScanOptions? options = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Quick scan that returns just the detected diagram type
    /// </summary>
    Task<DiagramTypeDetection> DetectDiagramTypeAsync(
        byte[] imageData,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Input for whiteboard scanning
/// </summary>
public sealed class WhiteboardInput
{
    /// <summary>
    /// Image data (JPEG, PNG, etc.)
    /// </summary>
    public byte[]? ImageData { get; init; }
    
    /// <summary>
    /// File path to image
    /// </summary>
    public string? FilePath { get; init; }
    
    /// <summary>
    /// Stream containing image data
    /// </summary>
    public Stream? ImageStream { get; init; }
    
    /// <summary>
    /// Hint about what type of diagram to expect
    /// </summary>
    public DiagramType? ExpectedDiagramType { get; init; }
    
    /// <summary>
    /// Additional context to help interpretation
    /// e.g., "This is a domain model for an e-commerce system"
    /// </summary>
    public string? ContextHint { get; init; }
    
    // Factory methods
    public static WhiteboardInput FromFile(string path) => new() { FilePath = path };
    public static WhiteboardInput FromBytes(byte[] data) => new() { ImageData = data };
    public static WhiteboardInput FromStream(Stream stream) => new() { ImageStream = stream };
}

/// <summary>
/// Options for whiteboard scanning
/// </summary>
public sealed class WhiteboardScanOptions
{
    /// <summary>
    /// Use AI API for semantic analysis (more accurate but requires API key)
    /// </summary>
    public bool UseAiAnalysis { get; init; } = true;
    
    /// <summary>
    /// Preferred AI provider for analysis
    /// </summary>
    public string? PreferredAiProvider { get; init; }
    
    /// <summary>
    /// Run in interactive mode - prompt for clarification on ambiguous elements
    /// </summary>
    public bool InteractiveMode { get; init; } = false;
    
    /// <summary>
    /// Callback for interactive clarification requests
    /// </summary>
    public Func<ClarificationRequest, Task<ClarificationResponse>>? ClarificationCallback { get; init; }
    
    /// <summary>
    /// Apply GA-based layout optimization to the output
    /// </summary>
    public bool OptimizeLayout { get; init; } = true;
    
    /// <summary>
    /// Minimum confidence threshold for including detected elements
    /// </summary>
    public double MinimumConfidence { get; init; } = 0.6;
    
    /// <summary>
    /// Auto-correct common OCR mistakes using context
    /// </summary>
    public bool AutoCorrectText { get; init; } = true;
    
    /// <summary>
    /// Expand common abbreviations (e.g., "Cust" → "Customer")
    /// </summary>
    public bool ExpandAbbreviations { get; init; } = true;
    
    /// <summary>
    /// Target output formats to generate
    /// </summary>
    public List<string> OutputFormats { get; init; } = ["Mermaid"];
    
    /// <summary>
    /// Image preprocessing options
    /// </summary>
    public ImagePreprocessingOptions Preprocessing { get; init; } = new();
}

/// <summary>
/// Image preprocessing configuration
/// </summary>
public sealed class ImagePreprocessingOptions
{
    /// <summary>
    /// Auto-rotate to correct orientation
    /// </summary>
    public bool AutoRotate { get; init; } = true;
    
    /// <summary>
    /// Apply perspective correction (for angled photos)
    /// </summary>
    public bool PerspectiveCorrection { get; init; } = true;
    
    /// <summary>
    /// Enhance contrast for better detection
    /// </summary>
    public bool EnhanceContrast { get; init; } = true;
    
    /// <summary>
    /// Remove shadows
    /// </summary>
    public bool RemoveShadows { get; init; } = true;
    
    /// <summary>
    /// Denoise the image
    /// </summary>
    public bool Denoise { get; init; } = true;
}

/// <summary>
/// Result of whiteboard scanning
/// </summary>
public sealed class WhiteboardScanResult
{
    /// <summary>
    /// The parsed semantic model
    /// </summary>
    public required SemanticModel Model { get; init; }
    
    /// <summary>
    /// Was scanning successful?
    /// </summary>
    public bool Success { get; init; } = true;
    
    /// <summary>
    /// Detected diagram type
    /// </summary>
    public DiagramType DetectedDiagramType { get; init; }
    
    /// <summary>
    /// Confidence in the overall interpretation (0-1)
    /// </summary>
    public double OverallConfidence { get; init; }
    
    /// <summary>
    /// Generated outputs in requested formats
    /// </summary>
    public Dictionary<string, GenerateResult> GeneratedOutputs { get; init; } = [];
    
    /// <summary>
    /// Detailed detection results for each element
    /// </summary>
    public List<DetectedElement> DetectedElements { get; init; } = [];
    
    /// <summary>
    /// Elements that couldn't be interpreted
    /// </summary>
    public List<UninterpretedElement> UninterpretedElements { get; init; } = [];
    
    /// <summary>
    /// Clarifications that were requested (if in interactive mode)
    /// </summary>
    public List<ClarificationRequest> RequestedClarifications { get; init; } = [];
    
    /// <summary>
    /// Preprocessing results (rotated image, etc.)
    /// </summary>
    public PreprocessingResult? Preprocessing { get; init; }
    
    /// <summary>
    /// Any errors encountered
    /// </summary>
    public List<ScanError> Errors { get; init; } = [];
    
    /// <summary>
    /// Warnings
    /// </summary>
    public List<ScanWarning> Warnings { get; init; } = [];
    
    /// <summary>
    /// Statistics about the scan
    /// </summary>
    public ScanStatistics? Statistics { get; init; }
}

/// <summary>
/// Types of diagrams that can be detected
/// </summary>
public enum DiagramType
{
    Unknown,
    
    // UML Diagrams
    ClassDiagram,
    SequenceDiagram,
    ActivityDiagram,
    StateDiagram,
    UseCaseDiagram,
    ComponentDiagram,
    DeploymentDiagram,
    
    // Other Diagram Types
    EntityRelationshipDiagram,
    Flowchart,
    MindMap,
    ArchitectureDiagram,
    NetworkDiagram,
    DataFlowDiagram,
    
    // Informal
    InformalSketch,
    Mixed
}

/// <summary>
/// Result of diagram type detection
/// </summary>
public sealed class DiagramTypeDetection
{
    public DiagramType PrimaryType { get; init; }
    public double Confidence { get; init; }
    public List<DiagramTypeCandidate> Alternatives { get; init; } = [];
}

public sealed class DiagramTypeCandidate
{
    public DiagramType Type { get; init; }
    public double Confidence { get; init; }
}

/// <summary>
/// A detected element from the whiteboard
/// </summary>
public sealed class DetectedElement
{
    public required string Id { get; init; }
    
    /// <summary>
    /// Type of element detected
    /// </summary>
    public DetectedElementType ElementType { get; init; }
    
    /// <summary>
    /// Bounding box in the original image (x, y, width, height)
    /// </summary>
    public BoundingBox BoundingBox { get; init; } = new();
    
    /// <summary>
    /// Extracted text content (if any)
    /// </summary>
    public string? Text { get; init; }
    
    /// <summary>
    /// OCR confidence for the text
    /// </summary>
    public double? TextConfidence { get; init; }
    
    /// <summary>
    /// What semantic entity this was interpreted as
    /// </summary>
    public string? InterpretedAsEntityId { get; init; }
    
    /// <summary>
    /// What semantic relationship this was interpreted as
    /// </summary>
    public string? InterpretedAsRelationshipId { get; init; }
    
    /// <summary>
    /// Overall confidence in this detection
    /// </summary>
    public double Confidence { get; init; }
    
    /// <summary>
    /// Alternative interpretations
    /// </summary>
    public List<AlternativeInterpretation> Alternatives { get; init; } = [];
    
    /// <summary>
    /// Raw detection data (for debugging)
    /// </summary>
    public Dictionary<string, object> RawData { get; init; } = [];
}

public enum DetectedElementType
{
    Unknown,
    
    // Shapes
    Rectangle,
    RoundedRectangle,
    Ellipse,
    Diamond,
    Cylinder,
    Cloud,
    StickFigure,
    Note,
    Package,
    
    // Lines & Arrows
    SolidLine,
    DashedLine,
    Arrow,
    BidirectionalArrow,
    DiamondArrow,        // Aggregation/Composition
    TriangleArrow,       // Inheritance
    
    // Text
    Text,
    Label,
    Stereotype,
    Multiplicity,
    
    // Containers
    Container,
    Swimlane,
    Partition
}

public sealed class BoundingBox
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;
}

public sealed class AlternativeInterpretation
{
    public required string Description { get; init; }
    public double Confidence { get; init; }
    public string? EntityId { get; init; }
    public string? RelationshipId { get; init; }
}

/// <summary>
/// Element that couldn't be interpreted
/// </summary>
public sealed class UninterpretedElement
{
    public required string Id { get; init; }
    public DetectedElementType ElementType { get; init; }
    public BoundingBox BoundingBox { get; init; } = new();
    public string? ExtractedText { get; init; }
    public required string Reason { get; init; }
    public byte[]? CroppedImage { get; init; }
}

/// <summary>
/// Request for user clarification on ambiguous elements
/// </summary>
public sealed class ClarificationRequest
{
    public required string Id { get; init; }
    public required string Question { get; init; }
    public required ClarificationType Type { get; init; }
    public List<ClarificationOption> Options { get; init; } = [];
    public byte[]? RelevantImageCrop { get; init; }
    public string? Context { get; init; }
}

public enum ClarificationType
{
    ElementType,
    TextContent,
    RelationshipDirection,
    RelationshipType,
    EntityClassification,
    AbbreviationExpansion,
    Other
}

public sealed class ClarificationOption
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public double Confidence { get; init; }
}

public sealed class ClarificationResponse
{
    public required string RequestId { get; init; }
    public required string SelectedOptionId { get; init; }
    public string? FreeformAnswer { get; init; }
}

/// <summary>
/// Result of image preprocessing
/// </summary>
public sealed class PreprocessingResult
{
    /// <summary>
    /// The preprocessed image data
    /// </summary>
    public byte[]? ProcessedImage { get; init; }
    
    /// <summary>
    /// Rotation applied (degrees)
    /// </summary>
    public double RotationApplied { get; init; }
    
    /// <summary>
    /// Was perspective correction applied?
    /// </summary>
    public bool PerspectiveCorrected { get; init; }
    
    /// <summary>
    /// Quality score of the preprocessed image (0-1)
    /// </summary>
    public double QualityScore { get; init; }
}

public sealed class ScanError
{
    public required string Code { get; init; }
    public required string Message { get; init; }
    public string? ElementId { get; init; }
}

public sealed class ScanWarning
{
    public required string Code { get; init; }
    public required string Message { get; init; }
}

public sealed class ScanStatistics
{
    public int ShapesDetected { get; init; }
    public int LinesDetected { get; init; }
    public int TextRegionsDetected { get; init; }
    public int EntitiesCreated { get; init; }
    public int RelationshipsCreated { get; init; }
    public int ClarificationsRequested { get; init; }
    public TimeSpan PreprocessingDuration { get; init; }
    public TimeSpan DetectionDuration { get; init; }
    public TimeSpan AnalysisDuration { get; init; }
    public TimeSpan TotalDuration { get; init; }
}
