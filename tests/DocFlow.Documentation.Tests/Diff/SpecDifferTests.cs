using DocFlow.Core.CanonicalModel;
using DocFlow.Documentation.Diff;
using Xunit;

namespace DocFlow.Documentation.Tests.Diff;

public class SpecDifferTests
{
    private readonly SpecDiffer _differ = new();

    [Fact]
    public void Diff_AddedOperation_IsNonBreaking()
    {
        var oldModel = ModelWith(Operations("listPets"));
        var newModel = ModelWith(Operations("listPets", "createPet"));

        var diff = _differ.Diff(oldModel, newModel);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(ChangeCategory.Operation, change.Category);
        Assert.Equal(ChangeSeverity.NonBreaking, change.Severity);
        Assert.Contains("Added operation", change.Description);
        Assert.Contains("createPet", change.Description);
    }

    [Fact]
    public void Diff_RemovedOperation_IsBreaking()
    {
        var oldModel = ModelWith(Operations("listPets", "deletePet"));
        var newModel = ModelWith(Operations("listPets"));

        var diff = _differ.Diff(oldModel, newModel);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(ChangeCategory.Operation, change.Category);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Contains("Removed operation", change.Description);
        Assert.Contains("deletePet", change.Description);
    }

    [Fact]
    public void Diff_AddedRequiredRequestField_IsBreaking()
    {
        // Pet schema gains a required `name` property.
        var oldModel = new SemanticModel();
        var oldPet = oldModel.CreateEntity("Pet", EntityClassification.DataTransferObject);
        oldPet.Properties.Add(Property("Id", "long", required: true));

        var newModel = new SemanticModel();
        var newPet = newModel.CreateEntity("Pet", EntityClassification.DataTransferObject);
        newPet.Properties.Add(Property("Id", "long", required: true));
        newPet.Properties.Add(Property("name", "string", required: true));

        var diff = _differ.Diff(oldModel, newModel);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(ChangeCategory.Schema, change.Category);
        Assert.Equal(ChangeSeverity.Breaking, change.Severity);
        Assert.Contains("Added required property", change.Description);
        Assert.Contains("Pet.name", change.Description);
    }

    [Fact]
    public void Diff_AddedOptionalQueryParam_IsNonBreaking()
    {
        var op = (string[] queryParams) => new ApiOperation
        {
            OperationId = "listPets",
            Method = ApiHttpMethod.Get,
            Path = "/pets",
            Parameters = queryParams.Select(name => new ApiParameter
            {
                Name = name,
                Location = ApiParameterLocation.Query,
                Required = false
            }).ToList()
        };

        var oldModel = ModelWith([op([])]);
        var newModel = ModelWith([op(["status"])]);

        var diff = _differ.Diff(oldModel, newModel);

        var change = Assert.Single(diff.Changes);
        Assert.Equal(ChangeCategory.Parameter, change.Category);
        Assert.Equal(ChangeSeverity.NonBreaking, change.Severity);
        Assert.Contains("optional query parameter `status`", change.Description);
    }

    [Fact]
    public void Diff_ChangedFieldType_IsBreaking()
    {
        var oldModel = new SemanticModel();
        var oldPet = oldModel.CreateEntity("Pet", EntityClassification.DataTransferObject);
        oldPet.Properties.Add(Property("Id", "int"));

        var newModel = new SemanticModel();
        var newPet = newModel.CreateEntity("Pet", EntityClassification.DataTransferObject);
        newPet.Properties.Add(Property("Id", "string"));

        var diff = _differ.Diff(oldModel, newModel);

        Assert.Contains(diff.Changes, c =>
            c.Category == ChangeCategory.Schema
            && c.Severity == ChangeSeverity.Breaking
            && c.Description.Contains("Changed type of")
            && c.Description.Contains("int")
            && c.Description.Contains("string"));
    }

    [Fact]
    public void Diff_NoChanges_ProducesEmptyChangelogWithHeader()
    {
        var oldModel = ModelWith(Operations("listPets"));
        var newModel = ModelWith(Operations("listPets"));

        var diff = _differ.Diff(oldModel, newModel);

        Assert.False(diff.HasChanges);
        Assert.Equal(0, diff.BreakingCount);
        Assert.Equal(0, diff.NonBreakingCount);

        var rendered = new ChangelogGenerator().Render(diff);
        Assert.Contains("# API Changelog", rendered);
        Assert.Contains("| Breaking | 0 |", rendered);
        Assert.Contains("| Non-breaking | 0 |", rendered);
        Assert.Contains("_No differences detected._", rendered);
    }

    [Fact]
    public void Diff_RequiredFlagFlip_IsBreakingOnlyWhenTighter()
    {
        // false → true on a schema property = breaking; true → false = non-breaking.
        var oldModel = new SemanticModel();
        var oldPet = oldModel.CreateEntity("Pet", EntityClassification.DataTransferObject);
        oldPet.Properties.Add(Property("id", "long", required: false));
        oldPet.Properties.Add(Property("name", "string", required: true));

        var newModel = new SemanticModel();
        var newPet = newModel.CreateEntity("Pet", EntityClassification.DataTransferObject);
        newPet.Properties.Add(Property("id", "long", required: true));     // tightened
        newPet.Properties.Add(Property("name", "string", required: false)); // relaxed

        var diff = _differ.Diff(oldModel, newModel);

        Assert.Contains(diff.Changes, c =>
            c.Path == "Pet.id" && c.Severity == ChangeSeverity.Breaking);
        Assert.Contains(diff.Changes, c =>
            c.Path == "Pet.name" && c.Severity == ChangeSeverity.NonBreaking);
    }

    // --- helpers ---------------------------------------------------------

    private static SemanticModel ModelWith(params ApiOperation[] ops)
    {
        return new SemanticModel
        {
            Api = new ApiSurface
            {
                Title = "Test",
                Version = "1.0",
                Operations = ops
            }
        };
    }

    private static ApiOperation[] Operations(params string[] ids) =>
        ids.Select(id => new ApiOperation
        {
            OperationId = id,
            Method = ApiHttpMethod.Get,
            Path = "/" + id
        }).ToArray();

    private static SemanticProperty Property(string name, string typeName, bool required = false) => new()
    {
        Name = name,
        Type = new SemanticType { Name = typeName, IsPrimitive = true },
        IsRequired = required
    };
}
