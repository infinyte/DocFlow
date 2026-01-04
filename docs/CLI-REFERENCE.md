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

Perform a full round-trip transformation: C# → Mermaid → C#

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

## Exit Codes

| Code | Description |
|------|-------------|
| 0 | Success |
| 1 | Error (file not found, parse error, generation error, missing config) |

---

## Examples

### Full Workflow

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
