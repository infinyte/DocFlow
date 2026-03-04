# DocFlow.Integration Module - Design Document

## Status: Complete (v0.1.0-preview)

The Integration module is fully implemented with three CLI subcommands for API integration automation.

**Implemented Features:**
- `docflow integrate analyze` - OpenAPI parsing and CDM mapping analysis with confidence scores
- `docflow integrate sla` - SLA data freshness validation
- `docflow integrate generate` - Code generation (DTOs, AutoMapper, HTTP clients, validators)

---

## Overview

DocFlow.Integration extends the platform from documents/code into **enterprise API integration automation**. It uses the same Canonical Semantic Model pattern to map between external API schemas and internal Canonical Data Models.

## Core Insight

The same architecture that transforms C# <-> Mermaid can transform External API <-> CDM:

```
DOCUMENTS/CODE DOMAIN              API INTEGRATION DOMAIN
----------------------             ---------------------

+-------------+                    +-----------------+
|   C# Code   |                    |  OpenAPI Spec   |
+-------------+                    +-----------------+
|   Mermaid   |                    |  JSON Samples   |
+-------------+                    +-----------------+
|  PlantUML   |                    |  GraphQL Schema |
+------+------+                    +--------+--------+
       |         +----------------+         |
       +-------->|    CANONICAL   |<--------+
                 |  SEMANTIC MODEL|
                 +--------+-------+
                          |
       +------------------+------------------+
       v                  v                  v
+-------------+    +-------------+    +-------------+
|  Generated  |    |  Generated  |    | Integration |
|    Code     |    |  Diagrams   |    |    Code     |
+-------------+    +-------------+    +-------------+
```

The IMS learns patterns in both domains!

---

## Project Structure

```
src/DocFlow.Integration/
+-- Schemas/                    # External API schema parsing
|   +-- OpenApiParser.cs        # OpenAPI 3.x parsing
|   +-- ISchemaParser.cs        # Parser interface
|   +-- SchemaParseResult.cs    # Parse result model
|
+-- Models/                     # Integration-specific models
|   +-- IntegrationSpec.cs      # Complete integration specification
|   +-- ApiEndpoint.cs          # API endpoint and parameter models
|   +-- ExternalSystemInfo.cs   # External system metadata
|
+-- Patterns/                   # IMS pattern definitions
|   +-- ApiMappingPatterns.cs   # Domain-specific mapping patterns
|
+-- Mapping/                    # CDM mapping engine
|   +-- CdmMapper.cs            # Maps external -> CDM with confidence
|   +-- MappingResult.cs        # Mapping result with confidence report
|   +-- EntityMapping.cs        # Entity-level mapping
|   +-- FieldMapping.cs         # Field-level mapping
|
+-- CodeGen/                    # Integration code generation
|   +-- IntegrationCodeGenerator.cs  # Main code generator
|
+-- Validation/                 # Integration validation
    +-- SlaValidator.cs         # SLA compliance checking
```

---

## Key Types

### IntegrationSpec

Complete specification for an external API integration:
- External system info (name, base URL, version)
- CDM reference
- Endpoint integrations with field mappings
- SLA requirements
- Authentication configuration
- IMS confidence report

### FieldMapping

Maps source field to target field:
- Source/target field names
- Transformation rule (type conversion, formatting, etc.)
- IMS confidence score
- Reasoning explanation
- Auto-mapped / verified flags

### ApiMappingPatterns

Pre-built patterns for common domains:
- **DateTime**: ISO/Unix timestamp conversions
- **Identifiers**: Primary key, foreign key, correlation ID patterns
- **Contact**: Email, phone, name patterns
- **Audit**: created_at, updated_at patterns

---

## CLI Commands

### Parse OpenAPI Spec

```bash
docflow integrate parse <spec.json> [options]
  -v, --verbose          Show entity property details
```

**Example:**
```bash
docflow integrate parse petstore.json -v
```

### Analyze CDM Mapping

```bash
docflow integrate analyze <spec.json> --cdm <path> [options]
  --cdm <path>           Path to CDM C# files (required)
  --threshold <percent>  Filter by confidence threshold
  -o, --output <file>    Save mapping report as JSON
  -v, --verbose          Show field-level mappings
```

**Example:**
```bash
docflow integrate analyze petstore.json --cdm Models/Entities.cs --threshold 70 -v
```

### Validate SLA Compliance

```bash
docflow integrate sla <url> --expected <duration> [options]
  --expected <duration>  Maximum acceptable data age (required)
  --samples <count>      Number of samples (default: 10)
  --interval <duration>  Time between samples (default: 5s)
  --timestamp-path <path>  JSON path to timestamp field
  --header <key=value>   Add HTTP header (repeatable)
  -o, --output <file>    Save report as JSON
  -v, --verbose          Show individual samples
```

**Duration formats:** `500ms`, `30s`, `5m`, `1h`

**Example:**
```bash
docflow integrate sla https://api.example.com/data --expected 30s --samples 20
```

### Generate Integration Code

```bash
docflow integrate generate <spec.json> --cdm <path> --output <dir> [options]
  --cdm <path>           Path to CDM C# files (required)
  --output <dir>         Output directory (required)
  --namespace <name>     Namespace (default: Integration.Generated)
  --generate <types>     What to generate: dtos,mappers,client,validators (default: all)
  -v, --verbose          Show generation details
```

**Example:**
```bash
docflow integrate generate petstore.json --cdm Models/ -o Generated/ -n MyApp.Integration
```

---

## Generated Artifacts

### 1. External DTOs

```csharp
public class PetDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}
```

### 2. AutoMapper Profiles

```csharp
// Pet -> Product (79% confidence)
CreateMap<PetDto, Product>()
    .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))  // 95% - Exact name match
    .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name))  // 95% - Exact name match
    .ForMember(d => d.Status, opt => opt.MapFrom(s => MapProductStatus(s.Status)))  // 95%
    // TODO: Manual mapping required for Price (no source field)
    .ForMember(d => d.Price, opt => opt.Ignore());

private static ProductStatus MapProductStatus(string value) => value?.ToLowerInvariant() switch
{
    "available" => ProductStatus.Available,
    "pending" => ProductStatus.Pending,
    "sold" => ProductStatus.Sold,
    _ => ProductStatus.Available
};
```

### 3. Typed HTTP Clients

```csharp
public interface IPetstoreAPIClient
{
    /// <summary>List all pets</summary>
    Task<object?> GetPetsAsync(CancellationToken ct = default);

    /// <summary>Create a pet</summary>
    Task<PetDto?> PostPetsAsync(PetDto pet, CancellationToken ct = default);

    /// <summary>Get a pet by ID</summary>
    Task<PetDto?> GetPetsByIdAsync(long petId, CancellationToken ct = default);
}
```

### 4. FluentValidation Validators

```csharp
public class PetDtoValidator : AbstractValidator<PetDto>
{
    public PetDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Status).MaximumLength(500);
    }
}
```

---

## CDM Mapping Algorithm

The `CdmMapper` uses a multi-pass field matching algorithm to ensure high-confidence matches are preferred:

### Pass 1: Exact Name Match (95% confidence)
Direct name equality (case-insensitive)

### Pass 2: ID Field Match (85% confidence)
Source ends with "Id" and target is "Id" or "{Entity}Id"

### Pass 3: Contains Match (75% confidence)
Target name contains source name or vice versa

### Pass 4: Foreign Key Pattern (70% confidence)
Source follows FK pattern (e.g., "petId" -> "ProductId")

### Pass 5: Date/Time Match (70% confidence)
Both fields are date/time types

### Semantic Entity Matching
Domain-specific mappings for common patterns:
- Pet -> Product
- Order -> Order
- Category -> ProductCategory
- Tag -> ProductCategory

---

## SLA Validation Feature

Automated data freshness checking:

```csharp
var report = await slaValidator.ValidateDataFreshnessAsync(new SlaValidationRequest
{
    EndpointUrl = "https://api.example.com/v1/flights/status/N12345",
    ExpectedMaxAge = TimeSpan.FromSeconds(30),
    SampleCount = 100,
    SampleInterval = TimeSpan.FromSeconds(5)
});
```

### Compliance Verdicts

| Verdict | Compliance Rate | Exit Code |
|---------|-----------------|-----------|
| COMPLIANT | 100% | 0 |
| MARGINALLY COMPLIANT | 90-99% | 0 |
| MINOR VIOLATION | 50-89% | 2 |
| SEVERE VIOLATION | 0-49% | 2 |

### Example Output

```
SLA Validation: COMPLIANT
Expected Max Age: 30s
Actual Average:   12.4s
Min Age:          8.2s
Max Age:          18.7s
Compliance:       100% (10/10 samples)
```

---

## Future Enhancements (Planned)

### IMS Pattern Learning
The IMS will learn from:
1. **Built-in patterns** - Domain-specific knowledge (e-commerce, etc.)
2. **Existing integrations** - Analyze your manual mappers
3. **User feedback** - Corrections improve future suggestions

```
docflow integrate learn ./src/Integrations/Petstore/Mapper.cs

Learning from example...
   Extracted 23 new patterns
   Updated confidence on 45 existing patterns

Next integration will have 94% auto-mapping accuracy!
```

### Additional Schema Support
- GraphQL schema parsing
- JSON Schema support
- Swagger 2.0 support

### Breaking Change Detection
```bash
docflow integrate validate <spec.json> --against <new-spec.json>
  --breaking-only              Only show breaking changes
```

---

## Sample Files

The `samples/integration-demos/` directory contains:
- `petstore.json` - Sample OpenAPI specification
- `SampleCdm/Entities.cs` - Sample CDM entities
- `sla-test-guide.md` - Guide for testing SLA validation

See [samples/integration-demos/README.md](../../samples/integration-demos/README.md) for usage examples.
