using DocFlow.Core.Abstractions;
using DocFlow.Integration.Schemas.OpenApi;
using Xunit;

namespace DocFlow.Integration.Tests.Schemas;

public class OpenApiParserLegacyEntryPointTests
{
    private const string PetstoreJsonPath = "Fixtures/petstore.json";

    [Fact]
    public async Task OpenApiParser_LegacyEntryPoint_StillProducesSameSemanticModel()
    {
        // Legacy entry point: ParseSchemaAsync via ParserInput.
        var legacy = new OpenApiParser();
        var legacyResult = await legacy.ParseSchemaAsync(ParserInput.FromFile(PetstoreJsonPath));
        Assert.True(legacyResult.Success);
        var legacyModel = legacyResult.Model;

        // New IApiSpecParser entry point: stream in, SemanticModel out.
        IApiSpecParser iface = new OpenApiParser();
        using var stream = File.OpenRead(PetstoreJsonPath);
        var newModel = await iface.ParseAsync(stream);

        // Both entry points surface equivalent structural data.
        Assert.Equal(
            legacyModel.Entities.Values.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal),
            newModel.Entities.Values.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal));

        Assert.NotNull(legacyModel.Api);
        Assert.NotNull(newModel.Api);

        Assert.Equal(
            legacyModel.Api!.Operations.Select(o => o.OperationId).OrderBy(id => id, StringComparer.Ordinal),
            newModel.Api!.Operations.Select(o => o.OperationId).OrderBy(id => id, StringComparer.Ordinal));

        Assert.Equal(legacyModel.Api.Title, newModel.Api.Title);
        Assert.Equal(legacyModel.Api.Version, newModel.Api.Version);
    }
}
