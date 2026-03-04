# DocFlow Architecture

This document describes the technical architecture of DocFlow, explaining the design decisions and patterns that enable bidirectional transformation between code, diagrams, APIs, and documentation.

## Core Concept: Canonical Semantic Model

DocFlow's architecture centers on a **Canonical Semantic Model** - an intermediate representation that captures the *meaning* of software models, not just their syntax.

### The Problem with Direct Translation

Traditional tools translate directly between formats (A -> B). This approach fails when:

1. **Information Loss**: Format A has concepts that B cannot represent
2. **Round-Trip Failure**: A -> B -> A produces different output than the original
3. **Semantic Mismatch**: The same concept has different syntax in each format

### The DocFlow Solution

```
+-------------+     +-------------------------------------+     +-------------+
|   Source    |     |       Canonical Semantic Model      |     |   Target    |
|   Format    |---->|                                     |---->|   Format    |
|  (C#, etc)  |     |  Entities, Properties, Operations   |     |  (Mermaid)  |
+-------------+     |  Relationships, Classifications     |     +-------------+
       |            |  DDD Patterns, Stereotypes          |            |
       |            +-------------------------------------+            |
       |                              |                                |
       |                              v                                |
       |            +-------------------------------------+            |
       +----------->|         Round-Trip Support          |<-----------+
                    |     Semantic preservation via       |
                    |     canonical representation        |
                    +-------------------------------------+
```

By routing all transformations through the canonical model, DocFlow:
- Preserves semantic meaning across formats
- Enables true round-trip transformations
- Supports adding new formats without N x M parser/generator combinations

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

## Parser -> Generator Pattern

All transformations follow the same pattern:

```
IModelParser: Input -> SemanticModel
IModelGenerator: SemanticModel -> Output
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
| OpenAPI | `OpenApiParser` | - |

---

## Transformation Pipelines

### C# to Mermaid Pipeline

```
C# Source File
      |
      v
+---------------------+
| CSharpModelParser   |  (Roslyn analysis)
| - Extract classes   |
| - Extract records   |
| - Extract enums     |
| - Detect DDD types  |
+---------------------+
      |
      v
+---------------------+
|   SemanticModel     |
+---------------------+
      |
      v
+------------------------+
| MermaidClassGenerator  |
| - Generate classDiagram|
| - Add stereotypes      |
| - Add relationships    |
+------------------------+
      |
      v
Mermaid .mmd File
```

### Whiteboard Scanning Pipeline

```
+-------------+     +-------------+     +-------------+     +-------------+
|   Image     |---->|   Base64    |---->|   Claude    |---->|   Mermaid   |
|   (JPG/PNG) |     |   Encode    |     | Vision API  |     |    Text     |
+-------------+     +-------------+     +-------------+     +------+------+
                                                                   |
                                                                   v
+-------------+     +-------------+     +----------------------------------+
|  Semantic   |<----|   Mermaid   |<----|         Prompt Engineering       |
|   Model     |     |   Parser    |     |  - Diagram type detection        |
+-------------+     +-------------+     |  - Entity/relationship extract   |
                                        |  - Mermaid syntax generation     |
                                        +----------------------------------+
```

**Key Components:**

- **WhiteboardScanner** (`DocFlow.Vision/WhiteboardScanner.cs`)
  - Orchestrates the scanning pipeline
  - Handles image loading and format detection
  - Manages diagram type detection
  - Converts AI output to SemanticModel

- **ClaudeProvider** (`DocFlow.AI/Providers/ClaudeProvider.cs`)
  - Implements `IAiProvider` interface
  - Handles Claude API communication
  - Supports vision (image analysis) and text completion
  - Multi-source API key resolution

---

## Integration Module Architecture

The Integration module extends the canonical model pattern to enterprise API integrations:

```
+-----------------------------------------------+
|            External API Ecosystem             |
+-----------------------+-----------------------+
|     OpenAPI 3.x JSON  |   OpenAPI 3.x YAML    |
+----------+------------+-----------+-----------+
           |                        |
           +------------------------+
                                    |
                                    v
                    +-------------------------------+
                    |     Canonical Semantic Model   |
                    |   (Same as Code/Diagrams!)     |
                    +---------------+---------------+
                                    |
                    +---------------+---------------+
                    |           CDM Mapper           |
                    |   (Multi-pass field matching)  |
                    +---------------+---------------+
                                    |
                                    v
                    +-------------------------------+
                    |   Internal Canonical Model     |
                    |   (Your Domain Model)          |
                    +-------------------------------+
                                    |
                    +---------------+---------------+
                    |      Code Generation           |
                    +---------------+---------------+
                                    |
         +-----------------+-----------------+------------------+
         |                 |                 |                  |
         v                 v                 v                  v
   +-----------+    +------------+    +----------+    +-------------+
   |   DTOs    |    | AutoMapper |    |  HTTP    |    | Validators  |
   |           |    |  Profiles  |    |  Client  |    |             |
   +-----------+    +------------+    +----------+    +-------------+
```

### CDM Mapping Algorithm

The `CdmMapper` uses a multi-pass field matching algorithm:

1. **Pass 1: Exact Match** (95% confidence)
   - Direct name equality (case-insensitive)

2. **Pass 2: ID Field Match** (85% confidence)
   - Source ends with "Id" and target is "Id" or "{Entity}Id"

3. **Pass 3: Contains Match** (75% confidence)
   - Target name contains source name or vice versa

4. **Pass 4: Foreign Key Pattern** (70% confidence)
   - Source follows FK pattern (e.g., "petId" -> "ProductId")

5. **Pass 5: Date/Time Match** (70% confidence)
   - Both fields are date/time types

### SLA Validation

The `SlaValidator` checks data freshness:

```csharp
var report = await slaValidator.ValidateDataFreshnessAsync(new SlaValidationRequest
{
    EndpointUrl = "https://api.example.com/v1/data",
    ExpectedMaxAge = TimeSpan.FromSeconds(30),
    SampleCount = 100,
    SampleInterval = TimeSpan.FromSeconds(5)
});
```

Compliance verdicts:
- **COMPLIANT**: 100% samples within SLA
- **MARGINALLY COMPLIANT**: 90-99% within SLA
- **MINOR VIOLATION**: 50-89% within SLA
- **SEVERE VIOLATION**: <50% within SLA

### Pre-built Domain Patterns

The Integration module ships with pre-seeded patterns across four categories:

**Identifiers:**
| Pattern | Matches | Semantic |
|---------|---------|----------|
| Primary Key | `id`, `*_id`, `guid`, `uuid` | Identity |
| Foreign Key | `*_id` suffix | Navigation |
| External Reference | `ext_id`, `ref_id`, `source_key` | External |
| Correlation ID | `correlation_id`, `trace_id`, `request_id` | Tracking |

**DateTime Conversions:**
| Rule | Input | Output |
|------|-------|--------|
| ISO to DateTime | ISO 8601 string | `DateTime` |
| Unix seconds | `long` epoch | `DateTime` |
| Unix millis | `long` epoch ms | `DateTime` |
| US date to ISO | `MM/dd/yyyy` | `yyyy-MM-dd` |

**Contact:**
`email` / `e_mail`, `phone` / `tel` / `mobile`, `first_name` / `fname`, `last_name` / `lname`

**Audit:**
`created_at` / `inserted_at`, `updated_at` / `modified_at`, `created_by`, `updated_by`

---

## Intelligent Mapping Service (IMS)

The IMS (designed, future implementation) learns transformation patterns from examples:

```
+-------------------+     +-------------------+     +-------------------+
|   Observed        |---->|   Pattern         |---->|   Learned         |
|   Transformation  |     |   Extraction      |     |   Patterns        |
+-------------------+     +-------------------+     +---------+---------+
                                                              |
                                                              v
+-------------------+     +-------------------+     +-------------------+
|   Suggestions     |<----|   Pattern         |<----|   New Input       |
|  with Confidence  |     |   Matching        |     |                   |
+-------------------+     +-------------------+     +-------------------+
```

### Key Concepts

- **LearnedPattern**: A transformation pattern with confidence score
- **PatternMatcher**: Applies patterns to new inputs
- **FeedbackLoop**: User corrections improve future suggestions

---

## Project Dependencies

```
DocFlow.CLI
+-- DocFlow.Core              # Canonical model, abstractions
+-- DocFlow.Diagrams          # Mermaid parsing & generation
|   +-- DocFlow.Core
+-- DocFlow.CodeAnalysis      # Roslyn-based C# parsing
|   +-- DocFlow.Core
+-- DocFlow.CodeGen           # C# code generation
|   +-- DocFlow.Core
+-- DocFlow.Vision            # Whiteboard scanning
|   +-- DocFlow.Core
|   +-- DocFlow.AI
+-- DocFlow.AI                # AI provider abstraction
|   +-- DocFlow.Core
+-- DocFlow.IMS               # Pattern learning
|   +-- DocFlow.Core
+-- DocFlow.Ontology          # DDD classification
|   +-- DocFlow.Core
+-- DocFlow.Integration       # API integration
|   +-- DocFlow.Core
|   +-- DocFlow.IMS
|   +-- DocFlow.CodeGen
+-- DocFlow.Documents         # Document pipeline (planned)
|   +-- DocFlow.Core
+-- DocFlow.Web               # Web UI (planned)
    +-- DocFlow.Core
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

### 6. Confidence Transparency
All AI-assisted and heuristic-based mappings include confidence scores and reasoning, allowing users to focus on low-confidence areas.

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

---

## File Locations

| Component | Key Files |
|-----------|-----------|
| Canonical Model | `src/DocFlow.Core/CanonicalModel/` |
| C# Parser | `src/DocFlow.CodeAnalysis/CSharp/CSharpModelParser.cs` |
| C# Generator | `src/DocFlow.CodeGen/CSharp/CSharpModelGenerator.cs` |
| Mermaid Parser | `src/DocFlow.Diagrams/Mermaid/MermaidClassDiagramParser.cs` |
| Mermaid Generator | `src/DocFlow.Diagrams/Mermaid/MermaidClassDiagramGenerator.cs` |
| Whiteboard Scanner | `src/DocFlow.Vision/WhiteboardScanner.cs` |
| Claude Provider | `src/DocFlow.AI/Providers/ClaudeProvider.cs` |
| OpenAPI Parser | `src/DocFlow.Integration/Schemas/OpenApiParser.cs` |
| CDM Mapper | `src/DocFlow.Integration/Mapping/CdmMapper.cs` |
| SLA Validator | `src/DocFlow.Integration/Validation/SlaValidator.cs` |
| Code Generator | `src/DocFlow.Integration/CodeGen/IntegrationCodeGenerator.cs` |
| CLI Entry Point | `src/DocFlow.CLI/Program.cs` |
