using DocFlow.Core.CanonicalModel;

namespace DocFlow.Documentation.Diff;

/// <summary>
/// Produces a <see cref="SpecDiff"/> between two <see cref="SemanticModel"/> instances.
/// Heuristics (industry conventions):
/// <list type="bullet">
/// <item><description>Operations: added → non-breaking; removed → breaking.</description></item>
/// <item><description>Parameters: required added → breaking; optional added → non-breaking; removed → breaking; type change → breaking; required flag flip true→false → non-breaking, false→true → breaking.</description></item>
/// <item><description>Schemas (entities): added → non-breaking; removed → breaking; property added-required → breaking; property added-optional → non-breaking; property removed → breaking; type change → breaking; required flag flip same as parameters.</description></item>
/// <item><description>Request/response content-entity rename on an operation → breaking.</description></item>
/// </list>
/// </summary>
public sealed class SpecDiffer
{
    public SpecDiff Diff(SemanticModel oldModel, SemanticModel newModel)
    {
        var changes = new List<SpecChange>();
        DiffOperations(oldModel.Api, newModel.Api, changes);
        DiffEntities(oldModel, newModel, changes);

        var ordered = changes
            .OrderBy(c => c.Severity)
            .ThenBy(c => c.Category)
            .ThenBy(c => c.Path, StringComparer.Ordinal)
            .ThenBy(c => c.Description, StringComparer.Ordinal)
            .ToList();

        return new SpecDiff { Changes = ordered };
    }

    private static void DiffOperations(ApiSurface? oldApi, ApiSurface? newApi, List<SpecChange> changes)
    {
        var oldOps = (oldApi?.Operations ?? [])
            .ToDictionary(o => o.OperationId, StringComparer.Ordinal);
        var newOps = (newApi?.Operations ?? [])
            .ToDictionary(o => o.OperationId, StringComparer.Ordinal);

        foreach (var added in newOps.Keys.Except(oldOps.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Operation,
                Severity = ChangeSeverity.NonBreaking,
                Description = $"Added operation `{added}`",
                Path = added
            });
        }

        foreach (var removed in oldOps.Keys.Except(newOps.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Operation,
                Severity = ChangeSeverity.Breaking,
                Description = $"Removed operation `{removed}`",
                Path = removed
            });
        }

        foreach (var id in oldOps.Keys.Intersect(newOps.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            DiffOperation(oldOps[id], newOps[id], changes);
        }
    }

    private static void DiffOperation(ApiOperation oldOp, ApiOperation newOp, List<SpecChange> changes)
    {
        if (oldOp.Method != newOp.Method)
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Operation,
                Severity = ChangeSeverity.Breaking,
                Description = $"Changed HTTP method of `{oldOp.OperationId}` from {oldOp.Method} to {newOp.Method}",
                Path = oldOp.OperationId
            });
        }

        if (!string.Equals(oldOp.Path, newOp.Path, StringComparison.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Operation,
                Severity = ChangeSeverity.Breaking,
                Description = $"Changed path of `{oldOp.OperationId}` from `{oldOp.Path}` to `{newOp.Path}`",
                Path = oldOp.OperationId
            });
        }

        DiffParameters(oldOp, newOp, changes);
        DiffRequestBody(oldOp, newOp, changes);
        DiffResponses(oldOp, newOp, changes);
    }

    private static void DiffParameters(ApiOperation oldOp, ApiOperation newOp, List<SpecChange> changes)
    {
        var oldByKey = oldOp.Parameters.ToDictionary(p => (p.Name, p.Location));
        var newByKey = newOp.Parameters.ToDictionary(p => (p.Name, p.Location));

        foreach (var key in newByKey.Keys.Except(oldByKey.Keys).OrderBy(k => k.Name, StringComparer.Ordinal))
        {
            var param = newByKey[key];
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Parameter,
                Severity = param.Required ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                Description = $"Added {(param.Required ? "required" : "optional")} {param.Location.ToString().ToLowerInvariant()} parameter `{param.Name}` to `{oldOp.OperationId}`",
                Path = $"{oldOp.OperationId}.{param.Name}"
            });
        }

        foreach (var key in oldByKey.Keys.Except(newByKey.Keys).OrderBy(k => k.Name, StringComparer.Ordinal))
        {
            var param = oldByKey[key];
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Parameter,
                Severity = ChangeSeverity.Breaking,
                Description = $"Removed {param.Location.ToString().ToLowerInvariant()} parameter `{param.Name}` from `{oldOp.OperationId}`",
                Path = $"{oldOp.OperationId}.{param.Name}"
            });
        }

        foreach (var key in oldByKey.Keys.Intersect(newByKey.Keys).OrderBy(k => k.Name, StringComparer.Ordinal))
        {
            var oldP = oldByKey[key];
            var newP = newByKey[key];

            if (oldP.Required != newP.Required)
            {
                changes.Add(new SpecChange
                {
                    Category = ChangeCategory.Parameter,
                    Severity = newP.Required ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                    Description = $"Parameter `{oldP.Name}` on `{oldOp.OperationId}` is now {(newP.Required ? "required" : "optional")} (was {(oldP.Required ? "required" : "optional")})",
                    Path = $"{oldOp.OperationId}.{oldP.Name}"
                });
            }

            var oldType = DescribeSchemaType(oldP.Schema);
            var newType = DescribeSchemaType(newP.Schema);
            if (!string.Equals(oldType, newType, StringComparison.Ordinal))
            {
                changes.Add(new SpecChange
                {
                    Category = ChangeCategory.Parameter,
                    Severity = ChangeSeverity.Breaking,
                    Description = $"Changed type of parameter `{oldP.Name}` on `{oldOp.OperationId}` from `{oldType}` to `{newType}`",
                    Path = $"{oldOp.OperationId}.{oldP.Name}"
                });
            }
        }
    }

    private static void DiffRequestBody(ApiOperation oldOp, ApiOperation newOp, List<SpecChange> changes)
    {
        var oldEntity = FirstEntityName(oldOp.RequestBody?.Content);
        var newEntity = FirstEntityName(newOp.RequestBody?.Content);

        if (oldEntity is null && newEntity is not null)
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.RequestBody,
                Severity = (newOp.RequestBody?.Required ?? false) ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                Description = $"Added request body `{newEntity}` to `{oldOp.OperationId}`",
                Path = oldOp.OperationId
            });
        }
        else if (oldEntity is not null && newEntity is null)
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.RequestBody,
                Severity = ChangeSeverity.Breaking,
                Description = $"Removed request body from `{oldOp.OperationId}`",
                Path = oldOp.OperationId
            });
        }
        else if (oldEntity is not null && newEntity is not null
                 && !string.Equals(oldEntity, newEntity, StringComparison.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.RequestBody,
                Severity = ChangeSeverity.Breaking,
                Description = $"Request body of `{oldOp.OperationId}` changed from `{oldEntity}` to `{newEntity}`",
                Path = oldOp.OperationId
            });
        }
    }

    private static void DiffResponses(ApiOperation oldOp, ApiOperation newOp, List<SpecChange> changes)
    {
        var oldStatuses = oldOp.Responses.Keys.ToHashSet(StringComparer.Ordinal);
        var newStatuses = newOp.Responses.Keys.ToHashSet(StringComparer.Ordinal);

        foreach (var added in newStatuses.Except(oldStatuses).OrderBy(s => s, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Response,
                Severity = ChangeSeverity.NonBreaking,
                Description = $"Added response `{added}` to `{oldOp.OperationId}`",
                Path = $"{oldOp.OperationId}:{added}"
            });
        }

        foreach (var removed in oldStatuses.Except(newStatuses).OrderBy(s => s, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Response,
                Severity = ChangeSeverity.Breaking,
                Description = $"Removed response `{removed}` from `{oldOp.OperationId}`",
                Path = $"{oldOp.OperationId}:{removed}"
            });
        }

        foreach (var status in oldStatuses.Intersect(newStatuses).OrderBy(s => s, StringComparer.Ordinal))
        {
            var oldEntity = FirstEntityName(oldOp.Responses[status].Content);
            var newEntity = FirstEntityName(newOp.Responses[status].Content);
            if (oldEntity is not null && newEntity is not null
                && !string.Equals(oldEntity, newEntity, StringComparison.Ordinal))
            {
                changes.Add(new SpecChange
                {
                    Category = ChangeCategory.Response,
                    Severity = ChangeSeverity.Breaking,
                    Description = $"Response `{status}` of `{oldOp.OperationId}` changed from `{oldEntity}` to `{newEntity}`",
                    Path = $"{oldOp.OperationId}:{status}"
                });
            }
        }
    }

    private static void DiffEntities(SemanticModel oldModel, SemanticModel newModel, List<SpecChange> changes)
    {
        var oldEntities = oldModel.Entities.Values
            .GroupBy(e => e.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var newEntities = newModel.Entities.Values
            .GroupBy(e => e.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var added in newEntities.Keys.Except(oldEntities.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Schema,
                Severity = ChangeSeverity.NonBreaking,
                Description = $"Added schema `{added}`",
                Path = added
            });
        }

        foreach (var removed in oldEntities.Keys.Except(newEntities.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Schema,
                Severity = ChangeSeverity.Breaking,
                Description = $"Removed schema `{removed}`",
                Path = removed
            });
        }

        foreach (var name in oldEntities.Keys.Intersect(newEntities.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            DiffEntityProperties(oldEntities[name], newEntities[name], changes);
        }
    }

    private static void DiffEntityProperties(SemanticEntity oldEntity, SemanticEntity newEntity, List<SpecChange> changes)
    {
        var oldProps = oldEntity.Properties
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var newProps = newEntity.Properties
            .GroupBy(p => p.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        foreach (var added in newProps.Keys.Except(oldProps.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            var prop = newProps[added];
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Schema,
                Severity = prop.IsRequired ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                Description = $"Added {(prop.IsRequired ? "required" : "optional")} property `{oldEntity.Name}.{added}`",
                Path = $"{oldEntity.Name}.{added}"
            });
        }

        foreach (var removed in oldProps.Keys.Except(newProps.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            changes.Add(new SpecChange
            {
                Category = ChangeCategory.Schema,
                Severity = ChangeSeverity.Breaking,
                Description = $"Removed property `{oldEntity.Name}.{removed}`",
                Path = $"{oldEntity.Name}.{removed}"
            });
        }

        foreach (var name in oldProps.Keys.Intersect(newProps.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
        {
            var oldP = oldProps[name];
            var newP = newProps[name];

            if (!string.Equals(oldP.Type.Name, newP.Type.Name, StringComparison.Ordinal))
            {
                changes.Add(new SpecChange
                {
                    Category = ChangeCategory.Schema,
                    Severity = ChangeSeverity.Breaking,
                    Description = $"Changed type of `{oldEntity.Name}.{name}` from `{oldP.Type.Name}` to `{newP.Type.Name}`",
                    Path = $"{oldEntity.Name}.{name}"
                });
            }

            if (oldP.IsRequired != newP.IsRequired)
            {
                changes.Add(new SpecChange
                {
                    Category = ChangeCategory.Schema,
                    Severity = newP.IsRequired ? ChangeSeverity.Breaking : ChangeSeverity.NonBreaking,
                    Description = $"Property `{oldEntity.Name}.{name}` is now {(newP.IsRequired ? "required" : "optional")} (was {(oldP.IsRequired ? "required" : "optional")})",
                    Path = $"{oldEntity.Name}.{name}"
                });
            }
        }
    }

    private static string? FirstEntityName(IReadOnlyDictionary<string, ApiMediaType>? content)
    {
        if (content is null || content.Count == 0) return null;
        var first = content.OrderBy(c => c.Key, StringComparer.Ordinal).First().Value;
        if (!string.IsNullOrEmpty(first.EntityName)) return first.EntityName;
        if (!string.IsNullOrEmpty(first.Schema?.EntityName)) return first.Schema!.EntityName;
        if (first.Schema?.Type == "array" && !string.IsNullOrEmpty(first.Schema.Items?.EntityName))
        {
            return $"array<{first.Schema.Items!.EntityName}>";
        }
        return null;
    }

    private static string DescribeSchemaType(ApiMediaType? media)
    {
        if (media is null) return "unknown";
        if (!string.IsNullOrEmpty(media.EntityName)) return media.EntityName;
        var schema = media.Schema;
        if (schema is null) return "unknown";
        if (schema.Type == "array" && schema.Items is not null)
        {
            var inner = !string.IsNullOrEmpty(schema.Items.EntityName) ? schema.Items.EntityName : schema.Items.Type;
            return $"array<{inner}>";
        }
        if (!string.IsNullOrEmpty(schema.EntityName)) return schema.EntityName;
        return string.IsNullOrEmpty(schema.Format) ? schema.Type : $"{schema.Type}({schema.Format})";
    }
}
