# discover

Recursively scans a directory and identifies OpenAPI documents by content.

## Synopsis

```
openapi-tool discover <path> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Directory to scan recursively for OpenAPI documents. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--ext <EXTENSIONS>` | `-e` | `json,yaml,yml` | Comma-separated list of file extensions to consider. |
| `--max-size <MB>` | `-s` | `10` | Skip files larger than this many megabytes. |
| `--no-validate` | | `false` | Skip full parse validation; use only the cheap magic-string prefilter. |
| `--format <FORMAT>` | `-f` | `table` | Output format: `table`, `json`, `yaml`, or `plain`. |
| `--output <OUTPUT>` | `-o` | stdout | Write results to a file instead of stdout. |

## Description

The `discover` command recursively scans a directory tree for files that look like OpenAPI documents. Since filenames are not reliable, detection uses a two-stage approach:

1. **Cheap prefilter**: Only files with allowed extensions and under the size cap are considered. The first 4 KB of each candidate file is read and matched against the structural signature of an OpenAPI document (a top-level `openapi:` or `swagger:` key anchored to line start).

2. **Full parse confirmation**: Each candidate is fully parsed with `Microsoft.OpenApi.Readers`. If parsing succeeds, the document is confirmed and metadata (title, version, spec version) is extracted. Use `--no-validate` to skip this stage for speed.

The `--no-validate` mode is useful for quickly scanning large repositories where you only need file paths, not metadata. With validation enabled, each result includes the spec version (e.g. `OpenApi3_0`, `OpenApi2_0`), document title, and document version.

## Output

Each result includes:

| Field | Description |
|-------|-------------|
| Path | Relative file path |
| Status | `Valid` or `Invalid` |
| Spec | OpenAPI spec version (e.g. `OpenApi3_0`) |
| Doc Version | The `info.version` from the document |
| Title | The `info.title` from the document |

## Examples

```bash
# Scan current directory
openapi-tool discover ./specs

# Scan with JSON output for piping to jq
openapi-tool discover ./specs --format json

# Scan only .yaml files, skip validation for speed
openapi-tool discover ./specs --ext yaml --no-validate

# Scan and write results to a file
openapi-tool discover ./specs --format json -o results.json

# Scan large repos with a higher size cap
openapi-tool discover ./monorepo --max-size 50
```
