# describe endpoint

Describes a specific endpoint in an OpenAPI document in detail.

## Synopsis

```
openapi-tool describe endpoint <path> <endpoint> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |
| `endpoint` | Yes | Endpoint in format `METHOD /path`, e.g. `GET /pets`. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--output <OUTPUT>` | `-o` | stdout | Write output to a file instead of stdout. |

## Description

The `describe endpoint` command shows full details for a single endpoint, including:

- **Operation metadata**: method, path, operationId, summary, description, tags, deprecation status
- **Parameters**: name, location (path/query/header/cookie), required flag, type, description
- **Request body**: description, required flag, content types
- **Responses**: status codes, descriptions, content types

The endpoint argument must be in the format `METHOD /path` where METHOD is one of `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`, `TRACE`. The path must match exactly as it appears in the OpenAPI document (including path parameters like `/pets/{petId}`).

## Examples

```bash
# Describe a GET endpoint
openapi-tool describe endpoint ./openapi.yaml "GET /pets"

# Describe a POST endpoint with path parameter
openapi-tool describe endpoint ./openapi.yaml "POST /pets/{petId}/upload"

# Write output to a file
openapi-tool describe endpoint ./openapi.yaml "GET /pets" -o pet-endpoint.txt
```
