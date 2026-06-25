# lint

Lints an OpenAPI document against a configurable ruleset.

## Synopsis

```
openapi-tool lint <spec> <config> [options]
```

## Arguments

| Argument | Required | Description |
|----------|----------|-------------|
| `spec` | Yes | Path to the OpenAPI document. |
| `config` | Yes | Path to the lint config YAML file. |

## Options

| Option | Short | Default | Description |
|--------|-------|---------|-------------|
| `--format <FORMAT>` | `-f` | `table` | Output format: `table`, `json`, `yaml`, or `plain`. |
| `--output <OUTPUT>` | `-o` | stdout | Write results to a file instead of stdout. |

## Description

The `lint` command goes beyond spec validation to check the *quality* of an OpenAPI document. While `validate` checks "is this a legal OpenAPI doc," `lint` checks "is this a *good* one."

Rules are defined in a YAML configuration file. Each rule has a severity (`error`, `warning`, or `info`) and optional rule-specific options. Only rules listed in the config are evaluated.

## Config File Format

```yaml
rules:
  operation-must-have-description:
    severity: error
  operationid-required:
    severity: error
  no-get-with-body:
    severity: warning
  all-4xx-responses:
    severity: warning
    options:
      codes:
        - "400"
        - "404"
  schema-must-have-description:
    severity: warning
  param-must-have-description:
    severity: warning
```

## Available Rules

| Rule | Description |
|------|-------------|
| `operation-must-have-description` | Flags operations missing both `description` and `summary`. |
| `operationid-required` | Flags operations without an `operationId`. |
| `no-get-with-body` | Flags GET operations that have a request body. |
| `all-4xx-responses` | Flags operations missing required 4xx response codes. Options: `codes` (list of status code strings). |
| `schema-must-have-description` | Flags component schemas missing a `description`. |
| `param-must-have-description` | Flags operation parameters missing a `description`. |

## Severities

| Severity | Meaning | Exit Code Impact |
|----------|---------|-------------------|
| `error` | Must fix | Counts toward non-zero exit code |
| `warning` | Should fix | Reported but does not affect exit code |
| `info` | Informational | Reported only |

## Exit Codes

| Code | Meaning |
|------|---------|
| 0 | No errors (warnings may be present) |
| 1 | One or more `error` severity findings |

## Output

Each finding includes:

| Field | Description |
|-------|-------------|
| Severity | `error`, `warning`, or `info` |
| Rule | The rule name that triggered the finding |
| Location | The path within the document (e.g. `GET /pets`, `#/components/schemas/Pet`) |
| Message | Description of the issue |

## Examples

```bash
# Lint a spec with a config file
openapi-tool lint ./openapi.yaml ./lint-config.yaml

# Output as JSON for CI integration
openapi-tool lint ./openapi.yaml ./lint-config.yaml --format json

# Write findings to a file
openapi-tool lint ./openapi.yaml ./lint-config.yaml -o findings.txt
```
