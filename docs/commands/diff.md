# diff

Diffs two OpenAPI documents and identifies breaking changes.

## Synopsis

```
openapi-tool diff <old> <new> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `old` | Yes | Path to the old (baseline) OpenAPI document. |
| `new` | Yes | Path to the new (modified) OpenAPI document. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--breaking-only` | | `false` | Show only breaking changes. |
| `--format <FORMAT>` | `-f` | `table` | Output format: `table`, `json`, `yaml`, or `plain`. |
| `--output <OUTPUT>` | `-o` | stdout | Write results to a file instead of stdout. |

## Description

The `diff` command performs a **semantic comparison** of two OpenAPI documents. Unlike a text diff, it compares the API model — paths, operations, parameters, schemas — and reports what changed, classified by impact.

This is particularly useful for:
- **API versioning reviews**: ensure a new version doesn't break existing clients
- **PR checks**: gate pull requests on breaking changes
- **Change logs**: generate human-readable summaries of what changed between versions

## Change Types

| Change | Description |
|--------|-------------|
| `+ added` | New path, operation, parameter, schema, or security scheme |
| `- removed` | Deleted path, operation, parameter, schema, or security scheme |
| `~ modified` | Changed parameter type, required status, schema type, or property requirements |

## Impact Classification

| Impact | Meaning | Examples |
|--------|---------|----------|
| `BREAKING` | May break existing clients | Removed path/operation, added required parameter, changed schema type, property became required, removed schema |
| `non-breaking` | Safe for existing clients | Added path/operation, added optional parameter, added response, added schema, removed optional parameter |

## Categories

Changes are categorized by the type of element that changed:

| Category | Description |
|----------|-------------|
| `Path` | A path was added or removed |
| `Operation` | An operation (HTTP method on a path) was added or removed |
| `Parameter` | A parameter was added, removed, or modified |
| `RequestBody` | Request body was added, removed, or its required status changed |
| `Response` | A response status code was added or removed |
| `Schema` | A schema was added, removed, or its type/properties changed |
| `SecurityScheme` | A security scheme was added or removed |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | No breaking changes detected |
| 1 | One or more breaking changes detected |

## Output

Each change includes:

| Field | Description |
|-------|-------------|
| Change | `+ added`, `- removed`, or `~ modified` |
| Impact | `BREAKING` or `non-breaking` |
| Category | The element category (Path, Operation, Parameter, etc.) |
| Location | The path within the document (e.g. `GET /pets`, `#/components/schemas/Pet`) |
| Description | Human-readable description of what changed |

## Examples

```bash
# Compare two versions of an API
openapi-tool diff ./openapi-v1.yaml ./openapi-v2.yaml

# Show only breaking changes (for CI gating)
openapi-tool diff ./openapi-v1.yaml ./openapi-v2.yaml --breaking-only

# Output as JSON for programmatic processing
openapi-tool diff ./openapi-v1.yaml ./openapi-v2.yaml --format json

# Write diff to a file
openapi-tool diff ./openapi-v1.yaml ./openapi-v2.yaml -o diff.txt
```
