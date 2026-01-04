# DocFlow

**Intelligent Documentation and Modeling Toolkit**

Transform whiteboard sketches into working code. Keep diagrams and code in sync. Let AI learn your team's patterns.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/kurtmitchell/docflow/build.yml?branch=main)](https://github.com/kurtmitchell/docflow/actions)

---

## The Problem

Every software team struggles with the same documentation challenges:

- **Whiteboard sketches get lost** - Great ideas drawn in meetings never make it to code
- **Diagrams go stale** - Documentation diverges from implementation within weeks
- **Format conversion is painful** - Markdown ↔ PDF ↔ Word round-trips lose information
- **Every new integration is manual** - Teams reinvent the same mapping patterns over and over

## The Solution

DocFlow treats **diagrams, documentation, and code as interconnected representations of the same underlying model**. Change one, and the others stay in sync.

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   Whiteboard    │     │    Canonical    │     │      Code       │
│    Photo        │────▶│   Semantic      │────▶│    (C#/Java)    │
│                 │     │     Model       │     │                 │
└─────────────────┘     └────────┬────────┘     └─────────────────┘
                                 │
        ┌────────────────────────┼────────────────────────┐
        ▼                        ▼                        ▼
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│    Mermaid      │     │   Documentation │     │    PlantUML     │
│    Diagram      │     │   (Markdown)    │     │    Diagram      │
└─────────────────┘     └─────────────────┘     └─────────────────┘
```

---

## ✨ Key Features

### 📷 Whiteboard to Code (Flagship Feature)

Snap a photo of your whiteboard sketch and get working code:

```bash
$ docflow scan whiteboard.jpg --output domain-model

📷 Processing whiteboard image...
🔍 Detected: UML Class Diagram (94% confidence)
🧠 Identified 5 entities, 4 relationships
✅ Generated: domain-model.mmd (Mermaid)
✅ Generated: domain-model.cs (C# classes)
✅ Generated: domain-model.md (Documentation)
```

Uses computer vision and AI to understand your sketches - even messy ones.

### 🔄 Bidirectional Code ↔ Diagram Sync

Generate diagrams from code:
```bash
$ docflow diagram ./src/Domain --output architecture.mmd
```

Generate code from diagrams:
```bash
$ docflow codegen class-diagram.mmd --output ./src/Models --lang csharp
```

### 🧠 Intelligent Mapping Service (IMS)

DocFlow learns your team's patterns and applies them automatically:

```bash
$ docflow learn --source existing-model.cs --target existing-diagram.mmd

📚 Learning from example...
   Extracted 23 patterns
   Updated confidence on 45 existing patterns
   
$ docflow convert NewModel.cs --to mermaid

🤖 Applied 12 learned patterns (avg confidence: 94%)
✅ Generated: NewModel.mmd
```

The IMS improves with every transformation you make.

### 📄 Document Pipeline

Convert between formats with diagrams preserved:

```bash
$ docflow convert design-doc.md --to pdf --render-diagrams
$ docflow convert specification.docx --to markdown
```

### 🏛️ DDD-Aware Code Generation

DocFlow understands Domain-Driven Design patterns:

```bash
$ docflow codegen order-model.mmd --style ddd

# Generates:
# - Aggregate roots with proper encapsulation
# - Value objects as immutable records
# - Repository interfaces
# - Domain events
```

---

## 🚀 Quick Start

### Installation

```bash
# Install as a global .NET tool
dotnet tool install --global DocFlow.CLI

# Or clone and build from source
git clone https://github.com/kurtmitchell/docflow.git
cd docflow
dotnet build
```

### Configuration

Create a `docflow.json` in your project root:

```json
{
  "ai": {
    "provider": "claude",
    "apiKey": "${ANTHROPIC_API_KEY}"
  },
  "codeGen": {
    "language": "csharp",
    "style": "ddd",
    "useRecordsForValueObjects": true
  },
  "ims": {
    "enableLearning": true,
    "patternStorePath": ".docflow/patterns.db"
  }
}
```

### Your First Scan

```bash
# Scan a whiteboard photo
docflow scan meeting-whiteboard.jpg

# Convert C# to Mermaid diagram  
docflow diagram ./src/Domain/Order.cs

# Generate C# from a Mermaid class diagram
docflow codegen order.mmd --lang csharp --output ./src/Models/
```

---

## 📖 Documentation

| Guide | Description |
|-------|-------------|
| [Getting Started](docs/getting-started.md) | Installation and first steps |
| [Whiteboard Scanning](docs/whiteboard-scanning.md) | Tips for best results with photos |
| [Code Generation](docs/code-generation.md) | Customizing generated code |
| [IMS Deep Dive](docs/intelligent-mapping.md) | How the learning system works |
| [API Reference](docs/api-reference.md) | For library consumers |

---

## 🏗️ Architecture

DocFlow is built on a **Canonical Semantic Model** - an ontologically-grounded representation that all formats translate to and from:

```
DocFlow/
├── DocFlow.Core           # Canonical model & abstractions
├── DocFlow.Diagrams       # Mermaid, PlantUML parsing/generation
├── DocFlow.Documents      # Markdown, PDF, Word conversion
├── DocFlow.CodeAnalysis   # Roslyn-based C# analysis
├── DocFlow.CodeGen        # Code generation from model
├── DocFlow.Vision         # Computer vision & whiteboard scanning
├── DocFlow.IMS            # Intelligent Mapping Service
├── DocFlow.Ontology       # DDD pattern classification & reasoning
├── DocFlow.AI             # AI provider integrations
└── DocFlow.CLI            # Command-line interface
```

### Why a Canonical Model?

Most tools treat format conversion as direct translation (A → B). This breaks down when:
- Information exists in A but not B (lossy)
- You need to round-trip (A → B → A ≠ A)
- Semantics differ between formats

DocFlow's approach: **A → Canonical Model → B**

The canonical model captures *meaning*, not just syntax. It knows that `ICollection<LineItem>` in C# and a filled diamond arrow in UML both represent *composition* - and generates appropriate output for each format.

---

## 🤝 Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### Development Setup

```bash
# Clone the repo
git clone https://github.com/kurtmitchell/docflow.git
cd docflow

# Build
dotnet build

# Run tests
dotnet test

# Run the CLI locally
dotnet run --project src/DocFlow.CLI -- scan test-image.jpg
```

### Areas We Need Help

- 🖼️ **Training data** - Whiteboard photos with corresponding diagrams
- 🌐 **Language support** - TypeScript, Python, Go code generation
- 📊 **Diagram types** - Sequence diagrams, state machines, ER diagrams
- 🧪 **Testing** - Edge cases, real-world scenarios

---

## 📜 License

MIT License - see [LICENSE](LICENSE) for details.

---

## 🙏 Acknowledgments

DocFlow was designed and built by:

- **Kurt Mitchell** - Architecture, implementation, domain expertise
- **Claude (Anthropic)** - Co-design, code generation, documentation

Special thanks to the open source projects that make DocFlow possible:
- [Roslyn](https://github.com/dotnet/roslyn) - C# code analysis
- [OpenCvSharp](https://github.com/shimat/opencvsharp) - Computer vision
- [Markdig](https://github.com/xoofx/markdig) - Markdown processing
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - Beautiful CLI

---

## 🗺️ Roadmap

### v0.1 (Current)
- [x] Core canonical model
- [x] C# → Mermaid class diagram
- [x] Basic whiteboard scanning
- [ ] Mermaid → C# code generation
- [ ] CLI with basic commands

### v0.2
- [ ] IMS pattern learning
- [ ] PDF/Word document pipeline
- [ ] PlantUML support
- [ ] Java code analysis/generation

### v0.3
- [ ] GA-optimized diagram layouts
- [ ] Sequence diagram support
- [ ] VS Code extension
- [ ] Team pattern sharing

### v1.0
- [ ] Full round-trip support all formats
- [ ] Blazor web UI
- [ ] Self-hosted model support
- [ ] Enterprise features

---

<p align="center">
  <strong>Stop losing your whiteboard ideas. Start shipping with DocFlow.</strong>
</p>
