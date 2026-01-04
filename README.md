# DocFlow

**Intelligent Documentation and Modeling Toolkit**

Transform code into diagrams, diagrams into code, and whiteboard sketches into working models. Built on a Canonical Semantic Model that preserves meaning across all transformations.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)]()
[![Tests](https://img.shields.io/badge/tests-72%20passing-brightgreen)]()

---

## Features

| Feature | Status | Description |
|---------|--------|-------------|
| C# to Mermaid | **Implemented** | Generate class diagrams from C# source code |
| Mermaid to C# | **Implemented** | Generate DDD-style C# from Mermaid diagrams |
| Round-trip Sync | **Implemented** | Bidirectional transformation with semantic preservation |
| Whiteboard Scanning | **Implemented** | AI-powered diagram extraction from photos |
| Professional CLI | **Implemented** | Spectre.Console with rich output |
| API Integration | **Scaffolded** | OpenAPI parsing, CDM mapping (designed) |
| Document Pipeline | Planned | PDF/Word/Markdown conversion |
| IMS Learning | Planned | Pattern learning from examples |

---

## Quick Start

### Installation

**Linux/macOS/WSL:**
```bash
curl -sSL https://raw.githubusercontent.com/kurtmitchell/docflow/main/install.sh | bash
```

**Windows PowerShell:**
```powershell
irm https://raw.githubusercontent.com/kurtmitchell/docflow/main/install.ps1 | iex
```

**From Source:**
```bash
git clone https://github.com/kurtmitchell/docflow.git
cd docflow
dotnet build
dotnet run --project src/DocFlow.CLI -- --help
```

### Usage Examples

```bash
# Generate Mermaid class diagram from C# source
docflow diagram Domain.cs -o domain.mmd

# Generate C# code from Mermaid diagram
docflow codegen diagram.mmd -o Models.cs --namespace MyApp.Domain

# AI-powered whiteboard scanning (requires API key)
docflow scan whiteboard.jpg -o extracted.mmd

# Full round-trip test with comparison
docflow roundtrip Domain.cs --compare -v
```

### API Key Configuration

The whiteboard scanner requires a Claude API key. Configure it using one of these methods (in priority order):

**1. Environment Variable:**
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

---

## Architecture

DocFlow uses a **Canonical Semantic Model** as the universal truth layer. All formats translate to and from this model, enabling lossless bidirectional transformations.

```
                    ┌─────────────────────────────────────┐
                    │       Canonical Semantic Model      │
                    │  (Entities, Relationships, DDD)     │
                    └──────────────┬──────────────────────┘
                                   │
       ┌───────────────┬───────────┼───────────┬───────────────┐
       │               │           │           │               │
       ▼               ▼           ▼           ▼               ▼
┌─────────────┐ ┌─────────────┐ ┌───────┐ ┌─────────┐ ┌─────────────┐
│   C# Code   │ │   Mermaid   │ │  API  │ │  Docs   │ │  Whiteboard │
│   (Roslyn)  │ │  Diagrams   │ │ Specs │ │  (TBD)  │ │   (Vision)  │
└─────────────┘ └─────────────┘ └───────┘ └─────────┘ └─────────────┘
```

### Why a Canonical Model?

Direct format conversion (A → B) breaks down when:
- Information exists in A but not B (lossy)
- You need round-trips (A → B → A ≠ A)
- Semantic meaning differs between formats

DocFlow's approach: **A → Canonical Model → B**

The model captures *meaning*, not just syntax. It knows that `ICollection<LineItem>` in C# and a filled diamond in Mermaid both represent *composition*.

### DDD Support

The semantic model understands Domain-Driven Design patterns:

| Classification | Generated As |
|----------------|--------------|
| AggregateRoot | Class with `<<AggregateRoot>>` stereotype |
| Entity | Class with identity property |
| ValueObject | Immutable record type |
| DomainService | Service class |
| Enum | Enumeration |
| Interface | Interface contract |

---

## Project Structure

```
DocFlow/
├── src/
│   ├── DocFlow.Core           # Canonical model & abstractions
│   ├── DocFlow.Diagrams       # Mermaid parsing & generation
│   ├── DocFlow.CodeAnalysis   # Roslyn-based C# parsing
│   ├── DocFlow.CodeGen        # C# code generation from model
│   ├── DocFlow.Vision         # AI-powered whiteboard scanning
│   ├── DocFlow.AI             # Claude API integration
│   ├── DocFlow.IMS            # Intelligent Mapping Service (pattern learning)
│   ├── DocFlow.Ontology       # DDD pattern classification
│   ├── DocFlow.Documents      # Document pipeline (planned)
│   ├── DocFlow.Integration    # API integration automation (scaffolded)
│   ├── DocFlow.Web            # Web UI (planned)
│   └── DocFlow.CLI            # Command-line interface
├── tests/
│   ├── DocFlow.CodeAnalysis.Tests  # 20 tests
│   ├── DocFlow.Diagrams.Tests      # 52 tests
│   └── DocFlow.CodeGen.Tests       # 19 tests
├── docs/
│   ├── ARCHITECTURE.md        # Technical architecture
│   ├── CLI-REFERENCE.md       # Complete CLI documentation
│   ├── CHANGELOG.md           # Version history
│   └── design/                # Design documents
└── samples/
    └── whiteboard-demos/      # Whiteboard scanner examples
```

### DocFlow.Integration (Scaffolded)

The Integration module extends DocFlow's canonical model pattern to enterprise API integrations:

- **OpenAPI/Swagger parsing** → Semantic model extraction
- **CDM (Canonical Data Model) mapping** → External API ↔ internal model
- **SLA validation** → Data freshness checking
- **Pre-built domain patterns** → Aviation, e-commerce, etc.

See [docs/design/integration-module.md](docs/design/integration-module.md) for the full design.

---

## CLI Commands

| Command | Alias | Description |
|---------|-------|-------------|
| `diagram <input>` | `d` | Generate Mermaid from C# source |
| `codegen <input>` | `c` | Generate C# from Mermaid diagram |
| `roundtrip <input>` | `r` | Full C# → Mermaid → C# round-trip |
| `scan <image>` | `s` | AI-powered whiteboard scanning |

See [docs/CLI-REFERENCE.md](docs/CLI-REFERENCE.md) for complete documentation.

---

## Development

### Prerequisites

- .NET 8.0 SDK
- Claude API key (for whiteboard scanning)

### Build & Test

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run CLI locally
dotnet run --project src/DocFlow.CLI -- diagram MyClass.cs
```

### Test Coverage

- **91+ unit tests** across 3 test projects
- C# parsing, Mermaid generation, round-trip preservation
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
- [x] C# → Mermaid class diagram generation
- [x] Mermaid → C# code generation (DDD-style)
- [x] Bidirectional round-trip with semantic preservation
- [x] AI-powered whiteboard scanning
- [x] Professional CLI with Spectre.Console
- [x] Integration module scaffolded

### v0.2.0
- [ ] IMS pattern learning from examples
- [ ] PlantUML support
- [ ] Sequence diagram support
- [ ] Integration module implementation

### v0.3.0
- [ ] PDF/Word document pipeline
- [ ] VS Code extension
- [ ] Web UI (Blazor)

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
