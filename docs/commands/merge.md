# merge

Merges multiple OpenAPI documents into a single unified specification.

## Synopsis

```
openapi-tool merge [files...] [options]
openapi-tool merge --config <config> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `files` | No* | OpenAPI files to merge (required unless `--config` is used). |

*When using `--config`, the file list comes from the config file.

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--config <CONFIG>` | | | Path to a JSON merge configuration file. |
| `--output <OUTPUT>` | `-o` | `merged-openapi.json` | Output file path. |
| `--title <TITLE>` | | | API title for the merged specification (required without `--config`). |
| `--version <VERSION>` | | | API version for the merged specification (required without `--config`). |
| `--schema-conflict <STRATEGY>` | | `rename` | Strategy for schema conflicts: `rename`, `first-wins`, or `fail`. |
| `--format <FORMAT>` | `-f` | `json` | Output format: `json` or `yaml`. |
| `--verbose` | `-v` | `false` | Show detailed progress and warnings. |

## Description

The `merge` command combines multiple OpenAPI specifications into a single unified document. This is ideal for microservice architectures where each service generates its own spec, but you need a combined specification for documentation portals, client SDK generation, or API gateway configuration.

## Two Modes

### Direct Invocation

Merge files directly from the command line. Requires `--title` and `--version`:

```bash
openapi-tool merge --title "Platform API" --version "1.0.0" -o merged.json api1.yaml api2.yaml
```

### Configuration File

Use a JSON config file for complex merge scenarios with per-source prefixes:

```bash
openapi-tool merge --config merge.config.json
```

## Config File Format

```json
{
  "info": {
    "title": "Platform API",
    "version": "1.0.0",
    "description": "Unified API documentation for all services"
  },
  "servers": [
    { "url": "https://api.example.com", "description": "Production" }
  ],
  "sources": [
    {
      "path": "./services/users/openapi.yaml",
      "pathPrefix": "/users",
      "operationIdPrefix": "users_",
      "name": "Users Service"
    },
    {
      "path": "./services/products/openapi.yaml",
      "pathPrefix": "/products",
      "operationIdPrefix": "products_",
      "name": "Products Service"
    }
  ],
  "output": "./merged-openapi.json",
  "schemaConflict": "rename"
}
```

### Config Properties

| Property | Required | Description |
|----------|----------|-------------|
| `info.title` | Yes | The title of the merged API |
| `info.version` | Yes | The version of the merged API |
| `info.description` | No | A description of the merged API |
| `servers` | No | Array of server definitions. Source servers are ignored; only configured servers appear in the output. |
| `sources` | Yes | Array of source specifications to merge |
| `sources[].path` | Yes | File path to the OpenAPI specification |
| `sources[].pathPrefix` | No | Prefix to prepend to all paths (e.g. `/users`) |
| `sources[].operationIdPrefix` | No | Prefix to prepend to all operationIds (e.g. `users_`) |
| `sources[].name` | No | Friendly name for warnings/errors (defaults to filename) |
| `output` | Yes* | File path for the merged output |
| `schemaConflict` | No | Schema conflict strategy (default: `rename`) |

*`output` can be overridden by `-o` on the command line.

## Schema Conflict Strategies

When multiple sources define schemas with the same name:

| Strategy | Behavior |
|----------|----------|
| `rename` (default) | Conflicting schemas are renamed using the source name as a prefix (e.g. `Products_Error`). All references are updated. |
| `first-wins` | The first schema is kept; subsequent schemas with the same name are ignored (with a warning). |
| `fail` | The merge fails with an error when schema conflicts are detected. |

## Merge Behavior

| Component | Behavior |
|-----------|----------|
| **Paths** | Combined from all sources; path prefixes applied if configured |
| **Operation IDs** | Prefixes applied if configured; duplicates generate warnings |
| **Schemas** | Deduplicated if identical; conflicts handled per strategy |
| **Security Schemes** | Combined; duplicates keep first definition (with warning) |
| **Tags** | Combined with descriptions preserved |
| **Servers** | Only servers from config are used; source servers ignored |

## Examples

```bash
# Basic merge with required metadata
openapi-tool merge --title "Platform API" --version "1.0.0" -o merged.json api1.yaml api2.yaml

# Merge with verbose output showing all warnings
openapi-tool merge -v --title "My API" --version "1.0.0" api1.yaml api2.yaml

# Use first-wins strategy for schema conflicts
openapi-tool merge --schema-conflict first-wins --title "API" --version "1.0" api1.yaml api2.yaml

# Use a configuration file with path and operationId prefixes
openapi-tool merge --config merge.config.json

# Output as YAML instead of JSON
openapi-tool merge --title "API" --version "1.0" --format yaml -o merged.yaml api1.yaml api2.yaml
```
