using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiUnusedFinder
{
    IReadOnlyList<UnusedComponent> Find(OpenApiDocument document);
}

public sealed record UnusedComponent(ComponentType Type, string Name);

public sealed class OpenApiUnusedFinder : IOpenApiUnusedFinder
{
    private readonly IOpenApiReferenceTracker _tracker;

    public OpenApiUnusedFinder(IOpenApiReferenceTracker tracker)
    {
        _tracker = tracker;
    }

    public IReadOnlyList<UnusedComponent> Find(OpenApiDocument document)
    {
        var referenced = _tracker.FindReferencedComponents(document);
        var unused = new List<UnusedComponent>();
        var components = document.Components;
        if (components is null) return unused;

        CheckUnused(components.Schemas, ComponentType.Schema, referenced, unused);
        CheckUnused(components.Parameters, ComponentType.Parameter, referenced, unused);
        CheckUnused(components.Responses, ComponentType.Response, referenced, unused);
        CheckUnused(components.RequestBodies, ComponentType.RequestBody, referenced, unused);
        CheckUnused(components.SecuritySchemes, ComponentType.SecurityScheme, referenced, unused);
        CheckUnused(components.Headers, ComponentType.Header, referenced, unused);
        CheckUnused(components.Examples, ComponentType.Example, referenced, unused);
        CheckUnused(components.Links, ComponentType.Link, referenced, unused);
        CheckUnused(components.Callbacks, ComponentType.Callback, referenced, unused);

        return unused;
    }

    private static void CheckUnused<T>(
        IDictionary<string, T>? components,
        ComponentType type,
        IReadOnlySet<ComponentRef> referenced,
        List<UnusedComponent> unused)
    {
        if (components is null) return;
        foreach (var (name, _) in components)
        {
            if (!referenced.Contains(new ComponentRef(type, name)))
                unused.Add(new UnusedComponent(type, name));
        }
    }
}