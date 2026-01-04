# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build the entire solution
dotnet build

# Run all tests
dotnet test

# Run a specific test project
dotnet test tests/DocFlow.Core.Tests

# Run a single test by filter
dotnet test --filter "FullyQualifiedName~TestMethodName"

# Run the CLI locally
dotnet run --project src/DocFlow.CLI -- <command> [args]
```

## Architecture Overview

DocFlow transforms between diagrams, documentation, and code by routing everything through a **Canonical Semantic Model** - an ontology-grounded intermediate representation.

### Core Data Flow

```
Source Format → IModelParser → SemanticModel → IModelGenerator → Target Format
```

All transformations are bidirectional. The semantic model captures **meaning**, not syntax - e.g., both `ICollection<LineItem>` in C# and a filled diamond in UML represent *composition*.

### Key Abstractions (DocFlow.Core)

- **SemanticModel** (`CanonicalModel/SemanticModel.cs`): The central model containing entities, relationships, and namespaces. All parsers write to it, all generators read from it.
- **SemanticEntity** (`CanonicalModel/SemanticEntity.cs`): Represents classes, interfaces, value objects, etc. with DDD-aware classification (`EntityClassification` enum).
- **SemanticRelationship** (`CanonicalModel/SemanticRelationship.cs`): Captures relationship semantics (Composition vs Aggregation vs Association, multiplicities, DDD patterns like `ReferenceById`).
- **IModelParser** / **IModelGenerator** (`Abstractions/IModelTransformers.cs`): Interfaces for format-specific transformers. `IBidirectionalTransformer` combines both for round-trip capable formats.

### Intelligent Mapping Service (DocFlow.IMS)

The IMS learns transformation patterns from examples and applies them to new inputs:
- Observes transformations and extracts `LearnedPattern` instances
- Suggests mappings with confidence scores (Bayesian-style with Laplace smoothing)
- Improves from user feedback via `MappingFeedback`
- Bidirectional by design: if A→B works, B→A should too

### Project Dependencies

```
DocFlow.CLI (entry point)
├── DocFlow.Core (canonical model, abstractions)
├── DocFlow.Diagrams (Mermaid, PlantUML)
├── DocFlow.Documents (Markdown, PDF, Word)
├── DocFlow.CodeAnalysis (Roslyn-based C# parsing)
├── DocFlow.CodeGen (code generation from model)
├── DocFlow.Vision (computer vision, whiteboard scanning)
├── DocFlow.IMS (pattern learning)
├── DocFlow.Ontology (DDD pattern classification)
└── DocFlow.AI (AI provider integrations)
```

### DDD Pattern Support

Entity classifications follow DDD tactical patterns: `AggregateRoot`, `Entity`, `ValueObject`, `DomainEvent`, `Repository`, etc. The model validates DDD invariants (e.g., entities should have identity, value objects should not).

### Configuration

Uses `docflow.json` in project root. Supports environment variable substitution (e.g., `${ANTHROPIC_API_KEY}`).

## Flagship Feature: Whiteboard Scanning
The killer demo feature is `docflow scan` - photograph a whiteboard sketch and convert it to working code. The pipeline: Image → Preprocessing (OpenCV) → Shape/Text Detection → AI Semantic Analysis (Claude API) → SemanticModel → Code/Diagram output. See `DocFlow.Vision/IWhiteboardScanner.cs` for the full interface.

## AI Strategy
Hybrid approach: Use local models (ONNX) for fast/cheap operations (shape detection, basic OCR), API calls (Claude/OpenAI) for semantic understanding. Provider abstraction in `DocFlow.AI/Providers/IAiProvider.cs`.

## Code Style
- .NET 8, C# 12, nullable enabled everywhere
- Prefer records for immutable types (especially Value Objects)
- Use `required` keyword for mandatory properties
- Async all the way down with CancellationToken support
- Follow Microsoft naming conventions

## Current Priority
Phase 1 MVP: C# → Mermaid class diagram generator. Proves the full pipeline (Roslyn parser → SemanticModel → Mermaid generator).
