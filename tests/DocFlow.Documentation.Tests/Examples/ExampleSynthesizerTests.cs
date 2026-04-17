using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Examples;
using Xunit;

namespace DocFlow.Documentation.Tests.Examples;

public class ExampleSynthesizerTests
{
    [Fact]
    public void Examples_PrimitiveSchema_ProducesSensibleValues()
    {
        var model = new SemanticModel();
        var synth = new ExampleSynthesizer(model);

        Assert.Equal("\"string\"", synth.Synthesize(Primitive("string")));
        Assert.Equal("\"2026-01-01T00:00:00Z\"", synth.Synthesize(Primitive("string", "date-time")));
        Assert.Equal("\"00000000-0000-0000-0000-000000000000\"", synth.Synthesize(Primitive("string", "uuid")));
        Assert.Equal("0", synth.Synthesize(Primitive("integer")));
        Assert.Equal("false", synth.Synthesize(Primitive("boolean")));
        Assert.Equal("[\"string\"]", synth.Synthesize(new ApiMediaType
        {
            Schema = new ApiSchema { Type = "array", Items = new ApiSchema { Type = "string" } }
        }));
    }

    [Fact]
    public void Examples_EnumConstrainedString_UsesFirstEnumValue()
    {
        var model = new SemanticModel();
        var synth = new ExampleSynthesizer(model);

        var media = new ApiMediaType
        {
            Schema = new ApiSchema { Type = "string", Enum = ["available", "pending", "sold"] }
        };

        Assert.Equal("\"available\"", synth.Synthesize(media));
    }

    [Fact]
    public void Examples_NestedObject_RespectsRequiredFields()
    {
        var model = new SemanticModel();
        var pet = model.CreateEntity("Pet", EntityClassification.DataTransferObject);
        pet.Properties.Add(new SemanticProperty
        {
            Name = "Id",
            Type = new SemanticType { Name = "long", IsPrimitive = true },
            IsRequired = true
        });
        pet.Properties.Add(new SemanticProperty
        {
            Name = "Name",
            Type = new SemanticType { Name = "string", IsPrimitive = true },
            IsRequired = true
        });
        pet.Properties.Add(new SemanticProperty
        {
            Name = "Nickname",
            Type = new SemanticType { Name = "string", IsPrimitive = true },
            IsRequired = false
        });

        var synth = new ExampleSynthesizer(model);
        var json = synth.Synthesize(new ApiMediaType { EntityName = "Pet" });

        Assert.NotNull(json);
        // Required fields must appear.
        Assert.Contains("\"Id\"", json);
        Assert.Contains("\"Name\"", json);
        // Optional field also appears (OpenAPI examples typically illustrate all fields).
        Assert.Contains("\"Nickname\"", json);
        // Primitive values render sensibly.
        Assert.Contains(": 0", json);
        Assert.Contains(": \"string\"", json);
    }

    [Fact]
    public void Examples_CircularSchema_Terminates()
    {
        // Node { Id: long, Parent: Node }  — self-reference.
        var model = new SemanticModel();
        var node = model.CreateEntity("Node", EntityClassification.Entity);
        node.Properties.Add(new SemanticProperty
        {
            Name = "Id",
            Type = new SemanticType { Name = "long", IsPrimitive = true }
        });
        node.Properties.Add(new SemanticProperty
        {
            Name = "Parent",
            Type = new SemanticType { Name = "Node" }
        });

        var synth = new ExampleSynthesizer(model);
        var json = synth.Synthesize(new ApiMediaType { EntityName = "Node" });

        Assert.NotNull(json);
        // Ellipsis marks the cycle termination.
        Assert.Contains("\"...\"", json);
    }

    [Fact]
    public void Examples_SpecProvidedExample_IsPreferredOverSynthesis()
    {
        var model = new SemanticModel();
        model.CreateEntity("Pet", EntityClassification.DataTransferObject);
        var synth = new ExampleSynthesizer(model);

        const string specExample = "{\n  \"id\": 42,\n  \"name\": \"Rex\"\n}";

        var result = synth.Synthesize(new ApiMediaType { EntityName = "Pet", Example = specExample });

        Assert.Equal(specExample.Trim(), result);
        // The synthesiser did not fall back to "\"string\"" generation.
        Assert.Contains("Rex", result);
    }

    private static ApiMediaType Primitive(string type, string? format = null) => new()
    {
        Schema = new ApiSchema { Type = type, Format = format }
    };
}
