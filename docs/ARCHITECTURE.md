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
    public ApiSurface? Api { get; set; }                // API-level surface (operations, params, servers, security)
}
```

The optional `Api` property is populated by `OpenApiParser` and carries the API surface —
operations, parameters, request/response bodies, servers, tags, and security schemes — so
downstream generators (documentation, diagrams, clients) can reason about the API without
re-parsing the source spec. See `src/DocFlow.Core/CanonicalModel/ApiSurface.cs`.

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
| Mermaid (class) | `MermaidClassDiagramParser` | `MermaidClassDiagramGenerator` |
| Mermaid (ER) | - | `MermaidErDiagramGenerator` |
| Mermaid (sequence) | - | `MermaidSequenceDiagramGenerator` |
| Mermaid (C4 context) | - | `MermaidC4ContextGenerator` |
| Mermaid (endpoint flow) | - | `MermaidEndpointFlowchartGenerator` |
| Whiteboard | `WhiteboardScanner` | - |
| OpenAPI | `OpenApiParser` (populates `SemanticModel.Api` incl. examples) | - |
| Design Docs (Markdown) | - | `MarkdownDocumentationGenerator` |
| Design Docs (HTML) | - | `StaticSiteRenderer` (Markdig) |

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

## Documentation Module Architecture

`DocFlow.Documentation` composes the OpenAPI parser with the existing Mermaid class diagram
generator to produce a navigable design-docs bundle. Phase 1 ships the Markdown MVP.

```
Source spec (OpenAPI 3.x)
    |
    v
+--------------------+
|   OpenApiParser    |  populates SemanticModel.Entities + Api
+---------+----------+
          |
          v
+-------------------------------+
|   MarkdownDocumentationGen    |
|                               |
|   +--------------------------+|
|   | OverviewSectionBuilder   ||
|   +--------------------------+|
|   +--------------------------+|
|   | DomainModelSectionBuilder||  invokes MermaidClassDiagramGenerator
|   |                          ||  + MermaidErDiagramGenerator (Er flag)
|   |                          ||  + injects <a id="entity-…"></a> anchors
|   +--------------------------+|
|   +--------------------------+|
|   | ArchitectureSectionBuilder|  Context flag:
|   |                          ||  + MermaidC4ContextGenerator
|   |                          ||  emits architecture.md + diagrams/context.mmd
|   +--------------------------+|
|   +--------------------------+|
|   | SecuritySectionBuilder   ||  emits security.md when schemes or
|   |                          ||  operation requirements exist
|   +--------------------------+|
|   +--------------------------+|
|   | EndpointSectionBuilder   ||  one page per tag (or path segment);
|   |                          ||  embeds MermaidSequenceDiagramGenerator (Sequence flag)
|   |                          ||  and MermaidEndpointFlowchartGenerator (Flow flag);
|   |                          ||  optional WithExamples → ExampleSynthesizer JSON;
|   |                          ||  entity refs linked into domain-model.md anchors;
|   |                          ||  also emits standalone sequences/<opId>.md
|   +--------------------------+|
|   +--------------------------+|
|   |  IndexSectionBuilder     ||  TOC built after siblings
|   +--------------------------+|
+---------+---------------------+
          |
          v
IReadOnlyList<GeneratedFile>   (pure in-memory; CLI writes to disk)
          |
          |  (optional, when --format html)
          v
+-------------------------------+
|   StaticSiteRenderer (Markdig)|   parallel .html for each .md;
|                               |   mermaid fences via Markdig diagrams extension;
|                               |   .md → .html link rewrite (fragments preserved);
|                               |   per-page sidebar nav with .active highlight;
|                               |   embedded assets/theme.css + Mermaid.js CDN
+-------------------------------+
          |
          v
<output-dir>/{index,overview,domain-model,architecture,security}.md(+.html),
             endpoints/<tag>.md(+.html),
             sequences/<opId>.md(+.html),
             diagrams/context.mmd,
             assets/{openapi.{json|yaml},theme.css}
```

### Diagram kind mapping

The `DocumentationOptions.Diagrams` flags enum selects which diagrams the generator emits.
The CLI default is `all`.

| Flag | Generator | Where it lands |
|------|-----------|----------------|
| `Class` | `MermaidClassDiagramGenerator` | `domain-model.md` |
| `Er` | `MermaidErDiagramGenerator` | `domain-model.md` (after the class fence) |
| `Sequence` | `MermaidSequenceDiagramGenerator` | `endpoints/<tag>.md` per operation + standalone `sequences/<opId>.md` |
| `Context` | `MermaidC4ContextGenerator` | `architecture.md` + standalone `diagrams/context.mmd` |
| `Flow` | `MermaidEndpointFlowchartGenerator` | `endpoints/<tag>.md` per operation |

**ER cardinality mapping:** `Composition` → `||--o{`, `Aggregation` → `}o--o{`,
`Association` → `}o--||`. Other `RelationshipType` values (e.g. `Inheritance`, `Dependency`) are
not rendered as ER relationships — the involved entities still appear as standalone blocks.

**Sequence participants:** always `Client` and `API`; `Auth` is added when the operation's
`SecurityRequirements` is non-empty. The request message includes HTTP method, path, and the
first request-body media type (preferring named entity references); the response message picks
the first 2xx response (falling back to the first listed).

**Context diagram:** a Mermaid `flowchart LR` (C4's dedicated primitive is still experimental)
with `Client`, an API container labelled with the spec title, one node per `ApiServer`, and one
external-system node per OAuth2 / OpenID-Connect security scheme.

**Endpoint flowchart:** `Request → Validate Params → [Authorize] → Handler → Response`. The
`Authorize` node is omitted when no security requirements are declared. Non-2xx responses are
rendered as dashed branches from `Handler`; 2xx responses use solid edges.

### Content depth (Phase 3)

- `--with-examples` activates `ExampleSynthesizer`, which prefers spec-provided
  `ApiMediaType.Example` payloads (captured by the OpenAPI parser via `OpenApiJsonWriter`) and
  otherwise synthesises JSON from the schema: enum[0] for constrained strings, ISO-8601
  placeholders for `date-time`, zero for numerics, single-element arrays, and an ellipsis
  (`"..."`) to terminate circular entity references.
- `SecuritySectionBuilder` emits `security.md` whenever the spec declares `securitySchemes` or
  any operation references a scheme. It produces a scheme-details table, a Mermaid
  `sequenceDiagram` per OAuth2 flow (authorizationCode, clientCredentials, implicit, password),
  and a per-operation requirements cross-reference.
- Endpoint pages link entity mentions to stable anchors inside `domain-model.md` in the form
  `[\`Pet\`](../domain-model.md#entity-pet)`; the anchors are inlined into the entity table
  cells with `<a id="entity-<kebab>"></a>` and survive both GitHub-flavoured Markdown and
  Markdig HTML rendering.

### HTML rendering (Phase 4)

`StaticSiteRenderer` uses `Markdig` with `UseAdvancedExtensions()` so fenced code blocks tagged
`mermaid` emit as `<div class="mermaid">…</div>` — Mermaid.js picks these up automatically. A
small compiled regex rewrites intra-bundle `.md` hrefs to `.html` (preserving the `#fragment`
tail). The sidebar nav is built from the file tree; the current page is tagged `class="active"`
on its `<a>`. `assets/theme.css` is shipped as an embedded resource of
`DocFlow.Documentation` (dark/light via `prefers-color-scheme`). Mermaid.js loads from
`cdn.jsdelivr.net/npm/mermaid@10`; an offline-asset follow-up is tracked in
`docs/todo.md`.

### Pluggable spec parsing (Phase 5)

`IApiSpecParser` in `DocFlow.Core/Abstractions/` abstracts "parse an API spec stream into a
`SemanticModel`"; each implementation exposes a `Name`, a `CanParse(path, content)` predicate,
and a `ParseAsync(Stream, ct)` method that throws `FormatException` on parse failure.
`SpecParserRegistry` picks the first registered parser whose `CanParse` returns true and throws
`InvalidOperationException` with a "registered parsers: …" diagnostic otherwise. `OpenApiParser`
implements the interface via explicit implementation so its legacy `ISchemaParser` entry point
stays unchanged; the CLI's `integrate docs` and `integrate diff` commands both go through the
registry rather than hardcoding OpenAPI.

### Watch mode and changelogs (Phase 5)

`integrate docs --watch` runs an initial build, then wires a `FileSystemWatcher` to the spec
file with a 300 ms debounce (guarded by `SemaphoreSlim` so bursts of `Changed` events collapse
into a single regeneration). Watch exits cleanly on cancellation.

`integrate diff <old> <new> -o changelog.md` loads both specs via the registry, computes a
`SpecDiff` with `SpecDiffer`, and renders a Markdown changelog via `ChangelogGenerator`. Diff
heuristics map each difference to a `ChangeSeverity` (Breaking or NonBreaking) across six
`ChangeCategory` buckets (Operation, Parameter, RequestBody, Response, Schema, Security):

| Change | Severity |
|--------|----------|
| Added operation / added optional parameter / added optional property | Non-breaking |
| Removed operation / removed parameter / removed property / removed response status | Breaking |
| Added required parameter or property | Breaking |
| Tightened required flag (false → true) | Breaking |
| Relaxed required flag (true → false) | Non-breaking |
| Changed parameter / field / request-body / response entity type | Breaking |
| HTTP method or path changed on a kept operationId | Breaking |

The rendered changelog leads with a Breaking / Non-breaking count summary, then groups by
severity → category.

### Design rules

- **Pure generator**: `IDocumentationGenerator.GenerateAsync` returns an in-memory file list.
  The CLI layer owns persistence.
- **Deterministic output**: every iteration is `OrderBy`-preceded; `MarkdownWriter` forces LF
  line endings and trims trailing whitespace. The domain-model builder clones the input
  `SemanticModel` with entities re-inserted in alphabetical order so the upstream class diagram
  generator produces stable text.
- **Purity check**: `DocFlow.Documentation` depends on Core + Diagrams only. It never references
  `DocFlow.Integration`; the CLI orchestrates parsing and passes the `SemanticModel` plus the
  raw source bytes via `DocumentationOptions.SourceSpec`.
- **Source spec preservation**: the original spec is passed through unmodified as
  `assets/openapi.<ext>` so readers can verify the docs against the authoritative source
  without leaving the bundle.

### CLI

```
docflow integrate docs <spec> \
    -o <dir> \
    [--format markdown|html] \
    [--diagrams class,er,sequence,context,flow] \
    [--with-examples] \
    [--group-by tag|path] \
    [--title "My API"] \
    [-v]
```

Exit codes: `0` success, `1` validation error (missing spec, unknown flag value, parser
failure), `2` I/O error (`IOException`/`UnauthorizedAccessException`/`NotSupportedException`).
Phase 1 only implements `--format markdown`; `--format html` returns exit 1 with a pointer to
issue #10.

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
+-- DocFlow.Documentation     # Design-docs bundle generation (Phase 1 complete)
|   +-- DocFlow.Core
|   +-- DocFlow.Diagrams
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
| OpenAPI Parser | `src/DocFlow.Integration/Schemas/OpenApi/OpenApiParser.cs` |
| CDM Mapper | `src/DocFlow.Integration/Mapping/CdmMapper.cs` |
| SLA Validator | `src/DocFlow.Integration/Validation/SlaValidator.cs` |
| Code Generator | `src/DocFlow.Integration/CodeGen/IntegrationCodeGenerator.cs` |
| ApiSurface Records | `src/DocFlow.Core/CanonicalModel/ApiSurface.cs` |
| Spec Parser Abstraction | `src/DocFlow.Core/Abstractions/IApiSpecParser.cs` |
| Spec Parser Registry | `src/DocFlow.Core/Abstractions/SpecParserRegistry.cs` |
| Documentation Generator | `src/DocFlow.Documentation/Markdown/MarkdownDocumentationGenerator.cs` |
| Documentation Section Builders | `src/DocFlow.Documentation/Markdown/Sections/` |
| Example Synthesizer | `src/DocFlow.Documentation/Examples/ExampleSynthesizer.cs` |
| Spec Differ | `src/DocFlow.Documentation/Diff/SpecDiffer.cs` |
| Changelog Generator | `src/DocFlow.Documentation/Diff/ChangelogGenerator.cs` |
| Static HTML Renderer | `src/DocFlow.Documentation/Html/StaticSiteRenderer.cs` |
| HTML Theme (embedded) | `src/DocFlow.Documentation/Html/Assets/theme.css` |
| ER Diagram Generator | `src/DocFlow.Diagrams/Mermaid/MermaidErDiagramGenerator.cs` |
| Sequence Diagram Generator | `src/DocFlow.Diagrams/Mermaid/MermaidSequenceDiagramGenerator.cs` |
| C4 Context Generator | `src/DocFlow.Diagrams/Mermaid/MermaidC4ContextGenerator.cs` |
| Endpoint Flowchart Generator | `src/DocFlow.Diagrams/Mermaid/MermaidEndpointFlowchartGenerator.cs` |
| CLI Entry Point | `src/DocFlow.CLI/Program.cs` |
