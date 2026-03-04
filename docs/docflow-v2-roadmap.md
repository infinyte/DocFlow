# DocFlow v2.0 Future Enhancements Roadmap

> **Document Version:** 1.0  
> **Created:** January 5, 2026  
> **Author:** Kurt Mitchell  
> **Status:** Planning

---

## Executive Summary

This document captures planned enhancements for DocFlow v2.0, building upon the v1.0-preview foundation. These features were identified during initial development and early usage but deferred to maintain focus on core functionality.

---

## 1. Stateful Project Model

### Current State (v1.0)
- All commands are **stateless** - parse, analyze, exit
- No persistence between runs
- Each command re-parses source files from scratch
- No workflow tracking

### Proposed Enhancement
Implement a `.docflow/` project folder with persistent state:

```
.docflow/
├── project.json          # Project metadata and settings
├── integrations/
│   ├── petstore.json     # Cached parsed API spec
│   └── external-api.json # Cached parsed API spec
├── mappings.json         # Confirmed field mappings
├── overrides.json        # Manual mapping corrections
└── history/
    └── analysis-2026-01-05.json  # Audit trail
```

### Benefits
| Benefit | Description |
|---------|-------------|
| **Performance** | Don't re-parse large specs every run |
| **Workflow Tracking** | Know which integrations are analyzed vs. generated |
| **Audit Trail** | "When did we last analyze vendor X's API?" |
| **Incremental Updates** | Spec changed? Only re-analyze the delta |
| **CI/CD Baseline** | Compare new spec version against stored baseline |

### New Commands
```bash
docflow init                           # Initialize .docflow/ project
docflow status                         # Show workflow state
docflow integrate add <spec>           # Add and parse spec to project
docflow integrate diff <spec>          # Compare spec against cached version
```

### Priority: **High**
### Effort: **Medium** (2-3 weeks)

---

## 2. Intelligent Mapping Service (IMS) - Learning System

### Current State (v1.0)
- Confidence scores calculated at runtime
- Displayed but not stored
- No learning between runs
- Pattern matching is static

### Proposed Enhancement
Implement a feedback loop that learns from confirmed mappings:

```
┌─────────────────┐     ┌──────────────────┐
│  New API Spec   │     │ Historical Store │
└────────┬────────┘     └────────┬─────────┘
         │                       │
         ▼                       ▼
┌─────────────────┐     ┌──────────────────┐
│  Pattern Match  │◄────│ Learned Patterns │
└────────┬────────┘     └──────────────────┘
         │                       ▲
         ▼                       │
┌─────────────────┐     ┌──────────────────┐
│ Confidence Score│────▶│  User Confirms   │
└─────────────────┘     └──────────────────┘
```

### Data Model
```json
// ~/.docflow/mappings.json
{
  "confirmedMappings": [
    {
      "sourceField": "petName",
      "targetField": "Name",
      "confidence": 95,
      "confirmedByUser": true,
      "context": "ecommerce",
      "apiSource": "petstore",
      "confirmedAt": "2026-01-05T18:30:00Z"
    }
  ],
  "learnedPatterns": {
    "ecommerce": {
      "pet|product|item": "Product",
      "category": "ProductCategory",
      "status": "Status"
    }
  },
  "rejectedMappings": [
    {
      "sourceField": "photoUrls",
      "suggestedTarget": "Images",
      "actualTarget": "PhotoUrls",
      "context": "ecommerce"
    }
  ]
}
```

### New Commands
```bash
docflow integrate confirm <mapping-id>    # Confirm a suggested mapping
docflow integrate reject <mapping-id>     # Reject and correct a mapping
docflow integrate learn --export          # Export learned patterns
docflow integrate learn --import <file>   # Import patterns from team
```

### Benefits
- Confidence improves with usage
- Team knowledge sharing via exported patterns
- Domain-specific learning (ecommerce, healthcare, finance)
- Reduces manual review over time

### Priority: **High**
### Effort: **High** (4-6 weeks)

---

## 3. SLA Monitoring & Alerting

### Current State (v1.0)
- One-time SLA validation via `integrate sla` command
- Returns verdict (COMPLIANT/VIOLATION)
- Exit codes for CI/CD integration
- No historical tracking

### Proposed Enhancement
Add continuous monitoring and alerting capabilities:

```bash
# Scheduled monitoring
docflow integrate sla <url> --monitor --interval 5m --alert-webhook <url>

# Historical analysis
docflow integrate sla-history --api <name> --since 7d

# Dashboard export
docflow integrate sla-report --format html --output sla-dashboard.html
```

### Features
| Feature | Description |
|---------|-------------|
| **Scheduled Monitoring** | Run SLA checks on interval (cron or daemon) |
| **Alerting** | Webhook notifications on violations |
| **Historical Storage** | Track SLA compliance over time |
| **Trend Analysis** | Detect degradation before SLA breach |
| **Reporting** | Generate compliance reports for vendors |

### Data Model
```json
// .docflow/sla-history.json
{
  "endpoints": {
    "petstore-inventory": {
      "url": "https://petstore.example.com/api/v1/pets",
      "expectedFreshness": "30s",
      "samples": [
        {
          "timestamp": "2026-01-05T18:00:00Z",
          "dataAge": "2s",
          "verdict": "COMPLIANT"
        }
      ],
      "complianceRate": 98.5,
      "averageDataAge": "1s"
    }
  }
}
```

### Priority: **Medium**
### Effort: **Medium** (2-3 weeks)

---

## 4. API Specification Quality Analysis

### Current State (v1.0)
- Parses OpenAPI specs
- Displays entities and properties
- Shows "Required" column from spec

### Proposed Enhancement
Add spec quality scoring and improvement suggestions:

```bash
docflow integrate lint <spec> --output report.md
```

### Quality Checks
| Check | Description | Severity |
|-------|-------------|----------|
| Missing `required` array | Properties should declare requirements | Warning |
| Missing descriptions | Endpoints/schemas lack documentation | Info |
| Inconsistent naming | `userId` vs `user_id` vs `UserID` | Warning |
| Missing examples | No example values in schema | Info |
| Missing error schemas | No 4xx/5xx response definitions | Warning |
| Missing authentication | No security schemes defined | Error |
| Unused schemas | Defined but never referenced | Info |
| Missing pagination | List endpoints without paging | Warning |

### Output Example
```
╭─API Quality Report: petstore.json──────────────────────╮
│ Overall Score: 72/100 (Acceptable)                     │
├────────────────────────────────────────────────────────┤
│ ⚠️  WARN: Pet schema has no required fields            │
│ ⚠️  WARN: Missing error response schemas               │
│ ℹ️  INFO: 4 properties lack descriptions               │
│ ℹ️  INFO: No example values provided                   │
╰────────────────────────────────────────────────────────╯
```

### Priority: **Medium**
### Effort: **Low** (1 week)

---

## 5. Bidirectional Sync (Diagram ↔ Code)

### Current State (v1.0)
- C# → Mermaid (diagram command)
- Mermaid → C# (codegen command)
- Whiteboard → Mermaid (scan command)
- Each direction is independent

### Proposed Enhancement
Implement true bidirectional sync with change detection:

```bash
# Watch mode - sync changes in either direction
docflow sync --watch ./Domain ./Diagrams

# Diff detection
docflow diff Domain.cs Domain.mmd --show-changes

# Merge changes
docflow merge --code Domain.cs --diagram Domain.mmd --resolve interactive
```

### Features
| Feature | Description |
|---------|-------------|
| **Watch Mode** | Auto-sync on file changes |
| **Change Detection** | Identify what changed and where |
| **Conflict Resolution** | Interactive merge for conflicts |
| **Selective Sync** | Sync specific entities only |

### Challenges
- Detecting "source of truth" when both changed
- Preserving code that has no diagram equivalent (method bodies)
- Handling renames vs. delete+add

### Priority: **Low**
### Effort: **High** (6-8 weeks)

---

## 6. Multi-Format Code Generation

### Current State (v1.0)
- Generates C# only
- Single file output
- Basic property generation

### Proposed Enhancement
Support multiple output formats and richer generation:

```bash
docflow codegen diagram.mmd --language typescript --output ./models
docflow codegen diagram.mmd --language python --output ./models
docflow codegen diagram.mmd --language java --output ./models
docflow codegen diagram.mmd --language csharp --style record --output ./models
```

### Language Support
| Language | Status | Features |
|----------|--------|----------|
| C# | ✅ v1.0 | Classes, inheritance, attributes |
| C# Records | 🔲 v2.0 | Immutable records |
| TypeScript | 🔲 v2.0 | Interfaces, types |
| Python | 🔲 v2.0 | Dataclasses, Pydantic |
| Java | 🔲 v2.0 | Classes, records |
| Kotlin | 🔲 v2.0 | Data classes |
| Go | 🔲 v2.0 | Structs |

### Priority: **Low**
### Effort: **Medium** (2-3 weeks per language)

---

## 7. IDE Extensions

### Current State (v1.0)
- CLI only
- No IDE integration

### Proposed Enhancement
Build extensions for popular IDEs:

| IDE | Extension Features |
|-----|-------------------|
| **VS Code** | Mermaid preview, codegen on save, diagram explorer |
| **Visual Studio** | Solution integration, right-click codegen |
| **JetBrains Rider** | Same as VS Code |

### VS Code Extension Features
- Side-by-side Mermaid preview
- "Generate Code" code lens on .mmd files
- "Generate Diagram" code lens on .cs files
- Problem panel integration for validation errors
- Snippet support for Mermaid class diagrams

### Priority: **Low**
### Effort: **High** (4-6 weeks per IDE)

---

## 8. Documentation Improvements

### Current State (v1.0)
- Basic README
- Inline code comments
- No comprehensive docs

### Required Documentation
| Document | Description | Status |
|----------|-------------|--------|
| README.md | Hero section, quick start, feature highlights | 🔲 Rewrite |
| CLAUDE.md | AI context file for project | 🔲 Update |
| docs/ARCHITECTURE.md | CSM pattern, transformation pipeline | 🔲 Create |
| docs/CLI-REFERENCE.md | Complete command reference | 🔲 Create |
| docs/CHANGELOG.md | Release notes | 🔲 Create |
| samples/README.md | Sample file documentation | 🔲 Create |

### Priority: **High**
### Effort: **Low** (3-5 days)

---

## 9. Test Coverage Improvements

### Current State (v0.1.0-preview)
- 72 tests passing (20 CodeAnalysis + 52 Diagrams)
- Core functionality covered
- Some edge cases missing

### Proposed Improvements
| Area | Current | Target |
|------|---------|--------|
| Unit Tests | 91 | 150+ |
| Integration Tests | ~10 | 30+ |
| E2E Tests | 0 | 10+ |
| Code Coverage | Unknown | 80%+ |

### Priority: **Medium**
### Effort: **Medium** (ongoing)

---

## 10. Performance Optimizations

### Current State (v1.0)
- Acceptable for small/medium specs
- No benchmarking
- No caching

### Proposed Improvements
| Optimization | Benefit |
|--------------|---------|
| Parallel parsing | Faster multi-file processing |
| Lazy loading | Reduced memory for large specs |
| Spec caching | Don't re-parse unchanged files |
| Incremental codegen | Only regenerate changed entities |

### Priority: **Low**
### Effort: **Medium** (2-3 weeks)

---

## Implementation Phases

### Phase 2.0 (Q1 2026)
1. ✅ Documentation improvements
2. ✅ API Specification Quality Analysis
3. ✅ Stateful Project Model (basic)

### Phase 2.1 (Q2 2026)
1. ✅ IMS Learning System (basic)
2. ✅ SLA Monitoring & Alerting
3. ✅ Test coverage improvements

### Phase 2.2 (Q3 2026)
1. ✅ TypeScript code generation
2. ✅ VS Code extension (basic)
3. ✅ Bidirectional sync (experimental)

### Phase 3.0 (Q4 2026)
1. ✅ Full IMS with team sharing
2. ✅ Multi-language code generation
3. ✅ IDE extensions (full)

---

## Contributing

These enhancements are open for contribution. To propose changes:

1. Open an issue describing the enhancement
2. Reference this document section
3. Include use cases and acceptance criteria
4. Submit PR with implementation

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-01-05 | Kurt Mitchell | Initial document |
