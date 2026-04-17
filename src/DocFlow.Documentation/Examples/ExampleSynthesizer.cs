using System.Text;
using DocFlow.Core.CanonicalModel;

namespace DocFlow.Documentation.Examples;

/// <summary>
/// Produces a JSON payload that illustrates an <see cref="ApiMediaType"/>.
/// Prefers a spec-provided example (<see cref="ApiMediaType.Example"/>) when available;
/// otherwise synthesizes a payload from the schema / referenced <see cref="SemanticEntity"/>.
///
/// Synthesis rules:
/// <list type="bullet">
/// <item><description><c>string</c> → first enum value if constrained, else <c>"string"</c> (or ISO-8601 placeholder for <c>date-time</c>).</description></item>
/// <item><description><c>integer</c>/<c>number</c> → <c>0</c>.</description></item>
/// <item><description><c>boolean</c> → <c>false</c>.</description></item>
/// <item><description><c>array</c> → single-element array of the item type.</description></item>
/// <item><description><c>object</c>/entity → all properties emitted; cycles terminate with <c>"..."</c>.</description></item>
/// </list>
/// </summary>
public sealed class ExampleSynthesizer
{
    private readonly IReadOnlyDictionary<string, SemanticEntity> _entitiesByName;

    public ExampleSynthesizer(SemanticModel model)
    {
        _entitiesByName = model.Entities.Values
            .GroupBy(e => e.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
    }

    /// <summary>
    /// Returns a JSON string illustrating <paramref name="media"/>, or null when nothing useful
    /// can be produced (neither a spec example nor a recognisable schema).
    /// </summary>
    public string? Synthesize(ApiMediaType media)
    {
        if (!string.IsNullOrWhiteSpace(media.Example))
        {
            return media.Example.Trim();
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(media.EntityName) && _entitiesByName.TryGetValue(media.EntityName, out var entity))
        {
            return SynthesizeEntity(entity, visited);
        }

        return media.Schema is null ? null : SynthesizeSchema(media.Schema, visited);
    }

    private string SynthesizeEntity(SemanticEntity entity, HashSet<string> visited)
    {
        if (!visited.Add(entity.Name))
        {
            return "\"...\"";
        }

        try
        {
            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var property in entity.Properties
                         .OrderBy(p => p.Name, StringComparer.Ordinal))
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append($"\"{Escape(property.Name)}\": ");
                sb.Append(SynthesizeProperty(property, visited));
            }
            sb.Append('}');
            return sb.ToString();
        }
        finally
        {
            visited.Remove(entity.Name);
        }
    }

    private string SynthesizeProperty(SemanticProperty property, HashSet<string> visited)
    {
        // Collection navigation property → array of element type.
        if (property.Type.IsCollection && property.Type.GenericArguments.Count > 0)
        {
            var elementName = property.Type.GenericArguments[0].Name;
            return $"[{SynthesizeByTypeName(elementName, visited)}]";
        }

        return SynthesizeByTypeName(property.Type.Name, visited);
    }

    private string SynthesizeByTypeName(string typeName, HashSet<string> visited)
    {
        if (_entitiesByName.TryGetValue(typeName, out var entity))
        {
            return SynthesizeEntity(entity, visited);
        }

        return typeName.ToLowerInvariant() switch
        {
            "string" => "\"string\"",
            "int" or "long" or "integer" or "short" or "byte" => "0",
            "decimal" or "double" or "float" or "number" => "0",
            "bool" or "boolean" => "false",
            "datetime" => "\"2026-01-01T00:00:00Z\"",
            "dateonly" or "date" => "\"2026-01-01\"",
            "timespan" or "time" => "\"00:00:00\"",
            "guid" or "uuid" => "\"00000000-0000-0000-0000-000000000000\"",
            _ => "\"string\""
        };
    }

    private string SynthesizeSchema(ApiSchema schema, HashSet<string> visited)
    {
        // A schema that resolves to a named entity
        if (!string.IsNullOrEmpty(schema.EntityName) && _entitiesByName.TryGetValue(schema.EntityName, out var entity))
        {
            return SynthesizeEntity(entity, visited);
        }

        return schema.Type.ToLowerInvariant() switch
        {
            "string" when schema.Enum.Count > 0 => $"\"{Escape(schema.Enum[0])}\"",
            "string" when string.Equals(schema.Format, "date-time", StringComparison.OrdinalIgnoreCase) => "\"2026-01-01T00:00:00Z\"",
            "string" when string.Equals(schema.Format, "date", StringComparison.OrdinalIgnoreCase) => "\"2026-01-01\"",
            "string" when string.Equals(schema.Format, "uuid", StringComparison.OrdinalIgnoreCase) => "\"00000000-0000-0000-0000-000000000000\"",
            "string" => "\"string\"",
            "integer" => "0",
            "number" => "0",
            "boolean" => "false",
            "array" when schema.Items is not null => $"[{SynthesizeSchema(schema.Items, visited)}]",
            "array" => "[]",
            "object" => "{}",
            _ => "null"
        };
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
