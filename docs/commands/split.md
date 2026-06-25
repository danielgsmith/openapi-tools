# split

Splits a monolithic OpenAPI document into multiple component files.

## Synopsis

```
openapi-tool split <path> <output>
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `path` | Yes | Path to the OpenAPI document. |
| `output` | Yes | Output directory for the split files. |

## Description

The `split` command takes a single OpenAPI document and breaks it into a multi-file structure. The main document file (`openapi.yaml`) retains all paths, info, servers, and security, while each component is extracted into its own file under a `components/` directory tree.

The resulting structure looks like:

```
output/
├── openapi.yaml                          # Main document with $refs to component files
└── components/
    ├── schemas/
    │   ├── Pet.yaml
    │   └── Error.yaml
    ├── parameters/
    │   └── PetIdParam.yaml
    ├── responses/
    │   └── NotFound.yaml
    ├── requestBodies/
    │   └── PetBody.yaml
    ├── responses/
    │   └── ...
    ├── securitySchemes/
    │   └── ApiKey.yaml
    ├── examples/
    │   └── ...
    ├── headers/
    │   └── ...
    └── links/
        └── ...
```

The main `openapi.yaml` replaces each component definition with a `$ref` pointing to its extracted file, so the split document is functionally equivalent to the original.

This is the inverse of the `resolve` command.

## Examples

```bash
# Split a spec into a directory
openapi-tool split ./openapi.yaml ./output/

# Split into a specific directory name
openapi-tool split ./monolith.yaml ./api-components/
```
