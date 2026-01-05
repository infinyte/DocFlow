# DocFlow

```
  ____                   _____   _
 |  _ \    ___     ___  |  ___| | |   ___   __      __
 | | | |  / _ \   / __| | |_    | |  / _ \  \ \ /\ / /
 | |_| | | (_) | | (__  |  _|   | | | (_) |  \ V  V /
 |____/   \___/   \___| |_|     |_|  \___/    \_/\_/
```

**Intelligent Documentation and Modeling Toolkit**

Transform whiteboard sketches into working code. Generate diagrams from source files. Map external APIs to your domain model. DocFlow is a complete toolkit for the modern software architect.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Tests](https://img.shields.io/badge/tests-91%20passing-brightgreen)]()

---

## Highlights

| Feature | Description |
|---------|-------------|
| **Whiteboard to Code** | Snap a photo of your whiteboard diagram, get working C# in seconds |
| **Bidirectional Transform** | C# to Mermaid and back with full semantic preservation |
| **API Integration** | Parse OpenAPI specs, map to your CDM, generate typed clients |
| **SLA Validation** | Validate data freshness against SLA requirements |
| **DDD Support** | Native understanding of aggregates, entities, and value objects |

---

## Quick Start

### Three Commands to See the Magic

```bash
# 1. Generate a Mermaid diagram from your C# domain model
docflow diagram Domain.cs -o domain.mmd

# 2. Scan a whiteboard photo and extract the diagram
docflow scan whiteboard.jpg -o extracted.mmd

# 3. Generate integration code from an OpenAPI spec
docflow integrate generate petstore.json --cdm Models/ -o Generated/
```

### Installation

**Linux/macOS/WSL:**
```bash
curl -sSL https://raw.githubusercontent.com/infinyte/docflow/main/install.sh | bash
```

**Windows PowerShell:**
```powershell
irm https://raw.githubusercontent.com/infinyte/docflow/main/install.ps1 | iex
```

**From Source:**
```bash
git clone https://github.com/infinyte/docflow.git
cd docflow
dotnet build
dotnet run --project src/DocFlow.CLI -- --help
```

---

## Commands

### Diagram Commands

Generate and transform between C# and Mermaid class diagrams.

```bash
# Generate Mermaid class diagram from C# source
docflow diagram Domain.cs -o domain.mmd

# Generate C# code from Mermaid diagram
docflow codegen diagram.mmd -o Models.cs --namespace MyApp.Domain

# Full round-trip test with comparison
docflow roundtrip Domain.cs --compare -v
```

### Whiteboard Scanning

AI-powered extraction of diagrams from photos using Claude Vision.

```bash
# Basic scan
docflow scan whiteboard.jpg -o extracted.mmd

# With context hint for better accuracy
docflow scan whiteboard.jpg -c "e-commerce order management" -v

# Full pipeline: whiteboard to C# code
docflow scan whiteboard.jpg -o temp.mmd && docflow codegen temp.mmd -o Models.cs
```

**Requirements:** Claude API key (see [Configuration](#configuration))

### Integration Commands

Analyze CDM mappings, validate SLAs, and generate integration code.

```bash
# Analyze mapping between API and your CDM
docflow integrate analyze petstore.json --cdm Models/Entities.cs --threshold 70

# Validate SLA data freshness
docflow integrate sla https://api.example.com/data --expected 30s --samples 10

# Generate integration code (DTOs, AutoMapper, HTTP client, validators)
docflow integrate generate petstore.json --cdm Models/ -o Generated/ -n MyApp.Integration
```

#### Generate Options

| Option | Description |
|--------|-------------|
| `--generate dtos` | External DTOs with JsonPropertyName attributes |
| `--generate mappers` | AutoMapper profiles with confidence comments |
| `--generate client` | Typed HTTP client interface |
| `--generate validators` | FluentValidation validators |
| `--generate all` | All of the above (default) |

---

## Configuration

### API Key Setup

The whiteboard scanner requires a Claude API key. Configure using one of these methods (in priority order):

**1. Environment Variable (Recommended):**
```bash
export ANTHROPIC_API_KEY='sk-ant-...'
```

**2. User Config (~/.docflow/config.json):**
```json
{
  "anthropicApiKey": "sk-ant-..."
}
```

**3. Project Config (./docflow.json):**
```json
{
  "anthropicApiKey": "sk-ant-..."
}
```

Get your API key at: https://console.anthropic.com/

---

## Architecture

DocFlow uses a **Canonical Semantic Model** as the universal truth layer. All formats translate to and from this model, enabling lossless bidirectional transformations.

```
                    +-------------------------------------+
                    |       Canonical Semantic Model      |
                    |  (Entities, Relationships, DDD)     |
                    +-----------------+-------------------+
                                      |
       +---------------+--------------+-------------+---------------+
       |               |              |             |               |
       v               v              v             v               v
+-------------+ +-------------+ +---------+ +-----------+ +---------------+
|   C# Code   | |   Mermaid   | | OpenAPI | |   CDM     | |  Whiteboard   |
|   (Roslyn)  | |  Diagrams   | |  Specs  | | Entities  | |   (Vision)    |
+-------------+ +-------------+ +---------+ +-----------+ +---------------+
```

### Why a Canonical Model?

Direct format conversion (A -> B) breaks down when:
- Information exists in A but not B (lossy)
- You need round-trips (A -> B -> A != A)
- Semantic meaning differs between formats

DocFlow's approach: **A -> Canonical Model -> B**

The model captures *meaning*, not just syntax. It knows that `ICollection<LineItem>` in C# and a filled diamond in Mermaid both represent *composition*.

---

## DDD Support

The semantic model understands Domain-Driven Design patterns:

| Classification | C# Output | Mermaid Output |
|----------------|-----------|----------------|
| AggregateRoot | Class with identity | `<<AggregateRoot>>` stereotype |
| Entity | Class with Id property | `<<Entity>>` stereotype |
| ValueObject | Immutable record type | `<<ValueObject>>` stereotype |
| DomainService | Service class | `<<Service>>` stereotype |
| Enum | Enumeration | `<<enumeration>>` stereotype |
| Interface | Interface contract | `<<interface>>` stereotype |

---

## Project Structure

```
DocFlow/
+-- src/
|   +-- DocFlow.Core           # Canonical model & abstractions
|   +-- DocFlow.Diagrams       # Mermaid parsing & generation
|   +-- DocFlow.CodeAnalysis   # Roslyn-based C# parsing
|   +-- DocFlow.CodeGen        # C# code generation from model
|   +-- DocFlow.Vision         # AI-powered whiteboard scanning
|   +-- DocFlow.AI             # Claude API integration
|   +-- DocFlow.IMS            # Intelligent Mapping Service
|   +-- DocFlow.Ontology       # DDD pattern classification
|   +-- DocFlow.Integration    # API integration automation
|   +-- DocFlow.Documents      # Document pipeline (planned)
|   +-- DocFlow.Web            # Web UI (planned)
|   +-- DocFlow.CLI            # Command-line interface
+-- tests/
|   +-- DocFlow.CodeAnalysis.Tests  # 20 tests
|   +-- DocFlow.Diagrams.Tests      # 52 tests
|   +-- DocFlow.CodeGen.Tests       # 19 tests
+-- docs/
|   +-- ARCHITECTURE.md        # Technical architecture
|   +-- CLI-REFERENCE.md       # Complete CLI documentation
|   +-- CHANGELOG.md           # Version history
|   +-- design/                # Design documents
+-- samples/
    +-- whiteboard-demos/      # Whiteboard scanner examples
    +-- integration-demos/     # Integration module examples
```

---

## Integration Module

The Integration module extends DocFlow's canonical model pattern to enterprise API integrations:

### Features

- **OpenAPI Parsing**: Extract semantic model from OpenAPI 3.x specs
- **CDM Mapping**: Map external DTOs to your Canonical Data Model with confidence scores
- **SLA Validation**: Validate data freshness against SLA requirements
- **Code Generation**: Generate DTOs, AutoMapper profiles, HTTP clients, validators

### Example Output

**Generated AutoMapper Profile:**
```csharp
// Pet -> Product (79% confidence)
CreateMap<PetDto, Product>()
    .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))  // 95% - Exact name match
    .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name))  // 95% - Exact name match
    .ForMember(d => d.Status, opt => opt.MapFrom(s => MapProductStatus(s.Status)))
    // TODO: Manual mapping required for Price (no source field)
    .ForMember(d => d.Price, opt => opt.Ignore());
```

**SLA Validation:**
```
SLA Validation: COMPLIANT
Expected Max Age: 30s
Actual Average:   12.4s
Compliance:       100% (10/10 samples)
```

See [docs/design/integration-module.md](docs/design/integration-module.md) for full documentation.

---

## Development

### Prerequisites

- .NET 8.0 SDK
- Claude API key (for whiteboard scanning)

### Build & Test

```bash
# Build
dotnet build

# Run all tests (91 tests)
dotnet test

# Run specific test project
dotnet test tests/DocFlow.CodeAnalysis.Tests
dotnet test tests/DocFlow.Diagrams.Tests
dotnet test tests/DocFlow.CodeGen.Tests

# Run CLI locally
dotnet run --project src/DocFlow.CLI -- diagram MyClass.cs
```

### Test Coverage

- **91 unit tests** across 3 test projects
- C# parsing accuracy (classes, records, interfaces, enums)
- Mermaid generation correctness
- Round-trip semantic preservation
- DDD pattern detection and classification

---

## Documentation

| Document | Description |
|----------|-------------|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Technical architecture and design |
| [CLI-REFERENCE.md](docs/CLI-REFERENCE.md) | Complete CLI command reference |
| [CHANGELOG.md](docs/CHANGELOG.md) | Version history and release notes |
| [Integration Design](docs/design/integration-module.md) | API integration module design |

---

## Roadmap

### v0.1.0-preview (Current)

- [x] C# -> Mermaid class diagram generation
- [x] Mermaid -> C# code generation (DDD-style)
- [x] Bidirectional round-trip with semantic preservation
- [x] AI-powered whiteboard scanning
- [x] Professional CLI with Spectre.Console
- [x] Integration module (analyze, sla, generate)
- [x] SLA data freshness validation
- [x] Integration code generation (DTOs, AutoMapper, clients, validators)

### v0.2.0

- [ ] IMS pattern learning from examples
- [ ] PlantUML support
- [ ] Sequence diagram support
- [ ] GraphQL schema parsing

### v0.3.0

- [ ] PDF/Word document pipeline
- [ ] VS Code extension
- [ ] Web UI (Blazor)

---

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Make your changes with tests
4. Ensure all tests pass (`dotnet test`)
5. Submit a pull request

---

## License

MIT License - see [LICENSE](LICENSE) for details.

---

## Acknowledgments

Built by **Kurt Mitchell** with **Claude (Anthropic)** as co-designer and implementation partner.

Open source dependencies:
- [Roslyn](https://github.com/dotnet/roslyn) - C# code analysis
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - CLI framework
- [Microsoft.OpenApi](https://github.com/microsoft/OpenAPI.NET) - OpenAPI parsing

---

<p align="center">
  <strong>Transform your diagrams. Generate your code. Ship faster with DocFlow.</strong>
</p>
