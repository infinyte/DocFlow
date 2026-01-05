using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using Xunit;

namespace DocFlow.Diagrams.Tests.Mermaid;

public class MermaidClassDiagramGeneratorTests
{
    private readonly MermaidClassDiagramGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_EmptyModel_ReturnsClassDiagramHeader()
    {
        var model = new SemanticModel();

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.StartsWith("classDiagram", result.Content);
    }

    [Theory]
    [InlineData(EntityClassification.AggregateRoot, "<<AggregateRoot>>")]
    [InlineData(EntityClassification.Entity, "<<Entity>>")]
    [InlineData(EntityClassification.ValueObject, "<<ValueObject>>")]
    [InlineData(EntityClassification.DomainService, "<<Service>>")]
    [InlineData(EntityClassification.Repository, "<<Repository>>")]
    [InlineData(EntityClassification.Interface, "<<interface>>")]
    [InlineData(EntityClassification.Enum, "<<enumeration>>")]
    public async Task GenerateAsync_EntityClassification_GeneratesCorrectStereotype(
        EntityClassification classification, string expectedStereotype)
    {
        var model = new SemanticModel();
        model.AddEntity(new SemanticEntity
        {
            Id = "1",
            Name = "TestEntity",
            Classification = classification
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains(expectedStereotype, result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Property_FormatsAsNameColonType()
    {
        var model = new SemanticModel();
        var entity = new SemanticEntity { Id = "1", Name = "Customer" };
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Name",
            Type = new SemanticType { Name = "string" },
            Visibility = Visibility.Public
        });
        model.AddEntity(entity);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("+Name : string", result.Content);
    }

    [Theory]
    [InlineData(Visibility.Public, "+")]
    [InlineData(Visibility.Private, "-")]
    [InlineData(Visibility.Protected, "#")]
    [InlineData(Visibility.Internal, "~")]
    public async Task GenerateAsync_Visibility_GeneratesCorrectSymbol(
        Visibility visibility, string expectedSymbol)
    {
        var model = new SemanticModel();
        var entity = new SemanticEntity { Id = "1", Name = "Test" };
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Prop",
            Type = new SemanticType { Name = "int" },
            Visibility = visibility
        });
        model.AddEntity(entity);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains($"{expectedSymbol}Prop : int", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_InheritanceRelationship_GeneratesCorrectArrow()
    {
        var model = new SemanticModel();
        var parent = model.CreateEntity("Animal");
        var child = model.CreateEntity("Dog");
        model.AddRelationship(child.Id, parent.Id, RelationshipType.Inheritance);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("Dog --|> Animal", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_ImplementationRelationship_GeneratesCorrectArrow()
    {
        var model = new SemanticModel();
        var iface = model.CreateEntity("IService", EntityClassification.Interface);
        var impl = model.CreateEntity("MyService");
        model.AddRelationship(impl.Id, iface.Id, RelationshipType.Implementation);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("MyService ..|> IService", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_CompositionRelationship_GeneratesCorrectArrow()
    {
        var model = new SemanticModel();
        var order = model.CreateEntity("Order", EntityClassification.AggregateRoot);
        var lineItem = model.CreateEntity("LineItem", EntityClassification.Entity);
        var rel = model.AddRelationship(order.Id, lineItem.Id, RelationshipType.Composition, "LineItems");
        rel.TargetMultiplicity = Multiplicity.ZeroOrMore;

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("Order --*", result.Content);
        Assert.Contains("LineItem", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_AssociationRelationship_GeneratesCorrectArrow()
    {
        var model = new SemanticModel();
        var order = model.CreateEntity("Order");
        var customer = model.CreateEntity("Customer");
        model.AddRelationship(order.Id, customer.Id, RelationshipType.Association, "Customer");

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("Order --> Customer", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Multiplicity_RenderedCorrectly()
    {
        var model = new SemanticModel();
        var order = model.CreateEntity("Order");
        var item = model.CreateEntity("Item");
        var rel = model.AddRelationship(order.Id, item.Id, RelationshipType.Composition);
        rel.SourceMultiplicity = Multiplicity.One;
        rel.TargetMultiplicity = Multiplicity.ZeroOrMore;

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("\"*\"", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_GenericType_UsesTildeNotation()
    {
        var model = new SemanticModel();
        var entity = new SemanticEntity { Id = "1", Name = "Container" };
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Items",
            Type = new SemanticType
            {
                Name = "ICollection",
                IsCollection = true,
                GenericArguments = [new SemanticType { Name = "Item" }]
            },
            Visibility = Visibility.Public
        });
        model.AddEntity(entity);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("ICollection~Item~", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_StaticMethod_HasDollarSign()
    {
        var model = new SemanticModel();
        var entity = new SemanticEntity { Id = "1", Name = "Factory" };
        entity.Operations.Add(new SemanticOperation
        {
            Name = "Create",
            ReturnType = new SemanticType { Name = "Factory" },
            IsStatic = true,
            Visibility = Visibility.Public
        });
        model.AddEntity(entity);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("Create() Factory$", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Enum_ListsMembers()
    {
        var model = new SemanticModel();
        var entity = new SemanticEntity
        {
            Id = "1",
            Name = "Status",
            Classification = EntityClassification.Enum
        };
        entity.Properties.Add(new SemanticProperty { Name = "Active", Type = new SemanticType { Name = "Status" } });
        entity.Properties.Add(new SemanticProperty { Name = "Inactive", Type = new SemanticType { Name = "Status" } });
        model.AddEntity(entity);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("<<enumeration>>", result.Content);
        Assert.Contains("Active", result.Content);
        Assert.Contains("Inactive", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Output_IsParseableByMermaidParser()
    {
        var model = new SemanticModel();
        var customer = model.CreateEntity("Customer", EntityClassification.Entity);
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

        var generateResult = await _generator.GenerateAsync(model);
        Assert.True(generateResult.Success);

        // Verify it can be parsed back
        var parser = new MermaidClassDiagramParser();
        var parseResult = await parser.ParseAsync(ParserInput.FromContent(generateResult.Content!));

        Assert.True(parseResult.Success);
        Assert.Single(parseResult.Model.Entities);
        Assert.Equal("Customer", parseResult.Model.Entities.Values.First().Name);
    }

    [Fact]
    public void TargetFormat_IsMermaid()
    {
        Assert.Equal("Mermaid", _generator.TargetFormat);
    }

    [Fact]
    public void DefaultExtension_IsMmd()
    {
        Assert.Equal(".mmd", _generator.DefaultExtension);
    }
}
