# DocFlow.Documentation Module - Design Document

## Status: Phase 1 Complete (v0.1.0-preview)

`DocFlow.Documentation` turns any DocFlow `SemanticModel` into a navigable design-documentation
bundle. Phase 1 ships the Markdown MVP driven by the OpenAPI parser; later phases add richer
diagrams, synthesized examples, static HTML rendering, and spec-to-spec diffing.

| Phase | Scope | Status |
|-------|-------|--------|
| 1 | Markdown bundle: overview, domain model (class diagram), endpoint pages, TOC, source-spec asset | **Complete** |
| 2 | Additional diagram kinds: ER, per-operation sequence, C4 context, endpoint flowchart | Planned |
| 3 | Example payload synthesis, enriched security section, cross-page linking | Planned |
| 4 | `--format html` static site renderer | Planned |
| 5 | `IApiSpecParser` abstraction + registry; `--watch` + `integrate diff` | Planned |

---

## Overview

```
Source spec (OpenAPI 3.x JSON/YAML)
   -> OpenApiParser -> SemanticModel { Entities, Relationships, ApiSurface }
      -> MarkdownDocumentationGenerator
         -> IReadOnlyList<GeneratedFile>
            -> (CLI) written to <output-dir>/
```

Nothing bypasses the canonical model. The generator is pure: it returns files in memory;
persistence is the CLI's responsibility.

### Output bundle (Phase 1)

```
<output-dir>/
├── index.md                  # TOC linking every other page
├── overview.md               # Title, version, description, servers, auth summary
├── domain-model.md           # Mermaid class diagram + entity table
├── endpoints/
│   └── <tag>.md              # One page per tag (or first path segment with --group-by path)
└── assets/
    └── openapi.json          # Byte-equivalent copy of the source spec (.yaml/.yml preserved)
```

---

## CLI

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

Exit codes: `0` success, `1` validation error, `2` I/O error.

Phase 1: only `--format markdown` is implemented. `--format html` returns exit 1 with a pointer
to the Phase 4 tracking issue. `--with-examples` is accepted but no-op until Phase 3.

---

## Architecture

### Canonical extension (Phase 1 Issue #1)

`SemanticModel` gained an optional `Api: ApiSurface` property carrying operations, parameters,
request/response bodies, servers, tags, and security schemes. Records:

- `ApiSurface` — root container (title, version, description, servers, operations, tags, security schemes)
- `ApiOperation` — `{ OperationId, Method, Path, Summary, Description, Tags, Parameters, RequestBody, Responses, SecurityRequirements, Deprecated }`
- `ApiParameter` / `ApiRequestBody` / `ApiResponse` / `ApiMediaType` / `ApiSchema`
- `ApiServer`, `ApiTag`, `ApiSecurityScheme`, `ApiSecurityFlow`, `ApiSecurityRequirement`

Enums: `ApiHttpMethod`, `ApiParameterLocation` (Query, Header, Path, Cookie), `ApiSecuritySchemeType`.

### Populating the surface (Phase 1 Issue #2)

`DocFlow.Integration.Schemas.OpenApi.OpenApiParser.BuildApiSurface` walks
`paths → operations → parameters/requestBody/responses`, resolves `$ref` to known schema names,
and fills in servers, tags, security schemes (including OAuth2 flows). Operations without an
`operationId` get a deterministic synthesized id of the form `{method}_{path}` lowercased with
non-alphanumerics collapsed to single underscores.

### Section builders (Phase 1 Issue #4)

`MarkdownDocumentationGenerator` orchestrates four stateless builders:

| Builder | Output |
|---------|--------|
| `OverviewSectionBuilder` | `overview.md` |
| `DomainModelSectionBuilder` | `domain-model.md` (embeds `MermaidClassDiagramGenerator` output in a `mermaid` fence) |
| `EndpointSectionBuilder` | `endpoints/<slug>.md` per tag (`--group-by tag`) or per first path segment (`--group-by path`) |
| `IndexSectionBuilder` | `index.md`, built last so it can link every sibling |

The generator sorts entities into a deterministic order before invoking the upstream class
diagram generator so Mermaid output is stable across runs. All iteration is explicitly ordered;
`MarkdownWriter` normalises line endings to LF and trims trailing whitespace so output lints cleanly
and snapshot tests are stable.

### CLI wiring (Phase 1 Issue #5)

`Program.ExecuteDocsCommand` parses the spec, runs the generator, then writes every
`GeneratedFile` to disk (creating subdirectories as needed). It maps `IOException`,
`UnauthorizedAccessException`, and `NotSupportedException` to exit code 2; parser/validation
failures to exit code 1.

### Source spec preservation (Phase 1 Issue #6)

The CLI reads the original spec and passes it to the generator via
`DocumentationOptions.SourceSpec`. The generator emits it unmodified as `assets/openapi.json`
(or `assets/openapi.yaml` / `.yml` when the source was YAML). Content preservation lets readers
verify the docs against the authoritative source without leaving the bundle.

---

## Testing

| Project | Tests |
|---------|-------|
| `DocFlow.Core.Tests` | ApiSurface records, enum coverage, backwards-compatibility |
| `DocFlow.Integration.Tests` | OpenAPI → ApiSurface, YAML/JSON equivalence, OAuth flows, deterministic operationId |
| `DocFlow.Documentation.Tests` | Generator file set, Mermaid embedding, determinism, snapshot via Verify.Xunit |
| `DocFlow.CLI.Tests` | End-to-end CLI invocation, exit codes, verbose output |

Determinism rules (§4.3 of the feature plan): every iteration is `OrderBy`-preceded; line
endings are LF; generated output contains no timestamps, user names, or absolute paths.

---

## Key Files

| File | Purpose |
|------|---------|
| `src/DocFlow.Core/CanonicalModel/ApiSurface.cs` | Canonical API-surface records |
| `src/DocFlow.Integration/Schemas/OpenApi/OpenApiParser.cs` | Populates `SemanticModel.Api` |
| `src/DocFlow.Documentation/Abstractions/IDocumentationGenerator.cs` | Generator contract |
| `src/DocFlow.Documentation/Markdown/MarkdownDocumentationGenerator.cs` | Phase 1 generator |
| `src/DocFlow.Documentation/Markdown/Sections/*SectionBuilder.cs` | Overview / DomainModel / Endpoint / Index builders |
| `src/DocFlow.Documentation/Options/DocumentationOptions.cs` | Generator options |
| `src/DocFlow.CLI/Program.cs` (`BuildDocsCommand`) | `integrate docs` subcommand |
