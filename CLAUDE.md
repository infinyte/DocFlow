# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status: v0.1.0-preview

DocFlow is an intelligent documentation and modeling toolkit. Current implementation status:

| Component | Status | Description |
|-----------|--------|-------------|
| C# to Mermaid | **Complete** | Roslyn-based parsing, class diagram generation |
| Mermaid to C# | **Complete** | DDD-style code generation with records |
| Round-trip | **Complete** | Bidirectional with semantic preservation |
| Whiteboard Scanner | **Complete** | Claude Vision API integration |
| CLI | **Complete** | System.CommandLine + Spectre.Console |
| Integration Module | **Complete** | OpenAPI parsing, CDM mapping, SLA validation, code generation |
| IMS Learning | Designed | Pattern learning system (not implemented) |
| Document Pipeline | Planned | PDF/Word conversion |

**Solution Stats:**
- 13 projects compiling (11 source + 2 test)
- 72 tests passing (20 CodeAnalysis + 52 Diagrams)

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run all tests (72 tests across 2 projects)
dotnet test

# Run specific test project
dotnet test tests/DocFlow.CodeAnalysis.Tests   # 20 tests
dotnet test tests/DocFlow.Diagrams.Tests       # 52 tests

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run the CLI locally
dotnet run --project src/DocFlow.CLI -- <command> [args]
```

## CLI Commands

### Diagram Commands

```bash
# Generate Mermaid from C#
docflow diagram <input.cs> [-o output.mmd] [-v]

# Generate C# from Mermaid
docflow codegen <input.mmd> [-o output.cs] [-n namespace]

# Full round-trip test
docflow roundtrip <input.cs> [-o dir] [--compare] [-v]

# AI-powered whiteboard scanning
docflow scan <image> [-o output.mmd] [-c context] [-v]
```

### Integration Commands

```bash
# Parse OpenAPI spec and display entities/endpoints
docflow integrate parse <spec.json> [-v]

# Analyze CDM mapping with confidence scores
docflow integrate analyze <spec.json> --cdm <path> [--threshold 70] [-v]

# Validate SLA data freshness
docflow integrate sla <url> --expected <duration> [--samples 10] [--interval 5s]

# Generate integration code (DTOs, AutoMapper, HTTP client, validators)
docflow integrate generate <spec.json> --cdm <path> -o <dir> [-n namespace]
```

## Architecture Overview

DocFlow transforms between diagrams, documentation, and code by routing everything through a **Canonical Semantic Model**.

### Core Data Flow

```
Source Format -> IModelParser -> SemanticModel -> IModelGenerator -> Target Format
```

All transformations are bidirectional. The semantic model captures **meaning**, not syntax.

### Key Abstractions (DocFlow.Core)

- **SemanticModel** (`CanonicalModel/SemanticModel.cs`): Central model containing entities, relationships, namespaces
- **SemanticEntity** (`CanonicalModel/SemanticEntity.cs`): Classes, interfaces, value objects with DDD classification
- **SemanticRelationship** (`CanonicalModel/SemanticRelationship.cs`): Relationship semantics (Composition, Aggregation, etc.)
- **IModelParser / IModelGenerator** (`Abstractions/IModelTransformers.cs`): Format-specific transformers

### Project Dependencies

```
DocFlow.CLI (entry point)
+-- DocFlow.Core           # Canonical model, abstractions
+-- DocFlow.Diagrams       # Mermaid parsing & generation
+-- DocFlow.CodeAnalysis   # Roslyn-based C# parsing
+-- DocFlow.CodeGen        # C# code generation
+-- DocFlow.Vision         # Whiteboard scanning (Claude Vision)
+-- DocFlow.AI             # AI provider abstraction (Claude API)
+-- DocFlow.IMS            # Intelligent Mapping Service
+-- DocFlow.Ontology       # DDD pattern classification
+-- DocFlow.Documents      # Document pipeline (planned)
+-- DocFlow.Integration    # API integration (complete)
+-- DocFlow.Web            # Web UI (planned)
```

### DDD Pattern Support

Entity classifications follow DDD tactical patterns:
- `AggregateRoot` - Aggregate boundary with identity
- `Entity` - Has identity, lifecycle
- `ValueObject` - Immutable, equality by value
- `DomainService` - Stateless operations
- `DomainEvent` - Something that happened
- `Repository` - Collection-like persistence
- `Interface` - Contract definition
- `Enum` - Enumeration type

## Implemented Features

### 1. C# to Mermaid (DocFlow.CodeAnalysis + DocFlow.Diagrams)

**Parser**: `CSharpModelParser` - Uses Roslyn to extract:
- Classes, records, interfaces, enums
- Properties with types and visibility
- Methods with signatures
- Inheritance and interface implementation
- Composition/aggregation from collection properties
- DDD stereotypes from naming conventions

**Generator**: `MermaidClassDiagramGenerator` - Produces:
- Valid Mermaid classDiagram syntax
- Property/method visibility markers (+, -, #)
- Relationship arrows (inheritance, composition, association)
- DDD stereotype annotations

### 2. Mermaid to C# (DocFlow.Diagrams + DocFlow.CodeGen)

**Parser**: `MermaidClassDiagramParser` - Extracts:
- Class definitions with members
- Stereotypes (<<interface>>, <<abstract>>, <<AggregateRoot>>)
- Relationships and multiplicities

**Generator**: `CSharpModelGenerator` - Produces:
- Nullable-enabled C# 12 code
- Records for ValueObjects, classes for Entities
- Proper access modifiers
- XML documentation comments
- DDD-style aggregate boundaries

### 3. Whiteboard Scanner (DocFlow.Vision + DocFlow.AI)

**Components**:
- `WhiteboardScanner` - Orchestrates the scanning pipeline
- `ClaudeProvider` - Claude API client with vision support
- `IWhiteboardScanner` interface for abstraction

**Flow**: Image -> Base64 -> Claude Vision API -> Mermaid text -> MermaidParser -> SemanticModel

**API Key Resolution** (priority order):
1. Environment variable: `ANTHROPIC_API_KEY`
2. User config: `~/.docflow/config.json`
3. Project config: `./docflow.json`

### 4. Integration Module (DocFlow.Integration)

Complete implementation extending the canonical model to API integrations:

**OpenAPI Parsing** (`Schemas/OpenApiParser.cs`):
- Parse OpenAPI 3.x specifications
- Extract entities, endpoints, parameters
- Convert to SemanticModel

**CDM Mapping** (`Mapping/CdmMapper.cs`):
- Multi-pass field matching (exact, ID, contains, FK pattern, datetime)
- Semantic entity matching for domain concepts
- Confidence scoring with reasoning

**SLA Validation** (`Validation/SlaValidator.cs`):
- Data freshness validation
- Duration parsing (30s, 5m, 1h, 500ms)
- Compliance verdicts (COMPLIANT, MARGINALLY COMPLIANT, MINOR/SEVERE VIOLATION)

**Code Generation** (`CodeGen/IntegrationCodeGenerator.cs`):
- External DTOs with `[JsonPropertyName]` attributes
- AutoMapper profiles with confidence comments and TODO markers
- Typed HTTP client interfaces
- FluentValidation validators

See `docs/design/integration-module.md` for full design.

## Code Style

- .NET 8, C# 12, nullable enabled everywhere
- Prefer records for immutable types (especially Value Objects)
- Use `required` keyword for mandatory properties
- Async all the way down with CancellationToken support
- Follow Microsoft naming conventions
- Use collection expressions `[]` instead of `new List<T>()`

## Testing

72 unit tests covering:
- C# parsing accuracy (class, record, interface, enum)
- Mermaid generation correctness
- Round-trip semantic preservation
- DDD pattern detection
- Relationship extraction

## Configuration

API keys can be configured via:
1. Environment variable: `ANTHROPIC_API_KEY`
2. User config: `~/.docflow/config.json` with `{"anthropicApiKey": "..."}`
3. Project config: `./docflow.json` with `{"anthropicApiKey": "..."}`

## Key Files

| File | Purpose |
|------|---------|
| `src/DocFlow.Core/CanonicalModel/SemanticModel.cs` | Central semantic model |
| `src/DocFlow.CodeAnalysis/CSharp/CSharpModelParser.cs` | C# -> SemanticModel |
| `src/DocFlow.Diagrams/Mermaid/MermaidClassDiagramGenerator.cs` | SemanticModel -> Mermaid |
| `src/DocFlow.Diagrams/Mermaid/MermaidClassDiagramParser.cs` | Mermaid -> SemanticModel |
| `src/DocFlow.CodeGen/CSharp/CSharpModelGenerator.cs` | SemanticModel -> C# |
| `src/DocFlow.Vision/WhiteboardScanner.cs` | Image -> SemanticModel |
| `src/DocFlow.AI/Providers/ClaudeProvider.cs` | Claude API integration |
| `src/DocFlow.Integration/Schemas/OpenApiParser.cs` | OpenAPI -> SemanticModel |
| `src/DocFlow.Integration/Mapping/CdmMapper.cs` | CDM mapping with confidence |
| `src/DocFlow.Integration/Validation/SlaValidator.cs` | SLA data freshness validation |
| `src/DocFlow.Integration/CodeGen/IntegrationCodeGenerator.cs` | Integration code generation |
| `src/DocFlow.CLI/Program.cs` | CLI entry point |

## Sample Files

| Location | Purpose |
|----------|---------|
| `samples/whiteboard-demos/` | Whiteboard scanner examples and test images |
| `samples/integration-demos/petstore.json` | Sample OpenAPI specification |
| `samples/integration-demos/SampleCdm/Entities.cs` | Sample CDM entities |
| `samples/integration-demos/sla-test-guide.md` | SLA testing guide with public APIs |
