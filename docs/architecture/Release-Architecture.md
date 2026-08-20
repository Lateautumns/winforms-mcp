# Release Architecture

The distributable has three independent layers:

```text
MCP Server (net8.0-windows)
  |-- UIA/UIA Worker for actions and bounded probes
  |-- RuntimeContracts (netstandard2.0) DTOs
  `-- RuntimeBridge (net472; net48; net8.0-windows) in the target app

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
The server package embeds the non-published Rendering assembly
(`Rhombus.WinFormsMcp.Rendering.dll`) so its dependency closure stays limited
to the three published packages plus nuget.org dependencies.

`scripts/package-local.ps1` is the reproducible local assembly path. It builds
packages and archives only; publishing is intentionally owned by the existing
release workflows and is outside unattended development. `scripts/pack-nuget.ps1`
is the shared pack step used by local packaging, CI package checks, and the
release workflows; it verifies package names, versions, TFM assets, and
required inter-project dependencies before any artifact is considered valid.
CI and release workflows pack once, pass that exact package directory to both
.NET Framework 4.7.2 consumer E2E tests, and publish those same `.nupkg` files;
they do not repack after the gates pass.

The repository uses `main` plus short-lived feature/release branches; there is
no permanent `dev` branch. Stable release runs are triggered by `main`. Beta
releases are manual-only and must be dispatched from the selected non-`main`
branch so their version commit cannot accidentally trigger the stable workflow.

## NuGet publishing order and failure semantics

When a release actually publishes, the three packages are pushed in dependency
order — `Rhombus.WinFormsMcp.RuntimeContracts` first, then
`Rhombus.WinFormsMcp.RuntimeBridge`, then `Rhombus.WinFormsMcp` (server) last —
each with `--skip-duplicate`. NuGet has **no cross-package transaction**: a
partially completed push (for example, contracts succeeded but the bridge push
failed) cannot be rolled back automatically. `--skip-duplicate` makes a rerun of
the same release safe, but the release workflow must treat any failed push as a
manual reconciliation point rather than assuming the three packages are
atomically consistent.
