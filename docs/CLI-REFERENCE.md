# CLI Reference

Complete documentation for the DocFlow command-line interface.

## Installation

```bash
# From source
dotnet run --project src/DocFlow.CLI -- <command> [options]

# As installed tool
docflow <command> [options]
```

## Global Options

These options are available on all commands:

| Option | Alias | Description |
|--------|-------|-------------|
| `--verbose` | `-v` | Show detailed output including entity tables and full generated content |
| `--quiet` | `-q` | Minimal output, only errors. Useful for scripting |
| `--help` | `-h`, `-?` | Show help and usage information |
| `--version` | | Show version information |

---

## Commands

### diagram

Generate a Mermaid class diagram from C# source code.

**Usage:**
```bash
docflow diagram <input> [options]
docflow d <input> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<input>` | C# source file or directory to parse |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--output <file>` | `-o` | Output file path (default: same name with .mmd extension) |
| `--recursive` | `-r` | Process all .cs files in directory recursively |
| `--no-relationships` | | Exclude relationship lines from diagram |

**Examples:**

```bash
# Single file
docflow diagram Domain/Order.cs

# Directory (non-recursive)
docflow diagram Domain/

# Directory (recursive) with custom output
docflow diagram src/Domain -r -o architecture.mmd

# Exclude relationships
docflow diagram Order.cs --no-relationships

# Verbose output
docflow diagram Order.cs -v
```

**Output:**

- Creates a `.mmd` file with valid Mermaid classDiagram syntax
- Displays entity count and relationship count
- Shows generated Mermaid content (unless `--quiet`)

---

### codegen

Generate C# code from a Mermaid class diagram.

**Usage:**
```bash
docflow codegen <input> [options]
docflow c <input> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<input>` | Mermaid diagram file (.mmd) to parse |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--output <file>` | `-o` | Output file path (default: same name with .cs extension) |
| `--namespace <name>` | `-n` | Namespace for generated code (default: derived from filename) |
| `--style <style>` | | Code style: `ddd` (default) or `poco` |

**Styles:**

| Style | Description |
|-------|-------------|
| `ddd` | Domain-Driven Design style with aggregates, value objects as records |
| `poco` | Plain Old CLR Objects - simple classes without DDD patterns |

**Examples:**

```bash
# Basic generation
docflow codegen domain.mmd

# Custom namespace
docflow codegen domain.mmd -n MyApp.Domain.Models

# POCO style
docflow codegen domain.mmd --style poco

# Custom output location
docflow codegen diagram.mmd -o src/Models/Generated.cs
```

**Output:**

- Creates a `.cs` file with nullable-enabled C# 12 code
- Records for ValueObjects, classes for Entities
- Proper access modifiers and XML documentation

---

### roundtrip

Perform a full round-trip transformation: C# -> Mermaid -> C#

**Usage:**
```bash
docflow roundtrip <input> [options]
docflow r <input> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<input>` | C# source file to round-trip |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--output <dir>` | `-o` | Output directory for generated files |
| `--compare` | | Show semantic diff between original and generated |

**Examples:**

```bash
# Basic round-trip
docflow roundtrip Domain/Order.cs

# With comparison
docflow roundtrip Order.cs --compare

# Custom output directory
docflow roundtrip Order.cs -o ./generated

# Verbose with comparison
docflow roundtrip Order.cs --compare -v
```

**Output Files:**

| File | Description |
|------|-------------|
| `<name>.mmd` | Intermediate Mermaid diagram |
| `<name>.generated.cs` | Round-tripped C# code |

**Comparison Output:**

When using `--compare`, displays:
- Entity count comparison (original vs round-trip)
- Relationship count comparison
- Entity classification breakdown (with `--verbose`)

---

### scan

Scan a whiteboard or diagram image using AI vision.

**Usage:**
```bash
docflow scan <image> [options]
docflow s <image> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<image>` | Whiteboard or diagram image to scan (PNG, JPG, WEBP, GIF) |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--output <file>` | `-o` | Output file path for extracted Mermaid diagram |
| `--context <hint>` | `-c` | Context hint for better interpretation |

**Supported Formats:**

- `.jpg`, `.jpeg`
- `.png`
- `.gif`
- `.webp`

**Examples:**

```bash
# Basic scan
docflow scan whiteboard.jpg

# With context hint
docflow scan meeting-notes.png -c "e-commerce domain model"

# Custom output
docflow scan diagram.jpg -o extracted.mmd

# Verbose output
docflow scan whiteboard.jpg -v
```

**Configuration Required:**

The scan command requires a Claude API key. Configure using one of:

1. **Environment variable:**
   ```bash
   export ANTHROPIC_API_KEY='sk-ant-...'
   ```

2. **User config:** `~/.docflow/config.json`
   ```json
   { "anthropicApiKey": "sk-ant-..." }
   ```

3. **Project config:** `./docflow.json`
   ```json
   { "anthropicApiKey": "sk-ant-..." }
   ```

**Output:**

- Diagram type with confidence score
- Entity count and relationship count
- Analysis time
- Generated Mermaid diagram
- Saves `.mmd` file

---

### integrate

API integration commands for parsing OpenAPI specs, CDM mapping, SLA validation, and code generation.

#### integrate parse

Parse an OpenAPI specification and display a summary of its entities and endpoints.

**Usage:**
```bash
docflow integrate parse <spec> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<spec>` | OpenAPI specification file (JSON or YAML) |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--verbose` | `-v` | Show property-level details for each entity |

**Examples:**

```bash
# Show summary
docflow integrate parse petstore.json

# Show entity property details
docflow integrate parse api.json -v
```

**Output:**

- API name and version
- Entity table (name, property count, type)
- Endpoint table (method, path, operation ID)
- Per-entity property details (with `-v`)

---

#### integrate analyze

Analyze mapping between external API and your Canonical Data Model.

**Usage:**
```bash
docflow integrate analyze <spec> --cdm <path> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<spec>` | OpenAPI specification file |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--cdm <path>` | | Path to CDM C# files (required) |
| `--output <file>` | `-o` | Save mapping report as JSON |
| `--threshold <percent>` | | Filter by minimum confidence (0-100) |
| `--verbose` | `-v` | Show field-level mappings |

**Examples:**

```bash
# Basic analysis
docflow integrate analyze petstore.json --cdm Models/Entities.cs

# With threshold filter
docflow integrate analyze api.json --cdm Domain/ --threshold 70

# Verbose with JSON output
docflow integrate analyze api.json --cdm Domain/ -v -o report.json
```

**Output:**

- Overall mapping confidence
- Entity mapping table with confidence scores
- Color-coded confidence levels:
  - Green (90-100%): High confidence
  - Yellow (70-89%): Medium confidence
  - Red (0-69%): Low confidence - needs review
- Field-level mappings with reasoning (with `-v`)

---

#### integrate sla

Validate SLA compliance for data freshness.

**Usage:**
```bash
docflow integrate sla <url> --expected <duration> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<url>` | API endpoint URL to validate |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--expected <duration>` | | Expected maximum data age (required) |
| `--samples <count>` | | Number of samples to collect (default: 10) |
| `--interval <duration>` | | Time between samples (default: 5s) |
| `--timestamp-path <path>` | | JSON path to timestamp field |
| `--header <key=value>` | | Add HTTP header (repeatable) |
| `--output <file>` | `-o` | Save report as JSON |
| `--verbose` | `-v` | Show individual samples |

**Duration Format:**

- `500ms` - milliseconds
- `30s` - seconds
- `5m` - minutes
- `1h` - hours

**Examples:**

```bash
# Basic SLA check
docflow integrate sla https://api.example.com/data --expected 30s

# With custom samples and interval
docflow integrate sla https://api.example.com/status --expected 1m --samples 20 --interval 10s

# With authentication header
docflow integrate sla https://api.example.com/data --expected 30s --header "Authorization=Bearer token123"

# Save report
docflow integrate sla https://api.example.com/data --expected 30s -o sla-report.json -v
```

**Output:**

- Live progress bar during sampling
- Verdict with color coding:
  - COMPLIANT (green): 100% samples within SLA
  - MARGINALLY COMPLIANT (yellow): 90-99% within SLA
  - MINOR VIOLATION (orange): 50-89% within SLA
  - SEVERE VIOLATION (red): <50% within SLA
- Statistics: expected, average, min, max ages
- Individual sample details (with `-v`)

**Exit Codes:**

| Code | Description |
|------|-------------|
| 0 | COMPLIANT or MARGINALLY COMPLIANT |
| 2 | MINOR VIOLATION or SEVERE VIOLATION |

---

#### integrate generate

Generate integration code from OpenAPI spec and CDM mappings.

**Usage:**
```bash
docflow integrate generate <spec> --cdm <path> --output <dir> [options]
```

**Arguments:**

| Argument | Description |
|----------|-------------|
| `<spec>` | OpenAPI specification file |

**Options:**

| Option | Alias | Description |
|--------|-------|-------------|
| `--cdm <path>` | | Path to CDM C# files (required) |
| `--output <dir>` | `-o` | Output directory (required) |
| `--namespace <name>` | `-n` | Namespace for generated code (default: Integration.Generated) |
| `--generate <types>` | | What to generate (default: all) |
| `--verbose` | `-v` | Show generation details |

**Generate Types:**

| Type | Description |
|------|-------------|
| `dtos` | External DTOs with `[JsonPropertyName]` attributes |
| `mappers` | AutoMapper profiles with confidence comments |
| `client` | Typed HTTP client interface |
| `validators` | FluentValidation validators |
| `all` | All of the above (default) |

**Examples:**

```bash
# Generate all artifacts
docflow integrate generate petstore.json --cdm Models/ -o Generated/

# Custom namespace
docflow integrate generate api.json --cdm Domain/ -o src/Integration -n MyApp.Integration

# Generate only DTOs and mappers
docflow integrate generate api.json --cdm Domain/ -o Generated/ --generate dtos,mappers

# Verbose output
docflow integrate generate api.json --cdm Domain/ -o Generated/ -v
```

**Generated Files:**

| Directory | Contents |
|-----------|----------|
| `External/` | DTO classes with JSON attributes |
| `Mapping/` | AutoMapper profile with confidence comments |
| `Client/` | HTTP client interface |
| `Validation/` | FluentValidation validators |

**Output:**

- Table of generated files with line counts
- Count of mappings needing manual review
- Total files and lines generated

---

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | Error (file not found, parse error, generation error, missing config) |
| 2 | SLA Violation (for `integrate sla` command) |

---

## Examples

### Full Workflow: Whiteboard to Code

```bash
# 1. Start with a whiteboard photo
docflow scan whiteboard.jpg -o domain.mmd -c "order management"

# 2. Generate C# from the extracted diagram
docflow codegen domain.mmd -n MyApp.Domain -o Models.cs

# 3. Later, update the C# and regenerate the diagram
docflow diagram Models.cs -o domain-updated.mmd

# 4. Verify round-trip preservation
docflow roundtrip Models.cs --compare -v
```

### Full Workflow: API Integration

```bash
# 1. Parse the OpenAPI spec
docflow integrate parse vendor-api.json -v

# 2. Analyze mapping to your CDM
docflow integrate analyze vendor-api.json --cdm src/Domain/Entities.cs --threshold 70 -v

# 3. Validate SLA compliance
docflow integrate sla https://vendor-api.com/data --expected 30s --samples 20

# 4. Generate integration code
docflow integrate generate vendor-api.json --cdm src/Domain/ -o src/Integration -n MyApp.Integration.Vendor
```

### Scripting

```bash
# Quiet mode for scripts
OUTPUT=$(docflow diagram src/Domain -r -q)
echo "Generated: $OUTPUT"

# Check exit code
if docflow codegen diagram.mmd -q; then
    echo "Success"
else
    echo "Failed"
fi

# SLA monitoring in CI/CD
if docflow integrate sla https://api.example.com/health --expected 5s --samples 5; then
    echo "SLA: PASS"
else
    echo "SLA: FAIL"
    exit 1
fi
```

### Batch Processing

```bash
# Process all C# files in a directory
for file in src/Domain/*.cs; do
    docflow diagram "$file" -q
done

# Combine into single diagram
docflow diagram src/Domain -r -o full-model.mmd
```

---

## Tips

1. **Use context hints** with `scan` for better accuracy on domain-specific diagrams
2. **Use verbose mode** (`-v`) when debugging transformation issues
3. **Use quiet mode** (`-q`) in CI/CD pipelines
4. **Use `--compare`** with roundtrip to verify semantic preservation
5. **Set up user config** (`~/.docflow/config.json`) to avoid repeating API key setup
6. **Use threshold filtering** with `integrate analyze` to focus on low-confidence mappings
7. **Review TODO comments** in generated AutoMapper profiles for manual mapping needs
8. **Run SLA validation** before production deployments to catch data freshness issues
