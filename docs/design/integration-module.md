# DocFlow.Integration Module - Design Document

## Status: Scaffolded (Core Types Implemented, Full Features Planned)

The Integration module has been scaffolded with core types and interfaces. The OpenAPI parser, schema models, and SLA validator foundations are in place. Full implementation is planned for a future release.

**Implemented:**
- `ISchemaParser` interface and `SchemaParseResult`
- `OpenApiParser` foundation
- `IntegrationSpec`, `ApiEndpoint`, `FieldMapping` models
- `ApiMappingPatterns` with aviation domain patterns
- `SlaValidator` structure

**Planned:**
- Full OpenAPI/Swagger parsing
- CDM mapping engine
- Code generation (DTOs, AutoMapper, HTTP clients)
- CLI commands for integration workflows

---

## Overview

DocFlow.Integration extends the platform from documents/code into **enterprise API integration automation**. It uses the same Canonical Semantic Model pattern to map between external API schemas and internal Canonical Data Models.

## Core Insight

The same architecture that transforms C# ↔ Mermaid can transform External API ↔ CDM:

```
DOCUMENTS/CODE DOMAIN              API INTEGRATION DOMAIN
──────────────────────             ─────────────────────

┌─────────────┐                    ┌─────────────────┐
│   C# Code   │                    │  OpenAPI Spec   │
├─────────────┤                    ├─────────────────┤
│   Mermaid   │                    │  JSON Samples   │
├─────────────┤                    ├─────────────────┤
│  PlantUML   │                    │  GraphQL Schema │
└──────┬──────┘                    └────────┬────────┘
       │         ┌──────────────────┐       │
       └────────►│    CANONICAL     │◄──────┘
                 │  SEMANTIC MODEL  │
                 └────────┬─────────┘
                          │
       ┌──────────────────┼──────────────────┐
       ▼                  ▼                  ▼
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  Generated  │    │  Generated  │    │ Integration │
│    Code     │    │  Diagrams   │    │    Code     │
└─────────────┘    └─────────────┘    └─────────────┘
```

The IMS learns patterns in both domains!

---

## Project Structure

```
src/DocFlow.Integration/
├── Schemas/                    # External API schema parsing
│   ├── OpenApi/               # OpenAPI 3.x / Swagger 2.0
│   ├── JsonSchema/            # JSON Schema
│   ├── GraphQL/               # GraphQL schemas
│   └── Soap/                  # Legacy WSDL/SOAP
│
├── Models/                     # Integration-specific models
│   ├── IntegrationSpec.cs     # Complete integration specification
│   └── ApiEndpoint.cs         # API endpoint and parameter models
│
├── Patterns/                   # IMS pattern definitions
│   └── ApiMappingPatterns.cs  # Domain-specific mapping patterns
│
├── Mapping/                    # CDM mapping engine
│   └── CdmMapper.cs           # Maps external → CDM with IMS
│
├── CodeGen/                    # Integration code generation
│   ├── DtoGenerator.cs        # External DTOs
│   ├── MapperGenerator.cs     # AutoMapper profiles
│   ├── ClientGenerator.cs     # Typed HTTP clients
│   └── ValidatorGenerator.cs  # FluentValidation validators
│
├── Validation/                 # Integration validation
│   └── SlaValidator.cs        # SLA compliance checking (!)
│
└── Analysis/                   # Change detection
    ├── BreakingChangeDetector.cs
    └── DriftDetector.cs
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
- **Aviation**: tail_num → TailNumber, arr_time → ArrivalDateTime, pax → PassengerCount
- **DateTime**: ISO/Unix timestamp conversions
- **Identifiers**: Primary key, foreign key, correlation ID patterns
- **Contact**: Email, phone, name patterns
- **Audit**: created_at, updated_at patterns

---

## CLI Commands (Planned)

```bash
# Generate integration scaffold from OpenAPI spec
docflow integrate <spec.json> --cdm <path>
  -o, --output <dir>           Output directory
  -n, --namespace <name>       Namespace for generated code
  --skip-client                Don't generate HTTP client
  --interactive                Prompt for low-confidence mappings

# Analyze mapping coverage
docflow integrate analyze <spec.json> --cdm <path>
  --report <file>              Output detailed report

# Validate against updated API spec
docflow integrate validate <spec.json> --against <new-spec.json>
  --breaking-only              Only show breaking changes

# Learn patterns from existing mappers
docflow integrate learn <existing-mapper.cs>
  --source-dto <dto.cs>
  --target-cdm <cdm.cs>

# SLA compliance validation
docflow integrate sla <endpoint-url> --expected <30s>
  --samples 100                Number of samples
  --report <file>              Output report
```

---

## Generated Artifacts

### 1. External DTOs

```csharp
public sealed record FlightBridgeReservationDto
{
    [JsonPropertyName("res_id")]
    public string? ResId { get; init; }
    
    [JsonPropertyName("arr_time")]
    public string? ArrTime { get; init; }
    // ...
}
```

### 2. AutoMapper Profiles

```csharp
CreateMap<FlightBridgeReservationDto, Reservation>()
    .ForMember(dest => dest.ReservationId, 
        opt => opt.MapFrom(src => src.ResId))  // 98% confidence
    .ForMember(dest => dest.ArrivalDateTime, 
        opt => opt.MapFrom(src => ParseDateTime(src.ArrTime)));  // 95%
```

### 3. Typed HTTP Clients

```csharp
public interface IFlightBridgeClient
{
    Task<Reservation?> GetReservationAsync(string id, CancellationToken ct);
    Task<FlightStatus?> GetFlightStatusAsync(string tailNumber, CancellationToken ct);
}
```

### 4. FluentValidation Validators

```csharp
RuleFor(x => x.TailNum)
    .Matches(@"^[A-Z0-9]{2,7}$")
    .WithMessage("Invalid tail number format");
```

---

## SLA Validation Feature

Automated data freshness checking - would have caught the 1200 Aero issue!

```csharp
var report = await slaValidator.ValidateDataFreshnessAsync(new SlaValidationRequest
{
    EndpointUrl = "https://api.1200aero.com/v1/flights/status/N12345",
    ExpectedMaxAge = TimeSpan.FromSeconds(30),
    SampleCount = 100,
    SampleInterval = TimeSpan.FromSeconds(5)
});

// Output:
// 🚨 SLA Validation: SevereViolation
// Expected Max Age: 00:00:30
// Actual Average Age: 02:34:17  ← This is the problem!
// Compliance: 0%
```

---

## IMS Integration Learning

The IMS learns from:
1. **Built-in patterns** - Domain-specific knowledge (aviation, etc.)
2. **Existing integrations** - Analyze your manual mappers
3. **User feedback** - Corrections improve future suggestions

```
docflow integrate learn ./src/Integrations/FlightBridge/Mapper.cs

📚 Learning from example...
   Extracted 23 new patterns
   Updated confidence on 45 existing patterns
   
Next integration will have 94% auto-mapping accuracy!
```

---

## Implementation Status

Current project status:
1. ✅ Core C# ↔ Mermaid pipeline (complete)
2. ✅ Whiteboard scanning with Claude Vision (complete)
3. ✅ Integration module scaffolded (types and interfaces)
4. ⏳ Document pipeline (PDF/Word) - planned
5. ⏳ Full Integration module implementation - planned

The architecture is in place and ready for full implementation!

---

## Aviation Domain Patterns (Pre-seeded)

| External Field Pattern | CDM Target | Confidence |
|------------------------|------------|------------|
| `tail_num`, `aircraft_id`, `registration` | TailNumber | 95% |
| `arr_time`, `eta`, `arrival` | ArrivalDateTime | 93% |
| `dep_time`, `etd`, `departure` | DepartureDateTime | 93% |
| `origin_icao`, `from_airport` | OriginAirportCode | 92% |
| `dest_icao`, `to_airport` | DestinationAirportCode | 92% |
| `pax`, `passenger_count` | PassengerCount | 90% |
| `fuel_qty`, `fuel_amount` | FuelQuantity | 88% |
| `fbo_id`, `handler` | FboId | 85% |

---

## Connection to OpenFlight CDM Work

This module directly applies the CDM architecture work from Signature Aviation:
- Same canonical model principles
- Same DDD patterns
- Same integration challenges (FlightBridge, 1200 Aero, etc.)

DocFlow.Integration is the tool that would have automated much of that integration work.
