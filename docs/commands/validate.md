# validate

Validates an OpenAPI document against the OpenAPI specification.

## Synopsis

```
openapi-tool validate <path>
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document (JSON or YAML). |

## Description

The `validate` command parses an OpenAPI document and runs the full OpenAPI validation ruleset against it. It reports any structural or semantic violations, such as missing required fields, invalid references, or malformed schemas.

Validation errors are printed with the JSON path where the error was found. If the document is valid, a success message is printed.

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | Document is valid |
| 1 | Validation errors found, or file could not be read |

## Examples

```bash
# Validate a single file
openapi-tool validate ./openapi.yaml

# Validate a JSON spec
openapi-tool validate ./openapi.json
```
