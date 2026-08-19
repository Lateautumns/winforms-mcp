# Release Architecture

The distributable has three independent layers:

```text
MCP Server (net8.0-windows)
  |-- UIA/UIA Worker for actions and bounded probes
  |-- RuntimeContracts (netstandard2.0) DTOs
  `-- RuntimeBridge (net48; net8.0-windows) in the target app

RendererHost
  |-- net48
  |-- netcoreapp3.1
  `-- net8.0-windows
```

RuntimeBridge communicates with the server through a per-process named pipe.
The pipe carries protocol-versioned JSON snapshots, never live WinForms or UIA
objects. `processId`, `bridgeInstanceId`, and managed IDs form the runtime
identity tuple. UIA remains the action layer; RuntimeBridge remains read-only.

The standalone ZIP contains the server output and one `rendererhost/<tfm>`
folder for each supported renderer target. The NPM package wraps that ZIP-style
`dist/` layout and starts the same executable on Windows x64. NuGet packages
are produced independently for the server, RuntimeContracts, and RuntimeBridge.

`scripts/package-local.ps1` is the reproducible local assembly path. It builds
packages and archives only; publishing is intentionally owned by the existing
release workflows and is outside unattended development.
