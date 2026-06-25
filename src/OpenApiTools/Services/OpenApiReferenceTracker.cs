using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace OpenApiTools.Services;

public sealed record ComponentRef(ComponentType Type, string Name);

public enum ComponentType
{
    Schema,
    Parameter,
    Response,
    RequestBody,
    SecurityScheme,
    Header,
    Example,
    Link,
    Callback,
}

public interface IOpenApiReferenceTracker
{
    IReadOnlySet<ComponentRef> FindReferencedComponents(OpenApiDocument document);
}

public sealed class OpenApiReferenceTracker : IOpenApiReferenceTracker
{
    public IReadOnlySet<ComponentRef> FindReferencedComponents(OpenApiDocument document)
    {
        var referenced = new HashSet<ComponentRef>();

        foreach (var (_, item) in document.Paths)
            CollectFromPathItem(item, referenced);

        if (document.Components is not null)
            CollectFromComponents(document.Components, referenced);

        return referenced;
    }

    private static void CollectFromPathItem(OpenApiPathItem item, HashSet<ComponentRef> refs)
    {
        foreach (var param in item.Parameters ?? [])
            CollectFromParameter(param, refs);

        foreach (var (_, op) in item.Operations)
            CollectFromOperation(op, refs);
    }

    private static void CollectFromOperation(OpenApiOperation op, HashSet<ComponentRef> refs)
    {
        foreach (var param in op.Parameters ?? [])
            CollectFromParameter(param, refs);

        if (op.RequestBody is not null)
            CollectFromRequestBody(op.RequestBody, refs);

        if (op.Responses is not null)
            foreach (var (_, resp) in op.Responses)
                CollectFromResponse(resp, refs);

        foreach (var sec in op.Security ?? [])
            foreach (var (scheme, _) in sec)
                TryAddRef(scheme, ComponentType.SecurityScheme, refs);
    }

    private static void CollectFromComponents(OpenApiComponents components, HashSet<ComponentRef> refs)
    {
        foreach (var (_, schema) in components.Schemas ?? new Dictionary<string, OpenApiSchema>())
            CollectFromSchema(schema, refs, isRoot: true);
        foreach (var (_, param) in components.Parameters ?? new Dictionary<string, OpenApiParameter>())
            CollectFromParameter(param, refs, isRoot: true);
        foreach (var (_, resp) in components.Responses ?? new Dictionary<string, OpenApiResponse>())
            CollectFromResponse(resp, refs, isRoot: true);
        foreach (var (_, body) in components.RequestBodies ?? new Dictionary<string, OpenApiRequestBody>())
            CollectFromRequestBody(body, refs, isRoot: true);
    }

    private static void CollectFromParameter(OpenApiParameter param, HashSet<ComponentRef> refs, bool isRoot = false)
    {
        if (!isRoot)
            TryAddRef(param, ComponentType.Parameter, refs);
        if (param.Schema is not null)
            CollectFromSchema(param.Schema, refs);
        foreach (var (_, example) in param.Examples ?? new Dictionary<string, OpenApiExample>())
            TryAddRef(example, ComponentType.Example, refs);
    }

    private static void CollectFromHeader(OpenApiHeader header, HashSet<ComponentRef> refs)
    {
        TryAddRef(header, ComponentType.Header, refs);
        if (header.Schema is not null)
            CollectFromSchema(header.Schema, refs);
    }

    private static void CollectFromRequestBody(OpenApiRequestBody body, HashSet<ComponentRef> refs, bool isRoot = false)
    {
        if (!isRoot)
            TryAddRef(body, ComponentType.RequestBody, refs);
        foreach (var (_, media) in body.Content ?? new Dictionary<string, OpenApiMediaType>())
            CollectFromMediaType(media, refs);
    }

    private static void CollectFromResponse(OpenApiResponse resp, HashSet<ComponentRef> refs, bool isRoot = false)
    {
        if (!isRoot)
            TryAddRef(resp, ComponentType.Response, refs);
        foreach (var (_, media) in resp.Content ?? new Dictionary<string, OpenApiMediaType>())
            CollectFromMediaType(media, refs);
        foreach (var (_, header) in resp.Headers ?? new Dictionary<string, OpenApiHeader>())
            CollectFromHeader(header, refs);
        foreach (var (_, link) in resp.Links ?? new Dictionary<string, OpenApiLink>())
            TryAddRef(link, ComponentType.Link, refs);
    }

    private static void CollectFromMediaType(OpenApiMediaType media, HashSet<ComponentRef> refs)
    {
        if (media.Schema is not null)
            CollectFromSchema(media.Schema, refs);
        foreach (var (_, example) in media.Examples ?? new Dictionary<string, OpenApiExample>())
            TryAddRef(example, ComponentType.Example, refs);
    }

    private static void CollectFromSchema(OpenApiSchema schema, HashSet<ComponentRef> refs, bool isRoot = false)
    {
        if (!isRoot)
            TryAddRef(schema, ComponentType.Schema, refs);
        if (schema.Items is not null)
            CollectFromSchema(schema.Items, refs);
        foreach (var (_, prop) in schema.Properties ?? new Dictionary<string, OpenApiSchema>())
            CollectFromSchema(prop, refs);
        foreach (var s in schema.AllOf ?? [])
            CollectFromSchema(s, refs);
        foreach (var s in schema.OneOf ?? [])
            CollectFromSchema(s, refs);
        foreach (var s in schema.AnyOf ?? [])
            CollectFromSchema(s, refs);
        if (schema.Not is not null)
            CollectFromSchema(schema.Not, refs);
        if (schema.AdditionalProperties is not null)
            CollectFromSchema(schema.AdditionalProperties, refs);
    }

    private static void TryAddRef(IOpenApiReferenceable item, ComponentType type, HashSet<ComponentRef> refs)
    {
        if (item.Reference is null) return;
        refs.Add(new ComponentRef(type, item.Reference.Id));
    }
}