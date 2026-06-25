# OpenApiTools Documentation

A `dotnet` global tool for managing and interacting with OpenAPI (Swagger) JSON and YAML documents.

## Commands

| Command | Description |
|---------|-------------|
| [help](commands/help.md) | Shows help for the application or a specific command |
| [validate](commands/validate.md) | Validates an OpenAPI document against the specification |
| [convert](commands/convert.md) | Converts an OpenAPI document between JSON and YAML |
| [discover](commands/discover.md) | Recursively scans a directory and identifies OpenAPI documents |
| [endpoints](commands/endpoints.md) | Lists all endpoints (paths and operations) in a document |
| [stats](commands/stats.md) | Shows overview statistics for a document |
| [search](commands/search.md) | Fuzzy searches endpoints, schemas, and other components |
| [describe endpoint](commands/describe-endpoint.md) | Describes a specific endpoint in detail |
| [describe schema](commands/describe-schema.md) | Describes a specific schema in detail |
| [unused](commands/unused.md) | Finds unused/orphaned components |
| [lint](commands/lint.md) | Lints a document against a configurable ruleset |
| [diff](commands/diff.md) | Diffs two documents and identifies breaking changes |
| [resolve](commands/resolve.md) | Resolves all `$ref` references into a single document |
| [split](commands/split.md) | Splits a monolithic document into multiple component files |
| [merge](commands/merge.md) | Merges multiple documents into a single unified specification |

## Installation

```bash
dotnet tool install --global OpenApiTools --add-source https://nuget.pkg.github.com/danielsmith/index.json
```

## Output Formats

Several commands support multiple output formats via `--format`:

| Format | Description |
|--------|-------------|
| `table` | Rich console table with colors (default) |
| `json` | Indented JSON, suitable for piping to `jq` |
| `yaml` | YAML, suitable for piping to `yq` |
| `plain` | Tab-separated values, suitable for `grep`/`awk` |

All commands that produce output also support `-o`/`--output` to write results to a file instead of stdout. When writing tables to a file, ANSI color codes are stripped automatically.

## Running in Development

```bash
dotnet run --project src/OpenApiTools/OpenApiTools.csproj -- <command> [options]
```
