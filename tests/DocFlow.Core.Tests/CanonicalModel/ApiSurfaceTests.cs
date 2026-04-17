using System.Reflection;
using System.Runtime.CompilerServices;
using DocFlow.Core.CanonicalModel;
using Xunit;

namespace DocFlow.Core.Tests.CanonicalModel;

public class ApiSurfaceTests
{
    [Fact]
    public void SemanticModel_WithoutApiSurface_RemainsBackwardsCompatible()
    {
        var model = new SemanticModel
        {
            Name = "Domain"
        };
        var customer = model.CreateEntity("Customer", EntityClassification.Class);

        Assert.Null(model.Api);
        Assert.Single(model.Entities);
        Assert.Same(customer, model.GetEntity(customer.Id));
        // Api is purely additive: callers that ignore the property observe no change.
        var issues = model.Validate();
        Assert.DoesNotContain(issues, i => i.Severity == ValidationSeverity.Error);
    }

    [Fact]
    public void ApiSurface_Records_AreValueEqual()
    {
        // Records with only scalar fields get structural equality for free.
        // (Record equality does not deep-compare IReadOnlyList / IReadOnlyDictionary members.)
        var serverA = new ApiServer { Url = "https://api.example.com", Description = "prod" };
        var serverB = new ApiServer { Url = "https://api.example.com", Description = "prod" };
        Assert.Equal(serverA, serverB);
        Assert.Equal(serverA.GetHashCode(), serverB.GetHashCode());

        var tagA = new ApiTag { Name = "pet", Description = "Pet operations" };
        var tagB = new ApiTag { Name = "pet", Description = "Pet operations" };
        Assert.Equal(tagA, tagB);

        var schemaA = new ApiSchema { Type = "integer", Format = "int64", Nullable = false };
        var schemaB = new ApiSchema { Type = "integer", Format = "int64", Nullable = false };
        Assert.Equal(schemaA, schemaB);

        var mediaA = new ApiMediaType { EntityName = "Pet" };
        var mediaB = new ApiMediaType { EntityName = "Pet" };
        Assert.Equal(mediaA, mediaB);

        // Different values are not equal.
        Assert.NotEqual(tagA, tagA with { Name = "store" });
    }

    [Fact]
    public void ApiOperation_RequiredMembers_AreMarkedAtCompileTime()
    {
        var requiredMembers = typeof(ApiOperation)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttribute<RequiredMemberAttribute>() is not null)
            .Select(p => p.Name)
            .ToHashSet();

        Assert.Contains(nameof(ApiOperation.OperationId), requiredMembers);
        Assert.Contains(nameof(ApiOperation.Method), requiredMembers);
        Assert.Contains(nameof(ApiOperation.Path), requiredMembers);

        Assert.True(typeof(ApiOperation).GetCustomAttribute<RequiredMemberAttribute>() is not null
            || requiredMembers.Count > 0,
            "ApiOperation must declare at least one required member.");
    }

    [Fact]
    public void ApiParameterLocation_Enum_CoversAllOpenApiLocations()
    {
        var values = Enum.GetValues<ApiParameterLocation>().ToHashSet();

        Assert.Contains(ApiParameterLocation.Query, values);
        Assert.Contains(ApiParameterLocation.Header, values);
        Assert.Contains(ApiParameterLocation.Path, values);
        Assert.Contains(ApiParameterLocation.Cookie, values);
        Assert.Equal(4, values.Count);
    }

    [Fact]
    public void ApiSurface_DefaultCollectionsAreEmpty()
    {
        var surface = new ApiSurface
        {
            Title = "Empty",
            Version = "0.0.0"
        };

        Assert.Empty(surface.Servers);
        Assert.Empty(surface.Operations);
        Assert.Empty(surface.Tags);
        Assert.Empty(surface.SecuritySchemes);
        Assert.Empty(surface.SecurityRequirements);
    }

    [Fact]
    public void ApiMediaType_CanReferenceEntityOrInlineSchema()
    {
        var entityRef = new ApiMediaType { EntityName = "Pet" };
        var inline = new ApiMediaType { Schema = new ApiSchema { Type = "string", Format = "date-time" } };

        Assert.Equal("Pet", entityRef.EntityName);
        Assert.Null(entityRef.Schema);
        Assert.Null(inline.EntityName);
        Assert.Equal("date-time", inline.Schema!.Format);
    }
}
