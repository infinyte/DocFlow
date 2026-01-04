# DocFlow Architecture

This document describes the technical architecture of DocFlow, explaining the design decisions and patterns that enable bidirectional transformation between code, diagrams, and documentation.

## Core Concept: Canonical Semantic Model

DocFlow's architecture centers on a **Canonical Semantic Model** - an intermediate representation that captures the *meaning* of software models, not just their syntax.

### The Problem with Direct Translation

Traditional tools translate directly between formats (A → B). This approach fails when:

1. **Information Loss**: Format A has concepts that B cannot represent
2. **Round-Trip Failure**: A → B → A produces different output than the original
3. **Semantic Mismatch**: The same concept has different syntax in each format

### The DocFlow Solution

```
┌─────────────┐     ┌─────────────────────────────────────┐     ┌─────────────┐
│   Source    │     │       Canonical Semantic Model      │     │   Target    │
│   Format    │────▶│                                     │────▶│   Format    │
│  (C#, etc)  │     │  Entities, Properties, Operations   │     │  (Mermaid)  │
└─────────────┘     │  Relationships, Classifications     │     └─────────────┘
       │            │  DDD Patterns, Stereotypes          │            │
       │            └─────────────────────────────────────┘            │
       │                              │                                │
       │                              ▼                                │
       │            ┌─────────────────────────────────────┐            │
       └───────────▶│         Round-Trip Support          │◀───────────┘
                    │     Semantic preservation via       │
                    │     canonical representation        │
                    └─────────────────────────────────────┘
```

By routing all transformations through the canonical model, DocFlow:
- Preserves semantic meaning across formats
- Enables true round-trip transformations
- Supports adding new formats without N×M parser/generator combinations

---

## Semantic Model Structure

### SemanticModel

The root container holding the complete model:

```csharp
public sealed class SemanticModel
{
    public string Id { get; init; }
    public string? Name { get; set; }
    public Dictionary<string, SemanticEntity> Entities { get; init; }
    public List<SemanticRelationship> Relationships { get; init; }
    public List<SemanticNamespace> Namespaces { get; init; }
    public ModelProvenance? Provenance { get; set; }
}
```

### SemanticEntity

Represents any type-like construct (class, interface, enum, etc.):

```csharp
public sealed class SemanticEntity
{
    public string Id { get; init; }
    public string Name { get; init; }
    public EntityClassification Classification { get; set; }
    public bool IsAbstract { get; set; }
    public List<SemanticProperty> Properties { get; init; }
    public List<SemanticOperation> Operations { get; init; }
    public List<string> Stereotypes { get; init; }
}
```

### Entity Classifications (DDD Support)

```csharp
public enum EntityClassification
{
    Class,           // Generic class
    AggregateRoot,   // DDD aggregate boundary
    Entity,          // DDD entity with identity
    ValueObject,     // DDD immutable value
    DomainService,   // Stateless domain operations
    DomainEvent,     // Something that happened
    Repository,      // Collection-like persistence
    Interface,       // Contract definition
    Enum,            // Enumeration
    Record           // Immutable data carrier
}
```

### SemanticRelationship

Captures relationships with full semantic information:

```csharp
public sealed class SemanticRelationship
{
    public string SourceEntityId { get; init; }
    public string TargetEntityId { get; init; }
    public RelationshipType Type { get; init; }  // Inheritance, Composition, etc.
    public string? SourceMultiplicity { get; set; }
    public string? TargetMultiplicity { get; set; }
}
```

---

## Parser → Generator Pattern

All transformations follow the same pattern:

```
IModelParser: Input → SemanticModel
IModelGenerator: SemanticModel → Output
```

### Parser Interface

```csharp
public interface IModelParser
{
    string FormatName { get; }
    IReadOnlyList<string> SupportedExtensions { get; }

    Task<ParseResult> ParseAsync(
        ParserInput input,
        ParserOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Generator Interface

```csharp
public interface IModelGenerator
{
    string FormatName { get; }
    string DefaultExtension { get; }

    Task<GenerateResult> GenerateAsync(
        SemanticModel model,
        GeneratorOptions? options = null,
        CancellationToken cancellationToken = default);
}
```

### Implemented Transformers

| Component | Parser | Generator |
|-----------|--------|-----------|
| C# | `CSharpModelParser` | `CSharpModelGenerator` |
| Mermaid | `MermaidClassDiagramParser` | `MermaidClassDiagramGenerator` |
| Whiteboard | `WhiteboardScanner` | - |

---

## Whiteboard Scanning Pipeline

The whiteboard scanner uses AI vision to extract diagrams from photos:

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   Image     │────▶│   Base64    │────▶│   Claude    │────▶│   Mermaid   │
│   (JPG/PNG) │     │   Encode    │     │ Vision API  │     │    Text     │
└─────────────┘     └─────────────┘     └─────────────┘     └──────┬──────┘
                                                                   │
                                                                   ▼
┌─────────────┐     ┌─────────────┐     ┌─────────────────────────────────┐
│  Semantic   │◀────│   Mermaid   │◀────│         Prompt Engineering       │
│   Model     │     │   Parser    │     │  - Diagram type detection        │
└─────────────┘     └─────────────┘     │  - Entity/relationship extract   │
                                        │  - Mermaid syntax generation     │
                                        └─────────────────────────────────┘
```

### Key Components

**WhiteboardScanner** (`DocFlow.Vision/WhiteboardScanner.cs`)
- Orchestrates the scanning pipeline
- Handles image loading and format detection
- Manages diagram type detection
- Converts AI output to SemanticModel

**ClaudeProvider** (`DocFlow.AI/Providers/ClaudeProvider.cs`)
- Implements `IAiProvider` interface
- Handles Claude API communication
- Supports vision (image analysis) and text completion
- Multi-source API key resolution

### Prompt Engineering

The whiteboard scanner uses carefully crafted prompts:

1. **Diagram Type Detection**: Quick classification of diagram type with confidence score
2. **Entity Extraction**: Detailed analysis to extract classes, properties, methods
3. **Relationship Mapping**: Identify inheritance, composition, association patterns
4. **Mermaid Generation**: Output valid Mermaid classDiagram syntax

---

## Integration Module Architecture

The Integration module (scaffolded, not fully implemented) extends the canonical model pattern to API integrations:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        External API Ecosystem                            │
├─────────────────┬─────────────────┬─────────────────┬───────────────────┤
│   OpenAPI 3.x   │   Swagger 2.0   │   GraphQL       │   JSON Samples    │
└────────┬────────┴────────┬────────┴────────┬────────┴─────────┬─────────┘
         │                 │                 │                  │
         └─────────────────┴─────────────────┴──────────────────┘
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │     Canonical Semantic Model   │
                    │   (Same as Code/Diagrams!)     │
                    └───────────────┬───────────────┘
                                    │
                    ┌───────────────┴───────────────┐
                    │         CDM Mapper             │
                    │   (IMS-powered mapping)        │
                    └───────────────┬───────────────┘
                                    │
                                    ▼
                    ┌───────────────────────────────┐
                    │   Internal Canonical Model     │
                    │   (Your Domain Model)          │
                    └───────────────────────────────┘
```

### Pre-built Domain Patterns

The Integration module includes pre-seeded patterns for common domains:

**Aviation Domain:**
| External Pattern | Canonical Target | Confidence |
|------------------|------------------|------------|
| `tail_num`, `aircraft_id` | TailNumber | 95% |
| `arr_time`, `eta` | ArrivalDateTime | 93% |
| `pax`, `passenger_count` | PassengerCount | 90% |

### SLA Validation

The SlaValidator checks data freshness to catch stale data issues:

```csharp
var report = await slaValidator.ValidateDataFreshnessAsync(new SlaValidationRequest
{
    EndpointUrl = "https://api.example.com/v1/data",
    ExpectedMaxAge = TimeSpan.FromSeconds(30),
    SampleCount = 100
});
```

See [docs/design/integration-module.md](design/integration-module.md) for full design.

---

## Intelligent Mapping Service (IMS)

The IMS (designed, not fully implemented) learns transformation patterns from examples:

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Observed      │────▶│   Pattern       │────▶│   Learned       │
│   Transformation│     │   Extraction    │     │   Patterns      │
└─────────────────┘     └─────────────────┘     └────────┬────────┘
                                                         │
                                                         ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Suggestions   │◀────│   Pattern       │◀────│   New Input     │
│  with Confidence│     │   Matching      │     │                 │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

### Key Concepts

- **LearnedPattern**: A transformation pattern with confidence score
- **PatternMatcher**: Applies patterns to new inputs
- **FeedbackLoop**: User corrections improve future suggestions

---

## Project Dependencies

```
DocFlow.CLI
├── DocFlow.Core              # Canonical model, abstractions
├── DocFlow.Diagrams          # Mermaid parsing & generation
│   └── DocFlow.Core
├── DocFlow.CodeAnalysis      # Roslyn-based C# parsing
│   └── DocFlow.Core
├── DocFlow.CodeGen           # C# code generation
│   └── DocFlow.Core
├── DocFlow.Vision            # Whiteboard scanning
│   ├── DocFlow.Core
│   └── DocFlow.AI
├── DocFlow.AI                # AI provider abstraction
│   └── DocFlow.Core
├── DocFlow.IMS               # Pattern learning
│   └── DocFlow.Core
├── DocFlow.Ontology          # DDD classification
│   └── DocFlow.Core
├── DocFlow.Integration       # API integration
│   ├── DocFlow.Core
│   ├── DocFlow.IMS
│   └── DocFlow.CodeGen
├── DocFlow.Documents         # Document pipeline (planned)
│   └── DocFlow.Core
└── DocFlow.Web               # Web UI (planned)
    └── DocFlow.Core
```

---

## Design Principles

### 1. Semantic Preservation
All transformations preserve meaning. A class in C# should have the same semantic representation whether it came from source code, a Mermaid diagram, or a whiteboard photo.

### 2. Extensibility
Adding a new format requires only a parser and/or generator. The canonical model stays unchanged.

### 3. DDD-First
The model understands Domain-Driven Design patterns natively. Aggregates, entities, and value objects are first-class concepts.

### 4. Async All the Way
All I/O-bound operations are async with CancellationToken support.

### 5. Nullable Safety
Nullable reference types are enabled throughout. No `NullReferenceException` surprises.

---

## Technology Stack

| Layer | Technology |
|-------|------------|
| Runtime | .NET 8.0 |
| Language | C# 12 |
| C# Parsing | Microsoft.CodeAnalysis.CSharp (Roslyn) |
| CLI | System.CommandLine + Spectre.Console |
| AI | Anthropic Claude API |
| OpenAPI | Microsoft.OpenApi.Readers |
| Testing | xUnit + FluentAssertions |
