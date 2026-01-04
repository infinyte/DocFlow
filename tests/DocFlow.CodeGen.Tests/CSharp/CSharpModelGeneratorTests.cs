using DocFlow.CodeGen.CSharp;
using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using Xunit;

namespace DocFlow.CodeGen.Tests.CSharp;

public class CSharpModelGeneratorTests
{
    private readonly CSharpModelGenerator _generator = new();

    [Fact]
    public async Task GenerateAsync_Entity_GeneratesClass()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Customer", EntityClassification.Entity);
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Id",
            Type = SemanticType.Guid,
            Visibility = Visibility.Public,
            Semantics = PropertySemantics.Identity
        });
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Name",
            Type = SemanticType.String,
            Visibility = Visibility.Public
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public class Customer", result.Content);
        Assert.Contains("public Guid Id { get; init; }", result.Content);
        Assert.Contains("public string Name { get; set; }", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_ValueObject_GeneratesRecord()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Address", EntityClassification.ValueObject);
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Street",
            Type = SemanticType.String,
            Visibility = Visibility.Public
        });
        entity.Properties.Add(new SemanticProperty
        {
            Name = "City",
            Type = SemanticType.String,
            Visibility = Visibility.Public
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public record Address", result.Content);
        Assert.Contains("string Street", result.Content);
        Assert.Contains("string City", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Enum_GeneratesEnumDeclaration()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("OrderStatus", EntityClassification.Enum);
        entity.Properties.Add(new SemanticProperty { Name = "Draft", Type = new SemanticType { Name = "OrderStatus" } });
        entity.Properties.Add(new SemanticProperty { Name = "Confirmed", Type = new SemanticType { Name = "OrderStatus" } });
        entity.Properties.Add(new SemanticProperty { Name = "Shipped", Type = new SemanticType { Name = "OrderStatus" } });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public enum OrderStatus", result.Content);
        Assert.Contains("Draft,", result.Content);
        Assert.Contains("Confirmed,", result.Content);
        Assert.Contains("Shipped", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Interface_GeneratesInterfaceDeclaration()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("IOrderRepository", EntityClassification.Interface);
        entity.Operations.Add(new SemanticOperation
        {
            Name = "GetByIdAsync",
            ReturnType = new SemanticType { Name = "Order" },
            Visibility = Visibility.Public,
            Parameters =
            [
                new SemanticParameter { Name = "id", Type = SemanticType.Guid }
            ]
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public interface IOrderRepository", result.Content);
        Assert.Contains("Order GetByIdAsync(Guid id);", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_CollectionProperty_GeneratesICollectionWithInit()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Order", EntityClassification.AggregateRoot);
        entity.Properties.Add(new SemanticProperty
        {
            Name = "LineItems",
            Type = SemanticType.CollectionOf(new SemanticType { Name = "LineItem" }),
            Visibility = Visibility.Public,
            Semantics = PropertySemantics.Collection
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public ICollection<LineItem> LineItems { get; init; } = new List<LineItem>();", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_ReferenceProperty_GeneratesProperty()
    {
        var model = new SemanticModel();
        var order = model.CreateEntity("Order", EntityClassification.Entity);
        var customer = model.CreateEntity("Customer", EntityClassification.Entity);

        order.Properties.Add(new SemanticProperty
        {
            Name = "Customer",
            Type = new SemanticType { Name = "Customer", ReferencedEntityId = customer.Id },
            Visibility = Visibility.Public,
            Semantics = PropertySemantics.Navigation
        });

        model.AddRelationship(order.Id, customer.Id, RelationshipType.Association, "Customer");

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public Customer Customer { get; set; }", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_Inheritance_GeneratesBaseClass()
    {
        var model = new SemanticModel();
        var animal = model.CreateEntity("Animal");
        var dog = model.CreateEntity("Dog");
        model.AddRelationship(dog.Id, animal.Id, RelationshipType.Inheritance);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public class Dog : Animal", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_InterfaceImplementation_IncludesInterface()
    {
        var model = new SemanticModel();
        var iface = model.CreateEntity("IService", EntityClassification.Interface);
        var impl = model.CreateEntity("MyService");
        model.AddRelationship(impl.Id, iface.Id, RelationshipType.Implementation);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public class MyService : IService", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_StaticMethod_HasStaticModifier()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Factory");
        entity.Operations.Add(new SemanticOperation
        {
            Name = "Create",
            ReturnType = new SemanticType { Name = "Factory" },
            Visibility = Visibility.Public,
            IsStatic = true
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public static Factory Create()", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_RequiredProperty_HasRequiredKeyword()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Customer");
        entity.Properties.Add(new SemanticProperty
        {
            Name = "Name",
            Type = new SemanticType { Name = "Customer" }, // Non-primitive to get required keyword
            Visibility = Visibility.Public,
            IsRequired = true
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("required", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_WithDescription_IncludesXmlDoc()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Customer");
        entity.Description = "Represents a customer in the system";

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("/// <summary>", result.Content);
        Assert.Contains("Represents a customer in the system", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_UseFileScopedNamespace()
    {
        var model = new SemanticModel { Name = "MyApp" };
        model.CreateEntity("Test");

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("namespace MyApp.Domain;", result.Content);
        Assert.DoesNotContain("namespace MyApp.Domain\n{", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_NullableProperty_IncludesQuestionMark()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Order");
        entity.Properties.Add(new SemanticProperty
        {
            Name = "ShippedAt",
            Type = SemanticType.Nullable(SemanticType.DateTime),
            Visibility = Visibility.Public
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("DateTime?", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_DomainEvent_GeneratesRecord()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("OrderCreatedEvent", EntityClassification.DomainEvent);
        entity.Properties.Add(new SemanticProperty
        {
            Name = "OrderId",
            Type = SemanticType.Guid,
            Visibility = Visibility.Public
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public record OrderCreatedEvent", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_MethodWithParameters_IncludesParameters()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("Calculator");
        entity.Operations.Add(new SemanticOperation
        {
            Name = "Add",
            ReturnType = SemanticType.Int,
            Visibility = Visibility.Public,
            Parameters =
            [
                new SemanticParameter { Name = "a", Type = SemanticType.Int },
                new SemanticParameter { Name = "b", Type = SemanticType.Int }
            ]
        });

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public int Add(int a, int b)", result.Content);
    }

    [Fact]
    public async Task GenerateAsync_AbstractClass_HasAbstractModifier()
    {
        var model = new SemanticModel();
        var entity = model.CreateEntity("BaseEntity", EntityClassification.AbstractClass);
        entity.Stereotypes.Add("abstract");

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);
        Assert.Contains("public abstract class BaseEntity", result.Content);
    }

    [Fact]
    public void TargetFormat_IsCSharp()
    {
        Assert.Equal("CSharp", _generator.TargetFormat);
    }

    [Fact]
    public void DefaultExtension_IsCs()
    {
        Assert.Equal(".cs", _generator.DefaultExtension);
    }

    [Fact]
    public async Task GenerateAsync_OrdersEntitiesCorrectly()
    {
        var model = new SemanticModel();
        model.CreateEntity("MyClass", EntityClassification.Class);
        model.CreateEntity("MyEnum", EntityClassification.Enum);
        model.CreateEntity("MyInterface", EntityClassification.Interface);
        model.CreateEntity("MyValueObject", EntityClassification.ValueObject);

        var result = await _generator.GenerateAsync(model);

        Assert.True(result.Success);

        // Enums should come first, then interfaces, then value objects, then classes
        var enumIndex = result.Content!.IndexOf("enum MyEnum");
        var interfaceIndex = result.Content.IndexOf("interface MyInterface");
        var recordIndex = result.Content.IndexOf("record MyValueObject");
        var classIndex = result.Content.IndexOf("class MyClass");

        Assert.True(enumIndex < interfaceIndex, "Enum should come before interface");
        Assert.True(interfaceIndex < recordIndex, "Interface should come before record");
        Assert.True(recordIndex < classIndex, "Record should come before class");
    }
}
