# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Status

DocFlow is an intelligent documentation and modeling toolkit. Current implementation status:

| Component | Status | Description |
|-----------|--------|-------------|
| C# → Mermaid | **Complete** | Roslyn-based parsing, class diagram generation |
| Mermaid → C# | **Complete** | DDD-style code generation with records |
| Round-trip | **Complete** | Bidirectional with semantic preservation |
| Whiteboard Scanner | **Complete** | Claude Vision API integration |
| CLI | **Complete** | System.CommandLine + Spectre.Console |
| Integration Module | **Scaffolded** | OpenAPI parsing, CDM mapping designed |
| IMS Learning | Designed | Pattern learning system (not implemented) |
| Document Pipeline | Planned | PDF/Word conversion |

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run all tests (91+ tests across 3 projects)
dotnet test

# Run specific test project
dotnet test tests/DocFlow.CodeAnalysis.Tests   # 20 tests
dotnet test tests/DocFlow.Diagrams.Tests       # 52 tests
dotnet test tests/DocFlow.CodeGen.Tests        # 19 tests

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run the CLI locally
dotnet run --project src/DocFlow.CLI -- <command> [args]
```

## CLI Commands

```bash
# Generate Mermaid from C#
docflow diagram <input.cs> [-o output.mmd] [-r] [-v]

# Generate C# from Mermaid
docflow codegen <input.mmd> [-o output.cs] [-n namespace] [--style ddd|poco]

# Full round-trip test
docflow roundtrip <input.cs> [-o dir] [--compare] [-v]

# AI-powered whiteboard scanning
docflow scan <image> [-o output.mmd] [-c context] [-v]
```

## Architecture Overview

DocFlow transforms between diagrams, documentation, and code by routing everything through a **Canonical Semantic Model**.

### Core Data Flow

```
Source Format → IModelParser → SemanticModel → IModelGenerator → Target Format
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
├── DocFlow.Core           # Canonical model, abstractions
├── DocFlow.Diagrams       # Mermaid parsing & generation
├── DocFlow.CodeAnalysis   # Roslyn-based C# parsing
├── DocFlow.CodeGen        # C# code generation
├── DocFlow.Vision         # Whiteboard scanning (Claude Vision)
├── DocFlow.AI             # AI provider abstraction (Claude API)
├── DocFlow.IMS            # Intelligent Mapping Service
├── DocFlow.Ontology       # DDD pattern classification
├── DocFlow.Documents      # Document pipeline (planned)
├── DocFlow.Integration    # API integration (scaffolded)
└── DocFlow.Web            # Web UI (planned)
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

**Flow**: Image → Base64 → Claude Vision API → Mermaid text → MermaidParser → SemanticModel

**API Key Resolution** (priority order):
1. Environment variable: `ANTHROPIC_API_KEY`
2. User config: `~/.docflow/config.json`
3. Project config: `./docflow.json`

### 4. Integration Module (DocFlow.Integration) - Scaffolded

Designed but not fully implemented. Extends the canonical model to API integrations:

- **OpenApiParser** - Parse OpenAPI 3.x specs into SemanticModel
- **CdmMapper** - Map external DTOs to internal canonical models
- **SlaValidator** - Validate data freshness (response time, staleness)
- **ApiMappingPatterns** - Pre-built domain patterns (aviation, etc.)

See `docs/design/integration-module.md` for full design.

## Code Style

- .NET 8, C# 12, nullable enabled everywhere
- Prefer records for immutable types (especially Value Objects)
- Use `required` keyword for mandatory properties
- Async all the way down with CancellationToken support
- Follow Microsoft naming conventions
- Use collection expressions `[]` instead of `new List<T>()`

## Testing

91+ unit tests covering:
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
| `src/DocFlow.CodeAnalysis/CSharp/CSharpModelParser.cs` | C# → SemanticModel |
| `src/DocFlow.Diagrams/Mermaid/MermaidClassDiagramGenerator.cs` | SemanticModel → Mermaid |
| `src/DocFlow.Diagrams/Mermaid/MermaidClassDiagramParser.cs` | Mermaid → SemanticModel |
| `src/DocFlow.CodeGen/CSharp/CSharpModelGenerator.cs` | SemanticModel → C# |
| `src/DocFlow.Vision/WhiteboardScanner.cs` | Image → SemanticModel |
| `src/DocFlow.AI/Providers/ClaudeProvider.cs` | Claude API integration |
| `src/DocFlow.CLI/Program.cs` | CLI entry point |
