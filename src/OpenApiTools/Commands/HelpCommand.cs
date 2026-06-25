using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace OpenApiTools.Commands;

public sealed class HelpCommand : AsyncCommand<HelpCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[command]")]
        [Description("The command to show help for. Omit to show general application help.")]
        public string? Command { get; init; }
    }

    public override Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var services = new ServiceCollection();
        Program.ConfigureServices(services);

        var registrar = new TypeRegistrar(services);
        var app = new CommandApp(registrar);
        app.Configure(Program.ConfigureCommands);

        var args = settings.Command is null
            ? new[] { "--help" }
            : new[] { settings.Command, "--help" };

        return app.RunAsync(args);
    }
}