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
- Full bidirectional transformation: C# -> Mermaid -> C#
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
- Rich output with Spectre.Console (colors, tables, panels, progress bars)
- ASCII art banner
- Commands:
  - `diagram` (alias: `d`) - Generate Mermaid from C#
  - `codegen` (alias: `c`) - Generate C# from Mermaid
  - `roundtrip` (alias: `r`) - Full round-trip test
  - `scan` (alias: `s`) - Whiteboard scanning
  - `integrate` - API integration sub-commands: `parse`, `analyze`, `sla`, `generate`
- Global options: `--verbose`, `--quiet`

#### Integration Module (DocFlow.Integration)
Complete API integration automation extending the canonical model:

**CDM Mapping Analysis** (`docflow integrate analyze`)
- Parse OpenAPI 3.x specifications
- Extract entities, endpoints, parameters
- Multi-pass field matching algorithm:
  - Pass 1: Exact name matches (95% confidence)
  - Pass 2: ID field matches (85% confidence)
  - Pass 3: Contains matches (75% confidence)
  - Pass 4: Foreign key patterns (70% confidence)
  - Pass 5: Date/time field matches (70% confidence)
- Semantic entity matching (Pet->Product, etc.)
- Confidence scoring with reasoning
- Rich CLI output with color-coded confidence levels
- Threshold filtering

**SLA Validation** (`docflow integrate sla`)
- Data freshness validation against SLA requirements
- Duration parsing: `30s`, `5m`, `1h`, `500ms`
- Multi-sample collection with configurable intervals
- Live progress display
- Compliance verdicts:
  - COMPLIANT (100%)
  - MARGINALLY COMPLIANT (90-99%)
  - MINOR VIOLATION (50-89%)
  - SEVERE VIOLATION (0-49%)
- JSON report export

**Code Generation** (`docflow integrate generate`)
- External DTOs with `[JsonPropertyName]` attributes
- AutoMapper profiles with:
  - Confidence comments for each mapping
  - TODO markers for unmapped fields
  - Unique enum mapping methods
  - No duplicate property mappings
- Typed HTTP client interfaces
- FluentValidation validators with:
  - Required field validation
  - String length constraints
- Rich CLI output showing generated files

**Pre-built Patterns**
- DateTime conversion patterns
- Identifier patterns (primary key, foreign key)
- Contact patterns (email, phone)
- Audit patterns (created_at, updated_at)

#### Testing
- 72 unit tests across 2 test projects:
  - DocFlow.CodeAnalysis.Tests: 20 tests
  - DocFlow.Diagrams.Tests: 52 tests

#### Documentation
- Comprehensive README with feature status
- CLAUDE.md for AI assistant context
- Architecture documentation
- CLI reference with all commands
- Integration module design document
- Sample files for testing:
  - `samples/whiteboard-demos/` - Whiteboard examples
  - `samples/integration-demos/` - OpenAPI specs and CDM samples

### Technical Details

- **.NET 8.0** with C# 12 features
- **13 projects** in solution (11 source + 2 test, all compiling)
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
- IMS (Intelligent Mapping Service) pattern learning from examples
- PlantUML support
- Sequence diagram support
- GraphQL schema parsing

### Planned for v0.3.0
- PDF/Word document pipeline
- VS Code extension
- Web UI (Blazor)
