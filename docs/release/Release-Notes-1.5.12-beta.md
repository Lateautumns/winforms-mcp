# Rhombus.WinFormsMcp 1.5.12-beta

This is a release-preparation draft. It is not a published release.

## Highlights

- Read-only RuntimeBridge inspection for managed WinForms controls, layout,
  bindings, HWNDs, source mapping, diagnostics, and bounded event traces.
- Optional AntdUI provider semantics, complex control trees, layered-window
  metadata, and theme/DPI rendering profiles.
- Incremental source indexing with fully qualified symbols for VS MCP and
  CodeGraph MCP handoff.
- Restartable UIA worker isolation for bounded high-risk probes.
- Local-only bridge IPC security and multi-process runtime identity with stale
  reference rejection.

## Compatibility

See [Compatibility Matrix](Compatibility-Matrix.md). In particular, the
current evidence covers .NET 8 Windows builds, RuntimeBridge net48/net8
targets, and RendererHost net48/netcoreapp3.1/net8 targets. Windows 10/11 and a
standalone .NET 10 run remain unverified.

## Upgrade notes

Existing UIA tools and required parameters are unchanged. `bridgeInstanceId`
is an optional input on runtime and diagnostics tools; clients may add it when
they need stale-reference protection after an application restart.

## Not included

This draft does not authorize NuGet push, NPM publish, GitHub release creation,
or automatic changes to `main`.
