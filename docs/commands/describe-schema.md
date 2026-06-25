# describe schema

Describes a specific schema in an OpenAPI document in detail.

## Synopsis

```
openapi-tool describe schema <path> <schema> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |
| `schema` | Yes | Name of the schema to describe. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--output <OUTPUT>` | `-o` | stdout | Write output to a file instead of stdout. |

## Description

The `describe schema` command shows full details for a single schema defined in `#/components/schemas`, including:

- **Schema metadata**: name, type, format, description, deprecation status
- **Required fields**: list of required property names
- **Properties table**: name, type, format, required flag, description

The schema name must match exactly as it appears in `components/schemas` in the document.

## Examples

```bash
# Describe the Pet schema
openapi-tool describe schema ./openapi.yaml Pet

# Describe a nested schema
openapi-tool describe schema ./openapi.yaml OrderItem

# Write output to a file
openapi-tool describe schema ./openapi.yaml Pet -o pet-schema.txt
```
