# help

Shows help for the application or a specific command.

## Synopsis

```
openapi-tool help [command]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `command` | No | The command to show help for. Omit to show general application help. |

## Description

The `help` command displays the same help output you get from `--help`, but as its own command. This is useful when exploring the tool or scripting help extraction.

Running `openapi-tool help` with no arguments shows the full list of commands with their descriptions and examples. Running `openapi-tool help <command>` shows the detailed help for a specific command, including all arguments, options, and examples.

## Examples

```bash
# Show general help with all commands
openapi-tool help

# Show help for the diff command
openapi-tool help diff

# Show help for the describe endpoint subcommand
openapi-tool help describe endpoint
```
