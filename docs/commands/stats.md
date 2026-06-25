# stats

Shows overview statistics for an OpenAPI document.

## Synopsis

```
openapi-tool stats <path> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--output <OUTPUT>` | `-o` | stdout | Write output to a file instead of stdout. |

## Description

The `stats` command provides a quick health/overview dashboard for an OpenAPI document. It displays:

- **Counts**: paths, operations, schemas, parameters, responses, request bodies, security schemes
- **Quality metrics**: deprecated operations, operations without descriptions, schemas without descriptions
- **HTTP method breakdown**: a bar chart showing the distribution of HTTP methods
- **Largest schemas**: the top 5 schemas by property count

This is particularly useful when inheriting an unfamiliar API to quickly assess its size and quality.

## Output

The output is a table of metrics followed by a bar chart of HTTP method distribution and a list of the largest schemas. This command does not support `--format` since the output is a dashboard, not structured data.

When writing to a file with `--output`, the table and bar chart are rendered as plain ASCII without ANSI color codes.

## Examples

```bash
# Show stats for a spec
openapi-tool stats ./openapi.yaml

# Write stats to a file
openapi-tool stats ./openapi.yaml -o stats.txt
```
