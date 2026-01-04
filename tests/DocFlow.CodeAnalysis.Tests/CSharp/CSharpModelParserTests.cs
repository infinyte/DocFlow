using DocFlow.CodeAnalysis.CSharp;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using Xunit;

namespace DocFlow.CodeAnalysis.Tests.CSharp;

public class CSharpModelParserTests
{
    private readonly CSharpModelParser _parser = new();

    [Fact]
    public async Task ParseAsync_SimpleClass_ExtractsNameAndProperties()
    {
        var code = """
            public class Customer
            {
                public string Name { get; set; }
                public int Age { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        Assert.Single(result.Model.Entities);

        var entity = result.Model.Entities.Values.First();
        Assert.Equal("Customer", entity.Name);
        Assert.Equal(2, entity.Properties.Count);
        Assert.Contains(entity.Properties, p => p.Name == "Name" && p.Type.Name == "string");
        Assert.Contains(entity.Properties, p => p.Name == "Age" && p.Type.Name == "int");
    }

    [Fact]
    public async Task ParseAsync_Record_ClassifiedAsValueObject()
    {
        var code = """
            public record Address(string Street, string City, string PostalCode);
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        Assert.Equal("Address", entity.Name);
        Assert.Equal(EntityClassification.ValueObject, entity.Classification);
        Assert.Contains(entity.Stereotypes, s => s == "record");
    }

    [Fact]
    public async Task ParseAsync_ClassWithIdAndCollection_ClassifiedAsAggregateRoot()
    {
        var code = """
            public class Order
            {
                public Guid Id { get; init; }
                public ICollection<LineItem> LineItems { get; init; }
            }

            public class LineItem
            {
                public Guid Id { get; init; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var order = result.Model.Entities.Values.First(e => e.Name == "Order");
        Assert.Equal(EntityClassification.AggregateRoot, order.Classification);
    }

    [Fact]
    public async Task ParseAsync_ClassWithIdOnly_ClassifiedAsEntity()
    {
        var code = """
            public class Product
            {
                public Guid Id { get; init; }
                public string Name { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        Assert.Equal(EntityClassification.Entity, entity.Classification);
    }

    [Fact]
    public async Task ParseAsync_Interface_ClassifiedAsInterface()
    {
        var code = """
            public interface IOrderRepository
            {
                Task<Order> GetByIdAsync(Guid id);
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        Assert.Equal("IOrderRepository", entity.Name);
        Assert.Equal(EntityClassification.Interface, entity.Classification);
    }

    [Fact]
    public async Task ParseAsync_Enum_ClassifiedAsEnum()
    {
        var code = """
            public enum OrderStatus
            {
                Draft,
                Confirmed,
                Shipped
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        Assert.Equal("OrderStatus", entity.Name);
        Assert.Equal(EntityClassification.Enum, entity.Classification);
        Assert.Equal(3, entity.Properties.Count);
    }

    [Fact]
    public async Task ParseAsync_Inheritance_CreatesRelationship()
    {
        var code = """
            public class Animal { }
            public class Dog : Animal { }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        Assert.Equal(2, result.Model.Entities.Count);

        var inheritanceRel = result.Model.Relationships
            .FirstOrDefault(r => r.Type == RelationshipType.Inheritance);
        Assert.NotNull(inheritanceRel);

        var dog = result.Model.Entities.Values.First(e => e.Name == "Dog");
        var animal = result.Model.Entities.Values.First(e => e.Name == "Animal");
        Assert.Equal(dog.Id, inheritanceRel.SourceEntityId);
        Assert.Equal(animal.Id, inheritanceRel.TargetEntityId);
    }

    [Fact]
    public async Task ParseAsync_InterfaceImplementation_CreatesRelationship()
    {
        var code = """
            public interface IService { }
            public class MyService : IService { }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var implRel = result.Model.Relationships
            .FirstOrDefault(r => r.Type == RelationshipType.Implementation);
        Assert.NotNull(implRel);
    }

    [Fact]
    public async Task ParseAsync_CollectionProperty_CreatesCompositionRelationship()
    {
        var code = """
            public class Order
            {
                public Guid Id { get; init; }
                public ICollection<LineItem> Items { get; init; }
            }

            public class LineItem
            {
                public Guid Id { get; init; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var compositionRel = result.Model.Relationships
            .FirstOrDefault(r => r.Type == RelationshipType.Composition);
        Assert.NotNull(compositionRel);

        var order = result.Model.Entities.Values.First(e => e.Name == "Order");
        Assert.Equal(order.Id, compositionRel.SourceEntityId);
    }

    [Fact]
    public async Task ParseAsync_ReferenceProperty_CreatesAssociationRelationship()
    {
        var code = """
            public class Order
            {
                public Guid Id { get; init; }
                public Customer Customer { get; set; }
            }

            public class Customer
            {
                public Guid Id { get; init; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var associationRel = result.Model.Relationships
            .FirstOrDefault(r => r.Type == RelationshipType.Association);
        Assert.NotNull(associationRel);
    }

    [Fact]
    public async Task ParseAsync_Visibility_ParsedCorrectly()
    {
        var code = """
            public class Example
            {
                public string PublicProp { get; set; }
                private string PrivateProp { get; set; }
                protected string ProtectedProp { get; set; }
                internal string InternalProp { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();

        Assert.Contains(entity.Properties, p => p.Name == "PublicProp" && p.Visibility == Visibility.Public);
        Assert.Contains(entity.Properties, p => p.Name == "PrivateProp" && p.Visibility == Visibility.Private);
        Assert.Contains(entity.Properties, p => p.Name == "ProtectedProp" && p.Visibility == Visibility.Protected);
        Assert.Contains(entity.Properties, p => p.Name == "InternalProp" && p.Visibility == Visibility.Internal);
    }

    [Fact]
    public async Task ParseAsync_RequiredAttribute_DetectedAsRequired()
    {
        var code = """
            using System.ComponentModel.DataAnnotations;

            public class Customer
            {
                [Required]
                public string Name { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        var nameProp = entity.Properties.First(p => p.Name == "Name");
        Assert.True(nameProp.IsRequired);
    }

    [Fact]
    public async Task ParseAsync_RequiredKeyword_DetectedAsRequired()
    {
        var code = """
            public class Customer
            {
                public required string Name { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        var nameProp = entity.Properties.First(p => p.Name == "Name");
        Assert.True(nameProp.IsRequired);
    }

    [Fact]
    public async Task ParseAsync_Methods_ExtractedCorrectly()
    {
        var code = """
            public class Calculator
            {
                public int Add(int a, int b) => a + b;
                public static double Multiply(double x, double y) => x * y;
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();

        Assert.Contains(entity.Operations, m => m.Name == "Add" && !m.IsStatic);
        Assert.Contains(entity.Operations, m => m.Name == "Multiply" && m.IsStatic);
    }

    [Fact]
    public async Task ParseAsync_IdProperty_HasIdentitySemantics()
    {
        var code = """
            public class Entity
            {
                public Guid Id { get; init; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        var idProp = entity.Properties.First(p => p.Name == "Id");
        Assert.Equal(PropertySemantics.Identity, idProp.Semantics);
    }

    [Fact]
    public async Task ParseAsync_GenericCollection_ParsedCorrectly()
    {
        var code = """
            public class Container
            {
                public List<string> Items { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        var prop = entity.Properties.First();

        Assert.True(prop.Type.IsCollection);
        Assert.Single(prop.Type.GenericArguments);
        Assert.Equal("string", prop.Type.GenericArguments[0].Name);
    }

    [Fact]
    public async Task ParseAsync_NullableType_ParsedCorrectly()
    {
        var code = """
            public class Example
            {
                public DateTime? OptionalDate { get; set; }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(code));

        Assert.True(result.Success);
        var entity = result.Model.Entities.Values.First();
        var prop = entity.Properties.First();

        Assert.True(prop.Type.IsNullable);
    }

    [Fact]
    public void CanParse_CSharpContent_ReturnsTrue()
    {
        var input = ParserInput.FromContent("public class Test { }");
        Assert.True(_parser.CanParse(input));
    }

    [Fact]
    public void CanParse_CsFilePath_ReturnsTrue()
    {
        var input = ParserInput.FromFile("test.cs");
        Assert.True(_parser.CanParse(input));
    }

    [Fact]
    public void SupportedExtensions_ContainsCsExtension()
    {
        Assert.Contains(".cs", _parser.SupportedExtensions);
    }
}
