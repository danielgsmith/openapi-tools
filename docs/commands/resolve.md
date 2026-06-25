# resolve

Resolves all `$ref` references into a single self-contained document.

## Synopsis

```
openapi-tool resolve <path> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--format <FORMAT>` | `-f` | `yaml` | Output format: `json` or `yaml`. |
| `--output <OUTPUT>` | `-o` | stdout | Output file path. If omitted, writes to stdout. |

## Description

The `resolve` command inlines all `$ref` references in an OpenAPI document, producing a single self-contained file with no external or internal references. This is also known as "bundling."

This is useful when:
- Feeding a spec to a tool that doesn't handle `$ref` well
- Sharing a spec as a single file
- Simplifying multi-file specs for easier consumption
- Pre-processing before `diff` to ensure consistent comparison

The resolved document is semantically equivalent to the original — all `$ref` targets are expanded inline at each reference site.

## Examples

```bash
# Resolve and output as YAML to stdout
openapi-tool resolve ./openapi.yaml

# Resolve and output as JSON to a file
openapi-tool resolve ./openapi.yaml --format json -o resolved.json

# Resolve and output as YAML to a file
openapi-tool resolve ./openapi.yaml --format yaml -o resolved.yaml
```
