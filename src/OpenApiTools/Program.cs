using Microsoft.Extensions.DependencyInjection;
using OpenApiTools.Commands;
using OpenApiTools.Services;
using Spectre.Console.Cli;

namespace OpenApiTools;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(ConfigureCommands);

        return await app.RunAsync(args);
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IOpenApiLoader, OpenApiLoader>();
        services.AddSingleton<IOpenApiDiscoverer, OpenApiDiscoverer>();
        services.AddSingleton<IOpenApiSerializer, OpenApiSerializer>();
        services.AddSingleton<IOpenApiReferenceTracker, OpenApiReferenceTracker>();
        services.AddSingleton<IOpenApiStatsService, OpenApiStatsService>();
        services.AddSingleton<IOpenApiEndpointLister, OpenApiEndpointLister>();
        services.AddSingleton<IOpenApiSearcher, OpenApiSearcher>();
        services.AddSingleton<IOpenApiDescriber, OpenApiDescriber>();
        services.AddSingleton<IOpenApiUnusedFinder, OpenApiUnusedFinder>();
        services.AddSingleton<ILinter, Linter>();
        services.AddSingleton<IOpenApiDiffer, OpenApiDiffer>();
        services.AddSingleton<IOpenApiResolver, OpenApiResolver>();
        services.AddSingleton<IOpenApiSplitter, OpenApiSplitter>();
        services.AddSingleton<IOpenApiMerger, OpenApiMerger>();
    }

    internal static void ConfigureCommands(IConfigurator config)
    {
        config.SetApplicationName("openapi-tool");

        config.AddCommand<HelpCommand>("help")
            .WithDescription("Shows help for the application or a specific command.");

        config.AddCommand<ValidateCommand>("validate")
            .WithDescription("Validates an OpenAPI document against the specification.")
            .WithExample("validate", "./openapi.yaml");

        config.AddCommand<ConvertCommand>("convert")
            .WithDescription("Converts an OpenAPI document between JSON and YAML.")
            .WithExample("convert", "./openapi.yaml", "--format", "json");

        config.AddCommand<DiscoverCommand>("discover")
            .WithDescription("Recursively scans a directory and identifies OpenAPI documents by content.")
            .WithExample("discover", "./specs")
            .WithExample("discover", "./specs", "--format", "json", "-o", "results.json");

        config.AddCommand<EndpointsCommand>("endpoints")
            .WithDescription("Lists all endpoints (paths and operations) in an OpenAPI document.")
            .WithExample("endpoints", "./openapi.yaml");

        config.AddCommand<StatsCommand>("stats")
            .WithDescription("Shows overview statistics for an OpenAPI document.")
            .WithExample("stats", "./openapi.yaml");

        config.AddCommand<SearchCommand>("search")
            .WithDescription("Fuzzy searches endpoints, schemas, and other components in an OpenAPI document.")
            .WithExample("search", "./openapi.yaml", "pet")
            .WithExample("search", "./openapi.yaml", "pet", "--components", "schemas");

        config.AddCommand<UnusedCommand>("unused")
            .WithDescription("Finds unused/orphaned components in an OpenAPI document.")
            .WithExample("unused", "./openapi.yaml");

        config.AddCommand<LintCommand>("lint")
            .WithDescription("Lints an OpenAPI document against a configurable ruleset.")
            .WithExample("lint", "./openapi.yaml", "./lint-config.yaml");

        config.AddCommand<DiffCommand>("diff")
            .WithDescription("Diffs two OpenAPI documents and identifies breaking changes.")
            .WithExample("diff", "./openapi-v1.yaml", "./openapi-v2.yaml")
            .WithExample("diff", "./openapi-v1.yaml", "./openapi-v2.yaml", "--breaking-only");

        config.AddCommand<ResolveCommand>("resolve")
            .WithDescription("Resolves all $ref references into a single self-contained document.")
            .WithExample("resolve", "./openapi.yaml")
            .WithExample("resolve", "./openapi.yaml", "--format", "json", "-o", "resolved.json");

        config.AddCommand<SplitCommand>("split")
            .WithDescription("Splits a monolithic OpenAPI document into multiple component files.")
            .WithExample("split", "./openapi.yaml", "./output/");

        config.AddCommand<MergeCommand>("merge")
            .WithDescription("Merges multiple OpenAPI documents into a single unified specification.")
            .WithExample("merge", "--title", "Platform API", "--version", "1.0.0", "-o", "merged.json", "api1.yaml", "api2.yaml")
            .WithExample("merge", "--config", "merge.config.json");

        config.AddBranch("describe", describe =>
        {
            describe.SetDescription("Describes a specific endpoint or schema in detail.");

            describe.AddCommand<DescribeEndpointCommand>("endpoint")
                .WithDescription("Describes a specific endpoint (METHOD /path).")
                .WithExample("endpoint", "./openapi.yaml", "GET /pets");

            describe.AddCommand<DescribeSchemaCommand>("schema")
                .WithDescription("Describes a specific schema by name.")
                .WithExample("schema", "./openapi.yaml", "Pet");
        });
    }
}