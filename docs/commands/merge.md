# merge

Merges multiple OpenAPI documents into a single unified specification.

## Synopsis

```
openapi-tool merge [files...] [options]
openapi-tool merge --config <config> [options]
```

## Arguments

| Argument | Required | Description                                                  |
| -------- | -------- | ------------------------------------------------------------ |
| `files`  | No\*     | OpenAPI files to merge (required unless `--config` is used). |

\*When using `--config`, the file list comes from the config file.

## Options

| Option                         | Short | Default               | Description                                                             |
| ------------------------------ | ----- | --------------------- | ----------------------------------------------------------------------- |
| `--config <CONFIG>`            |       |                       | Path to a JSON merge configuration file.                                |
| `--output <OUTPUT>`            | `-o`  | `merged-openapi.json` | Output file path.                                                       |
| `--title <TITLE>`              |       |                       | API title for the merged specification (required without `--config`).   |
| `--version <VERSION>`          |       |                       | API version for the merged specification (required without `--config`). |
| `--schema-conflict <STRATEGY>` |       | `rename-incoming`     | Schema conflict strategy: `keep-existing`, `keep-incoming`, `rename-existing`, `rename-incoming`, `rename-both`, or `fail`. |
| `--schema-identical <STRATEGY>` |       | `dedupe`              | Handling for identical duplicate schemas: `dedupe`, `warn-and-dedupe`, or `fail`. |
| `--format <FORMAT>`            | `-f`  | `json`                | Output format: `json` or `yaml`.                                        |
| `--verbose`                    | `-v`  | `false`               | Show detailed progress and warnings.                                    |

## Description

The `merge` command combines multiple OpenAPI specifications into a single unified document. This is ideal for microservice architectures where each service generates its own spec, but you need a combined specification for documentation portals, client SDK generation, or API gateway configuration.

## Two Modes

### Direct Invocation

Merge files directly from the command line. Requires `--title` and `--version`:

```bash
openapi-tool merge --title "Platform API" --version "1.0.0" -o merged.json api1.yaml api2.yaml
```

### Configuration File

Use a JSON config file for complex merge scenarios with per-source prefixes and per-component conflict policies:

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
    "conflicts": {
        "schemas": {
            "identical": "dedupe",
            "conflict": "rename-incoming"
        },
        "responses": {
            "identical": "dedupe",
            "conflict": "fail"
        }
    }
}
```

### Config Properties

| Property                      | Required | Description                                                                                            |
| ----------------------------- | -------- | ------------------------------------------------------------------------------------------------------ |
| `info.title`                  | Yes      | The title of the merged API                                                                            |
| `info.version`                | Yes      | The version of the merged API                                                                          |
| `info.description`            | No       | A description of the merged API                                                                        |
| `servers`                     | No       | Array of server definitions. Source servers are ignored; only configured servers appear in the output. |
| `sources`                     | Yes      | Array of source specifications to merge                                                                |
| `sources[].path`              | Yes      | File path to the OpenAPI specification                                                                 |
| `sources[].pathPrefix`        | No       | Prefix to prepend to all paths (e.g. `/users`)                                                         |
| `sources[].operationIdPrefix` | No       | Prefix to prepend to all operationIds (e.g. `users_`)                                                  |
| `sources[].name`              | No       | Friendly name for warnings/errors (defaults to filename)                                               |
| `output`                      | Yes\*    | File path for the merged output                                                                        |
| `conflicts`                   | No       | Per-component duplicate and conflict policies.                                                         |
| `conflicts.<type>.identical`  | No       | Handling for identical duplicates: `dedupe`, `warn-and-dedupe`, or `fail`.                            |
| `conflicts.<type>.conflict`   | No       | Handling for conflicting duplicates: `keep-existing`, `keep-incoming`, `rename-existing`, `rename-incoming`, `rename-both`, or `fail`. |

Supported `<type>` values:

- `schemas`
- `parameters`
- `responses`
- `requestBodies`
- `headers`
- `examples`
- `links`
- `callbacks`
- `securitySchemes`

\*`output` can be overridden by `-o` on the command line.

## Conflict Policies

Policies are evaluated separately for:

- identical duplicates: same name, structurally equivalent definition
- conflicts: same name, different definition

Supported conflict actions:

| Action | Behavior |
|--------|----------|
| `keep-existing` | Keep the first merged definition and rewrite later references to it. |
| `keep-incoming` | Replace the existing definition with the later one. |
| `rename-existing` | Rename the already-merged definition using its source name as a prefix, then keep the incoming definition under the original name. |
| `rename-incoming` | Keep the existing definition and rename the incoming one using the source name as a prefix. |
| `rename-both` | Rename both definitions using their source names as prefixes. |
| `fail` | Stop the merge with an error diagnostic. |

Supported identical-duplicate actions:

| Action | Behavior |
|--------|----------|
| `dedupe` | Keep one copy without warning. |
| `warn-and-dedupe` | Keep one copy and emit a warning. |
| `fail` | Stop the merge even if the definitions are identical. |

## Merge Behavior

| Component            | Behavior                                                       |
| -------------------- | -------------------------------------------------------------- |
| **Paths**            | Combined from all sources; path prefixes applied if configured. Same path with different methods is merged; same path with the same method keeps the first operation and warns. |
| **Operation IDs**    | Prefixes applied if configured; duplicates generate warnings   |
| **Schemas**          | Deduplicated if identical; conflicts handled by per-component policy |
| **Security Schemes** | Deduplicated if identical; conflicts handled by per-component policy |
| **Tags**             | Combined by name; missing descriptions and external docs are filled from later sources when available |
| **Document Security**| Duplicate top-level security requirements are deduplicated     |
| **Servers**          | Only servers from config are used; source servers ignored      |

## Examples

```bash
# Basic merge with required metadata
openapi-tool merge --title "Platform API" --version "1.0.0" -o merged.json api1.yaml api2.yaml

# Merge with verbose output showing all warnings
openapi-tool merge -v --title "My API" --version "1.0.0" api1.yaml api2.yaml

# Keep the later conflicting schema definition
openapi-tool merge --schema-conflict keep-incoming --title "API" --version "1.0" api1.yaml api2.yaml

# Rename the existing conflicting schema and keep the new one at the original name
openapi-tool merge --schema-conflict rename-existing --title "API" --version "1.0" api1.yaml api2.yaml

# Warn when identical schemas overlap
openapi-tool merge --schema-identical warn-and-dedupe --title "API" --version "1.0" api1.yaml api2.yaml

# Namespace both conflicting schema definitions
openapi-tool merge --schema-conflict rename-both --title "API" --version "1.0" api1.yaml api2.yaml

# Use a configuration file with path and operationId prefixes
openapi-tool merge --config merge.config.json

# Output as YAML instead of JSON
openapi-tool merge --title "API" --version "1.0" --format yaml -o merged.yaml api1.yaml api2.yaml
```
