# Whiteboard Scanner Demo

Test the DocFlow AI-powered whiteboard scanning feature with your own images.

## Prerequisites

### 1. Build DocFlow

```bash
cd /path/to/DocFlow
dotnet build
```

### 2. Configure Claude API Key

The scanner uses Claude's Vision API. Set up your key using one of these methods:

**Option 1: Environment Variable (Recommended)**
```bash
# Linux/macOS/WSL
export ANTHROPIC_API_KEY='sk-ant-api03-...'

# Windows Command Prompt
set ANTHROPIC_API_KEY=sk-ant-api03-...

# Windows PowerShell
$env:ANTHROPIC_API_KEY='sk-ant-api03-...'
```

**Option 2: User Config File**

Create `~/.docflow/config.json`:
```json
{
  "anthropicApiKey": "sk-ant-api03-..."
}
```

**Option 3: Project Config File**

Create `docflow.json` in your working directory:
```json
{
  "anthropicApiKey": "sk-ant-api03-..."
}
```

Get your API key at: https://console.anthropic.com/

---

## Usage

### Basic Scan

```bash
# Using the CLI directly
dotnet run --project src/DocFlow.CLI -- scan path/to/whiteboard.jpg

# With output file
dotnet run --project src/DocFlow.CLI -- scan whiteboard.jpg -o diagram.mmd

# With context hint for better accuracy
dotnet run --project src/DocFlow.CLI -- scan whiteboard.jpg -c "e-commerce order management"

# Verbose mode
dotnet run --project src/DocFlow.CLI -- scan whiteboard.jpg -v
```

### If Installed Globally

```bash
docflow scan whiteboard.jpg
docflow scan whiteboard.jpg -o output.mmd -c "DDD aggregate design"
```

---

## Supported Image Formats

| Format | Extension |
|--------|-----------|
| JPEG | `.jpg`, `.jpeg` |
| PNG | `.png` |
| GIF | `.gif` |
| WebP | `.webp` |

---

## Tips for Best Results

### Image Quality

1. **Good Lighting**: Ensure the whiteboard is well-lit without glare or shadows
2. **High Contrast**: Use dark markers (black, blue) on white background
3. **Clear Handwriting**: Write legibly with thick marker strokes
4. **Clean Background**: Erase old content, avoid clutter

### Camera Angle

1. **Perpendicular**: Take photos straight-on, not at an angle
2. **Full Frame**: Include all diagram elements in the shot
3. **Stable**: Avoid motion blur - hold camera steady
4. **High Resolution**: Use highest quality camera setting

### Diagram Style

1. **Standard Notation**: Use recognizable UML-style boxes
2. **Clear Labels**: Write class names and property names clearly
3. **Distinct Arrows**: Make relationship lines clear with proper arrowheads
4. **Spacing**: Leave space between elements

---

## Example Diagrams to Draw

### Simple Class Diagram

```
┌────────────────────┐
│     Customer       │
├────────────────────┤
│ - id: int          │
│ - name: string     │
│ - email: string    │
├────────────────────┤
│ + save()           │
│ + validate()       │
└────────────────────┘
         │
         │ places
         ▼
┌────────────────────┐
│      Order         │
├────────────────────┤
│ - orderId: int     │
│ - date: DateTime   │
│ - total: decimal   │
└────────────────────┘
```

### DDD Aggregate

```
    <<Aggregate Root>>
┌────────────────────┐
│   Reservation      │
├────────────────────┤
│ - id               │
│ - status           │
└────────────────────┘
         │
    ┌────┴────┐
    ▼         ▼
┌────────┐  ┌──────────┐
│ Flight │  │ Passenger│
└────────┘  └──────────┘
```

---

## Example Output

```
$ docflow scan whiteboard.jpg -c "order management"

  ____                   _____   _
 |  _ \    ___     ___  |  ___| | |   ___   __      __
 | | | |  / _ \   / __| | |_    | |  / _ \  \ \ /\ / /
 | |_| | | (_) | | (__  |  _|   | | | (_) |  \ V  V /
 |____/   \___/   \___| |_|     |_|  \___/    \_/\_/

v1.0.0-preview - Intelligent Documentation and Modeling Toolkit

Scanning whiteboard image: whiteboard.jpg

╭──────────────────┬───────────────╮
│ Property         │ Value         │
├──────────────────┼───────────────┤
│ Diagram Type     │ ClassDiagram  │
│ Confidence       │ 87%           │
│ Entities Found   │ 4             │
│ Relationships    │ 3             │
│ Analysis Time    │ 2.1s          │
╰──────────────────┴───────────────╯

Detected Entities:
┌────────────┬────────────┬────────────┐
│ Entity     │ Properties │ Operations │
├────────────┼────────────┼────────────┤
│ Customer   │ 3          │ 2          │
│ Order      │ 4          │ 1          │
│ OrderLine  │ 2          │ 0          │
│ Product    │ 3          │ 0          │
└────────────┴────────────┴────────────┘

╭─Generated Mermaid Diagram────────────────────────────────╮
│ classDiagram                                             │
│     class Customer {                                     │
│         -id: int                                         │
│         -name: string                                    │
│         -email: string                                   │
│         +save()                                          │
│         +validate()                                      │
│     }                                                    │
│     class Order {                                        │
│         -orderId: int                                    │
│         -date: DateTime                                  │
│         -status: string                                  │
│         -total: decimal                                  │
│         +calculateTotal()                                │
│     }                                                    │
│     Customer --> Order                                   │
│     Order *-- OrderLine                                  │
│     OrderLine --> Product                                │
╰──────────────────────────────────────────────────────────╯

Success! Diagram saved to whiteboard.mmd
Completed in 2847ms
```

---

## Next Steps

After generating the Mermaid diagram, you can:

```bash
# Generate C# code from the diagram
docflow codegen whiteboard.mmd -o Models.cs -n MyApp.Domain

# Verify with round-trip
docflow roundtrip Models.cs --compare

# View the Mermaid diagram
# Copy content to https://mermaid.live for visualization
```

---

## Troubleshooting

### "Claude API key not configured"

Set one of the configuration options shown above.

### "Unsupported image format"

Use JPG, PNG, GIF, or WEBP formats.

### Poor Recognition Results

- Improve lighting and contrast
- Take a straighter photo
- Use thicker markers
- Add context with `-c` flag
- Ensure all text is legible

### API Errors

- Verify API key is valid
- Check Anthropic account credits
- Ensure internet connectivity
- Check for rate limiting (wait and retry)

### Empty or Minimal Output

- The image may be too small or blurry
- Content may not be recognizable as a diagram
- Try adding a context hint with `-c`
