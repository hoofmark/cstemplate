# CLI Reference

The `cstemplate` CLI is a cross-platform .NET global tool for compiling and
running C# template files.

## Installation

```bash
dotnet tool install --global HoofMark.CSharpTemplating.Cli
```

After installation, the `cstemplate` command is available on your PATH.

## Commands

### cstemplate run

Compiles and executes a template file, writing generated files to disk.

```
cstemplate run <template> [--output <dir>] [--config <file>] [--json]
```

**Arguments**

| Name | Description |
|---|---|
| `<template>` | Path to the `.template.cs` file to run. Required. |

**Options**

| Option | Short | Description |
|---|---|---|
| `--output <dir>` | `-o` | Output root directory for generated files. Defaults to a `generated/` folder next to the template file. Overrides the `outputRoot` value in `cstemplate.config.json`. |
| `--config <file>` | `-c` | Path to a JSON config file. Defaults to the sibling `.json` file with the same base name as the template. |
| `--json` | | Emit structured JSON output instead of human-readable text. Used by the VS Code extension. |

**Example**

```bash
cstemplate run src/templates/OrderService.template.cs --output src/generated
```

---

### cstemplate check

Compiles a template and reports any errors without executing it. Useful for
validating templates in CI, or as a pre-run check. The VS Code extension uses
this command to surface compile errors as inline diagnostics on save.

```
cstemplate check <template> [--json]
```

**Arguments**

| Name | Description |
|---|---|
| `<template>` | Path to the `.template.cs` file to check. Required. |

**Options**

| Option | Description |
|---|---|
| `--json` | Emit structured JSON output. Used by the VS Code extension. |

**Example**

```bash
cstemplate check src/templates/OrderService.template.cs
```

---

## Exit codes

The CLI process exits with one of the following codes, which can be used to
distinguish error types in scripts and CI pipelines.

| Code | Name | Description |
|---|---|---|
| `0` | Success | The template ran successfully and all output files were written. |
| `1` | CompilationError | The template failed to compile (Roslyn reported errors). |
| `2` | ExecutionError | The template compiled but threw an unhandled exception during execution. |
| `3` | InputError | Bad input — missing template file, malformed `cstemplate.config.json`, unresolvable NuGet packages, etc. |
| `99` | UnexpectedError | An unexpected internal error occurred. |

---

## JSON output mode

When `--json` is passed, the CLI writes a single JSON object to stdout instead
of human-readable text. This is consumed by the VS Code extension but can also
be useful for scripting.

**Success**

```json
{
  "status": "success",
  "templateName": "OrderService",
  "outputRoot": "C:/project/src/generated",
  "generatedFiles": [
    "C:/project/src/generated/OrderService.cs"
  ],
  "fileCount": 1
}
```

**Compilation error**

```json
{
  "status": "compilationError",
  "message": "Template compilation failed with 1 error(s).",
  "diagnostics": [
    {
      "severity": "error",
      "message": "The name 'Foo' does not exist in the current context",
      "filePath": "C:/project/src/templates/OrderService.template.cs",
      "line": 12,
      "column": 9
    }
  ]
}
```

**Execution error**

```json
{
  "status": "executionError",
  "message": "Template 'OrderService' threw an exception during execution.",
  "innerExceptionType": "System.IO.FileNotFoundException",
  "innerMessage": "Could not find file 'model.xml'.",
  "stackTrace": "   at OrderService.Run(...)"
}
```

**Possible `status` values:** `success`, `compilationError`, `executionError`, `error`

---

## Workspace configuration

The CLI searches upward from the template file's directory for a
`cstemplate.config.json` file. See [Workspace Configuration](workspace-config.md)
for the full schema reference.
