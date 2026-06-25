# openapi-tools

A `dotnet` global tool for discovering, validating, diffing, merging, and transforming OpenAPI (Swagger) JSON and YAML documents.

## Installation

```bash
# From GitHub Packages
# Authenticate to the feed first if you have not already added it locally.
dotnet tool install --global OpenApiTools --add-source https://nuget.pkg.github.com/danielgsmith/index.json
```

Package id: `OpenApiTools`

Installed command: `openapi-tool`

## Commands

| Command | Description |
|---------|-------------|
| `help` | Shows help for the application or a specific command |
| `validate` | Validates an OpenAPI document against the specification |
| `convert` | Converts an OpenAPI document between JSON and YAML |
| `discover` | Recursively scans a directory and identifies OpenAPI documents |
| `endpoints` | Lists all endpoints (paths and operations) in a document |
| `stats` | Shows overview statistics for a document |
| `search` | Fuzzy searches endpoints, schemas, and other components |
| `describe endpoint` | Describes a specific endpoint in detail |
| `describe schema` | Describes a specific schema in detail |
| `unused` | Finds unused/orphaned components |
| `lint` | Lints a document against a configurable ruleset |
| `diff` | Diffs two documents and identifies breaking changes |
| `resolve` | Resolves all `$ref` references into a single document |
| `split` | Splits a monolithic document into multiple component files |
| `merge` | Merges multiple documents into a single unified specification |

See the [full documentation](docs/index.md) for detailed usage of each command.

## Usage

```bash
openapi-tool validate ./openapi.yaml
openapi-tool convert ./openapi.yaml --format json -o openapi.json
openapi-tool diff ./v1.yaml ./v2.yaml --breaking-only
openapi-tool merge --title "Platform API" --version "1.0" -o merged.json api1.yaml api2.yaml
```

Run `openapi-tool --help` to list all available commands.

## Development

```bash
dotnet build
dotnet test
dotnet pack src/OpenApiTools/OpenApiTools.csproj -c Release
```

CI validates packaging by installing the packed tool and smoke testing it:

```bash
dotnet pack src/OpenApiTools/OpenApiTools.csproj -c Release -o artifacts
dotnet tool install --tool-path ./.tools OpenApiTools --add-source ./artifacts
./.tools/openapi-tool --help
./.tools/openapi-tool merge --help
```

## Versioning

Package and assembly versions are managed automatically with `Nerdbank.GitVersioning`.

- The version root is defined in `version.json`
- CI and publish builds require full git history (`fetch-depth: 0`)
- Release tags should use the `v` prefix, for example `v0.1.0`

### Running locally without installing

```bash
# Direct invocation
dotnet run --project src/OpenApiTools/OpenApiTools.csproj -- validate samples/petstore.yaml

# Or add a shell alias (in ~/.zshrc) for a native-feeling dev loop:
alias openapi-tool="dotnet run --project $(pwd)/src/OpenApiTools/OpenApiTools.csproj --no-build --"
```

## License

MIT
