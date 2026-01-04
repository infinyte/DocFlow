# Changelog

All notable changes to DocFlow will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0-preview] - 2025-01-04

### Added

#### Core Pipeline
- **Canonical Semantic Model** - Universal intermediate representation for all transformations
- **SemanticModel** with entities, relationships, and namespaces
- **DDD Classifications** - AggregateRoot, Entity, ValueObject, DomainService, DomainEvent, Repository, Interface, Enum
- **Relationship Types** - Inheritance, Composition, Aggregation, Association, Dependency, Implementation

#### C# to Mermaid (DocFlow.CodeAnalysis + DocFlow.Diagrams)
- Roslyn-based C# parsing for classes, records, interfaces, enums
- Property extraction with types and visibility modifiers
- Method extraction with parameters and return types
- Inheritance and interface implementation detection
- Composition/aggregation inference from collection properties
- DDD stereotype detection from naming conventions
- Mermaid classDiagram generation with proper syntax

#### Mermaid to C# (DocFlow.Diagrams + DocFlow.CodeGen)
- Mermaid classDiagram parser with stereotype support
- C# code generator with nullable-enabled output
- Records for ValueObjects, classes for Entities
- XML documentation comment generation
- Proper access modifier mapping (+, -, #)

#### Round-Trip Support
- Full bidirectional transformation: C# → Mermaid → C#
- Semantic preservation across transformations
- `--compare` flag for diff visualization

#### Whiteboard Scanner (DocFlow.Vision + DocFlow.AI)
- AI-powered diagram extraction from photos
- Claude Vision API integration (claude-sonnet-4-20250514)
- Diagram type detection with confidence scoring
- Support for JPG, PNG, GIF, WEBP formats
- Context hints for improved accuracy

#### Claude API Integration (DocFlow.AI)
- `ClaudeProvider` with vision and text completion support
- Multi-source API key resolution:
  1. Environment variable: `ANTHROPIC_API_KEY`
  2. User config: `~/.docflow/config.json`
  3. Project config: `./docflow.json`
- Helpful error messages with configuration instructions

#### CLI (DocFlow.CLI)
- Professional CLI with System.CommandLine
- Rich output with Spectre.Console (colors, tables, panels)
- ASCII art banner
- Commands:
  - `diagram` (alias: `d`) - Generate Mermaid from C#
  - `codegen` (alias: `c`) - Generate C# from Mermaid
  - `roundtrip` (alias: `r`) - Full round-trip test
  - `scan` (alias: `s`) - Whiteboard scanning
- Global options: `--verbose`, `--quiet`

#### Integration Module (DocFlow.Integration) - Scaffolded
- OpenAPI 3.x parser foundation (`OpenApiParser`)
- CDM mapping engine design (`CdmMapper`)
- SLA validation for data freshness (`SlaValidator`)
- Pre-built aviation domain patterns (`ApiMappingPatterns`)
- Integration specification model (`IntegrationSpec`)

#### Testing
- 91+ unit tests across 3 test projects
- DocFlow.CodeAnalysis.Tests (20 tests)
- DocFlow.Diagrams.Tests (52 tests)
- DocFlow.CodeGen.Tests (19 tests)

#### Documentation
- Comprehensive README with feature status
- CLAUDE.md for AI assistant context
- Architecture documentation
- CLI reference
- Integration module design document

### Technical Details

- **.NET 8.0** with C# 12 features
- **Nullable reference types** enabled throughout
- **Async/await** with CancellationToken support
- **Records** for immutable types
- **Collection expressions** for clean syntax

### Dependencies

- Microsoft.CodeAnalysis.CSharp (Roslyn) - C# parsing
- Spectre.Console - CLI framework
- System.CommandLine - Command parsing
- Microsoft.OpenApi.Readers - OpenAPI parsing
- OpenCvSharp4 - Image preprocessing (Vision)

---

## Unreleased

### Planned for v0.2.0
- IMS (Intelligent Mapping Service) pattern learning
- PlantUML support
- Sequence diagram support
- Full Integration module implementation

### Planned for v0.3.0
- PDF/Word document pipeline
- VS Code extension
- Web UI (Blazor)
