# Integration Module Demos

This directory contains sample files for testing the DocFlow Integration module commands.

## Contents

| File | Description |
|------|-------------|
| `petstore.json` | Sample OpenAPI 3.0.3 specification |
| `SampleCdm/Entities.cs` | Sample Canonical Data Model entities (e-commerce) |
| `sla-test-guide.md` | Guide for testing SLA validation with public APIs |

---

## Development Setup

All commands in this guide use the `docflow-dev.sh` script (Linux/Mac) or `docflow-dev.cmd` (Windows) which runs the CLI from the local build.

```bash
# From the repository root:
./docflow-dev.sh <command> [args]

# On Windows:
docflow-dev.cmd <command> [args]
```

---

## Quick Start

### 1. Analyze CDM Mapping

```bash
./docflow-dev.sh integrate analyze samples/integration-demos/petstore.json --cdm samples/integration-demos/SampleCdm/Entities.cs -v
```

**Expected Output:**
```
Overall Mapping Confidence: 79%

Entity Mappings:
+----------+----------------+------------+
| External | CDM Target     | Confidence |
+----------+----------------+------------+
| Pet      | Product        | 79%        |
| Category | ProductCategory| 95%        |
| Tag      | ProductCategory| 95%        |
| Order    | Order          | 69%        |
+----------+----------------+------------+
```

### 2. Validate SLA (Using Public APIs)

```bash
# Test with World Time API
./docflow-dev.sh integrate sla http://worldtimeapi.org/api/timezone/UTC --expected 30s --samples 5 --interval 2s
```

See `sla-test-guide.md` for more public API testing options.

### 3. Generate Integration Code

```bash
# Generate to /tmp/generated directory
./docflow-dev.sh integrate generate samples/integration-demos/petstore.json --cdm samples/integration-demos/SampleCdm/Entities.cs -o /tmp/generated -n Petstore.Integration
```

**Expected Output:**
```
Generated Files:
+--------------------------------------+-------+--------+
| File                                 | Lines | Status |
+--------------------------------------+-------+--------+
| Client/IPetstoreAPIClient.cs         |    23 | * New  |
| External/CategoryDto.cs              |    19 | * New  |
| External/OrderDto.cs                 |    35 | * New  |
| External/PetDto.cs                   |    33 | * New  |
| External/TagDto.cs                   |    17 | * New  |
| Mapping/PetstoreAPIMappingProfile.cs |    67 | * New  |
| Validation/CategoryDtoValidator.cs   |    13 | * New  |
| Validation/OrderDtoValidator.cs      |    13 | * New  |
| Validation/PetDtoValidator.cs        |    15 | * New  |
| Validation/TagDtoValidator.cs        |    13 | * New  |
+--------------------------------------+-------+--------+

* Generated 10 files (248 lines)
! 4 mappings need manual review (see TODO comments)
```

---

## Sample CDM

The `SampleCdm/Entities.cs` file contains a simple e-commerce domain model:

```csharp
namespace SampleCdm;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ProductCategory Category { get; set; } = null!;
    public decimal Price { get; set; }
    public ProductStatus Status { get; set; }
}

public class ProductCategory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
}

public class Order
{
    public Guid OrderId { get; set; }
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public DateTime OrderDate { get; set; }
    public OrderStatus Status { get; set; }
}

public enum ProductStatus { Available, Pending, Sold }
public enum OrderStatus { Placed, Approved, Delivered }
```

---

## Sample OpenAPI Spec

The `petstore.json` file is a simplified Petstore API with:

- **Entities**: Pet, Category, Tag, Order
- **Endpoints**: GET/POST /pets, GET /pets/{id}, POST /orders

This demonstrates common integration scenarios:
- Entity mapping with different naming (Pet -> Product)
- Nested objects (Category)
- Collections (Tags)
- Enum mapping (status fields)
- ID field mapping patterns

---

## Generated Code Examples

### External DTO

```csharp
public class PetDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("category")]
    public CategoryDto Category { get; set; } = null!;

    [JsonPropertyName("tags")]
    public List<TagDto> Tags { get; set; } = [];

    [JsonPropertyName("status")]
    public string Status { get; set; } = "";
}
```

### AutoMapper Profile

```csharp
// Pet -> Product (79% confidence)
CreateMap<PetDto, Product>()
    .ForMember(d => d.Id, opt => opt.MapFrom(s => s.Id))  // 95% - Exact name match
    .ForMember(d => d.Name, opt => opt.MapFrom(s => s.Name))  // 95% - Exact name match
    .ForMember(d => d.Category, opt => opt.MapFrom(s => MapProductCategory(s.Category)))
    .ForMember(d => d.Status, opt => opt.MapFrom(s => MapProductStatus(s.Status)))
    // TODO: Manual mapping required for Tags (no target field)
    // TODO: Manual mapping required for Price (no source field)
    .ForMember(d => d.Price, opt => opt.Ignore());
```

### HTTP Client Interface

```csharp
public interface IPetstoreAPIClient
{
    /// <summary>List all pets</summary>
    Task<object?> GetPetsAsync(CancellationToken ct = default);

    /// <summary>Create a pet</summary>
    Task<PetDto?> PostPetsAsync(PetDto pet, CancellationToken ct = default);

    /// <summary>Get a pet by ID</summary>
    Task<PetDto?> GetPetsByIdAsync(long petId, CancellationToken ct = default);

    /// <summary>Place an order</summary>
    Task<OrderDto?> PostOrdersAsync(OrderDto order, CancellationToken ct = default);
}
```

### FluentValidation Validator

```csharp
public class PetDtoValidator : AbstractValidator<PetDto>
{
    public PetDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Status).MaximumLength(500);
    }
}
```

---

## Full Integration Workflow

```bash
# 1. Analyze how the external API maps to your domain model
./docflow-dev.sh integrate analyze samples/integration-demos/petstore.json --cdm samples/integration-demos/SampleCdm/Entities.cs --threshold 70 -v

# 2. (Optional) Validate SLA if the API is live
./docflow-dev.sh integrate sla https://petstore.example.com/api/v1/pets --expected 30s --samples 10

# 3. Generate integration code
./docflow-dev.sh integrate generate samples/integration-demos/petstore.json --cdm samples/integration-demos/SampleCdm/Entities.cs -o ./Generated -n MyApp.Integration.Petstore

# 4. Review and customize
# - Check TODO comments in generated mapper
# - Add manual mappings for unmapped fields
# - Implement the HTTP client interface
```

---

## Next Steps

After generating code:

1. **Review TODO comments** in the AutoMapper profile for fields needing manual mapping
2. **Implement the HTTP client** interface with your preferred HTTP library
3. **Register services** in your DI container:
   - AutoMapper profile
   - FluentValidation validators
   - HTTP client implementation
4. **Add unit tests** for custom mappings

---

## Troubleshooting

### "No CDM entities found"
Ensure the CDM path points to valid C# files with class definitions.

### Low confidence mappings
Review field names in both the OpenAPI spec and CDM. Consider adding semantic patterns to `ApiMappingPatterns.cs`.

### Missing mappings
Fields with "???" as target have no automatic match. Add manual mappings in the generated AutoMapper profile.

### SLA validation errors
- Ensure the URL is accessible
- Check network connectivity
- Verify the endpoint returns valid JSON
