using Microsoft.OpenApi.Models;
using OpenApiTools.Models;

namespace OpenApiTools.Services;

public interface IOpenApiSearcher
{
    IReadOnlyList<SearchResult> Search(
        OpenApiDocument document,
        string query,
        IReadOnlySet<string> components,
        double threshold = 0.4);
}

public sealed class OpenApiSearcher : IOpenApiSearcher
{
    public IReadOnlyList<SearchResult> Search(
        OpenApiDocument document,
        string query,
        IReadOnlySet<string> components,
        double threshold = 0.4)
    {
        var results = new List<SearchResult>();

        if (components.Contains("endpoints") || components.Contains("operations"))
            SearchEndpoints(document, query, threshold, results);

        if (components.Contains("schemas"))
            SearchSchemas(document, query, threshold, results);

        if (components.Contains("parameters"))
            SearchParameters(document, query, threshold, results);

        if (components.Contains("responses"))
            SearchResponses(document, query, threshold, results);

        if (components.Contains("securityschemes") || components.Contains("security"))
            SearchSecuritySchemes(document, query, threshold, results);

        return results.OrderByDescending(r => r.Score).ToList();
    }

    private static void SearchEndpoints(OpenApiDocument doc, string query, double threshold, List<SearchResult> results)
    {
        foreach (var (path, item) in doc.Paths ?? new OpenApiPaths())
        {
            foreach (var (method, op) in item.Operations)
            {
                var methodStr = method.ToString().ToUpperInvariant();
                var name = $"{methodStr} {path}";
                var score = Math.Max(
                    FuzzyMatcher.Score(query, path),
                    Math.Max(
                        FuzzyMatcher.Score(query, op.OperationId),
                        FuzzyMatcher.Score(query, op.Summary)));

                if (score >= threshold)
                    results.Add(new SearchResult(
                        Component: "endpoint",
                        Name: name,
                        Path: path,
                        Description: op.Summary ?? op.Description,
                        Score: score));
            }
        }
    }

    private static void SearchSchemas(OpenApiDocument doc, string query, double threshold, List<SearchResult> results)
    {
        foreach (var (name, schema) in doc.Components?.Schemas ?? new Dictionary<string, OpenApiSchema>())
        {
            var score = Math.Max(
                FuzzyMatcher.Score(query, name),
                FuzzyMatcher.Score(query, schema.Description));

            if (score >= threshold)
                results.Add(new SearchResult(
                    Component: "schema",
                    Name: name,
                    Path: $"#/components/schemas/{name}",
                    Description: schema.Description,
                    Score: score));
        }
    }

    private static void SearchParameters(OpenApiDocument doc, string query, double threshold, List<SearchResult> results)
    {
        foreach (var (name, param) in doc.Components?.Parameters ?? new Dictionary<string, OpenApiParameter>())
        {
            var score = Math.Max(
                FuzzyMatcher.Score(query, name),
                FuzzyMatcher.Score(query, param.Description));

            if (score >= threshold)
                results.Add(new SearchResult(
                    Component: "parameter",
                    Name: name,
                    Path: $"#/components/parameters/{name}",
                    Description: param.Description,
                    Score: score));
        }
    }

    private static void SearchResponses(OpenApiDocument doc, string query, double threshold, List<SearchResult> results)
    {
        foreach (var (name, resp) in doc.Components?.Responses ?? new Dictionary<string, OpenApiResponse>())
        {
            var score = Math.Max(
                FuzzyMatcher.Score(query, name),
                FuzzyMatcher.Score(query, resp.Description));

            if (score >= threshold)
                results.Add(new SearchResult(
                    Component: "response",
                    Name: name,
                    Path: $"#/components/responses/{name}",
                    Description: resp.Description,
                    Score: score));
        }
    }

    private static void SearchSecuritySchemes(OpenApiDocument doc, string query, double threshold, List<SearchResult> results)
    {
        foreach (var (name, scheme) in doc.Components?.SecuritySchemes ?? new Dictionary<string, OpenApiSecurityScheme>())
        {
            var score = Math.Max(
                FuzzyMatcher.Score(query, name),
                FuzzyMatcher.Score(query, scheme.Description));

            if (score >= threshold)
                results.Add(new SearchResult(
                    Component: "securityscheme",
                    Name: name,
                    Path: $"#/components/securitySchemes/{name}",
                    Description: scheme.Description,
                    Score: score));
        }
    }
}