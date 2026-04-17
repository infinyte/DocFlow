# DocFlow TODO

Tracks the feature plan for **API Spec → Design Documentation & Diagrams** (see
[docs/design/documentation-module.md](design/documentation-module.md) for the full design doc).

## Status

| Phase | Issues | Status |
|-------|--------|--------|
| 1 — Foundations (MVP Markdown) | #1–#6 | **Done** |
| 2 — Rich Diagrams | #7, #8 | **Done** |
| 3 — Content Depth | #9 | **Done** |
| 4 — HTML Rendering | #10 | **Done** |
| 5 — Pluggability & Polish | #11, #12 | **Done** |

**Test suite:** 152 passing (1 skipped) across 6 test projects. 80 tests added since
pre-feature baseline; all 72 pre-existing tests still pass.

**Feature-level Definition of Done:** all 12 issues merged. Running
`docflow integrate docs samples/integration-demos/petstore.json -o ./out` produces the
navigable Markdown bundle; `--format html` produces the parallel static HTML site; `--watch`
regenerates on spec change; `docflow integrate diff old.json new.json -o changelog.md`
produces a breaking / non-breaking classified Markdown changelog.

## Phase 1 — Foundations (Done)

- [x] **#1 Extend `SemanticModel` with `ApiSurface`** — new records in
  `src/DocFlow.Core/CanonicalModel/ApiSurface.cs`; optional `Api` property on `SemanticModel`;
  6 new tests in `DocFlow.Core.Tests`.
- [x] **#2 Populate `ApiSurface` in `OpenApiParser`** — walks paths/operations/params/
  requestBody/responses; resolves `$ref` to entity names; populates servers, tags, security
  schemes with OAuth2 flows; synthesizes deterministic `{method}_{path}` operationIds. 6 new
  tests in `DocFlow.Integration.Tests` including YAML/JSON equivalence.
- [x] **#3 Scaffold `DocFlow.Documentation` project** — net8.0, references Core + Diagrams only
  (no Integration dependency). `IDocumentationGenerator`, `GeneratedFile`,
  `DocumentationOptions`, `DocumentationFormat`, `DiagramKinds` (flags), `GroupBy`.
- [x] **#4 Implement `MarkdownDocumentationGenerator` (MVP)** — four section builders
  (Overview, DomainModel, Endpoint, Index); deterministic ordering; `MarkdownWriter` enforces
  LF line endings and trims trailing whitespace. 11 tests in `DocFlow.Documentation.Tests`
  including a `Verify.Xunit` snapshot for `endpoints/pet.md`.
- [x] **#5 Add `docflow integrate docs` CLI subcommand** — all flags wired
  (`--format`, `--diagrams`, `--with-examples`, `--group-by`, `--title`, `-v`); exit codes
  0/1/2 for success/validation/IO; 5 new tests in `DocFlow.CLI.Tests`.
- [x] **#6 Copy source spec to assets; ship design doc** — `DocumentationOptions.SourceSpec`
  plumbs the raw spec through the generator; `docs/design/documentation-module.md` committed;
  `README.md` + `CLAUDE.md` updated. 2 new CLI tests covering JSON byte-identical copy and
  YAML preservation.

## Phase 2 — Rich Diagrams (Done)

- [x] **#7 ER and Sequence Mermaid generators** — `MermaidErDiagramGenerator` emits
  `erDiagram` with cardinality mapped from `RelationshipType` (Composition→`||--o{`,
  Aggregation→`}o--o{`, Association→`}o--||`); non-structural relationships are dropped and
  orphan entities render as standalone blocks. `MermaidSequenceDiagramGenerator` takes an
  `ApiOperation` and emits Client/API actors plus optional Auth when security requirements are
  present; request/response messages include method, path, request-body entity, and the first
  2xx response. `DomainModelSectionBuilder` appends the ER fence after the class fence when
  `DiagramKinds.Er` is set; `EndpointSectionBuilder` embeds a sequence fence per operation and
  emits standalone `sequences/<operationId>.md` pages when `DiagramKinds.Sequence` is set. CLI
  default `--diagrams` expanded to `class,er,sequence`. 10 new tests in `DocFlow.Diagrams.Tests`
  (5 ER + 5 Sequence) and 1 new integration test in `DocFlow.Documentation.Tests`.
- [x] **#8 C4 Context and Endpoint Flowchart generators** — `MermaidC4ContextGenerator` uses a
  `flowchart LR` fallback (Mermaid's dedicated C4 primitive is still experimental) with
  Client, API container, per-server deployment nodes, and OAuth/OpenID IdP nodes; rendered
  into a new `architecture.md` alongside a standalone `diagrams/context.mmd`.
  `MermaidEndpointFlowchartGenerator` produces `Request → Validate → [Authorize] → Handler →
  Response` with dashed branches from `Handler` to non-2xx responses and solid edges to 2xx.
  `ArchitectureSectionBuilder` emits when `DiagramKinds.Context` is set; `EndpointSectionBuilder`
  embeds the flowchart when `DiagramKinds.Flow` is set. CLI default `--diagrams` bumped to
  `all`. 8 new tests in `DocFlow.Diagrams.Tests` (4 Context + 4 Flowchart) and 2 new integration
  tests in `DocFlow.Documentation.Tests`.

## Phase 3 — Content Depth (Done)

- [x] **#9 Example synthesis and enriched security section** — `ExampleSynthesizer` (in
  `DocFlow.Documentation/Examples/`) produces JSON from `ApiMediaType` + the entity catalogue:
  prefers `ApiMediaType.Example` (captured by the OpenAPI parser via `OpenApiJsonWriter`),
  otherwise synthesises from the schema — enum[0] / ISO-8601 date-time / UUID placeholders /
  single-element arrays / required-respecting objects / `"..."` on cycles. `--with-examples`
  adds `### Example Request/Response` blocks to endpoint pages. New `SecuritySectionBuilder`
  emits `security.md` when the spec declares `securitySchemes` or any operation references one:
  a scheme-details table, a Mermaid `sequenceDiagram` per OAuth2 flow (authorizationCode,
  clientCredentials, implicit, password), and a per-operation requirements cross-reference.
  Entity references on endpoint pages render as links into stable
  `domain-model.md#entity-<kebab>` anchors that `DomainModelSectionBuilder` injects inside the
  entity-table cells. `ApiMediaType.Example` narrowed from `object?` to `string?`. 9 new tests
  (5 Examples + 3 Security + 1 CrossLinks).

## Phase 4 — HTML Rendering (Done)

- [x] **#10 Static HTML site renderer** — `DocFlow.Documentation` now depends on
  `Markdig` 0.34.0; `StaticSiteRenderer` converts the Markdown bundle into parallel `.html`
  files while preserving `.mmd` / `.json` assets. Markdig's advanced-diagrams extension emits
  mermaid fences as `<div class="mermaid">` so Mermaid.js auto-initialises on load; a compiled
  regex rewrites intra-bundle `.md` hrefs to `.html` and preserves `#fragment` tails. The
  sidebar nav is built from the file tree with `.active` highlighting for the current page.
  `Html/Assets/theme.css` ships as an embedded resource (dark/light via `prefers-color-scheme`)
  and is emitted alongside HTML as `assets/theme.css`. Mermaid.js loads from
  `cdn.jsdelivr.net/npm/mermaid@10`. CLI `--format html` now runs the renderer (replacing the
  Phase 1 error). 5 new tests in `DocFlow.Documentation.Tests/Html/` (1 skipped — offline-asset
  packaging follow-up); `Cli_Docs_HtmlFlag_Phase1_ReturnsError` replaced by
  `Cli_Docs_HtmlFlag_WritesHtmlBundle`.

## Phase 5 — Pluggability & Polish (Done)

- [x] **#11 `IApiSpecParser` abstraction + registry** — `IApiSpecParser` in
  `DocFlow.Core/Abstractions/` (`Name`, `CanParse(path, content)`, `ParseAsync(Stream, ct) →
  SemanticModel`). `SpecParserRegistry` picks the first parser whose `CanParse` returns true
  and otherwise throws `InvalidOperationException` with a "registered parsers: …" message.
  `OpenApiParser` implements the interface via explicit implementation, so the legacy
  `ISchemaParser` entry point is untouched and `ParseSchemaAsync(ParserInput)` continues to
  work. CLI `integrate docs` now goes through the registry rather than hardcoding OpenAPI.
  7 new tests in `DocFlow.Integration.Tests/Schemas/` covering JSON/YAML selection,
  content-sniff fallback, missing-parser diagnostic, a `StubGraphQlParser` proving one-file
  extensibility, and a regression test confirming the legacy entry point still produces an
  equivalent `SemanticModel`.
- [x] **#12 Watch mode and spec-diff changelog** — `integrate docs --watch` wires a
  `FileSystemWatcher` with a 300 ms debounce (`SemaphoreSlim`-guarded so bursts of `Changed`
  events collapse into a single regen) and prints Spectre.Console status lines on each
  refresh. New `integrate diff <old> <new> -o <file>` subcommand: loads both specs via the
  registry, runs `SpecDiffer` (in `DocFlow.Documentation/Diff/`), and emits
  `ChangelogGenerator` Markdown with a Breaking / Non-breaking summary table and sections
  grouped by severity → category (Operation / Parameter / RequestBody / Response / Schema /
  Security). Diff heuristics: removed / required-tighter / type change = breaking; added
  optional / relaxed required / added response status / added new schema = non-breaking.
  7 new `SpecDifferTests` in `DocFlow.Documentation.Tests/Diff/` (all six scenarios from the
  issue plus a required-flag flip-direction check) and 1 end-to-end watch test in
  `DocFlow.CLI.Tests/Integrate/WatchModeTests.cs` that polls for mtime updates within a 10 s
  budget.

## Cross-Cutting Requirements (Ongoing)

- Zero new compiler warnings.
- xUnit only; deterministic tests; no network or time-based assertions.
- `\n` line endings in all generated output; no timestamps/user names/absolute paths.
- Every issue that ships user-visible CLI behavior updates `README.md` and `CLAUDE.md`.
