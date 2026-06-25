using System.ComponentModel;
using Microsoft.OpenApi.Services;
using Microsoft.OpenApi.Validations;
using OpenApiTools.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class ValidateCommand : AsyncCommand<ValidateCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<path>")]
        [Description("Path to the OpenAPI document (JSON or YAML).")]
        public string Path { get; init; } = string.Empty;
    }

    private readonly IOpenApiLoader _loader;

    public ValidateCommand(IOpenApiLoader loader)
    {
        _loader = loader;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        try
        {
            var document = await _loader.LoadAsync(settings.Path);

            var validator = new OpenApiValidator(ValidationRuleSet.GetDefaultRuleSet());
            var walker = new OpenApiWalker(validator);
            walker.Walk(document);

            var errors = validator.Errors.ToList();

            if (errors.Count == 0)
            {
                AnsiConsole.MarkupLine($"[green]✓[/] '{settings.Path}' is a valid OpenAPI document.");
                return 0;
            }

            foreach (var error in errors)
            {
                AnsiConsole.MarkupLine($"[red]✗[/] {error.Pointer} — {error.Message}");
            }

            return 1;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }
}