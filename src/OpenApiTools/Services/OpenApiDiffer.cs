using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiDiffer
{
    IReadOnlyList<DiffChange> Diff(OpenApiDocument oldDoc, OpenApiDocument newDoc);
}

public sealed class OpenApiDiffer : IOpenApiDiffer
{
    public IReadOnlyList<DiffChange> Diff(OpenApiDocument oldDoc, OpenApiDocument newDoc)
    {
        var changes = new List<DiffChange>();

        DiffPaths(oldDoc, newDoc, changes);
        DiffSchemas(oldDoc, newDoc, changes);
        DiffSecuritySchemes(oldDoc, newDoc, changes);

        return changes;
    }

    private static void DiffPaths(OpenApiDocument oldDoc, OpenApiDocument newDoc, List<DiffChange> changes)
    {
        var oldPaths = oldDoc.Paths ?? new OpenApiPaths();
        var newPaths = newDoc.Paths ?? new OpenApiPaths();

        foreach (var (path, newItem) in newPaths)
        {
            if (!oldPaths.TryGetValue(path, out var oldItem))
            {
                changes.Add(new DiffChange(
                    DiffChangeType.Added, DiffImpact.NonBreaking, DiffCategory.Path,
                    path, "Path added."));

                foreach (var (method, _) in newItem.Operations)
                    changes.Add(new DiffChange(
                        DiffChangeType.Added, DiffImpact.NonBreaking, DiffCategory.Operation,
                        $"{method.ToString().ToUpperInvariant()} {path}", "Operation added."));
                continue;
            }

            DiffOperations(path, oldItem, newItem, changes);
        }

        foreach (var (path, oldItem) in oldPaths)
        {
            if (!newPaths.ContainsKey(path))
            {
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.Path,
                    path, "Path removed."));

                foreach (var (method, _) in oldItem.Operations)
                    changes.Add(new DiffChange(
                        DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.Operation,
                        $"{method.ToString().ToUpperInvariant()} {path}", "Operation removed."));
            }
        }
    }

    private static void DiffOperations(string path, OpenApiPathItem oldItem, OpenApiPathItem newItem, List<DiffChange> changes)
    {
        var oldOps = oldItem.Operations;
        var newOps = newItem.Operations;

        foreach (var (method, newOp) in newOps)
        {
            var methodStr = method.ToString().ToUpperInvariant();
            var loc = $"{methodStr} {path}";

            if (!oldOps.TryGetValue(method, out var oldOp))
            {
                changes.Add(new DiffChange(
                    DiffChangeType.Added, DiffImpact.NonBreaking, DiffCategory.Operation,
                    loc, "Operation added."));
                continue;
            }

            DiffParameters(loc, oldOp, newOp, changes);
            DiffRequestBody(loc, oldOp, newOp, changes);
            DiffResponses(loc, oldOp, newOp, changes);
        }

        foreach (var (method, _) in oldOps)
        {
            if (!newOps.ContainsKey(method))
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.Operation,
                    $"{method.ToString().ToUpperInvariant()} {path}", "Operation removed."));
        }
    }

    private static void DiffParameters(string loc, OpenApiOperation oldOp, OpenApiOperation newOp, List<DiffChange> changes)
    {
        var oldParams = (oldOp.Parameters ?? []).Where(p => p.In is not null).ToDictionary(p => $"{p.In}:{p.Name}");
        var newParams = (newOp.Parameters ?? []).Where(p => p.In is not null).ToDictionary(p => $"{p.In}:{p.Name}");

        foreach (var (key, newParam) in newParams)
        {
            if (!oldParams.TryGetValue(key, out var oldParam))
            {
                var impact = newParam.Required ? DiffImpact.Breaking : DiffImpact.NonBreaking;
                changes.Add(new DiffChange(
                    DiffChangeType.Added, impact, DiffCategory.Parameter,
                    $"{loc} param:{key}",
                    newParam.Required ? "Required parameter added (breaking)." : "Optional parameter added."));
                continue;
            }

            if (oldParam.Required && !newParam.Required)
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.NonBreaking, DiffCategory.Parameter,
                    $"{loc} param:{key}", "Parameter changed from required to optional."));

            if (!oldParam.Required && newParam.Required)
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.Breaking, DiffCategory.Parameter,
                    $"{loc} param:{key}", "Parameter changed from optional to required (breaking)."));

            var oldType = oldParam.Schema?.Type;
            var newType = newParam.Schema?.Type;
            if (oldType != newType && oldType is not null && newType is not null)
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.Breaking, DiffCategory.Parameter,
                    $"{loc} param:{key}", $"Parameter type changed from '{oldType}' to '{newType}' (breaking)."));
        }

        foreach (var (key, oldParam) in oldParams)
        {
            if (!newParams.ContainsKey(key) && oldParam.Required)
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.Parameter,
                    $"{loc} param:{key}", "Required parameter removed (breaking)."));
            else if (!newParams.ContainsKey(key))
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.NonBreaking, DiffCategory.Parameter,
                    $"{loc} param:{key}", "Optional parameter removed."));
        }
    }

    private static void DiffRequestBody(string loc, OpenApiOperation oldOp, OpenApiOperation newOp, List<DiffChange> changes)
    {
        if (oldOp.RequestBody is null && newOp.RequestBody is not null)
        {
            var impact = newOp.RequestBody.Required ? DiffImpact.Breaking : DiffImpact.NonBreaking;
            changes.Add(new DiffChange(
                DiffChangeType.Added, impact, DiffCategory.RequestBody,
                $"{loc} body", "Request body added."));
        }
        else if (oldOp.RequestBody is not null && newOp.RequestBody is null)
        {
            var impact = oldOp.RequestBody.Required ? DiffImpact.Breaking : DiffImpact.NonBreaking;
            changes.Add(new DiffChange(
                DiffChangeType.Removed, impact, DiffCategory.RequestBody,
                $"{loc} body", "Request body removed."));
        }
        else if (oldOp.RequestBody is not null && newOp.RequestBody is not null)
        {
            if (oldOp.RequestBody.Required && !newOp.RequestBody.Required)
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.NonBreaking, DiffCategory.RequestBody,
                    $"{loc} body", "Request body changed from required to optional."));

            if (!oldOp.RequestBody.Required && newOp.RequestBody.Required)
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.Breaking, DiffCategory.RequestBody,
                    $"{loc} body", "Request body changed from optional to required (breaking)."));
        }
    }

    private static void DiffResponses(string loc, OpenApiOperation oldOp, OpenApiOperation newOp, List<DiffChange> changes)
    {
        var oldResponses = oldOp.Responses ?? new Dictionary<string, OpenApiResponse>();
        var newResponses = newOp.Responses ?? new Dictionary<string, OpenApiResponse>();

        foreach (var (code, _) in newResponses)
        {
            if (!oldResponses.ContainsKey(code))
                changes.Add(new DiffChange(
                    DiffChangeType.Added, DiffImpact.NonBreaking, DiffCategory.Response,
                    $"{loc} {code}", "Response added."));
        }

        foreach (var (code, _) in oldResponses)
        {
            if (!newResponses.ContainsKey(code))
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.NonBreaking, DiffCategory.Response,
                    $"{loc} {code}", "Response removed."));
        }
    }

    private static void DiffSchemas(OpenApiDocument oldDoc, OpenApiDocument newDoc, List<DiffChange> changes)
    {
        var oldSchemas = oldDoc.Components?.Schemas ?? new Dictionary<string, OpenApiSchema>();
        var newSchemas = newDoc.Components?.Schemas ?? new Dictionary<string, OpenApiSchema>();

        foreach (var (name, newSchema) in newSchemas)
        {
            if (!oldSchemas.TryGetValue(name, out var oldSchema))
            {
                changes.Add(new DiffChange(
                    DiffChangeType.Added, DiffImpact.NonBreaking, DiffCategory.Schema,
                    $"#/components/schemas/{name}", "Schema added."));
                continue;
            }

            if (oldSchema.Type != newSchema.Type && oldSchema.Type is not null && newSchema.Type is not null)
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.Breaking, DiffCategory.Schema,
                    $"#/components/schemas/{name}", $"Schema type changed from '{oldSchema.Type}' to '{newSchema.Type}' (breaking)."));

            var oldRequired = oldSchema.Required ?? new HashSet<string>();
            var newRequired = newSchema.Required ?? new HashSet<string>();

            foreach (var req in newRequired.Except(oldRequired))
                changes.Add(new DiffChange(
                    DiffChangeType.Modified, DiffImpact.Breaking, DiffCategory.Schema,
                    $"#/components/schemas/{name}", $"Property '{req}' became required (breaking)."));

            var oldProps = oldSchema.Properties ?? new Dictionary<string, OpenApiSchema>();
            var newProps = newSchema.Properties ?? new Dictionary<string, OpenApiSchema>();

            foreach (var propName in oldProps.Keys.Intersect(newProps.Keys))
            {
                var oldType = oldProps[propName].Type;
                var newType = newProps[propName].Type;
                if (oldType != newType && oldType is not null && newType is not null)
                    changes.Add(new DiffChange(
                        DiffChangeType.Modified, DiffImpact.Breaking, DiffCategory.Schema,
                        $"#/components/schemas/{name}/{propName}",
                        $"Property '{propName}' type changed from '{oldType}' to '{newType}' (breaking)."));
            }

            foreach (var propName in oldProps.Keys.Except(newProps.Keys))
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.Schema,
                    $"#/components/schemas/{name}/{propName}", "Property removed (breaking)."));
        }

        foreach (var (name, _) in oldSchemas)
        {
            if (!newSchemas.ContainsKey(name))
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.Schema,
                    $"#/components/schemas/{name}", "Schema removed (breaking)."));
        }
    }

    private static void DiffSecuritySchemes(OpenApiDocument oldDoc, OpenApiDocument newDoc, List<DiffChange> changes)
    {
        var oldSchemes = oldDoc.Components?.SecuritySchemes ?? new Dictionary<string, OpenApiSecurityScheme>();
        var newSchemes = newDoc.Components?.SecuritySchemes ?? new Dictionary<string, OpenApiSecurityScheme>();

        foreach (var (name, _) in newSchemes)
        {
            if (!oldSchemes.ContainsKey(name))
                changes.Add(new DiffChange(
                    DiffChangeType.Added, DiffImpact.NonBreaking, DiffCategory.SecurityScheme,
                    $"#/components/securitySchemes/{name}", "Security scheme added."));
        }

        foreach (var (name, _) in oldSchemes)
        {
            if (!newSchemes.ContainsKey(name))
                changes.Add(new DiffChange(
                    DiffChangeType.Removed, DiffImpact.Breaking, DiffCategory.SecurityScheme,
                    $"#/components/securitySchemes/{name}", "Security scheme removed (breaking)."));
        }
    }
}