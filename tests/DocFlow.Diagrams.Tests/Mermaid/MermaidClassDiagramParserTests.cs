using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using Xunit;

namespace DocFlow.Diagrams.Tests.Mermaid;

public class MermaidClassDiagramParserTests
{
    private readonly MermaidClassDiagramParser _parser = new();

    [Fact]
    public async Task ParseAsync_SimpleClass_ExtractsEntity()
    {
        var mermaid = """
            classDiagram
                class Customer {
                    +Id : Guid
                    +Name : string
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        Assert.Single(result.Model.Entities);

        var entity = result.Model.Entities.Values.First();
        Assert.Equal("Customer", entity.Name);
        Assert.Equal(2, entity.Properties.Count);
    }

    [Theory]
    [InlineData("<<AggregateRoot>>", EntityClassification.AggregateRoot)]
    [InlineData("<<Entity>>", EntityClassification.Entity)]
    [InlineData("<<ValueObject>>", EntityClassification.ValueObject)]
    [InlineData("<<Service>>", EntityClassification.DomainService)]
    [InlineData("<<Repository>>", EntityClassification.Repository)]
    [InlineData("<<interface>>", EntityClassification.Interface)]
    [InlineData("<<enumeration>>", EntityClassification.Enum)]
    public async Task ParseAsync_Stereotype_MapsToClassification(
        string stereotype, EntityClassification expectedClassification)
    {
        var mermaid = $$"""
            classDiagram
                class Test {
                    {{stereotype}}
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        Assert.Equal(expectedClassification, entity.Classification);
    }

    [Theory]
    [InlineData("+", Visibility.Public)]
    [InlineData("-", Visibility.Private)]
    [InlineData("#", Visibility.Protected)]
    [InlineData("~", Visibility.Internal)]
    public async Task ParseAsync_Visibility_ParsedCorrectly(
        string symbol, Visibility expectedVisibility)
    {
        var mermaid = $$"""
            classDiagram
                class Test {
                    {{symbol}}Prop : string
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var prop = result.Model.Entities.Values.First().Properties.First();
        Assert.Equal(expectedVisibility, prop.Visibility);
    }

    [Fact]
    public async Task ParseAsync_PropertyWithType_ExtractsNameAndType()
    {
        var mermaid = """
            classDiagram
                class Customer {
                    +Name : string
                    +Age : int
                    +CreatedAt : DateTime
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();

        Assert.Contains(entity.Properties, p => p.Name == "Name" && p.Type.Name == "string");
        Assert.Contains(entity.Properties, p => p.Name == "Age" && p.Type.Name == "int");
        Assert.Contains(entity.Properties, p => p.Name == "CreatedAt" && p.Type.Name == "DateTime");
    }

    [Fact]
    public async Task ParseAsync_GenericType_ParsedWithTildeNotation()
    {
        var mermaid = """
            classDiagram
                class Order {
                    +Items : ICollection~LineItem~
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var prop = result.Model.Entities.Values.First().Properties.First();

        Assert.Equal("ICollection", prop.Type.Name);
        Assert.True(prop.Type.IsCollection);
        Assert.Single(prop.Type.GenericArguments);
        Assert.Equal("LineItem", prop.Type.GenericArguments[0].Name);
    }

    [Fact]
    public async Task ParseAsync_NullableType_ParsedCorrectly()
    {
        var mermaid = """
            classDiagram
                class Order {
                    +ShippedAt : DateTime?
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var prop = result.Model.Entities.Values.First().Properties.First();
        Assert.True(prop.Type.IsNullable);
    }

    [Fact]
    public async Task ParseAsync_InheritanceRelationship_Parsed()
    {
        var mermaid = """
            classDiagram
                class Animal {
                }
                class Dog {
                }
                Dog --|> Animal
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        Assert.Single(result.Model.Relationships);

        var rel = result.Model.Relationships.First();
        Assert.Equal(RelationshipType.Inheritance, rel.Type);
    }

    [Fact]
    public async Task ParseAsync_ImplementationRelationship_Parsed()
    {
        var mermaid = """
            classDiagram
                class IService {
                    <<interface>>
                }
                class MyService {
                }
                MyService ..|> IService
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var rel = result.Model.Relationships.First();
        Assert.Equal(RelationshipType.Implementation, rel.Type);
    }

    [Fact]
    public async Task ParseAsync_CompositionRelationship_Parsed()
    {
        var mermaid = """
            classDiagram
                class Order {
                }
                class LineItem {
                }
                Order --* LineItem
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var rel = result.Model.Relationships.First();
        Assert.Equal(RelationshipType.Composition, rel.Type);
    }

    [Fact]
    public async Task ParseAsync_AssociationRelationship_Parsed()
    {
        var mermaid = """
            classDiagram
                class Order {
                }
                class Customer {
                }
                Order --> Customer
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var rel = result.Model.Relationships.First();
        Assert.Equal(RelationshipType.Association, rel.Type);
    }

    [Fact]
    public async Task ParseAsync_RelationshipWithMultiplicity_Parsed()
    {
        var mermaid = """
            classDiagram
                class Order {
                }
                class LineItem {
                }
                Order --* "*" LineItem : items
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var rel = result.Model.Relationships.First();
        Assert.Equal("items", rel.Name);
    }

    [Fact]
    public async Task ParseAsync_Method_ParsedCorrectly()
    {
        var mermaid = """
            classDiagram
                class Calculator {
                    +Add(a, b) int
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        Assert.Single(entity.Operations);

        var method = entity.Operations.First();
        Assert.Equal("Add", method.Name);
        Assert.Equal(2, method.Parameters.Count);
    }

    [Fact]
    public async Task ParseAsync_StaticMethod_DetectedByDollarSign()
    {
        var mermaid = """
            classDiagram
                class Factory {
                    +Create() Factory$
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var method = result.Model.Entities.Values.First().Operations.First();
        Assert.True(method.IsStatic);
    }

    [Fact]
    public async Task ParseAsync_RoundTrip_ProducesEquivalentModel()
    {
        // Create original model
        var originalModel = new SemanticModel();
        var customer = originalModel.CreateEntity("Customer", EntityClassification.Entity);
        customer.Properties.Add(new SemanticProperty
        {
            Name = "Id",
            Type = SemanticType.Guid,
            Visibility = Visibility.Public
        });
        customer.Properties.Add(new SemanticProperty
        {
            Name = "Name",
            Type = SemanticType.String,
            Visibility = Visibility.Public
        });

        var order = originalModel.CreateEntity("Order", EntityClassification.AggregateRoot);
        order.Properties.Add(new SemanticProperty
        {
            Name = "Id",
            Type = SemanticType.Guid,
            Visibility = Visibility.Public
        });

        originalModel.AddRelationship(order.Id, customer.Id, RelationshipType.Association, "Customer");

        // Generate Mermaid
        var generator = new MermaidClassDiagramGenerator();
        var generateResult = await generator.GenerateAsync(originalModel);
        Assert.True(generateResult.Success);

        // Parse back
        var parseResult = await _parser.ParseAsync(ParserInput.FromContent(generateResult.Content!));
        Assert.True(parseResult.Success);

        // Verify equivalence
        Assert.Equal(originalModel.Entities.Count, parseResult.Model.Entities.Count);
        Assert.Equal(originalModel.Relationships.Count, parseResult.Model.Relationships.Count);

        var roundTripCustomer = parseResult.Model.Entities.Values.First(e => e.Name == "Customer");
        Assert.Equal(EntityClassification.Entity, roundTripCustomer.Classification);
        Assert.Equal(2, roundTripCustomer.Properties.Count);
    }

    [Fact]
    public void CanParse_MermaidContent_ReturnsTrue()
    {
        var input = ParserInput.FromContent("classDiagram\n  class Test { }");
        Assert.True(_parser.CanParse(input));
    }

    [Fact]
    public void CanParse_NonMermaidContent_ReturnsFalse()
    {
        var input = ParserInput.FromContent("public class Test { }");
        Assert.False(_parser.CanParse(input));
    }

    [Fact]
    public void CanParse_MmdFilePath_ReturnsTrue()
    {
        var input = ParserInput.FromFile("diagram.mmd");
        Assert.True(_parser.CanParse(input));
    }

    [Fact]
    public void SupportedExtensions_ContainsMmd()
    {
        Assert.Contains(".mmd", _parser.SupportedExtensions);
    }

    [Fact]
    public async Task ParseAsync_IdProperty_HasIdentitySemantics()
    {
        var mermaid = """
            classDiagram
                class Entity {
                    +Id : Guid
                }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(mermaid));

        Assert.True(result.Success);
        var prop = result.Model.Entities.Values.First().Properties.First();
        Assert.Equal(PropertySemantics.Identity, prop.Semantics);
    }
}
