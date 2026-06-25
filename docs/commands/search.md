# search

Fuzzy searches endpoints, schemas, and other components in an OpenAPI document.

## Synopsis

```
openapi-tool search <path> <query> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |
| `query` | Yes | Search query (fuzzy match). |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--components <COMPONENTS>` | `-c` | all | Comma-separated components to search. |
| `--threshold <THRESHOLD>` | `-t` | `0.4` | Match threshold 0.0-1.0. |
| `--format <FORMAT>` | `-f` | `table` | Output format: `table`, `json`, `yaml`, or `plain`. |
| `--output <OUTPUT>` | `-o` | stdout | Write results to a file instead of stdout. |

## Components

The `--components` option accepts a comma-separated list of component types to search:

| Value | Searches |
|-------|----------|
| `endpoints` | Paths and operations (matches against path, operationId, summary) |
| `schemas` | Component schemas (matches against name and description) |
| `parameters` | Component parameters (matches against name and description) |
| `responses` | Component responses (matches against name and description) |
| `security` | Component security schemes (matches against name and description) |

When omitted, all component types are searched.

## Fuzzy Matching

The search uses a Levenshtein-distance-based scoring algorithm:

1. **Exact substring** match scores 1.0
2. **Word prefix** match (any word starts with the query) scores 0.85
3. **Edit distance** score = `1.0 - (distance / max_length)`

Results with a score below the threshold (default 0.4) are excluded. Lower the threshold for broader matching, raise it for stricter matching.

## Output

Each result includes:

| Field | Description |
|-------|-------------|
| Component | The type of component matched (endpoint, schema, parameter, response, securityscheme) |
| Name | Display name (e.g. `GET /pets` for endpoints, `Pet` for schemas) |
| Location | The JSON path within the document |
| Description | The component's description, if any |
| Score | Match score (0.00-1.00) |

## Examples

```bash
# Search everything for "pet"
openapi-tool search ./openapi.yaml pet

# Search only schemas
openapi-tool search ./openapi.yaml pet --components schemas

# Search endpoints and schemas with a lower threshold for broader results
openapi-tool search ./openapi.yaml usr --components endpoints,schemas --threshold 0.3

# Output as JSON for scripting
openapi-tool search ./openapi.yaml order --format json

# Write to a file
openapi-tool search ./openapi.yaml order -o results.json
```
