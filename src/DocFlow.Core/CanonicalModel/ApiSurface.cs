namespace DocFlow.Core.CanonicalModel;

/// <summary>
/// API-level information layered onto the canonical <see cref="SemanticModel"/>.
/// Captures operations, parameters, request/response bodies, servers, security schemes and tags
/// so that downstream generators (documentation, diagrams, clients) can reason about the API surface
/// without re-parsing the source spec.
/// </summary>
public sealed record ApiSurface
{
    /// <summary>API title (e.g. "Petstore").</summary>
    public required string Title { get; init; }

    /// <summary>API version string from the source spec.</summary>
    public required string Version { get; init; }

    /// <summary>Human-readable description of the API.</summary>
    public string? Description { get; init; }

    /// <summary>Deployment targets declared by the spec.</summary>
    public IReadOnlyList<ApiServer> Servers { get; init; } = [];

    /// <summary>All operations keyed by <see cref="ApiOperation.OperationId"/>.</summary>
    public IReadOnlyList<ApiOperation> Operations { get; init; } = [];

    /// <summary>Tag definitions (the operations themselves carry tag names).</summary>
    public IReadOnlyList<ApiTag> Tags { get; init; } = [];

    /// <summary>Security schemes keyed by scheme name.</summary>
    public IReadOnlyDictionary<string, ApiSecurityScheme> SecuritySchemes { get; init; }
        = new Dictionary<string, ApiSecurityScheme>();

    /// <summary>
    /// Default security requirements applied when an operation does not declare its own.
    /// Each requirement in the list is OR'd; the schemes inside a single requirement are AND'd.
    /// </summary>
    public IReadOnlyList<ApiSecurityRequirement> SecurityRequirements { get; init; } = [];
}

/// <summary>A single API operation (method + path pair).</summary>
public sealed record ApiOperation
{
    /// <summary>
    /// Stable identifier for the operation. When the source spec omits <c>operationId</c>,
    /// parsers synthesize a deterministic id of the form <c>{method}_{path}</c>.
    /// </summary>
    public required string OperationId { get; init; }

    /// <summary>HTTP method for the operation.</summary>
    public required ApiHttpMethod Method { get; init; }

    /// <summary>URL path template (e.g. <c>/pets/{petId}</c>).</summary>
    public required string Path { get; init; }

    /// <summary>One-line summary.</summary>
    public string? Summary { get; init; }

    /// <summary>Longer description.</summary>
    public string? Description { get; init; }

    /// <summary>Tag names associated with the operation.</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>Parameters (path, query, header, cookie).</summary>
    public IReadOnlyList<ApiParameter> Parameters { get; init; } = [];

    /// <summary>Request body, if the operation accepts one.</summary>
    public ApiRequestBody? RequestBody { get; init; }

    /// <summary>Responses keyed by status code (or <c>"default"</c>).</summary>
    public IReadOnlyDictionary<string, ApiResponse> Responses { get; init; }
        = new Dictionary<string, ApiResponse>();

    /// <summary>
    /// Security requirements specific to this operation. When empty, the surface's
    /// default <see cref="ApiSurface.SecurityRequirements"/> apply.
    /// </summary>
    public IReadOnlyList<ApiSecurityRequirement> SecurityRequirements { get; init; } = [];

    /// <summary>True if the spec marks this operation as deprecated.</summary>
    public bool Deprecated { get; init; }
}

/// <summary>HTTP methods recognised by the canonical model.</summary>
public enum ApiHttpMethod
{
    Get,
    Put,
    Post,
    Delete,
    Options,
    Head,
    Patch,
    Trace
}

/// <summary>Where a parameter is bound in the HTTP request.</summary>
public enum ApiParameterLocation
{
    Query,
    Header,
    Path,
    Cookie
}

/// <summary>A single input parameter for an operation.</summary>
public sealed record ApiParameter
{
    public required string Name { get; init; }
    public required ApiParameterLocation Location { get; init; }
    public string? Description { get; init; }
    public bool Required { get; init; }
    public bool Deprecated { get; init; }

    /// <summary>The media shape of the parameter value.</summary>
    public ApiMediaType? Schema { get; init; }
}

/// <summary>Body payload accepted by an operation.</summary>
public sealed record ApiRequestBody
{
    public string? Description { get; init; }
    public bool Required { get; init; }

    /// <summary>Content keyed by media type (e.g. <c>application/json</c>).</summary>
    public IReadOnlyDictionary<string, ApiMediaType> Content { get; init; }
        = new Dictionary<string, ApiMediaType>();
}

/// <summary>A response variant keyed by status code on its containing operation.</summary>
public sealed record ApiResponse
{
    public required string Description { get; init; }

    /// <summary>Content keyed by media type.</summary>
    public IReadOnlyDictionary<string, ApiMediaType> Content { get; init; }
        = new Dictionary<string, ApiMediaType>();

    /// <summary>Response headers keyed by header name.</summary>
    public IReadOnlyDictionary<string, ApiParameter> Headers { get; init; }
        = new Dictionary<string, ApiParameter>();
}

/// <summary>
/// The payload shape for a given media type. Either <see cref="EntityName"/> references
/// a <see cref="SemanticEntity"/> by name, or <see cref="Schema"/> describes a primitive/inline shape.
/// At most one of the two is populated; both null means "untyped".
/// </summary>
public sealed record ApiMediaType
{
    /// <summary>Name of a <see cref="SemanticEntity"/> in the enclosing model.</summary>
    public string? EntityName { get; init; }

    /// <summary>Inline schema when the payload does not map to a named entity.</summary>
    public ApiSchema? Schema { get; init; }

    /// <summary>Literal example payload as a JSON string, if the spec supplied one.</summary>
    public string? Example { get; init; }
}

/// <summary>
/// A reduced schema descriptor for primitive or inline shapes. Named object schemas are
/// projected into <see cref="SemanticEntity"/> instances and referenced by name instead.
/// </summary>
public sealed record ApiSchema
{
    /// <summary>JSON schema type: <c>string</c>, <c>integer</c>, <c>number</c>, <c>boolean</c>, <c>array</c>, <c>object</c>.</summary>
    public required string Type { get; init; }

    /// <summary>Optional format hint (e.g. <c>int32</c>, <c>date-time</c>, <c>uuid</c>).</summary>
    public string? Format { get; init; }

    /// <summary>Element schema when <see cref="Type"/> is <c>array</c>.</summary>
    public ApiSchema? Items { get; init; }

    /// <summary>Enumeration of allowed values, stringified.</summary>
    public IReadOnlyList<string> Enum { get; init; } = [];

    /// <summary>Whether this schema is nullable.</summary>
    public bool Nullable { get; init; }

    /// <summary>
    /// Name of a <see cref="SemanticEntity"/> this schema resolves to. Populated for inline
    /// schemas that <c>$ref</c> a named component (e.g. an <c>array</c> whose items are a Pet),
    /// where the outer <see cref="ApiMediaType.EntityName"/> cannot carry the link.
    /// </summary>
    public string? EntityName { get; init; }
}

/// <summary>A deployment target for the API.</summary>
public sealed record ApiServer
{
    public required string Url { get; init; }
    public string? Description { get; init; }
}

/// <summary>A tag definition. Operations reference tags by name via <see cref="ApiOperation.Tags"/>.</summary>
public sealed record ApiTag
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

/// <summary>Kind of security scheme.</summary>
public enum ApiSecuritySchemeType
{
    ApiKey,
    Http,
    OAuth2,
    OpenIdConnect,
    MutualTls
}

/// <summary>A security scheme definition. Oauth2 schemes populate <see cref="Flows"/>.</summary>
public sealed record ApiSecurityScheme
{
    public required string Name { get; init; }
    public required ApiSecuritySchemeType Type { get; init; }
    public string? Description { get; init; }

    /// <summary>For <see cref="ApiSecuritySchemeType.ApiKey"/>: where the key is passed.</summary>
    public ApiParameterLocation? In { get; init; }

    /// <summary>For <see cref="ApiSecuritySchemeType.ApiKey"/>: the name of the header/query/cookie parameter.</summary>
    public string? ParameterName { get; init; }

    /// <summary>For <see cref="ApiSecuritySchemeType.Http"/>: the HTTP auth scheme (e.g. <c>bearer</c>, <c>basic</c>).</summary>
    public string? Scheme { get; init; }

    /// <summary>For HTTP bearer schemes: hints at the bearer token format (e.g. <c>JWT</c>).</summary>
    public string? BearerFormat { get; init; }

    /// <summary>For <see cref="ApiSecuritySchemeType.OpenIdConnect"/>: the discovery URL.</summary>
    public string? OpenIdConnectUrl { get; init; }

    /// <summary>For <see cref="ApiSecuritySchemeType.OAuth2"/>: flows keyed by flow type name.</summary>
    public IReadOnlyDictionary<string, ApiSecurityFlow> Flows { get; init; }
        = new Dictionary<string, ApiSecurityFlow>();
}

/// <summary>A single OAuth2 flow (authorization_code, client_credentials, implicit, password).</summary>
public sealed record ApiSecurityFlow
{
    public string? AuthorizationUrl { get; init; }
    public string? TokenUrl { get; init; }
    public string? RefreshUrl { get; init; }

    /// <summary>Scopes advertised by this flow keyed by scope name, values are descriptions.</summary>
    public IReadOnlyDictionary<string, string> Scopes { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// A security requirement. The <see cref="Schemes"/> map names referenced schemes to the scopes
/// required on them. An operation accepts any single requirement in its list (OR), but every
/// scheme within the requirement must be satisfied (AND).
/// </summary>
public sealed record ApiSecurityRequirement
{
    public required IReadOnlyDictionary<string, IReadOnlyList<string>> Schemes { get; init; }
}
