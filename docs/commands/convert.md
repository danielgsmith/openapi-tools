# convert

Converts an OpenAPI document between JSON and YAML formats.

## Synopsis

```
openapi-tool convert <path> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document (JSON or YAML). |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--format <FORMAT>` | `-f` | `json` | Target format: `json` or `yaml`. |
| `--output <OUTPUT>` | `-o` | stdout | Output file path. If omitted, writes to stdout. |

## Description

The `convert` command reads an OpenAPI document in one format and serializes it to another. This is useful for converting YAML specs to JSON for tooling that requires JSON, or vice versa for human readability.

The conversion parses the document through the OpenAPI object model, so the output is normalized (keys are ordered, references are standardized).

## Examples

```bash
# Convert YAML to JSON, output to stdout
openapi-tool convert ./openapi.yaml --format json

# Convert YAML to JSON, write to file
openapi-tool convert ./openapi.yaml --format json -o openapi.json

# Convert JSON to YAML
openapi-tool convert ./openapi.json --format yaml -o openapi.yaml
```
