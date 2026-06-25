# endpoints

Lists all endpoints (paths and operations) in an OpenAPI document.

## Synopsis

```
openapi-tool endpoints <path> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--format <FORMAT>` | `-f` | `table` | Output format: `table`, `json`, `yaml`, or `plain`. |
| `--output <OUTPUT>` | `-o` | stdout | Write results to a file instead of stdout. |

## Description

The `endpoints` command extracts every path and HTTP method combination from the document and presents them as a flat list. This is useful for getting a quick overview of an API's surface area.

Each endpoint entry includes:

| Field | Description |
|-------|-------------|
| Method | HTTP method (GET, POST, PUT, DELETE, PATCH, etc.) |
| Path | The URL path template |
| OperationId | The `operationId` if defined |
| Summary | The `summary` if defined |
| Tags | Comma-separated tag names |
| Deprecated | `yes` if the operation is marked deprecated |

## Plain Format

When using `--format plain`, the output is tab-separated with columns: Method, Path, OperationId, Summary, Tags (semicolon-separated), and Deprecated (literal `deprecated` or empty). This is suitable for piping to `grep`, `awk`, or other text tools.

## Examples

```bash
# List all endpoints
openapi-tool endpoints ./openapi.yaml

# Output as JSON for scripting
openapi-tool endpoints ./openapi.yaml --format json

# Output as plain text for grep
openapi-tool endpoints ./openapi.yaml --format plain | grep DELETE

# Write to a file
openapi-tool endpoints ./openapi.yaml -o endpoints.txt
```
