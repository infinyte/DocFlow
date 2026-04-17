using DocFlow.Core.Abstractions;
using DocFlow.Core.CanonicalModel;
using DocFlow.Integration.Schemas.OpenApi;
using Xunit;

namespace DocFlow.Integration.Tests.Schemas;

public class OpenApiParserApiSurfaceTests
{
    private const string PetstoreJsonPath = "Fixtures/petstore.json";

    private readonly OpenApiParser _parser = new();

    [Fact]
    public async Task Parse_Petstore_ProducesApiSurface()
    {
        var result = await _parser.ParseAsync(ParserInput.FromFile(PetstoreJsonPath));

        Assert.True(result.Success);
        Assert.NotNull(result.Model.Api);

        var api = result.Model.Api!;
        Assert.Equal("Petstore API", api.Title);
        Assert.Equal("1.0.0", api.Version);
        Assert.NotEmpty(api.Operations);
        Assert.NotEmpty(api.Servers);

        // Every documented tag appears on at least one operation. (If the spec has no tags,
        // this is vacuously true — the assertion still holds.)
        foreach (var tag in api.Tags)
        {
            Assert.Contains(api.Operations, op => op.Tags.Contains(tag.Name));
        }
    }

    [Fact]
    public async Task Parse_Petstore_OperationIds_MatchSpec()
    {
        var result = await _parser.ParseAsync(ParserInput.FromFile(PetstoreJsonPath));

        var api = result.Model.Api!;
        var ids = api.Operations.Select(o => o.OperationId).ToHashSet();

        // These four operationIds are declared explicitly in samples/integration-demos/petstore.json.
        Assert.Contains("listPets", ids);
        Assert.Contains("createPet", ids);
        Assert.Contains("getPetById", ids);
        Assert.Contains("placeOrder", ids);
    }

    [Fact]
    public async Task Parse_Petstore_Responses_LinkToEntities()
    {
        var result = await _parser.ParseAsync(ParserInput.FromFile(PetstoreJsonPath));

        var api = result.Model.Api!;
        var getPet = api.Operations.Single(o => o.OperationId == "getPetById");
        var ok = getPet.Responses["200"];

        var jsonMedia = ok.Content["application/json"];
        Assert.Equal("Pet", jsonMedia.EntityName);
    }

    [Fact]
    public async Task Parse_YamlSpec_IsEquivalentToJson()
    {
        const string yaml = """
            openapi: 3.0.3
            info:
              title: YAML Pet
              version: '1.0'
            paths:
              /pets:
                get:
                  operationId: listPets
                  responses:
                    '200':
                      description: ok
                      content:
                        application/json:
                          schema:
                            $ref: '#/components/schemas/Pet'
            components:
              schemas:
                Pet:
                  type: object
                  properties:
                    id:
                      type: integer
                    name:
                      type: string
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(yaml));

        Assert.True(result.Success, string.Join("; ", result.Errors.Select(e => e.Message)));
        Assert.NotNull(result.Model.Api);
        var api = result.Model.Api!;
        Assert.Equal("YAML Pet", api.Title);
        Assert.Contains(api.Operations, o => o.OperationId == "listPets");
        Assert.Equal("Pet", api.Operations.Single().Responses["200"].Content["application/json"].EntityName);
    }

    [Fact]
    public async Task Parse_SecuritySchemes_IncludeOAuthFlows()
    {
        const string json = """
            {
              "openapi": "3.0.3",
              "info": { "title": "OAuth API", "version": "1.0" },
              "paths": {},
              "components": {
                "securitySchemes": {
                  "oauth2": {
                    "type": "oauth2",
                    "flows": {
                      "authorizationCode": {
                        "authorizationUrl": "https://auth.example/auth",
                        "tokenUrl": "https://auth.example/token",
                        "scopes": { "read": "Read things", "write": "Write things" }
                      },
                      "clientCredentials": {
                        "tokenUrl": "https://auth.example/token",
                        "scopes": { "admin": "Admin access" }
                      }
                    }
                  }
                }
              }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(json));

        Assert.True(result.Success);
        var api = result.Model.Api!;
        Assert.True(api.SecuritySchemes.ContainsKey("oauth2"));
        var oauth = api.SecuritySchemes["oauth2"];
        Assert.Equal(ApiSecuritySchemeType.OAuth2, oauth.Type);
        Assert.True(oauth.Flows.ContainsKey("authorizationCode"));
        Assert.True(oauth.Flows.ContainsKey("clientCredentials"));
        Assert.Equal("https://auth.example/auth", oauth.Flows["authorizationCode"].AuthorizationUrl);
        Assert.Contains("read", oauth.Flows["authorizationCode"].Scopes.Keys);
    }

    [Fact]
    public async Task Parse_MissingOperationId_GeneratesDeterministicId()
    {
        const string json = """
            {
              "openapi": "3.0.3",
              "info": { "title": "No Ids", "version": "1.0" },
              "paths": {
                "/pets/{petId}": {
                  "get": {
                    "responses": { "200": { "description": "ok" } }
                  }
                }
              }
            }
            """;

        var result = await _parser.ParseAsync(ParserInput.FromContent(json));

        Assert.True(result.Success);
        var api = result.Model.Api!;
        var op = Assert.Single(api.Operations);

        // Format is {method}_{path} with non-alphanumerics collapsed to underscores and lowercased.
        Assert.Equal("get_pets_petid", op.OperationId);
    }
}
