using SharpYaml.Serialization;
using Spectre.Console;
using System.Text.Json;

namespace OpenApiTools.Services;

public static class OutputWriter
{
    public static string ToJson<T>(T data)
    {
        return JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    public static string ToYaml<T>(T data)
    {
        var serializer = new Serializer(new SerializerSettings { EmitAlias = false });
        return serializer.Serialize(data);
    }

    public static string ToPlain<T>(IEnumerable<T> items, Func<T, string[]> selector)
    {
        var lines = items.Select(item => string.Join('\t', selector(item)));
        return string.Join(Environment.NewLine, lines);
    }

    public static string RenderTable(Action<IAnsiConsole> render)
    {
        var sw = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(sw) });
        render(console);
        return sw.ToString();
    }

    public static async Task WriteOutputAsync(string? output, string content)
    {
        if (string.IsNullOrWhiteSpace(output))
            AnsiConsole.WriteLine(content);
        else
        {
            await File.WriteAllTextAsync(output, content);
            AnsiConsole.MarkupLine("[green]✓[/] Wrote output to '" + output + "'.");
        }
    }

    public static async Task WriteOutputAsync(string? output, Action<IAnsiConsole> render)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            render(AnsiConsole.Console);
        }
        else
        {
            var sw = new StringWriter();
            var console = AnsiConsole.Create(new AnsiConsoleSettings
            {
                Out = new AnsiConsoleOutput(sw),
                Ansi = AnsiSupport.No,
            });
            render(console);
            await File.WriteAllTextAsync(output, sw.ToString());
            AnsiConsole.MarkupLine("[green]✓[/] Wrote output to '" + output + "'.");
        }
    }
}