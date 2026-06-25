# unused

Finds unused/orphaned components in an OpenAPI document.

## Synopsis

```
openapi-tool unused <path> [options]
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

The `unused` command walks the entire OpenAPI document and tracks every component that is referenced by a `$ref` from any path, operation, or other component. Any component that is defined but never referenced is reported as unused.

This is useful for cleaning up mature specs that have accumulated dead weight from removed endpoints, deprecated schemas, or refactored parameters.

The following component types are checked:

- **Schemas** (`#/components/schemas/*`)
- **Parameters** (`#/components/parameters/*`)
- **Responses** (`#/components/responses/*`)
- **Request Bodies** (`#/components/requestBodies/*`)
- **Security Schemes** (`#/components/securitySchemes/*`)
- **Headers** (`#/components/headers/*`)
- **Examples** (`#/components/examples/*`)
- **Links** (`#/components/links/*`)
- **Callbacks** (`#/components/callbacks/*`)

> **Note**: Security schemes defined in `components/securitySchemes` are only considered "used" if they are referenced by a `security` requirement on an operation or at the document root. Defining a security scheme without using it is flagged as unused.

## Output

Each result includes:

| Field | Description |
|-------|-------------|
| Type | The component type (Schema, Parameter, Response, etc.) |
| Name | The component name as it appears in `components` |

## Examples

```bash
# Find unused components
openapi-tool unused ./openapi.yaml

# Output as JSON for scripting
openapi-tool unused ./openapi.yaml --format json

# Output as plain text for piping to xargs
openapi-tool unused ./openapi.yaml --format plain

# Write to a file
openapi-tool unused ./openapi.yaml -o unused.txt
```
