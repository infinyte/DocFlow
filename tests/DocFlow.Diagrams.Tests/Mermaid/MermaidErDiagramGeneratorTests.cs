using DocFlow.Core.CanonicalModel;
using DocFlow.Diagrams.Mermaid;
using Xunit;

namespace DocFlow.Diagrams.Tests.Mermaid;

public class MermaidErDiagramGeneratorTests
{
    private readonly MermaidErDiagramGenerator _generator = new();

    [Fact]
    public void Er_TwoEntitiesWithComposition_EmitsCorrectCardinality()
    {
        var model = new SemanticModel { Name = "Shop" };
        var order = model.CreateEntity("Order", EntityClassification.AggregateRoot);
        var line = model.CreateEntity("LineItem", EntityClassification.Entity);
        model.AddRelationship(order.Id, line.Id, RelationshipType.Composition, name: "contains");

        var output = _generator.Generate(model);

        Assert.StartsWith("erDiagram", output);
        Assert.Contains("Order ||--o{ LineItem : contains", output);
    }

    [Fact]
    public void Er_Aggregation_And_Association_EmitCorrectCardinality()
    {
        var model = new SemanticModel { Name = "Shop" };
        var dept = model.CreateEntity("Department", EntityClassification.Entity);
        var emp = model.CreateEntity("Employee", EntityClassification.Entity);
        var cust = model.CreateEntity("Customer", EntityClassification.Entity);
        var card = model.CreateEntity("LoyaltyCard", EntityClassification.Entity);

        model.AddRelationship(dept.Id, emp.Id, RelationshipType.Aggregation, name: "employs");
        model.AddRelationship(cust.Id, card.Id, RelationshipType.Association, name: "holds");

        var output = _generator.Generate(model);

        Assert.Contains("Department }o--o{ Employee : employs", output);
        Assert.Contains("Customer }o--|| LoyaltyCard : holds", output);
    }

    [Fact]
    public void Er_NoRelationships_EmitsSingleEntity()
    {
        var model = new SemanticModel { Name = "Solo" };
        model.CreateEntity("Customer", EntityClassification.Entity);

        var output = _generator.Generate(model);

        Assert.StartsWith("erDiagram", output);
        Assert.Contains("Customer {", output);
        // No relationship arrows should appear.
        Assert.DoesNotContain("--", output);
    }

    [Fact]
    public void Er_Deterministic_OrdersEntitiesAlphabetically()
    {
        // Build two models with the same entities in different insertion orders.
        var modelA = new SemanticModel { Name = "A" };
        modelA.CreateEntity("Zeta", EntityClassification.Entity);
        modelA.CreateEntity("Alpha", EntityClassification.Entity);
        modelA.CreateEntity("Mu", EntityClassification.Entity);

        var modelB = new SemanticModel { Name = "B" };
        modelB.CreateEntity("Alpha", EntityClassification.Entity);
        modelB.CreateEntity("Mu", EntityClassification.Entity);
        modelB.CreateEntity("Zeta", EntityClassification.Entity);

        var outA = _generator.Generate(modelA);
        var outB = _generator.Generate(modelB);

        Assert.Equal(outA, outB);

        // And the entities appear in alphabetical order inside the output.
        var alphaIdx = outA.IndexOf("Alpha", StringComparison.Ordinal);
        var muIdx = outA.IndexOf("Mu", StringComparison.Ordinal);
        var zetaIdx = outA.IndexOf("Zeta", StringComparison.Ordinal);
        Assert.True(alphaIdx < muIdx, "Alpha should appear before Mu");
        Assert.True(muIdx < zetaIdx, "Mu should appear before Zeta");
    }

    [Fact]
    public void Er_SkipsNonStructuralRelationships()
    {
        var model = new SemanticModel { Name = "Types" };
        var animal = model.CreateEntity("Animal", EntityClassification.Class);
        var dog = model.CreateEntity("Dog", EntityClassification.Class);
        model.AddRelationship(dog.Id, animal.Id, RelationshipType.Inheritance);

        var output = _generator.Generate(model);

        // Inheritance is not an ER-diagram concept — it should not render as a relationship line.
        Assert.DoesNotContain("||--|{", output);
        Assert.DoesNotContain(": inheritance", output);
        // Both entities still show up as blocks because neither is touched by a rendered relationship.
        Assert.Contains("Animal {", output);
        Assert.Contains("Dog {", output);
    }
}
